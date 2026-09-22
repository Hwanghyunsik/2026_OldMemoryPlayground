using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;
using Shinmyeong.Save;
using Shinmyeong.UI;

namespace Shinmyeong.Flow.Screens
{
    /// 간이 결과 공통 뼈대 (8-4 확정 · 시안 SCR-010/015/020): 수치·버튼·일시정지 없음 · 수집물 순차 등장 ·
    /// 응원 문구 + 다음 활동 예고 · 자동 진행 6초(기준안) · 남은 시간 숫자 금지 — 게이지로만.
    /// 수집물 구분(v2.4 확정): 위 상자 = 제대로 담긴 것 / 아래 상자 = 다르게 담긴 것(기울여 배치) ·
    /// 종류 단위 묶음 + 개수 배지 · 라운드 번호 미표시. TTS 허용 — 음성 도입 시 여기서 재생.
    /// 화면 자체는 게임별 독립 페이지(확정) — 파생 클래스가 문구·데이터·그림을 각자 정의한다.
    public abstract class StoryResultBase : ScreenBase
    {
        protected const float AutoSeconds = 6f; // 기준안 · 프로토타입 검증 후 조정

        protected class Collectible
        {
            public string Label;
            public int Count;    // 종류 묶음 개수 (1이면 배지 생략)
            public bool Proper;  // true = 위 상자 / false = 아래 상자(기울임)
        }

        Image _gaugeFill;
        RectTransform _properBox;
        RectTransform _otherBox;
        readonly List<GameObject> _items = new List<GameObject>();

        protected abstract string BackgroundSprite { get; }
        protected abstract (string sprite, Rect rect) Title { get; }  // 결과 상태 간판 (8-6-1 문구가 그림에 포함)
        protected abstract string ProperHeading { get; }
        protected abstract string OtherHeading { get; }
        protected abstract string ConfirmText { get; }  // 확인 문구 (없으면 null)
        protected abstract string NextText { get; }     // 다음 단계 권유
        protected abstract ScreenId NextScreen { get; }
        protected abstract string ArtCategory { get; }  // 수집물 그림 분류 (Docs/92)
        protected virtual bool ShowNames => false;      // 음식은 이름표를 붙인다 (6-9)
        protected abstract List<Collectible> BuildCollectibles();

        protected override void BuildUi()
        {
            UiKit.Background(transform, BackgroundSprite);
            var (sprite, rect) = Title;
            UiKit.ImgFit(transform, "Title", sprite, rect.x, rect.y, rect.width, rect.height);

            _properBox = UiKit.Frame(transform, "CollectedCrops", 98, 171, 1724, 303, Skin.Paper, Skin.Hex("c2b286", 0.88f));
            UiKit.Txt(_properBox, "Heading", 33, 24, 400, 56, ProperHeading, 36, 7, Skin.Green, TextAnchor.MiddleLeft);
            _otherBox = UiKit.Frame(transform, "DifferentCrops", 98, 488, 1724, 304, Skin.Paper, Skin.Hex("c2b286", 0.88f));
            UiKit.Txt(_otherBox, "Heading", 36, 27, 400, 56, OtherHeading, 36, 7, Skin.Hex("b86a16"), TextAnchor.MiddleLeft);

            var notice = UiKit.Node(transform, "NextStoryNotice", 509, 819, 902, 123);
            UiKit.Img(notice, "Background", "Box-Round-15", 0, 0, 902, 123, Skin.Hex("362f2d", 0.9f), sliced: true);
            if (ConfirmText != null)
            {
                UiKit.Txt(notice, "Heading", 40, 12, 822, 56, ConfirmText, 40, 7, Skin.Gold);
                UiKit.Txt(notice, "Description", 40, 68, 822, 40, NextText, 28, 5, Skin.Cream);
            }
            else
                UiKit.Txt(notice, "Heading", 40, 0, 822, 123, NextText, 40, 7, Skin.Gold);

            var progress = UiKit.Node(transform, "NextStoryProgress", 619, 968, 682, 74);
            UiKit.Txt(progress, "Caption", 0, 0, 682, 39, "잠시 후 다음 이야기가 이어져요", 25, 6, Skin.Ink);
            _gaugeFill = UiKit.Bar(progress, "Gauge", 0, 47, 682, 27, Skin.Track2, Skin.Navy, "TimeBar-Bg-02", "Circle-25");
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
            PlaceRow(_properBox, list.FindAll(c => c.Proper), proper: true);
            PlaceRow(_otherBox, list.FindAll(c => !c.Proper), proper: false);

            // 수집물 순차 등장 (확정)
            foreach (var go in _items)
                go.SetActive(false);
            UiKit.SetBar(_gaugeFill, 1f);
            foreach (var go in _items)
            {
                go.SetActive(true);
                yield return new WaitForSeconds(0.15f);
            }

            float start = Time.time;
            while (Time.time - start < AutoSeconds)
            {
                UiKit.SetBar(_gaugeFill, Mathf.Clamp01(1f - (Time.time - start) / AutoSeconds));
                yield return null;
            }
            Flow.Go(NextScreen);
        }

