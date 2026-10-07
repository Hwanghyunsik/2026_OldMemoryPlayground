using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Flow;
using Shinmyeong.Interaction;
using Shinmyeong.Save;
using Shinmyeong.Tracking;
using Shinmyeong.UI;

namespace Shinmyeong.Games.Shopping
{
    /// 장보기 정식 게임 루프 (무대 그림은 시안 SCR-013: 점포 4종 · 상품 카드 · 발판 5 · 장바구니 · 목표 칩).
    /// 04 게임구성표 확정 데이터 그대로:
    ///   R1~R4 발판 3(L1·C·R1)·점포 2 · R5~ 발판 5·점포 4 · 목표 R1~2:1·R3~7:2·R8~10:3 (합 21 · Q2 질의)
    ///   점포-재료 매핑 고정 · 진열 무작위: 한 판 같은 재료 진열 3회 이하 · 라운드 내 중복 진열은
    ///   점포 풀 4종이 서로 겹치지 않아 자동 충족
    ///   목표 발판 무작위 제약: 라운드 내 중복 금지 · 외곽 최대 1 · 외곽↔외곽 연속 금지
    /// 판정(07 문서 5-8): 도착만 판정(ArrivalJudge) · 다른 점포 정지 = 그대로 구매(실패 없음)
    ///   · 구매 후 중앙 장바구니 복귀로 목표 1건 완료 · 같은 점포 중복 구매 허용(Q4 결정 2026-09-22 — 5-8 「재판정 제외」 폐기)
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
        [Tooltip("힌트 타이머를 리셋하는 유효 이동 변위(C4 기준안) — 발판 간격 0.167의 절반 수준. 제자리 흔들림은 못 미친다")]
        [SerializeField] float _hintMoveResetDelta = 0.08f;

        /// 10라운드 완주 시에만 발생 — 중도 종료는 기록을 남기지 않는다(6-2)
        public event System.Action<PlayRecord, IReadOnlyList<string>> Finished;

        // 발판/영역: 0 L2 · 1 L1 · 2 C · 3 R1 · 4 R2 — 화면 x = 320·640·960·1280·1600 (판정 중심도 같은 비율)
        const int ZoneC = 2;
        static readonly float[] ZoneCenters = { 1f / 6f, 2f / 6f, 3f / 6f, 4f / 6f, 5f / 6f };
        static readonly string[] ZoneNames = { "L2", "L1", "C", "R1", "R2" };
        static readonly bool[] IsOuter = { true, false, false, false, true };
        static float ZoneX(int zone) => ZoneCenters[zone] * 1920f;

        class StoreDef
        {
            public int Zone;
            public string Name;
            public string[] Pool;
            public float W, H, Top;   // 점포 그림 크기·상단 (시안 MarketStalls)
        }

        static readonly StoreDef[] StoreDefs =
        {
            new StoreDef { Zone = 0, Name = "방앗간", Pool = new[] { "쌀가루", "밀가루", "팥", "깨", "밤" }, W = 310, H = 390, Top = 338 },
            new StoreDef { Zone = 1, Name = "채소 가게", Pool = new[] { "호박", "시금치", "당근", "파", "버섯" }, W = 301, H = 349, Top = 373 },
            new StoreDef { Zone = 3, Name = "정육점", Pool = new[] { "소고기", "갈비", "달걀", "두부" }, W = 286, H = 365, Top = 351 },
            new StoreDef { Zone = 4, Name = "건어물·반찬", Pool = new[] { "미역", "김", "김치", "간장", "당면", "곶감" }, W = 298, H = 382, Top = 343 },
        };

        static int TargetCount(int round) => round <= 2 ? 1 : round <= 7 ? 2 : 3;
        static bool WidePads(int round) => round >= 5;

        class StoreView
        {
            public StoreDef Def;
            public GameObject Go;
            public Image Frame;       // 상품 카드 틀 (점등 시 On 그림)
            public Image Item;        // 재료 그림
            public Text ItemText;     // 그림 없을 때 이름
            public Image LabelBg;
            public Text Label;
            public GameObject DoneBadge;
            public RectTransform Light;     // 이펙트가 없을 때만 쓰는 대체 빛 그림
            public RectTransform Card;      // 상품 카드 — 목표 강조·구매 반짝임 이펙트 자리
            public GameObject TargetEffect; // 점등 중 반복 재생 (P_TargetPoint_Game2)
            public string Displayed;
            public bool Purchased;
        }

