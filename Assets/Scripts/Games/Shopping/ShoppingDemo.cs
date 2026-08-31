using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;
using Shinmyeong.Save;
using Shinmyeong.Tracking;

namespace Shinmyeong.Games.Shopping
{
    /// 장보기 정식 게임 루프 (그래픽은 X-Box 플레이스홀더 — 자산 도입 시 교체).
    /// 04 게임구성표 확정 데이터 그대로:
    ///   R1~R4 발판 3(L1·C·R1)·점포 2 · R5~ 발판 5·점포 4 · 목표 R1~2:1·R3~7:2·R8~10:3 (합 21 · Q2 질의)
    ///   점포-재료 매핑 고정 · 진열 무작위: 한 판 같은 재료 진열 3회 이하 · 라운드 내 중복 진열은
    ///   점포 풀 4종이 서로 겹치지 않아 자동 충족
    ///   목표 발판 무작위 제약: 라운드 내 중복 금지 · 외곽 최대 1 · 외곽↔외곽 연속 금지
    /// 판정(07 문서 5-8): 도착만 판정(ArrivalJudge) · 다른 점포 정지 = 그대로 구매(실패 없음)
    ///   · 구매 후 중앙 장바구니 복귀로 목표 1건 완료 · 이미 구매한 점포 재판정 제외
    /// 힌트(5-8-1 · C4 기준안): 10초 무활동 시 이동 유도 — 유의미한 몸 이동(임계 이상 변위)은
    ///   유효 행동으로 타이머 리셋 · 제자리 흔들림은 리셋하지 않음
    public class ShoppingDemo : MonoBehaviour
    {
        [Header("도착 판정 (C1 기준안)")]
        [SerializeField] float _windowSeconds = 0.7f;
        [SerializeField] float _maxDriftInWindow = 0.035f;
        [SerializeField] float _enterHalfWidth = 0.06f;
        [SerializeField] float _exitHalfWidth = 0.09f;
        [SerializeField] float _hintIntervalSeconds = 10f;
        [Tooltip("힌트 타이머를 리셋하는 유효 이동 변위(C4 기준안) — 발판 간격 0.175의 절반 수준. 제자리 흔들림은 못 미친다")]
        [SerializeField] float _hintMoveResetDelta = 0.08f;

        /// 10라운드 완주 시에만 발생 — 중도 종료는 기록을 남기지 않는다(6-2)
        public event System.Action<PlayRecord, IReadOnlyList<string>> Finished;

        // 발판/영역: 0 L2 · 1 L1 · 2 C · 3 R1 · 4 R2
        const int ZoneC = 2;
        static readonly float[] ZoneCenters = { 0.15f, 0.325f, 0.5f, 0.675f, 0.85f };
        static readonly string[] ZoneNames = { "L2", "L1", "C", "R1", "R2" };
        static readonly bool[] IsOuter = { true, false, false, false, true };

        class StoreDef
        {
            public int Zone;
            public string Name;
            public string[] Pool;
        }

        static readonly StoreDef[] StoreDefs =
        {
            new StoreDef { Zone = 0, Name = "방앗간", Pool = new[] { "쌀가루", "밀가루", "팥", "깨", "밤" } },
            new StoreDef { Zone = 1, Name = "채소 가게", Pool = new[] { "호박", "시금치", "당근", "파", "버섯" } },
            new StoreDef { Zone = 3, Name = "정육점", Pool = new[] { "소고기", "갈비", "달걀", "두부" } },
            new StoreDef { Zone = 4, Name = "건어물·반찬", Pool = new[] { "미역", "김", "김치", "간장", "당면", "곶감" } },
        };

        static int TargetCount(int round) => round <= 2 ? 1 : round <= 7 ? 2 : 3;
        static bool WidePads(int round) => round >= 5;

        class StoreView
        {
            public StoreDef Def;
            public GameObject Go;
            public Image Panel;
            public Text ItemText;
            public GameObject DoneBadge;
            public string Displayed;
            public bool Purchased;
        }

        RectTransform _stageRoot;
        Text _roundText;
        Text _elapsedText;
        Text _guideText;
        Text _targetListText;
        RectTransform _userMarker;
        Image _basketPanel;
        readonly Image[] _pads = new Image[5];
        readonly Dictionary<int, StoreView> _stores = new Dictionary<int, StoreView>();

