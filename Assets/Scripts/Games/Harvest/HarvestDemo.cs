using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Flow;
using Shinmyeong.Interaction;
using Shinmyeong.Save;
using Shinmyeong.UI;

namespace Shinmyeong.Games.Harvest
{
    /// 수확하기 정식 게임 루프 (무대 그림은 시안 SCR-008: 넝쿨·나무에 매달린 작물 + 바구니).
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
        // 넝쿨·나무 그림 6종을 자리마다 번갈아 건다 (폭 · 시안 HarvestPlants + 2026-09-23 추가 수령 Tree/Vine-03)
        static readonly (string sprite, float w, float h)[] Plants =
        {
            ("SCR-008-Tree-01", 265, 567), ("SCR-008-Vine-01", 147, 570),
            ("SCR-008-Tree-02", 262, 563), ("SCR-008-Vine-02", 174, 575),
            ("SCR-008-Tree-03", 262, 560), ("SCR-008-Vine-03", 151, 573),
        };
        const float SlotLeft = 570f, SlotRight = 1330f;   // 작물 자리 중심 x 범위 (시안 4자리 기준)
        const float CropTop = 390f, CropW = 200f, CropH = 240f;
        const float ArrowSize = 120f, ArrowGap = 16f, ArrowTravel = 40f; // 목표 강조 아래 방향 화살표 — 작물 바로 아래에서 아래로 내려가는 움직임
        static readonly Vector2 BasketCenter = new Vector2(960f, 900f);
        static readonly Vector2 ShowPoint = new Vector2(960f, 640f); // 수확한 작물을 보여 주는 자리 — 바구니 바로 위 가운데 (튜토리얼 영상)

        enum Kind { Target, Decoy, Bug }

        class Crop
        {
            public Kind Kind;
            public string Name;
            public GameObject Go;
            public RectTransform Rect;
            public Image Image;
            public Vector2 Center;
        }

        /// 10라운드 완주 시에만 발생 — 중도 종료는 기록을 남기지 않는다(6-2)
        public event System.Action<PlayRecord, IReadOnlyList<string>> Finished;

        PlayRecord _play;
        float _startTime;
        RectTransform _stageRoot;
        RectTransform _plantsRoot;
        RectTransform _cropsRoot;
        PlayHud _hud;
        Image _basketImage;
        RectTransform _glow;
        RectTransform _arrow;
        Image _arrowImage;
        readonly List<Crop> _crops = new List<Crop>();
        readonly List<string> _records = new List<string>();
        readonly Dictionary<string, int> _targetUseCount = new Dictionary<string, int>(); // 같은 작물 한 판 2회 이하 (확정)
        PullTarget _pulledTarget;
        GameObject _targetEffect;
        readonly System.Random _rng = new System.Random();
        bool _subscribed;

