using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Save;

namespace Shinmyeong.Flow.Screens
{
    /// 간이 결과 공통 뼈대 (8-4 확정): 수치·버튼·일시정지 없음 · 수집물 순차 등장 ·
    /// 응원 문구 + 다음 활동 예고 · 자동 진행 6초(기준안) · 남은 시간 숫자 금지 — 게이지로만.
    /// 수집물 구분(v2.4 확정): 위 선반 = 제대로 담긴 것 / 아래 단 = 따로 담긴 것(기울여 배치) ·
    /// 종류 단위 묶음 + 개수 배지 · 라운드 번호 미표시. TTS 허용 — 음성 도입 시 여기서 재생.
    /// 화면 자체는 게임별 독립 페이지(확정) — 파생 클래스가 문구·데이터·배색을 각자 정의한다.
    public abstract class StoryResultBase : ScreenBase
    {
        protected const float AutoSeconds = 6f; // 기준안 · 프로토타입 검증 후 조정

        protected class Collectible
        {
            public string Label;
            public int Count;    // 종류 묶음 개수 (1이면 배지 생략)
            public bool Proper;  // true = 위 선반 / false = 아래 단(기울임)
        }

        Image _gaugeFill;
        readonly List<GameObject> _items = new List<GameObject>();

        protected abstract Color Background { get; }
        protected abstract string TitleText { get; }    // 결과 상태 (8-6-1)
        protected abstract string ConfirmText { get; }  // 확인 문구 (없으면 null)
        protected abstract string NextText { get; }     // 다음 단계 권유
        protected abstract ScreenId NextScreen { get; }
        protected abstract List<Collectible> BuildCollectibles();

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", Background);
            UiKit.Label(transform, "Title", new Vector2(0.5f, 0.85f), new Vector2(1100, 90), 58, TitleText);
            if (ConfirmText != null)
                UiKit.Label(transform, "Confirm", new Vector2(0.5f, 0.76f), new Vector2(1000, 50), 34, ConfirmText);

            // 위 선반 / 아래 단 — 위·아래 위치와 기울임으로만 구분 (텍스트·O/X·색상 단독 구분 금지)
            UiKit.Panel(transform, "Shelf", new Color(1f, 1f, 1f, 0.10f))
                .rectTransform.SetSizeWithAnchors(new Vector2(0.5f, 0.545f), new Vector2(1080, 12));
            UiKit.Panel(transform, "Ground", new Color(0f, 0f, 0f, 0.22f))
                .rectTransform.SetSizeWithAnchors(new Vector2(0.5f, 0.335f), new Vector2(1080, 12));

            UiKit.Label(transform, "Next", new Vector2(0.5f, 0.23f), new Vector2(1000, 60), 40, NextText);
            UiKit.Label(transform, "AutoInfo", new Vector2(0.5f, 0.135f), new Vector2(800, 40), 26, "잠시 후 다음 이야기가 이어져요");