        ArrivalJudge _judge;
        readonly Queue<int> _arrivals = new Queue<int>();
        readonly List<string> _records = new List<string>();
        readonly Dictionary<string, int> _itemUseCount = new Dictionary<string, int>();
        readonly System.Random _rng = new System.Random();
        PlayRecord _play;
        float _startTime;
        bool _running;
        int _prevTargetPad = -1; // 외곽↔외곽 연속 금지는 라운드 경계를 넘어서도 적용
        int _lastMoveZone = -1;  // 좌우 이동 횟수 집계용 — 직전 도착 발판

        static readonly Color PadNormal = new Color(0.35f, 0.38f, 0.4f);
        static readonly Color PadLit = new Color(1f, 0.85f, 0.25f);
        static readonly Color PadCReturn = new Color(0.35f, 0.75f, 0.95f);
        static readonly Color StoreNormal = new Color(0.3f, 0.34f, 0.3f);
        static readonly Color StoreLit = new Color(0.75f, 0.6f, 0.2f);

        public void Begin(RectTransform host)
        {
            End();

            var rootGo = new GameObject("ShoppingStage");
            rootGo.transform.SetParent(host, false);
            var rect = rootGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            _stageRoot = rect;

            BuildStage();

            var zones = new List<ArrivalZone>();
            for (int i = 0; i < 5; i++)
                zones.Add(new ArrivalZone
                {
                    Id = i,
                    Center = ZoneCenters[i],
                    EnterHalfWidth = _enterHalfWidth,
                    ExitHalfWidth = _exitHalfWidth,
                });
            _judge = new ArrivalJudge(zones, _windowSeconds, _maxDriftInWindow);
            _judge.Arrived += OnArrived;

            _records.Clear();
            _itemUseCount.Clear();
            _arrivals.Clear();
            _prevTargetPad = -1;
            _lastMoveZone = -1;
            _play = PlayRecord.Start("Shopping");
            GamePause.ResetAccumulated();
            _startTime = Time.time;
            _running = true;
            StartCoroutine(GameLoop());
        }

        public void End()
        {
            _running = false;
            StopAllCoroutines();
            _stores.Clear();
            if (_stageRoot != null)
            {
                Destroy(_stageRoot.gameObject);
                _stageRoot = null;
            }
        }

        void OnArrived(int zone)
        {
            _arrivals.Enqueue(zone);
            // 좌우 이동 방향별 횟수 — 발판 도착 기준 (기준안 · 지표 표시 단위 확정 시 재검토)
            if (_lastMoveZone >= 0 && zone != _lastMoveZone)
            {
                if (zone > _lastMoveZone)
                    _play.MoveRightCount++;
                else
                    _play.MoveLeftCount++;
            }
            _lastMoveZone = zone;
        }

        void Update()
        {
            if (!_running || _judge == null)
                return;
            if (GamePause.IsPaused)
                return; // 일시정지 중에는 도착 판정·마커 갱신 정지 (경과 시간은 timeScale=0으로 이미 멈춤)
            var svc = BodyTrackingService.Instance;
            if (svc == null || !svc.BodyValid)
            {
                _judge.Reset();
                return;
            }
            _judge.Tick(svc.BodyCenterX01, Time.time);

            // 사용자 현재 위치 표시 (개발 판단 C6 제안: 발판 위 마커)
            if (_userMarker != null)
            {
                _userMarker.anchorMin = _userMarker.anchorMax = new Vector2(svc.BodyCenterX01, 0.27f);
                _userMarker.anchoredPosition = Vector2.zero;
            }
            if (_elapsedText != null)
            {
                int sec = Mathf.FloorToInt(Time.time - _startTime);
                _elapsedText.text = $"{sec / 60:00}:{sec % 60:00}"; // 경과 시간 — 제한 아님(확정)
            }
        }

