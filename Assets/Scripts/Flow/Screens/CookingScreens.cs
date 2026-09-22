using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Games.Cooking;
using Shinmyeong.Interaction;
using Shinmyeong.UI;

namespace Shinmyeong.Flow.Screens
{
    // SCR-017 요리하기 튜토리얼은 GameTutorialScreen 공용 템플릿 사용 (2-5 확정: 템플릿 1 + 데이터 3벌)
    // — 조리 시뮬레이션이 아니라 시각 기억 게임(설계서)

    /// SCR-018 요리하기 재료 선택 (레시피 기억 팝업 SCR-018-1은 데모 내부의 하위 단계 화면 — 모드 공용).
    /// 왼쪽 세로 받침: 일시정지(x63 y334) + 차림표 다시 보기(x63 y589 · 데모가 만든다). 배경·HUD·카드는 CookingDemo.
    public class CookingPlayScreen : ScreenBase
    {
        CookingDemo _game;
        GameObject _pauseButton;

        protected override void BuildUi()
        {
            UiKit.Background(transform, "SCR-017-Bg");
            var stack = UiKit.Node(transform, "LeftButtonsBackground", 48, 319, 190, 442);
            UiKit.Img(stack, "Black", "Box-Round-34", 0, 0, 190, 442, Skin.Hex("241a14", 0.8f), sliced: true);
            UiKit.Img(stack, "Divider", "Dot-Line-2", 18, 210, 151, 2, new Color(1f, 1f, 1f, 0.4f), sliced: true);
            var pause = UiKit.NavButton(transform, "PauseButton", 63, 334, "일시정지", "Icon-Pause", PausePopup.Open);
            Destroy(pause.transform.Find("Black").gameObject);
            pause.gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(좌측) — 확정 위치(하단 중앙 220×100)와 다름 · 기획 확인 대기(Q5)";
            _pauseButton = pause.gameObject;
        }

        void Update()
        {
            // 레시피 기억 팝업 중에는 일시정지를 호출할 수 없다 — 버튼 자체를 숨긴다(설계서 확정)
            if (_pauseButton != null && _game != null)
                _pauseButton.SetActive(!_game.RecipePopupOpen);
        }

        protected override void OnEnter()
        {
            if (_game == null)
            {
                _game = gameObject.AddComponent<CookingDemo>();
                _game.Finished += OnFinished;
            }
            _game.Begin(Rect);
            _pauseButton.transform.SetAsLastSibling();
        }

        protected override void OnExit() => _game?.End();

        void OnFinished(Save.PlayRecord record, IReadOnlyList<string> lines)
        {
            Flow.CommitPlay(record);
            CookingResultScreen.LastPlay = record;
            CookingStoryResultScreen.LastPlay = record;
            Flow.Go(Flow.Mode == GameMode.Story ? ScreenId.SCR_020 : ScreenId.SCR_019);
        }
    }

    /// SCR-019 요리하기 개별 결과 (6-9 확정 · 판정 단위 = 음식 10개 고정 · 시안 SCR-019).
    /// 요약 카드 3종: 완성한 음식 · 다른 재료가 들어간 음식 · 전체 활동 시간 (합 10 · 분모 미표기).
    /// 나열: 음식 카드 · 위 = 완성(초록 상자) / 아래 = 다른 재료(주황 상자) · 라운드 순서로 왼쪽부터 · 빈 칸 미표시 ·
    /// 카드 안 개별 문구·배지·라운드 번호 없음. 힌트·다시 보기·반응 시간·어떤 재료가 달랐는지는 ADM 전용.
    public class CookingResultScreen : ScreenBase
    {
        public static Save.PlayRecord LastPlay;

        ResultKit.Header _header;
        RectTransform _panel;
        Text[] _cardValues;
        GameObject _recordsButton;
        RectTransform _properBox;
        RectTransform _otherBox;
        readonly List<GameObject> _items = new List<GameObject>();

        protected override void BuildUi()
        {
            UiKit.Background(transform, "SCR-017-Bg");
            _header = ResultKit.BuildHeader(transform, "요리를 마쳤어요", "만드신 음식을 살펴보세요");
            _panel = ResultKit.BuildPanel(transform);

            _cardValues = new Text[3];
            _cardValues[0] = ResultKit.SummaryCard(_panel, "Correct", 0, ResultKit.CardKind.Proper, "완성한 음식");
            _cardValues[1] = ResultKit.SummaryCard(_panel, "Different", 1, ResultKit.CardKind.Other, "다른 재료가 들어간 음식");
            _cardValues[1].transform.parent.Find("Label").GetComponent<Text>().fontSize = 24;
            _cardValues[2] = ResultKit.SummaryCard(_panel, "Duration", 2, ResultKit.CardKind.Duration, "전체 활동 시간");

            _properBox = ResultKit.MaterialBox(_panel, "CorrectMaterials", 233, proper: true);
            _otherBox = ResultKit.MaterialBox(_panel, "DifferentMaterials", 457, proper: false);
            ResultKit.Ribbon(_panel, "GreenRibbon", 487, 211, true, "완성된 음식");
            ResultKit.Ribbon(_panel, "OrangeRibbon", 483, 435, false, "다른 재료가 들어간 음식", 26);

            // 다시 하기 = 레시피 기억부터 재시작 (재료 선택만 반복하면 기억 과제가 성립하지 않는다 · 설계서)
            _recordsButton = ResultKit.BuildButtons(transform,
                () => Flow.Go(ScreenId.SCR_018), () => Flow.Go(ScreenId.SCR_005), () => Flow.Go(ScreenId.SCR_023));
        }

        protected override void OnEnter()
        {
            foreach (var go in _items)
                if (go != null)
                    Destroy(go);
            _items.Clear();

            ResultKit.ApplyHeader(_header, Flow);
            var done = new List<string>();
            var other = new List<string>();
            if (LastPlay != null)
                foreach (var round in LastPlay.CookingRounds) // 라운드 순서 유지
                {
                    bool allCorrect = true;
                    foreach (var pick in round.Picks)
                        allCorrect &= pick.Correct;
                    (allCorrect ? done : other).Add(round.Food);
                }

            _cardValues[0].text = ResultKit.CountText(done.Count);
            _cardValues[1].text = ResultKit.CountText(other.Count);
            _cardValues[2].text = ResultKit.DurationText(LastPlay != null ? LastPlay.DurationSec : 0f);

            PlaceRow(_properBox, done, proper: true);
            PlaceRow(_otherBox, other, proper: false);

            _recordsButton.SetActive(!Flow.IsGuest);
        }

        // 왼쪽부터 채우고 남는 칸은 비워 둔다(미표시) — 음식 카드에 배지·문구 없음
        void PlaceRow(RectTransform box, List<string> foods, bool proper)
        {
            float pitch = ResultKit.Pitch(foods.Count, 1180, 110, 134);
            for (int i = 0; i < foods.Count && i < 10; i++)
                _items.Add(ResultKit.MaterialItem(box, i, pitch, proper, foods[i],
                    ArtCatalog.Get(ArtCatalog.Food, foods[i]), 1)); // 완성 음식 이미지는 결과에서 사용 가능(6-7)
        }
    }
}