        RectTransform _stageRoot;
        PlayHud _hud;
        Text _guideText;
        RectTransform _userMarker;
        Image _basketImage;
        readonly Image[] _pads = new Image[5];
        readonly Dictionary<int, StoreView> _stores = new Dictionary<int, StoreView>();
        RectTransform _chipsRoot;

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

        public void Begin(RectTransform host)
        {
            End();

            var rootGo = new GameObject("ShoppingStage");
            rootGo.transform.SetParent(host, false);
            _stageRoot = UiKit.Stretch(rootGo);

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

            // 사용자 현재 위치 표시 (개발 판단 C6 제안: 발판 아래 작은 점)
            if (_userMarker != null)
                _userMarker.anchoredPosition = new Vector2(svc.BodyCenterX01 * 1920f, -1050f);
        }

        void BuildStage()
        {
            // 점포 4 + 상품 카드 (세로 1:1 정렬) — 점등은 뒤의 빛 그림 + On 틀
            foreach (var def in StoreDefs)
            {
                float cx = ZoneX(def.Zone);
                var store = UiKit.Node(_stageRoot, $"Store_{def.Name}", cx - def.W * 0.5f, def.Top, def.W, def.H);
                var stall = UiKit.ImgFit(store, "Stall", null, 0, 0, def.W, def.H);
                if (!ArtCatalog.TryApply(stall, ArtCatalog.Store, def.Name))
                {
                    UiKit.Apply(stall, "Box-Round-23", sliced: true);
                    stall.color = Skin.Hex("8d7b5a");
                    UiKit.Txt(store, "Name", 0, 10, def.W, 40, def.Name, 26, 6, Color.white);
                }
                var light = UiKit.Img(_stageRoot, $"Light_{def.Name}", "SCR-013-Light", 0, 0, 621, 620).rectTransform;
                light.pivot = new Vector2(0.5f, 0.5f);
                light.anchoredPosition = new Vector2(cx, -565f);
                light.gameObject.SetActive(false);

                // 상품 카드 188×202 (점포 위 · 시안 Products)
                var card = UiKit.Node(_stageRoot, $"Product_{def.Name}", cx - 94, 464, 188, 202);
                var frame = UiKit.Img(card, "Frame", "SCR-013-Food-Bg", -26, -25, 239, 239, Color.white, sliced: true);
                var item = UiKit.ImgFit(card, "Food", null, 27, 22, 135, 135);
                var itemText = UiKit.Txt(card, "ItemName", 10, 40, 168, 100, "", 30, 6, Skin.Brown);
                // 목표 강조·구매 반짝임 이펙트는 카드 틀 위 · 재료 그림 뒤 (튜토리얼 영상)
                EffectKit.KeepInFront(item.gameObject);
                EffectKit.KeepInFront(itemText.gameObject);
                var labelBg = UiKit.ImgFit(card, "LabelBackground", "SCR-013-Food-Name-Bg", 24, 163, 140, 48);
                var label = UiKit.Txt(labelBg.transform, "Label", 0, 0, 140, 48, "", 23, 5, Color.white);
                // 구매 완료 표시 (확정) — 카드 오른쪽 위 체크
                var done = UiKit.Node(card, "Done", 140, -20, 55, 55);
                UiKit.Img(done, "Circle", "Circle-55", 0, 0, 55, 55, Color.white);
                UiKit.ImgFit(done, "Check", "Icon-Check", 8, 12, 39, 33, Skin.Green);
                done.gameObject.SetActive(false);

                _stores[def.Zone] = new StoreView
                {
                    Def = def,
                    Go = store.gameObject,
                    Frame = frame,
                    Item = item,
                    ItemText = itemText,
                    LabelBg = labelBg,
                    Label = label,
                    DoneBadge = done.gameObject,
                    Light = light,
                    Card = card,
                };
                card.SetParent(store, true); // 점포와 함께 켜고 끈다
                light.SetAsFirstSibling();
            }

            // 중앙 장바구니
            UiKit.ImgFit(_stageRoot, "BasketShadow", "Basket-Shadow", 840, 623, 243, 97);
            _basketImage = UiKit.ImgFit(_stageRoot, "Basket", "Basket-02", 830, 472, 260, 263);

            // 안내 문구 알약 (발판 위)
            var guide = UiKit.Node(_stageRoot, "MovementGuide", 712, 755, 496, 54);
            UiKit.Pill(guide, "Background", 0, 0, 496, 54, Skin.DimBrown);
            _guideText = UiKit.Txt(guide, "Label", 0, 0, 496, 54, "", 23, 5, Skin.Cream);

            // 발판 5 (입체 발판 그림 · 버튼 형태 아님)
            for (int i = 0; i < 5; i++)
            {
                var pad = UiKit.ImgFit(_stageRoot, $"Pad_{ZoneNames[i]}", "SCR-013-FoodBoard", 0, 0, 319, 255);
                pad.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                pad.rectTransform.anchoredPosition = new Vector2(ZoneX(i), -953f);
                _pads[i] = pad;
            }

            // 사용자 위치 마커
            var marker = UiKit.Img(_stageRoot, "UserMarker", "Circle-52", 0, 0, 30, 30, Skin.Blue);
            marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _userMarker = marker.rectTransform;

            // 공통 HUD — 목표 칩 3개는 GoalRoot에 직접 그린다 · 경과 시간 · 진행 레일 (2-5 확정)
            _hud = PlayHud.Create(_stageRoot);
            _hud.UseCustomGoal();
            _chipsRoot = UiKit.Group(_hud.GoalRoot, "TargetChips");
        }

