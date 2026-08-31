using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Games.Cooking;

namespace Shinmyeong.Flow.Screens
{
    // SCR-017 요리하기 튜토리얼은 GameTutorialScreen 공용 템플릿 사용 (2-5 확정: 템플릿 1 + 데이터 3벌)
    // — 조리 시뮬레이션이 아니라 시각 기억 게임(설계서)

    /// SCR-018 요리하기 재료 선택 (레시피 기억 팝업 SCR-018-1은 데모 내부의 하위 단계 화면 — 모드 공용)
    public class CookingPlayScreen : ScreenBase
    {
        CookingDemo _game;
        GameObject _pauseButton;

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.13f, 0.12f, 0.10f));
            // 일시정지 — 하단 중앙 220×100 (확정 · 07 문서 2-4)
            _pauseButton = UiKit.Button(transform, "Pause", new Vector2(0.5f, 0.14f), new Vector2(220, 100),
                "일시정지", PausePopup.Open, color: new Color(0.3f, 0.3f, 0.35f)).gameObject;
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

    /// SCR-019 요리하기 개별 결과 (6-9 확정 · 판정 단위 = 음식 10개 고정).
    /// 요약 카드 3종: 완성한 음식 · 다른 재료가 들어간 음식 · 전체 활동 시간 (합 10 · 분모 미표기).
    /// 나열: 음식 카드 10장 · 위·아래 2단 분리 + 단 제목이 문구 역할 · 각 단 10칸 고정 슬롯(빈 칸 미표시) ·
    /// 라운드 순서로 왼쪽부터 · 색상 계열 구분(완성 초록 / 다른 주황·갈색) · 카드 안 개별 문구·배지·라운드 번호 없음.
    /// 힌트·다시 보기·반응 시간·어떤 재료가 달랐는지는 ADM 전용 — 표시하지 않는다.
    public class CookingResultScreen : ScreenBase
    {
        public static Save.PlayRecord LastPlay;

        static readonly Color DoneColor = new Color(0.32f, 0.62f, 0.38f);
        static readonly Color OtherColor = new Color(0.78f, 0.52f, 0.28f);

        Text[] _cardValues;
        GameObject _recordsButton;
        readonly List<GameObject> _items = new List<GameObject>();

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.14f, 0.12f, 0.10f));
            UiKit.Label(transform, "Header", new Vector2(0.5f, 0.9f), new Vector2(900, 70), 50, "요리를 마쳤어요");
            UiKit.Label(transform, "Sub", new Vector2(0.5f, 0.84f), new Vector2(900, 44), 30, "만드신 음식을 살펴보세요");

            string[] cardNames = { "완성한 음식", "다른 재료가 들어간 음식", "전체 활동 시간" };
            _cardValues = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var card = UiKit.Panel(transform, $"Card_{i}", new Color(1f, 1f, 1f, 0.08f));
                card.rectTransform.SetSizeWithAnchors(new Vector2(0.32f + 0.18f * i, 0.73f), new Vector2(300, 100));
                UiKit.Label(card.transform, "Name", new Vector2(0.5f, 0.74f), new Vector2(290, 34), 24, cardNames[i]);
                _cardValues[i] = UiKit.Label(card.transform, "Value", new Vector2(0.5f, 0.32f), new Vector2(280, 44), 34, "");
            }

            // 단 제목 — 색상만으로 구분하지 않는다 (확정 6-9-3)
            UiKit.Label(transform, "TopTitle", new Vector2(0.145f, 0.615f), new Vector2(260, 36), 26, "완성한 음식");
            UiKit.Label(transform, "BottomTitle", new Vector2(0.2f, 0.415f), new Vector2(380, 36), 26, "다른 재료가 들어간 음식");

            // 다시 하기 = 레시피 기억부터 재시작 (재료 선택만 반복하면 기억 과제가 성립하지 않는다 · 설계서)
            UiKit.Button(transform, "Retry", new Vector2(0.3f, 0.145f), new Vector2(240, 95), "다시 하기",
                () => Flow.Go(ScreenId.SCR_018));
            UiKit.Button(transform, "Lobby", new Vector2(0.5f, 0.145f), new Vector2(280, 100), "다른 활동 고르기",
                () => Flow.Go(ScreenId.SCR_005), color: new Color(0.3f, 0.6f, 0.35f));
            _recordsButton = UiKit.Button(transform, "Records", new Vector2(0.7f, 0.145f), new Vector2(240, 95),
                "내 기록 보기", () => Flow.Go(ScreenId.SCR_023)).gameObject;
        }

        protected override void OnEnter()
        {
            foreach (var go in _items)
                if (go != null)
                    Destroy(go);
            _items.Clear();

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

            _cardValues[0].text = $"{done.Count} 가지";
            _cardValues[1].text = $"{other.Count} 가지";
            _cardValues[2].text = FormatDuration(LastPlay != null ? LastPlay.DurationSec : 0f);

            PlaceRow(done, 0.55f, DoneColor);
            PlaceRow(other, 0.35f, OtherColor);

            _recordsButton.SetActive(!Flow.IsGuest);
        }

        // 10칸 고정 슬롯 — 왼쪽부터 채우고 남는 칸은 비워 둔다(미표시 · 컨테이너 크기 고정)
        void PlaceRow(List<string> foods, float y, Color color)
        {
            for (int i = 0; i < foods.Count && i < 10; i++)
            {
                float x = 0.135f + i * 0.081f;
                var card = UiKit.Panel(transform, $"Food_{foods[i]}", color);
                card.rectTransform.SetSizeWithAnchors(new Vector2(x, y), new Vector2(115, 110));
                // 완성 음식 그림이 있으면 교체 (6-7: 개별·간이 결과에서 사용 가능)
                if (!UI.ArtCatalog.TryAddIcon(card, UI.ArtCatalog.Food, foods[i]))
                    UiKit.Label(card.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(108, 100), 25, foods[i]);
                _items.Add(card.gameObject);
            }
        }

        static string FormatDuration(float sec)
        {
            int s = Mathf.FloorToInt(sec);
            return $"{s / 60}분 {s % 60}초";
        }
    }
}
