using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;
using Shinmyeong.Save;
using Shinmyeong.UI;

namespace Shinmyeong.Games.Harvest
{
    /// 수확하기 정식 게임 루프 (그래픽은 X-Box 플레이스홀더 — 자산 도입 시 교체).
    /// 04 게임구성표 라운드 규칙 그대로: R1~3 3지(벌레0) · R4~7 4지(벌레1) · R8~10 5지(벌레1)
    ///   · 목표 작물은 한 판 안에서 같은 작물 2회 이하(확정).
    /// 힌트(3-2 확정): 10초 무수확 시 목표 재강조 반복 — 다른 작물 수확 = 라운드 종료라 타이머
    ///   리셋 규칙 자체가 불필요(C4) · 주기 10초 반복·최대 횟수 없음은 C9 기준안.
    /// 화면 플로우에서 Begin/End로 켜고 끄며, 10라운드 종료 시 Finished를 낸다.
    public class HarvestDemo : MonoBehaviour
    {
        [SerializeField] float _highlightSeconds = 1f;
        [SerializeField] float _hintIntervalSeconds = 10f;

        static readonly string[] CropNames = { "호박", "오이", "가지", "토마토", "고추", "대추", "감", "밤" };

        enum Kind { Target, Decoy, Bug }

        class Crop
        {
            public Kind Kind;
            public string Name;
            public GameObject Go;
            public RectTransform Rect;
            public Image Image;
        }

        /// 10라운드 완주 시에만 발생 — 중도 종료는 기록을 남기지 않는다(6-2)
        public event System.Action<PlayRecord, IReadOnlyList<string>> Finished;

        PlayRecord _play;
        float _startTime;
        RectTransform _stageRoot;
        PlayHud _hud;
        RectTransform _basket;
        Image _basketImage;
        GameObject _arrow;
        readonly List<Crop> _crops = new List<Crop>();
        readonly List<string> _records = new List<string>();
        readonly Dictionary<string, int> _targetUseCount = new Dictionary<string, int>(); // 같은 작물 한 판 2회 이하 (확정)
        PullTarget _pulledTarget;
        readonly System.Random _rng = new System.Random();
        bool _subscribed;

        public void Begin(RectTransform host)
        {
            End();

            var rootGo = new GameObject("HarvestStage");
            rootGo.transform.SetParent(host, false);
            var rect = rootGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            _stageRoot = rect;

            BuildStage();

            if (!_subscribed && PullJudge.Instance != null)
            {
                PullJudge.Instance.Pulled += OnPulled;
                _subscribed = true;
            }

            _records.Clear();
            _targetUseCount.Clear();
            _play = PlayRecord.Start("Harvest");
            GamePause.ResetAccumulated();
            _startTime = Time.time;
            StartCoroutine(GameLoop());
        }

        public void End()
        {
            StopAllCoroutines();
            _crops.Clear();
            if (_stageRoot != null)
            {
                Destroy(_stageRoot.gameObject);
                _stageRoot = null;
            }
            if (PullJudge.Instance != null)
                PullJudge.Instance.JudgingEnabled = true;
        }

        void OnDestroy()
        {
            if (_subscribed && PullJudge.Instance != null)
                PullJudge.Instance.Pulled -= OnPulled;
        }

        void OnPulled(PullTarget target) => _pulledTarget = target;

        void BuildStage()
        {
            _hud = PlayHud.Create(_stageRoot); // 공통 HUD — 목표 패널·경과 시간·진행 레일 (2-5 확정)

            // 하단 중앙은 일시정지 버튼 자리(2-4 확정) — 바구니는 우측으로 (임시 배치 · 자산 적용 시 정리)
            var basketGo = CreateImage(_stageRoot, "Basket", new Vector2(0.74f, 0.16f), new Vector2(240, 130),
                new Color(0.55f, 0.38f, 0.2f), false);
            _basket = basketGo.GetComponent<RectTransform>();
            _basketImage = basketGo.GetComponent<Image>();
            CreateText(basketGo.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(200, 60), 32, "바구니");

            _arrow = CreateText(_stageRoot, "TargetArrow", new Vector2(0.5f, 0.5f), new Vector2(80, 80), 60, "▼").gameObject;
            _arrow.GetComponent<Text>().color = new Color(1f, 0.85f, 0.1f);
            _arrow.SetActive(false);
        }