        IEnumerator GameLoop()
        {
            for (int round = 1; round <= 10; round++)
                yield return RunRound(round);

            Debug.Log("[Shopping] ===== 10라운드 종료 =====\n" + string.Join("\n", _records));
            _play.DurationSec = Time.time - _startTime; // timeScale=0 정지로 일시정지 시간은 이미 제외됨
            _play.PausedSec = GamePause.AccumulatedSec;
            ClearChips();
            _guideText.text = "장보기를 마쳤어요";
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
                _pads[i].gameObject.SetActive(wide || !IsOuter[i]);
            foreach (var kv in _stores)
            {
                kv.Value.Go.SetActive(activeStoreZones.Contains(kv.Key));
                kv.Value.Purchased = false;
                kv.Value.DoneBadge.SetActive(false);
            }
            SetLights(-1, false, false);

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
                bool itemArt = ArtCatalog.TryApply(store.Item, ArtCatalog.Ingredient, store.Displayed);
                store.Item.gameObject.SetActive(itemArt);
                store.ItemText.text = itemArt ? "" : store.Displayed;
                store.Label.text = store.Displayed;
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

            _hud.SetRound(round); // 라운드 표시는 HUD 진행 레일 하나뿐 (중복 배치 금지)
            float roundStart = Time.time;
            bool roundAllCorrect = true;

            // 목표 순서대로 진행: 이동·정지 → 구매 → 중앙 복귀
            var boughtList = new List<(string item, bool correct)>();
            for (int ti = 0; ti < targetCount; ti++)
            {
                int targetZone = targets[ti];
                string targetItem = _stores[targetZone].Displayed;

                UpdateTargetChips(targets, boughtList, ti);
                SetLights(targetZone, storeLit: true, padCReturn: false);
                _guideText.text = "빛나는 발판으로 이동하세요"; // 8-6-1 확정 문구

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
                        // 이미 구매한 점포도 다시 판정한다 (2026-09-22 결정 · Q4): 「이미 구매한 점포 재판정 제외」(5-8)를
                        // 그대로 두면 나중 목표 점포를 먼저 산 뒤 그 점포가 점등됐을 때 진행이 막힌다(소프트락)
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
                SetLights(-1, false, false); // 목표 강조를 먼저 끄고 담김 반짝임을 보인다 (튜토리얼 영상 순서)
                EffectKit.Play(EffectKit.ShoppingGet, boughtStore.Card);
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

                // 담기 연출 — 장바구니가 살짝 커졌다 돌아온다
                yield return BasketPop();
                SetLights(-1, false, false);
            }

            UpdateTargetChips(targets, boughtList, targetCount);
            _play.ShoppingRoundSecs.Add(Time.time - roundStart);
            if (roundAllCorrect)
                _play.SuccessCount++; // 모든 목표를 그대로 구매한 라운드 (9-1 확정값으로 재검토)
            _guideText.text = "";
            yield return new WaitForSeconds(0.8f);
        }