            var gaugeBg = UiKit.Panel(transform, "GaugeBg", new Color(1f, 1f, 1f, 0.15f));
            gaugeBg.rectTransform.SetSizeWithAnchors(new Vector2(0.5f, 0.09f), new Vector2(560, 16));
            var fill = UiKit.Panel(gaugeBg.transform, "Fill", new Color(0.95f, 0.85f, 0.45f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            _gaugeFill = fill;
        }

        protected override void OnEnter()
        {
            foreach (var go in _items)
                if (go != null)
                    Destroy(go);
            _items.Clear();
            StartCoroutine(Run());
        }

        protected override void OnExit() => StopAllCoroutines();

        IEnumerator Run()
        {
            var list = BuildCollectibles();
            BuildItemCards(list);

            // 수집물 순차 등장 (확정)
            foreach (var go in _items)
                go.SetActive(false);
            _gaugeFill.fillAmount = 1f;
            foreach (var go in _items)
            {
                go.SetActive(true);
                yield return new WaitForSeconds(0.15f);
            }

            float start = Time.time;
            while (Time.time - start < AutoSeconds)
            {
                _gaugeFill.fillAmount = Mathf.Clamp01(1f - (Time.time - start) / AutoSeconds);
                yield return null;
            }
            Flow.Go(NextScreen);
        }

        void BuildItemCards(List<Collectible> list)
        {
            var proper = list.FindAll(c => c.Proper);
            var other = list.FindAll(c => !c.Proper);
            PlaceRow(proper, 0.615f, tilt: false);
            PlaceRow(other, 0.405f, tilt: true);
        }

        void PlaceRow(List<Collectible> row, float y, bool tilt)
        {
            const float spacing = 0.085f;
            for (int i = 0; i < row.Count; i++)
            {
                float x = 0.5f + (i - (row.Count - 1) * 0.5f) * spacing;
                var card = UiKit.Panel(transform, $"Item_{row[i].Label}", tilt
                    ? new Color(0.45f, 0.42f, 0.38f)
                    : new Color(0.55f, 0.6f, 0.45f));
                var rect = card.rectTransform;
                rect.SetSizeWithAnchors(new Vector2(x, y), new Vector2(130, 120));
                if (tilt)
                    rect.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? -8f : 8f);
                UiKit.Label(card.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(120, 100), 28, row[i].Label);
                if (row[i].Count > 1)
                {
                    var badge = UiKit.Label(card.transform, "Badge", new Vector2(0.85f, 0.85f), new Vector2(60, 34), 24,
                        $"×{row[i].Count}");
                    badge.color = new Color(1f, 0.9f, 0.5f);
                }
                _items.Add(card.gameObject);
            }
        }

        /// (종류, 위/아래) 단위로 묶고 개수를 센다 — 낱개 나열하지 않는다 (확정)
        protected static List<Collectible> Group(IEnumerable<(string label, bool proper)> items)
        {
            var list = new List<Collectible>();
            foreach (var (label, proper) in items)
            {
                var found = list.Find(c => c.Label == label && c.Proper == proper);
                if (found != null)
                    found.Count++;
                else
                    list.Add(new Collectible { Label = label, Count = 1, Proper = proper });
            }
            return list;
        }
    }

    /// SCR-010 수확하기 간이 결과
    public class HarvestStoryResultScreen : StoryResultBase
    {
        public static PlayRecord LastPlay;

        protected override Color Background => new Color(0.13f, 0.16f, 0.11f);
        protected override string TitleText => "바구니가 가득 찼어요";
        protected override string ConfirmText => "필요한 재료가 모였어요";
        protected override string NextText => "이제 시장에 다녀올까요?";
        protected override ScreenId NextScreen => ScreenId.SCR_011;

        protected override List<Collectible> BuildCollectibles()
        {
            var items = new List<(string, bool)>();
            if (LastPlay != null)
                foreach (var r in LastPlay.HarvestRounds)
                    items.Add((r.Picked, r.Result == "정답"));
            return Group(items);
        }
    }

    /// SCR-015 장보기 간이 결과 — 장바구니에 준비된 재료 + 다음 요리 단계 예고
    public class ShoppingStoryResultScreen : StoryResultBase
    {
        public static PlayRecord LastPlay;

        protected override Color Background => new Color(0.15f, 0.14f, 0.11f);
        protected override string TitleText => "장바구니가 묵직해졌어요";
        protected override string ConfirmText => "필요한 것을 다 담았어요";
        protected override string NextText => "이제 부엌으로 가 볼까요?";
        protected override ScreenId NextScreen => ScreenId.SCR_016;

        protected override List<Collectible> BuildCollectibles()
        {
            var items = new List<(string, bool)>();
            if (LastPlay != null)
                foreach (var r in LastPlay.ShoppingItems)
                    items.Add((r.BoughtItem, r.Correct));
            return Group(items);
        }
    }

    /// SCR-020 요리하기 간이 결과 — 완성한 음식(음식 단위 10개 · 6-9-6) + 잔치상 단계 예고.
    /// 완성 음식 이미지는 간이 결과에서 사용 가능(6-7 확정) — 자산 도입 시 카드에 적용
    public class CookingStoryResultScreen : StoryResultBase
    {
        public static PlayRecord LastPlay;