        IEnumerator GameLoop()
        {
            for (int round = 1; round <= 10; round++)
                yield return RunRound(round);

            Debug.Log("[Harvest] ===== 10라운드 종료 =====\n" + string.Join("\n", _records));
            _play.DurationSec = Time.time - _startTime; // timeScale=0 정지로 일시정지 시간은 이미 제외됨
            _play.PausedSec = GamePause.AccumulatedSec;
            _hud.SetGoal("수확을 마쳤어요");
            yield return new WaitForSeconds(1.2f);
            Finished?.Invoke(_play, _records);
        }

        IEnumerator RunRound(int round)
        {
            int count = round <= 3 ? 3 : round <= 7 ? 4 : 5;
            bool hasBug = round >= 4;

            // 작물 배정 — 목표 1 · 벌레(목표와 같은 종) 0~1 · 나머지 다른 작물.
            // 목표는 한 판 같은 작물 2회 이하 (확정 · 8종×2=16 ≥ 10라운드라 후보가 마르지 않는다)
            var targetPool = new List<string>();
            foreach (var n in CropNames)
                if (!_targetUseCount.TryGetValue(n, out var used) || used < 2)
                    targetPool.Add(n);
            string targetName = targetPool[_rng.Next(targetPool.Count)];
            _targetUseCount[targetName] = _targetUseCount.TryGetValue(targetName, out var uc) ? uc + 1 : 1;
            var decoyPool = new List<string>();
            foreach (var n in CropNames)
                if (n != targetName)
                    decoyPool.Add(n);
            Shuffle(decoyPool);

            var kinds = new List<Kind> { Kind.Target };
            if (hasBug)
                kinds.Add(Kind.Bug);
            while (kinds.Count < count)
                kinds.Add(Kind.Decoy);
            Shuffle(kinds);

            int decoyIdx = 0;
            for (int i = 0; i < count; i++)
            {
                var kind = kinds[i];
                string name = kind == Kind.Decoy ? decoyPool[decoyIdx++] : targetName;
                _crops.Add(CreateCrop(kind, name, new Vector2(SlotX(count, i), 0.68f)));
            }

            // 라운드 표시는 HUD 진행 레일 하나뿐(중복 배치 금지) · 안내는 8-6-1 확정 문구 + 목표 데이터
            _hud.SetRound(round);
            _hud.SetGoal($"「{targetName}」 손을 대고 아래로 당겨 주세요");
            if (PullJudge.Instance != null)
                PullJudge.Instance.JudgingEnabled = false;

            // 목표 강조 약 1초 — 끝나는 시점이 반응 시간 t=0
            var target = _crops.Find(c => c.Kind == Kind.Target);
            yield return Highlight(target);
            float t0 = Time.time;
            float nextHintAt = t0 + _hintIntervalSeconds;
            int hintCount = 0;
            if (PullJudge.Instance != null)
                PullJudge.Instance.JudgingEnabled = true;

            _pulledTarget = null;
            while (_pulledTarget == null)
            {
                if (Time.time >= nextHintAt)
                {
                    hintCount++;
                    nextHintAt = Time.time + _hintIntervalSeconds;
                    yield return Highlight(target); // 자동 힌트 = 목표 재강조 · TTS 없음
                }
                yield return null;
            }

            if (PullJudge.Instance != null)
                PullJudge.Instance.JudgingEnabled = false;
            float reaction = Time.time - t0;

            var pulled = _crops.Find(c => c.Go == _pulledTarget.gameObject);
            string result = pulled.Kind == Kind.Target ? "정답" : pulled.Kind == Kind.Bug ? "벌레" : "다른 선택";
            _records.Add($"R{round}: {pulled.Name}({result}) · 반응 {reaction:F1}s · 힌트 {hintCount}회");
            Debug.Log($"[Harvest] {_records[_records.Count - 1]}");

            _play.HarvestRounds.Add(new HarvestRoundRecord
            {
                Round = round,
                Picked = pulled.Name,
                Result = result,
                ReactionSec = reaction,
                HintCount = hintCount,
            });
            _play.HintCount += hintCount;
            if (pulled.Kind == Kind.Target)
                _play.SuccessCount++; // 힌트 후 성공도 성공 (확정)

            if (pulled.Kind == Kind.Target)
                yield return ArcIntoBasket(pulled);
            else
                yield return FlyOff(pulled);

            foreach (var crop in _crops)
                if (crop.Go != null)
                    Destroy(crop.Go);
            _crops.Clear();
            yield return new WaitForSeconds(0.6f);
        }