        IEnumerator BasketPop()
        {
            var rect = _basketImage.rectTransform;
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float s = 1f + 0.12f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.3f));
                rect.localScale = Vector3.one * s;
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        void SetLights(int targetZone, bool storeLit, bool padCReturn)
        {
            for (int i = 0; i < 5; i++)
            {
                if (!_pads[i].gameObject.activeSelf)
                    continue;
                bool lit = (i == targetZone && storeLit) || (i == ZoneC && padCReturn);
                UiKit.Apply(_pads[i], lit ? "SCR-013-FoodBoard-On" : "SCR-013-FoodBoard");
                _pads[i].preserveAspect = true;
            }
            foreach (var kv in _stores)
            {
                bool lit = kv.Key == targetZone && storeLit && kv.Value.Go.activeSelf;
                var store = kv.Value;
                // 목표 강조 — 디자이너 이펙트(빛 테 + 퍼지는 빛)를 점등 동안 반복 · 없으면 대체 빛 그림
                if (lit && store.TargetEffect == null)
                    store.TargetEffect = EffectKit.Play(EffectKit.ShoppingTarget, store.Card, loop: true);
                else if (!lit)
                    EffectKit.Stop(ref store.TargetEffect);
                store.Light.gameObject.SetActive(lit && store.TargetEffect == null);
                UiKit.Apply(store.Frame, lit ? "SCR-013-Food-Bg-On" : "SCR-013-Food-Bg", sliced: !lit);
                UiKit.Apply(store.LabelBg, lit ? "SCR-013-Food-Name-Bg_On" : "SCR-013-Food-Name-Bg");
                store.LabelBg.preserveAspect = true;
            }
        }

        /// 목표 칩 3개 (시안 헤더): 산 것 = 초록 채움 + 체크 · 지금 = 흰 칸 + 재료 그림 · 다음 = 흐리게
        void UpdateTargetChips(List<int> targets, List<(string item, bool correct)> bought, int currentIndex)
        {
            ClearChips();
            int n = targets.Count;
            float total = n * 353 + (n - 1) * 11;
            float start = (1184 - total) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                string item = i < bought.Count ? bought[i].item : _stores[targets[i]].Displayed;
                var chip = UiKit.Node(_chipsRoot, $"Chip{i + 1}", start + i * 364, 42, 353, 98);
                if (i < bought.Count)
                {
                    UiKit.Img(chip, "Face", "Box-Round-15", 0, 0, 353, 98, Skin.Green, sliced: true);
                    UiKit.Img(chip, "Circle", "Circle-55", 26, 21, 55, 55, Color.white);
                    UiKit.ImgFit(chip, "Check", "Icon-Check", 34, 25, 43, 39, Skin.Green);
                    UiKit.Txt(chip, "Label", 135, 0, 210, 98, item, 40, 6, Color.white);
                    continue;
                }
                bool current = i == currentIndex;
                UiKit.Img(chip, "Face", "Box-Round-15", 0, 0, 353, 98, Skin.Face, sliced: true);
                if (current)
                    UiKit.Img(chip, "Outline", "Box-Round-15-Outline", 0, 0, 353, 98, Skin.Green, sliced: true);
                var art = ArtCatalog.Get(ArtCatalog.Ingredient, item);
                if (art != null)
                {
                    var food = UiKit.ImgFit(chip, "Food", null, current ? 4 : 30, current ? 0 : 8, current ? 140 : 83, current ? 98 : 83,
                        current ? Color.white : new Color(1f, 1f, 1f, 0.35f));
                    food.sprite = art;
                }
                UiKit.Txt(chip, "Label", 135, 0, 210, 98, item, 40, 6, current ? Skin.BrownDark : Skin.Outline2);
            }
        }

        void ClearChips()
        {
            for (int i = _chipsRoot.childCount - 1; i >= 0; i--)
                Destroy(_chipsRoot.GetChild(i).gameObject);
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
            _guideText.color = Skin.Gold;
            yield return new WaitForSeconds(1.5f);
            _guideText.color = Skin.Cream;
            _guideText.text = prev;
        }
    }
}