        protected override Color Background => new Color(0.16f, 0.13f, 0.11f);
        protected override string TitleText => "음식이 다 되었어요";
        protected override string ConfirmText => null; // 8-6-1 확정 문구는 2단 구조
        protected override string NextText => "이제 상을 차려 볼까요?";
        protected override ScreenId NextScreen => ScreenId.SCR_021;

        protected override List<Collectible> BuildCollectibles()
        {
            var list = new List<Collectible>();
            if (LastPlay == null)
                return list;
            foreach (var round in LastPlay.CookingRounds)
            {
                bool allCorrect = true;
                foreach (var pick in round.Picks)
                    allCorrect &= pick.Correct;
                list.Add(new Collectible { Label = round.Food, Count = 1, Proper = allCorrect });
            }
            return list;
        }
    }

    /// SCR-022 최종 잔치상 (9-1~9-4 확정).
    /// 판정: 3게임 성공 합 ÷ 30 → 2단계 65% · 3단계 85%(기준값 · ADM 조정 가능하게 설계 — ADM 구현 시 연결).
    /// 단계 차이는 양이 아니라 화려함 · 단계 명칭은 화면에 표시하지 않는다 · 낮은 단계도 상은 가득 차 있다.
    public class FeastScreen : ScreenBase
    {
        const float AutoReturnSeconds = 15f;   // 기준값 · ADM 조정 가능하게 설계
        const float Stage2Threshold = 0.65f;   // 기준값
        const float Stage3Threshold = 0.85f;   // 기준값

        static readonly string[] StageMent =
        {
            "정성이 담긴 상이 차려졌어요",
            "이웃과 나눌 만한 상이 되었어요",
            "온 동네가 모일 잔치상이 되었어요",
        };