        void BuildStage()
        {
            // 바닥면 — 발판이 서는 공간임을 나타낸다 (필수 · 발주서)
            var floor = CreatePanel(_stageRoot, "Floor", new Color(0.3f, 0.27f, 0.22f));
            SetAnchors(floor.rectTransform, new Vector2(0f, 0.05f), new Vector2(1f, 0.32f));

            // 상단 좌측은 일시정지 버튼 자리(장보기 예외 · 2-4) — 라운드 표기는 그 옆으로
            _roundText = CreateText(_stageRoot, "Round", new Vector2(0.26f, 0.93f), new Vector2(300, 50), 34, "");
            _elapsedText = CreateText(_stageRoot, "Elapsed", new Vector2(0.9f, 0.93f), new Vector2(200, 50), 32, "00:00");
            _targetListText = CreateText(_stageRoot, "Targets", new Vector2(0.5f, 0.93f), new Vector2(800, 60), 34, "");
            _guideText = CreateText(_stageRoot, "Guide", new Vector2(0.5f, 0.66f), new Vector2(1000, 60), 34, "");

            // 발판 5 + 점포 4 + 중앙 장바구니 (세로 1:1 정렬)
            for (int i = 0; i < 5; i++)
            {
                var pad = CreatePanel(_stageRoot, $"Pad_{ZoneNames[i]}", PadNormal);
                var padRect = pad.rectTransform;
                padRect.anchorMin = padRect.anchorMax = new Vector2(ZoneCenters[i], 0.16f);
                padRect.sizeDelta = new Vector2(220, 80);
                _pads[i] = pad;

                if (i == ZoneC)
                {
                    var basket = CreatePanel(_stageRoot, "Basket", new Color(0.5f, 0.36f, 0.2f));
                    var basketRect = basket.rectTransform;
                    basketRect.anchorMin = basketRect.anchorMax = new Vector2(ZoneCenters[i], 0.47f);
                    basketRect.sizeDelta = new Vector2(190, 150);
                    CreateText(basket.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(170, 60), 30, "장바구니");
                    _basketPanel = basket;
                }
            }

            foreach (var def in StoreDefs)
            {
                var store = CreatePanel(_stageRoot, $"Store_{def.Name}", StoreNormal);
                var storeRect = store.rectTransform;
                storeRect.anchorMin = storeRect.anchorMax = new Vector2(ZoneCenters[def.Zone], 0.47f);
                storeRect.sizeDelta = new Vector2(200, 160);
                CreateText(store.transform, "Name", new Vector2(0.5f, 0.82f), new Vector2(190, 40), 26, def.Name);
                // 점포 안 큰 상품 카드 1개 (발주서: 복잡한 진열 없음)
                var itemBox = CreatePanel(store.transform, "ItemBox", new Color(0.9f, 0.9f, 0.85f));
                var itemRect = itemBox.rectTransform;
                itemRect.anchorMin = itemRect.anchorMax = new Vector2(0.5f, 0.38f);
                itemRect.sizeDelta = new Vector2(150, 80);
                var itemText = CreateText(itemBox.transform, "Item", new Vector2(0.5f, 0.5f), new Vector2(140, 70), 30, "");
                itemText.color = new Color(0.15f, 0.15f, 0.15f);

                var badge = CreateText(store.transform, "Done", new Vector2(0.5f, 0.08f), new Vector2(150, 34), 24, "샀어요 ✓").gameObject;
                badge.GetComponent<Text>().color = new Color(0.5f, 0.95f, 0.6f);
                badge.SetActive(false);

                _stores[def.Zone] = new StoreView
                {
                    Def = def,
                    Go = store.gameObject,
                    Panel = store,
                    ItemText = itemText,
                    DoneBadge = badge,
                };
            }

            // 사용자 위치 마커
            var marker = CreatePanel(_stageRoot, "UserMarker", new Color(0.3f, 0.85f, 1f));
            _userMarker = marker.rectTransform;
            _userMarker.sizeDelta = new Vector2(46, 46);
        }

        IEnumerator GameLoop()
        {
            for (int round = 1; round <= 10; round++)
                yield return RunRound(round);

            Debug.Log("[Shopping] ===== 10라운드 종료 =====\n" + string.Join("\n", _records));
            _play.DurationSec = Time.time - _startTime; // timeScale=0 정지로 일시정지 시간은 이미 제외됨
            _play.PausedSec = GamePause.AccumulatedSec;
            _guideText.text = "장보기를 마쳤어요";
            _targetListText.text = "";
            yield return new WaitForSeconds(1.2f);
            Finished?.Invoke(_play, _records);
        }