        /// 178×178 카드 · 상자 안 x 33부터 185 간격(많으면 좁힌다) · y 98
        void PlaceRow(RectTransform box, List<Collectible> row, bool proper)
        {
            float pitch = ResultKit.Pitch(row.Count, 1724 - 66, 178, 185);
            for (int i = 0; i < row.Count; i++)
            {
                float x = 33 + i * pitch;
                var card = UiKit.Node(box, $"Item{i + 1}", x, 98, 178, 178);
                UiKit.Img(card, "Face", "Box-Round-23", 1, 1, 175, 175, Skin.Face, sliced: true);
                UiKit.Img(card, "Outline", "Box-Round-23-Outline", 1, 1, 175, 175, Skin.Outline, sliced: true);
                var art = ArtCatalog.Get(ArtCategory, row[i].Label);
                if (art != null)
                {
                    var icon = UiKit.ImgFit(card, "Crop", null, 22, ShowNames ? 16 : 22, 134, ShowNames ? 112 : 134);
                    icon.sprite = art;
                }
                else
                    UiKit.Txt(card, "Label", 10, 10, 158, ShowNames ? 110 : 158, row[i].Label, 28, 6, Skin.Brown);
                if (ShowNames)
                    UiKit.Txt(card, "FoodName", 9, 132, 160, 32, row[i].Label, 26, 6, Skin.Hex("3b2917"));
                if (!proper)
                {
                    UiKit.PlaceCentered(card, x, 98, 178, 178);
                    card.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? 5f : -5f);
                }
                _items.Add(card.gameObject);
                // 개수 배지는 맨 위 층(Badges)에 두어 다음 카드에 가려지지 않게 한다 (카드가 촘촘할 때)
                if (row[i].Count > 1)
                    _items.Add(UiKit.Badge(ResultKit.BadgeLayer(box), $"Quantity{i + 1}", x + 135, 98 - 14, 49, row[i].Count, Skin.Green, 33).transform.parent.gameObject);
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

    /// SCR-010 수확하기 간이 결과 — 「바구니가 가득 찼어요」
    public class HarvestStoryResultScreen : StoryResultBase
    {
        public static PlayRecord LastPlay;

        protected override string BackgroundSprite => "SCR-008-Bg";
        protected override (string, Rect) Title => ("SCR-010-Title", new Rect(467, 40, 908, 132));
        protected override string ProperHeading => "바구니에 담은 것";
        protected override string OtherHeading => "다르게 담긴 것";
        protected override string ConfirmText => "필요한 재료가 모였어요";
        protected override string NextText => "이제 시장에 다녀올까요?";
        protected override ScreenId NextScreen => ScreenId.SCR_011;
        protected override string ArtCategory => ArtCatalog.CropLaid;

        protected override List<Collectible> BuildCollectibles()
        {
            var items = new List<(string, bool)>();
            if (LastPlay != null)
                foreach (var r in LastPlay.HarvestRounds)
                    items.Add((r.Picked, r.Result == "정답"));
            return Group(items);
        }
    }

    /// SCR-015 장보기 간이 결과 — 「장바구니가 묵직졌어요」 + 다음 요리 단계 예고
    public class ShoppingStoryResultScreen : StoryResultBase
    {
        public static PlayRecord LastPlay;

        protected override string BackgroundSprite => "SCR-013-Bg";
        protected override (string, Rect) Title => ("SCR-015-Title", new Rect(445, 40, 953, 139));
        protected override string ProperHeading => "장바구니에 담은 것";
        protected override string OtherHeading => "다르게 담긴 것";
        protected override string ConfirmText => "필요한 것을 다 담았어요";
        protected override string NextText => "이제 부엌으로 가 볼까요?";
        protected override ScreenId NextScreen => ScreenId.SCR_016;
        protected override string ArtCategory => ArtCatalog.Ingredient;

        protected override List<Collectible> BuildCollectibles()
        {
            var items = new List<(string, bool)>();
            if (LastPlay != null)
                foreach (var r in LastPlay.ShoppingItems)
                    items.Add((r.BoughtItem, r.Correct));
            return Group(items);
        }
    }

    /// SCR-020 요리하기 간이 결과 — 「음식이 다 되었어요」 · 완성 음식(음식 단위 10개 · 6-9-6) + 잔치상 단계 예고.
    /// 완성 음식 이미지는 간이 결과에서 사용 가능(6-7 확정)
    public class CookingStoryResultScreen : StoryResultBase
    {
        public static PlayRecord LastPlay;

        protected override string BackgroundSprite => "SCR-017-Bg";
        protected override (string, Rect) Title => ("SCR-020-Title", new Rect(517, 40, 808, 143));
        protected override string ProperHeading => "완성된 음식";
        protected override string OtherHeading => "재료가 달랐던 음식";
        protected override string ConfirmText => null; // 8-6-1 확정 문구는 2단 구조
        protected override string NextText => "이제 상을 차려 볼까요?";
        protected override ScreenId NextScreen => ScreenId.SCR_021;
        protected override string ArtCategory => ArtCatalog.Food;
        protected override bool ShowNames => true;

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

    /// SCR-022 최종 잔치상 (9-1~9-4 확정 · 시안 SCR-022).
    /// 판정: 3게임 성공 합 ÷ 30 → 2단계 65% · 3단계 85%(기준값 · ADM 조정 가능하게 설계 — ADM 구현 시 연결).
    /// 단계 차이는 양이 아니라 화려함(잔치상 그림 3종 · 문구는 그림에 포함) · 단계 명칭은 화면에 표시하지 않는다.
    public class FeastScreen : ScreenBase
    {
        const float AutoReturnSeconds = 15f;   // 기준값 · ADM 조정 가능하게 설계
        const float Stage2Threshold = 0.65f;   // 기준값
        const float Stage3Threshold = 0.85f;   // 기준값

        static readonly string[] TableSprites = { "SCR-022-Table-01", "SCR-022-Table-02", "SCR-022-Table-03" };
        /// 단계별 문구 (그림에 포함 · TTS 도입 시 읽어 준다): 정성이 담긴 상 / 이웃과 나눌 만한 상 / 온 동네가 모일 잔치상
        public static readonly string[] StageMent =
        {
            "정성이 담긴 상이 차려졌어요",
            "이웃과 나눌 만한 상이 되었어요",
            "온 동네가 모일 잔치상이 되었어요",
        };

        Image _table;
        Text[] _successTexts;
        Image _gaugeFill;
        GameObject _gaugeRoot;
        GameObject _recordsButton;
        Coroutine _timer;

        protected override void BuildUi()
        {
            UiKit.Background(transform, "SCR-022-Bg");
            UiKit.ImgFit(transform, "Title", "SCR-022-Title", 550, 50, 742, 143);

            var panel = UiKit.Img(transform, "ResultPanel", "SCR-022-Box", 348, 138, 1221, 816);
            _table = UiKit.ImgFit(panel.transform, "FeastTable", TableSprites[0], 80, 70, 1057, 387);

            // 하단: 게임 3종 성공 횟수 n / 10 — 퍼센트·평균·등급 표시 금지 (확정)
            _successTexts = new Text[3];
            _successTexts[0] = ActivityCard(panel.transform, 100, "Icon-Harvest", "Icon-Harvest-Text", 132, Skin.GreenDeep);
            _successTexts[1] = ActivityCard(panel.transform, 446, "Icon-Shopping", "Icon-Shopping-Text", 104, Skin.Orange);
            _successTexts[2] = ActivityCard(panel.transform, 792, "Icon-Cooking", "Icon-Cooking-Text", 132, Skin.Purple);

            _gaugeRoot = UiKit.Node(panel.transform, "ReturnGauge", 353, 658, 562, 78).gameObject;
            UiKit.Txt(_gaugeRoot.transform, "ReturnNotice", 0, 0, 562, 45, "잠시 후 처음 화면으로 돌아가요", 25, 4, Skin.BrownDark);
            _gaugeFill = UiKit.Bar(_gaugeRoot.transform, "Gauge", 0, 60, 562, 18, Skin.Track3, Skin.Green, "TimeBar", "TimeBar");

            // 버튼 2종 — 처음 화면으로(좌) · 내 기록 보기(우 · 비회원 미표시) (확정 9-3)
            UiKit.NavButton(transform, "HomeButton", 121, 477, "처음으로", "Icon-Home", () => Flow.Go(ScreenId.SCR_004))
                .gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(좌측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";
            var records = UiKit.NavButton(transform, "RecordsButton", 1655, 478, "내 기록 보기", "Icon-Record", OpenRecords, labelSize: 21);
            records.gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(우측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";
            _recordsButton = records.gameObject;
        }

        static Text ActivityCard(Transform parent, float x, string icon, string label, float labelW, Color color)
        {
            var card = UiKit.Card(parent, icon, x, 484, 333, 163, Skin.Face, color);
            UiKit.ImgFit(card, "ActivityIcon", icon, 24, 21, 126, 124);
            UiKit.ImgFit(card, "ActivityTitle", label, 169 + (132 - labelW) * 0.5f, 19, labelW, 50);
            var value = UiKit.Txt(card, "Value", 120, 65, 115, 79, "", 60, 7, color, TextAnchor.MiddleRight);
            UiKit.Txt(card, "Total", 240, 90, 62, 48, "/10", 30, 5, Skin.Muted, TextAnchor.MiddleLeft);
            return value;
        }

        protected override void OnEnter()
        {
            int harvest = Success("Harvest");
            int shopping = Success("Shopping");
            int cooking = Success("Cooking");
            float ratio = (harvest + shopping + cooking) / 30f;
            int stage = ratio >= Stage3Threshold ? 3 : ratio >= Stage2Threshold ? 2 : 1;

            // 양이 아니라 화려함의 차이 — 그림만 바뀐다 (단계 명칭 미표시)
            _table.sprite = Skin.Sprite(TableSprites[stage - 1]);
            _successTexts[0].text = harvest.ToString();
            _successTexts[1].text = shopping.ToString();
            _successTexts[2].text = cooking.ToString();

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
                UiKit.SetBar(_gaugeFill, Mathf.Clamp01(1f - (Time.time - start) / AutoReturnSeconds));
                yield return null;
            }
            Flow.Go(ScreenId.SCR_004);
        }
    }
}