        static float SlotX(int count, int index)
        {
            const float left = 0.28f, right = 0.72f;
            if (count == 1)
                return 0.5f;
            return Mathf.Lerp(left, right, index / (float)(count - 1));
        }

        Crop CreateCrop(Kind kind, string name, Vector2 anchor)
        {
            // 벌레 먹은 것만 상시 식별 표식(어두운 색+표기) · 다른 작물은 목표와 같은 표현
            var color = kind == Kind.Bug ? new Color(0.45f, 0.35f, 0.2f) : new Color(0.4f, 0.7f, 0.35f);
            var go = CreateImage(_stageRoot, $"Crop_{name}_{kind}", anchor, new Vector2(160, 160), color, true);
            // 자산이 있으면 매달린/벌레 그림으로 교체, 없으면 색+라벨 플레이스홀더 (Docs/92)
            bool hasArt = ArtCatalog.TryApply(go.GetComponent<Image>(),
                kind == Kind.Bug ? ArtCatalog.CropBug : ArtCatalog.CropHanging, name);
            if (!hasArt)
                CreateText(go.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(150, 60), 30,
                    kind == Kind.Bug ? $"{name}\n(벌레)" : name);
            go.AddComponent<PullTarget>();
            return new Crop
            {
                Kind = kind,
                Name = name,
                Go = go,
                Rect = go.GetComponent<RectTransform>(),
                Image = go.GetComponent<Image>(),
            };
        }

        IEnumerator Highlight(Crop target)
        {
            var arrowRect = _arrow.GetComponent<RectTransform>();
            arrowRect.anchorMin = arrowRect.anchorMax = target.Rect.anchorMin + new Vector2(0f, 0.12f);
            arrowRect.anchoredPosition = Vector2.zero;
            _arrow.SetActive(true);
            var baseColor = target.Image.color;
            float t = 0f;
            while (t < _highlightSeconds)
            {
                t += Time.deltaTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 12f);
                target.Image.color = Color.Lerp(baseColor, new Color(1f, 0.95f, 0.4f), pulse * 0.7f);
                target.Rect.localScale = Vector3.one * (1f + 0.08f * pulse);
                yield return null;
            }
            target.Image.color = baseColor;
            target.Rect.localScale = Vector3.one;
            _arrow.SetActive(false);
        }

        IEnumerator ArcIntoBasket(Crop crop)
        {
            var from = crop.Rect.anchorMin;
            var to = _basket.anchorMin + new Vector2(0f, 0.04f);
            float t = 0f;
            const float duration = 0.7f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                var pos = Vector2.Lerp(from, to, u);
                pos.y += 0.12f * Mathf.Sin(Mathf.PI * u); // 포물선
                crop.Rect.anchorMin = crop.Rect.anchorMax = pos;
                crop.Rect.anchoredPosition = Vector2.zero;
                crop.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.5f, u);
                yield return null;
            }
            Destroy(crop.Go);
            // 바구니 반짝임
            var baseColor = _basketImage.color;
            _basketImage.color = new Color(1f, 0.85f, 0.3f);
            yield return new WaitForSeconds(0.15f);
            _basketImage.color = baseColor;
        }

        IEnumerator FlyOff(Crop crop)
        {
            // 상단 대각선으로 회전하며 퇴장 — 사용자 방향(아래)으로 날아오지 않는다
            var from = crop.Rect.anchorMin;
            float dir = from.x < 0.5f ? -1f : 1f;
            var to = from + new Vector2(dir * 0.35f, 0.6f);
            float t = 0f;
            const float duration = 0.6f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                crop.Rect.anchorMin = crop.Rect.anchorMax = Vector2.Lerp(from, to, u);
                crop.Rect.anchoredPosition = Vector2.zero;
                crop.Rect.localRotation = Quaternion.Euler(0, 0, dir * -540f * u);
                yield return null;
            }
            Destroy(crop.Go);
        }

        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        GameObject CreateImage(Transform parent, string name, Vector2 anchor, Vector2 size, Color color, bool round)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (round)
            {
#if UNITY_EDITOR
                img.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
#endif
            }
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            return go;
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
            text.text = content;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            return text;
        }
    }
}