        Text _ment;
        Image _table;
        readonly List<Image> _dishes = new List<Image>();
        readonly List<GameObject> _garnish = new List<GameObject>();
        Text[] _successTexts;
        Image _gaugeFill;
        GameObject _gaugeRoot;
        GameObject _recordsButton;
        Coroutine _timer;

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.14f, 0.11f, 0.10f));
            UiKit.Label(transform, "Title", new Vector2(0.5f, 0.88f), new Vector2(1000, 90), 60, "잔치가 열렸어요");

            // 잔치상 — 항상 가득 찬 상차림 (X-Box 플레이스홀더 · 자산 도입 시 3단계 일러스트로 교체)
            _table = UiKit.Panel(transform, "Table", new Color(0.45f, 0.3f, 0.2f));
            _table.rectTransform.SetSizeWithAnchors(new Vector2(0.5f, 0.585f), new Vector2(900, 300));
            for (int i = 0; i < 6; i++)
            {
                var dish = UiKit.Panel(_table.transform, $"Dish_{i}", Color.white);
                dish.rectTransform.SetSizeWithAnchors(
                    new Vector2(0.14f + 0.144f * i, i % 2 == 0 ? 0.62f : 0.34f), new Vector2(110, 96));
                _dishes.Add(dish);
                // 고명(2단계+)·특별 장식(3단계) 자리 — 단계 명칭·등급 표기는 두지 않는다
                var deco = UiKit.Label(dish.transform, "Deco", new Vector2(0.5f, 0.82f), new Vector2(90, 30), 22, "✿");
                deco.color = new Color(1f, 0.75f, 0.4f);
                deco.gameObject.SetActive(false);
                _garnish.Add(deco.gameObject);
            }

            _ment = UiKit.Label(transform, "Ment", new Vector2(0.5f, 0.38f), new Vector2(1000, 60), 42, "");

            // 하단: 게임 3종 성공 횟수 n / 10 — 퍼센트·평균·등급 표시 금지 (확정)
            string[] names = { "수확하기", "장보기", "요리하기" };
            _successTexts = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var card = UiKit.Panel(transform, $"Success_{i}", new Color(1f, 1f, 1f, 0.08f));
                card.rectTransform.SetSizeWithAnchors(new Vector2(0.38f + 0.12f * i, 0.27f), new Vector2(200, 90));
                UiKit.Label(card.transform, "Name", new Vector2(0.5f, 0.72f), new Vector2(180, 34), 24, names[i]);
                _successTexts[i] = UiKit.Label(card.transform, "Value", new Vector2(0.5f, 0.3f), new Vector2(180, 44), 34, "");
            }

            // 버튼 2종 — 처음 화면으로(좌 · 주 CTA) · 내 기록 보기(우) · 안전 영역 안 (확정 9-3)
            UiKit.Button(transform, "Home", new Vector2(0.42f, 0.145f), new Vector2(300, 100),
                "처음 화면으로", () => Flow.Go(ScreenId.SCR_004), color: new Color(0.3f, 0.6f, 0.35f));
            _recordsButton = UiKit.Button(transform, "Records", new Vector2(0.6f, 0.145f), new Vector2(280, 95),
                "내 기록 보기", OpenRecords).gameObject;

            var gaugeBg = UiKit.Panel(transform, "GaugeBg", new Color(1f, 1f, 1f, 0.15f));
            gaugeBg.rectTransform.SetSizeWithAnchors(new Vector2(0.5f, 0.055f), new Vector2(560, 16));
            var fill = UiKit.Panel(gaugeBg.transform, "Fill", new Color(0.95f, 0.85f, 0.45f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            _gaugeFill = fill;
            _gaugeRoot = gaugeBg.gameObject;
            UiKit.Label(_gaugeRoot.transform, "AutoInfo", new Vector2(0.5f, -1.2f), new Vector2(800, 34), 24,
                "잠시 후 처음 화면으로 돌아가요"); // 게이지와 함께 숨겨진다
        }

        protected override void OnEnter()
        {
            int harvest = Success("Harvest");
            int shopping = Success("Shopping");
            int cooking = Success("Cooking");
            float ratio = (harvest + shopping + cooking) / 30f;
            int stage = ratio >= Stage3Threshold ? 3 : ratio >= Stage2Threshold ? 2 : 1;

            ApplyStage(stage);
            _successTexts[0].text = $"{harvest} / 10";
            _successTexts[1].text = $"{shopping} / 10";
            _successTexts[2].text = $"{cooking} / 10";

            // 스토리 모드의 유일한 기록 진입점 — 비회원에게는 버튼 자체를 표시하지 않는다 (확정)
            _recordsButton.SetActive(!Flow.IsGuest);

            // 기록 보기에서 복귀한 경우 자동 복귀 타이머를 재개하지 않는다 (확정 9-3)
            if (Flow.LastMoveWasBack)
                _gaugeRoot.SetActive(false);
            else
            {
                _gaugeRoot.SetActive(true);
                _timer = StartCoroutine(AutoReturn());
            }
        }

        protected override void OnExit() => StopAllCoroutines();

        int Success(string activity) => Flow.StorySuccess.TryGetValue(activity, out var n) ? n : 0;

        void ApplyStage(int stage)
        {
            _ment.text = StageMent[stage - 1];
            // 양이 아니라 화려함의 차이 — 접시 수는 같고 색·장식만 달라진다
            _table.color = stage >= 3 ? new Color(0.55f, 0.35f, 0.18f)
                : stage == 2 ? new Color(0.5f, 0.33f, 0.2f) : new Color(0.45f, 0.3f, 0.2f);
            for (int i = 0; i < _dishes.Count; i++)
            {
                _dishes[i].color = stage >= 3 ? new Color(1f, 0.95f, 0.75f)
                    : stage == 2 ? new Color(0.95f, 0.92f, 0.85f) : new Color(0.88f, 0.86f, 0.82f);
                _garnish[i].SetActive(stage >= 2 && (stage >= 3 || i % 2 == 0));
            }
        }

        void OpenRecords()
        {
            // 기록을 읽는 도중 화면이 넘어가지 않게 게이지를 정지 (확정)
            if (_timer != null)
            {
                StopCoroutine(_timer);
                _timer = null;
            }
            _gaugeRoot.SetActive(false);
            Flow.Go(ScreenId.SCR_023);
        }

        IEnumerator AutoReturn()
        {
            float start = Time.time;
            while (Time.time - start < AutoReturnSeconds)
            {
                _gaugeFill.fillAmount = Mathf.Clamp01(1f - (Time.time - start) / AutoReturnSeconds);
                yield return null;
            }
            Flow.Go(ScreenId.SCR_004);
        }
    }
}