        IEnumerator RunRound(int round)
        {
            bool wide = WidePads(round);
            var activeStoreZones = new List<int>();
            foreach (var def in StoreDefs)
                if (wide || !IsOuter[def.Zone])
                    activeStoreZones.Add(def.Zone);

            // 3발판 구간: 바깥 발판·점포는 비활성이 아니라 아예 미표시 (확정)
            for (int i = 0; i < 5; i++)
            {
                bool visible = wide || !IsOuter[i];
                _pads[i].gameObject.SetActive(visible);
                _pads[i].color = PadNormal;
            }
            foreach (var kv in _stores)
            {
                bool visible = activeStoreZones.Contains(kv.Key);
                kv.Value.Go.SetActive(visible);
                kv.Value.Panel.color = StoreNormal;
                kv.Value.Purchased = false;
                kv.Value.DoneBadge.SetActive(false);
            }

            // 진열: 각 점포에 취급 재료 중 1종 · 한 판 같은 재료 3회 이하
            foreach (int zone in activeStoreZones)
            {
                var store = _stores[zone];
                var candidates = new List<string>();
                foreach (var item in store.Def.Pool)
                    if (!_itemUseCount.ContainsKey(item) || _itemUseCount[item] < 3)
                        candidates.Add(item);
                if (candidates.Count == 0)
                    candidates.AddRange(store.Def.Pool);
                store.Displayed = candidates[_rng.Next(candidates.Count)];
                store.ItemText.text = store.Displayed;
                // 한 판 같은 재료 3회 이하는 「진열」 기준 (04 구성표 진열 규칙 #3)
                _itemUseCount[store.Displayed] = _itemUseCount.TryGetValue(store.Displayed, out var used) ? used + 1 : 1;
            }

            // 목표 발판 선택 — 무작위 제약: 라운드 내 중복 금지 · 외곽 최대 1 · 외곽↔외곽 연속 금지
            int targetCount = TargetCount(round);
            var targets = new List<int>();
            int outerUsed = 0;
            for (int i = 0; i < targetCount; i++)
            {
                var pool = new List<int>();
                foreach (int zone in activeStoreZones)
                {
                    if (targets.Contains(zone))
                        continue;
                    if (IsOuter[zone] && outerUsed >= 1)
                        continue;
                    if (_prevTargetPad >= 0 && IsOuter[zone] && IsOuter[_prevTargetPad] && zone != _prevTargetPad)
                        continue; // 외곽↔외곽 연속 금지 (라운드 경계 포함)
                    pool.Add(zone);
                }
                int picked = pool[_rng.Next(pool.Count)];
                targets.Add(picked);
                if (IsOuter[picked])
                    outerUsed++;
                _prevTargetPad = picked;
            }

            _roundText.text = $"{round} / 10 라운드";
            float roundStart = Time.time;
            bool roundAllCorrect = true;

            // 목표 순서대로 진행: 이동·정지 → 구매 → 중앙 복귀
            var boughtList = new List<(string item, bool correct)>();
            for (int ti = 0; ti < targetCount; ti++)
            {
                int targetZone = targets[ti];
                string targetItem = _stores[targetZone].Displayed;

                UpdateTargetList(targets, boughtList, ti);
                SetLights(targetZone, storeLit: true, padCReturn: false);
                _guideText.text = "빛나는 발판으로 가서 잠깐 멈춰 서 주세요";

                // ① 점포 도착 대기 — 다른 점포에 멈추면 그대로 구매 (실패 없음 · 확정 5-11)
                float waitStart = Time.time;
                float nextHint = waitStart + _hintIntervalSeconds;
                float lastActivityX = BodyXOrNaN();
                int boughtZone = -1;
                int itemHints = 0;
                _arrivals.Clear();
                while (boughtZone < 0)
                {
                    while (_arrivals.Count > 0)
                    {
                        int z = _arrivals.Dequeue();
                        if (z == ZoneC || !activeStoreZones.Contains(z))
                            continue;
                        if (_stores[z].Purchased)
                            continue; // 같은 라운드 이미 구매한 점포 재판정 제외 (확정)
                        boughtZone = z;
                        break;
                    }
                    nextHint = HintTimerTick(nextHint, ref lastActivityX);
                    if (boughtZone < 0 && Time.time >= nextHint)
                    {
                        // 힌트: 목표 재알림이 아니라 「움직여야 한다」 전달 (확정 5-8-1)
                        nextHint = Time.time + _hintIntervalSeconds;
                        itemHints++;
                        yield return FlashGuide("몸을 움직여 빛나는 발판 위에 서 보세요");
                    }
                    yield return null;
                }

                float arriveTime = Time.time - waitStart;
                var boughtStore = _stores[boughtZone];
                bool correct = boughtZone == targetZone;
                boughtStore.Purchased = true;
                boughtStore.DoneBadge.SetActive(true); // 구매 완료 표시 (확정)
                boughtList.Add((boughtStore.Displayed, correct));
                _records.Add($"R{round} 목표{ti + 1}: {targetItem}({ZoneNames[targetZone]}) → {boughtStore.Displayed}({ZoneNames[boughtZone]}) {(correct ? "그대로" : "다르게")} · {arriveTime:F1}s");
                Debug.Log($"[Shopping] {_records[_records.Count - 1]}");

                _play.ShoppingItems.Add(new ShoppingItemRecord
                {
                    Round = round,
                    TargetItem = targetItem,
                    TargetZone = ZoneNames[targetZone],
                    BoughtItem = boughtStore.Displayed,
                    BoughtZone = ZoneNames[boughtZone],
                    Correct = correct,
                    ArriveSec = arriveTime,
                    HintCount = itemHints,
                });
                _play.HintCount += itemHints;
                roundAllCorrect &= correct;

                // ② 중앙 복귀 대기
                SetLights(targetZone, storeLit: false, padCReturn: true);
                _guideText.text = "가운데 장바구니로 돌아와 주세요";
                nextHint = Time.time + _hintIntervalSeconds;
                lastActivityX = BodyXOrNaN();
                bool returned = false;
                _arrivals.Clear();
                while (!returned)
                {
                    while (_arrivals.Count > 0)
                        if (_arrivals.Dequeue() == ZoneC)
                            returned = true;
                    nextHint = HintTimerTick(nextHint, ref lastActivityX);
                    if (!returned && Time.time >= nextHint)
                    {
                        nextHint = Time.time + _hintIntervalSeconds;
                        _play.HintCount++; // 복귀 유도 힌트도 도움 횟수에 포함
                        yield return FlashGuide("가운데 발판으로 돌아와 주세요");
                    }
                    yield return null;
                }

                // 담기 연출 (자리)
                _basketPanel.color = new Color(1f, 0.8f, 0.35f);
                yield return new WaitForSeconds(0.25f);
                _basketPanel.color = new Color(0.5f, 0.36f, 0.2f);
                SetLights(-1, false, false);
            }

            UpdateTargetList(targets, boughtList, targetCount);
            _play.ShoppingRoundSecs.Add(Time.time - roundStart);
            if (roundAllCorrect)
                _play.SuccessCount++; // 모든 목표를 그대로 구매한 라운드 (9-1 확정값으로 재검토)
            _guideText.text = "";
            yield return new WaitForSeconds(0.8f);
        }