        public void Begin(RectTransform host)
        {
            End();

            var rootGo = new GameObject("HarvestStage");
            rootGo.transform.SetParent(host, false);
            _stageRoot = UiKit.Stretch(rootGo);

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
            _plantsRoot = UiKit.Group(_stageRoot, "HarvestPlants");
            // 목표 강조 빛 (작물 뒤) — 디자이너 이펙트(P_TargetPoint_Game1)가 없을 때만 쓰는 대체 그림
            _glow = UiKit.Img(_stageRoot, "TargetGlow", "SCR-013-Light", 0, 0, 420, 420).rectTransform;
            _glow.pivot = new Vector2(0.5f, 0.5f);
            _glow.gameObject.SetActive(false);

            // 바구니 (하단 중앙 · 시안 HarvestBasket) — 작물보다 먼저 만들어 작물이 바구니 앞에 그려진다(담김 연출)
            UiKit.Img(_stageRoot, "BasketShadow", "Circle-125", 788, 964, 329, 70, Skin.Hex("402108", 0.3f));
            _basketImage = UiKit.ImgFit(_stageRoot, "Basket", "Basket", 784, 769, 353, 260);

            _cropsRoot = UiKit.Group(_stageRoot, "HarvestMaterials");

            // 목표 강조 아래 방향 화살표 (04 구성표 · 05 발주서 03 시트 · 07 4-3) — 왼쪽 화살표 그림을 아래로 돌려 쓴다
            _arrowImage = UiKit.ImgFit(_stageRoot, "TargetArrow", "Icon-Arrow-02", 0, 0, ArrowSize, ArrowSize, Skin.Orange);
            _arrow = _arrowImage.rectTransform;
            _arrow.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var outline = _arrowImage.gameObject.AddComponent<Outline>(); // 넝쿨·나무 위에서도 보이게 흰 테두리
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(4f, -4f);
            _arrow.gameObject.SetActive(false);

            _hud = PlayHud.Create(_stageRoot); // 공통 HUD — 목표 패널·경과 시간·진행 레일 (2-5 확정)
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

            BuildPlants(count);
            int decoyIdx = 0;
            for (int i = 0; i < count; i++)
            {
                var kind = kinds[i];
                string name = kind == Kind.Decoy ? decoyPool[decoyIdx++] : targetName;
                _crops.Add(CreateCrop(kind, name, SlotX(count, i)));
            }

            // 라운드 표시는 HUD 진행 레일 하나뿐(중복 배치 금지) · 안내는 8-6-1 확정 문구 + 목표 그림
            _hud.SetRound(round);
            _hud.SetGoal($"「{targetName}」 손을 대고 아래로 당겨 주세요", ArtCatalog.Get(ArtCatalog.CropHanging, targetName));
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
            EffectKit.Stop(ref _targetEffect);

            var pulled = _crops.Find(c => c.Go == _pulledTarget.gameObject);
            string result = pulled.Kind == Kind.Target ? "정답" : pulled.Kind == Kind.Bug ? "벌레" : "다른 선택";
            _records.Add($"R{round}: {pulled.Name}({result}) · 반응 {reaction:F1}s · 힌트 {hintCount}회");
            Debug.Log($"[Harvest] {_records[_records.Count - 1]}");

            _play.HarvestRounds.Add(new HarvestRoundRecord
            {
                Round = round,
                Target = targetName,
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
            if (count == 1)
                return 960f;
            return Mathf.Lerp(SlotLeft, SlotRight, index / (float)(count - 1));
        }

        /// 자리 수만큼 넝쿨·나무를 건다 (3·4·5개 어느 배치에서도 작물 크기는 같다 · 발주서 02)
        void BuildPlants(int count)
        {
            for (int i = _plantsRoot.childCount - 1; i >= 0; i--)
                Destroy(_plantsRoot.GetChild(i).gameObject);
            for (int i = 0; i < count; i++)
            {
                var (sprite, w, h) = Plants[i % Plants.Length];
                UiKit.ImgFit(_plantsRoot, $"Plant{i + 1}", sprite, SlotX(count, i) - w * 0.5f, 246, w, h);
            }
        }

        Crop CreateCrop(Kind kind, string name, float centerX)
        {
            var go = new GameObject($"Crop_{name}_{kind}");
            go.transform.SetParent(_cropsRoot, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            var rect = UiKit.PlaceCentered(go.GetComponent<RectTransform>(), centerX - CropW * 0.5f, CropTop, CropW, CropH);

            // 자산이 있으면 매달린/벌레 그림, 없으면 색+라벨 플레이스홀더 (Docs/92).
            // 벌레 그림이 없는 작물은 성한 그림 + 벌레 아이콘 병기(상시 식별 표식 · 강조 없이 구분)
            bool hasArt = ArtCatalog.TryApply(img, kind == Kind.Bug ? ArtCatalog.CropBug : ArtCatalog.CropHanging, name);
            bool bugFallback = false;
            if (!hasArt && kind == Kind.Bug)
                bugFallback = hasArt = ArtCatalog.TryApply(img, ArtCatalog.CropHanging, name);
            if (!hasArt)
            {
                UiKit.Apply(img, "Box-Round-23", sliced: true);
                img.color = kind == Kind.Bug ? Skin.Hex("8a6a3a") : Skin.Hex("6fae5a");
                UiKit.Txt(go.transform, "Label", 0, 0, CropW, CropH, kind == Kind.Bug ? $"{name}\n(벌레)" : name, 30, 6, Color.white);
            }
            if (bugFallback)
            {
                // 비율 유지로 축소된 실제 그림 영역을 구해 그 오른쪽 아래에 벌레를 붙인다 (사각형 기준이면 공중에 뜬다)
                var sp = img.sprite;
                float scale = Mathf.Min(CropW / sp.rect.width, CropH / sp.rect.height);
                float drawnW = sp.rect.width * scale, drawnH = sp.rect.height * scale;
                float left = (CropW - drawnW) * 0.5f, top = (CropH - drawnH) * 0.5f;
                UiKit.ImgFit(go.transform, "Bug", "Bug", left + drawnW * 0.62f, top + drawnH * 0.55f, 60, 60);
            }

            go.AddComponent<PullTarget>();
            EffectKit.KeepInFront(go); // 목표 강조는 작물 뒤에서 퍼진다 (담김 반짝임은 front로 작물 앞)
            return new Crop
            {
                Kind = kind,
                Name = name,
                Go = go,
                Rect = rect,
                Image = img,
                Center = new Vector2(centerX, CropTop + CropH * 0.5f),
            };
        }

        IEnumerator Highlight(Crop target)
        {
            // 목표 강조 이펙트(빛 테 + 퍼지는 빛 · 튜토리얼 영상과 같은 연출) — 한 주기 재생, 당기면 바로 지운다
            EffectKit.Stop(ref _targetEffect);
            _targetEffect = EffectKit.Play(EffectKit.HarvestTarget, target.Rect);
            _glow.anchoredPosition = new Vector2(target.Center.x, -target.Center.y);
            _glow.gameObject.SetActive(_targetEffect == null);
            _glow.SetAsFirstSibling();
            _plantsRoot.SetAsFirstSibling();
            float arrowTop = target.Center.y + CropH * 0.5f + ArrowGap + ArrowSize * 0.5f;
            _arrow.gameObject.SetActive(true);
            float t = 0f;
            while (t < _highlightSeconds)
            {
                t += Time.deltaTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 12f);
                target.Rect.localScale = Vector3.one * (1f + 0.1f * pulse);
                _glow.localScale = Vector3.one * (0.9f + 0.2f * pulse);
                // 화살표는 위에서 아래로만 반복해 내려간다 (당기는 방향 안내 · 위로 튀어 오르지 않게 톱니 모양)
                float drop = Mathf.Repeat(t * 2f, 1f);
                _arrow.anchoredPosition = new Vector2(target.Center.x, -(arrowTop + ArrowTravel * drop));
                var c = _arrowImage.color;
                c.a = 1f - 0.6f * drop * drop;
                _arrowImage.color = c;
                yield return null;
            }
            target.Rect.localScale = Vector3.one;
            _glow.gameObject.SetActive(false);
            _arrow.gameObject.SetActive(false);
        }

        /// 정답 수확 연출 (튜토리얼 영상 순서): ① 가운데(바구니 위)로 이동 → ② 담김 반짝임 → ③ 작아지며 바구니로
        IEnumerator ArcIntoBasket(Crop crop)
        {
            // ① 가운데로 이동 — 부드럽게 감속
            yield return MoveCrop(crop, crop.Center, ShowPoint, 1f, 1f, 0.45f);

            // ② 그 자리에서 반짝임 (작물 앞) — 테가 퍼지는 동안 잠시 머문다
            EffectKit.Play(EffectKit.HarvestGet, crop.Rect, front: true);
            yield return new WaitForSeconds(0.5f);

            // ③ 작아지며 바구니 안으로
            yield return MoveCrop(crop, ShowPoint, BasketCenter + new Vector2(0f, -30f), 1f, 0.45f, 0.4f);
            Destroy(crop.Go);
            // 바구니 반짝임
            _basketImage.color = new Color(1f, 0.9f, 0.6f);
            yield return new WaitForSeconds(0.15f);
            _basketImage.color = Color.white;
        }

        IEnumerator MoveCrop(Crop crop, Vector2 from, Vector2 to, float scaleFrom, float scaleTo, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                float e = 1f - (1f - u) * (1f - u); // 감속
                var pos = Vector2.Lerp(from, to, e);
                crop.Rect.anchoredPosition = new Vector2(pos.x, -pos.y);
                crop.Rect.localScale = Vector3.one * Mathf.Lerp(scaleFrom, scaleTo, e);
                yield return null;
            }
        }

        IEnumerator FlyOff(Crop crop)
        {
            // 상단 대각선으로 회전하며 퇴장 — 사용자 방향(아래)으로 날아오지 않는다
            var from = crop.Center;
            float dir = from.x < 960f ? -1f : 1f;
            var to = from + new Vector2(dir * 650f, -700f);
            float t = 0f;
            const float duration = 0.6f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                var pos = Vector2.Lerp(from, to, u);
                crop.Rect.anchoredPosition = new Vector2(pos.x, -pos.y);
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
    }
}