        void SetLights(int targetZone, bool storeLit, bool padCReturn)
        {
            for (int i = 0; i < 5; i++)
            {
                if (!_pads[i].gameObject.activeSelf)
                    continue;
                _pads[i].color = i == targetZone && storeLit ? PadLit
                    : i == ZoneC && padCReturn ? PadCReturn
                    : PadNormal;
            }
            foreach (var kv in _stores)
                if (kv.Value.Go.activeSelf)
                    kv.Value.Panel.color = kv.Key == targetZone && storeLit ? StoreLit : StoreNormal;
        }

        void UpdateTargetList(List<int> targets, List<(string item, bool correct)> bought, int currentIndex)
        {
            var parts = new List<string>();
            for (int i = 0; i < targets.Count; i++)
            {
                if (i < bought.Count)
                    parts.Add($"<color=#7ee787>{bought[i].item} ✓</color>");
                else if (i == currentIndex)
                    parts.Add($"<color=#ffd75e>[{_stores[targets[i]].Displayed}]</color>");
                else
                    parts.Add($"<color=#888888>{_stores[targets[i]].Displayed}</color>");
            }
            _targetListText.text = "사야 할 것 :  " + string.Join("   ", parts);
        }

        /// 유의미한 몸 이동은 유효 행동으로 힌트 타이머를 리셋한다 (C4 기준안).
        /// 마지막 유효 지점 대비 변위가 임계 이상일 때만 — 제자리 흔들림은 임계에 못 미쳐 리셋되지 않는다
        float HintTimerTick(float nextHint, ref float lastActivityX)
        {
            var svc = BodyTrackingService.Instance;
            if (svc == null || !svc.BodyValid)
                return nextHint;
            float x = svc.BodyCenterX01;
            if (float.IsNaN(lastActivityX))
            {
                lastActivityX = x;
                return nextHint;
            }
            if (Mathf.Abs(x - lastActivityX) < _hintMoveResetDelta)
                return nextHint;
            lastActivityX = x;
            return Time.time + _hintIntervalSeconds;
        }

        static float BodyXOrNaN()
        {
            var svc = BodyTrackingService.Instance;
            return svc != null && svc.BodyValid ? svc.BodyCenterX01 : float.NaN;
        }

        IEnumerator FlashGuide(string message)
        {
            string prev = _guideText.text;
            _guideText.text = message;
            _guideText.color = new Color(1f, 0.85f, 0.3f);
            yield return new WaitForSeconds(1.5f);
            _guideText.color = Color.white;
            _guideText.text = prev;
        }

        // ---- UI 헬퍼 ----

        Image CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.sizeDelta = Vector2.zero;
        }

        Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 size, int fontSize, string content)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.text = content;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            return text;
        }
    }
}
