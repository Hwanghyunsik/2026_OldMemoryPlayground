using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Games.Shopping;
using Shinmyeong.Interaction;
using Shinmyeong.UI;

namespace Shinmyeong.Flow.Screens
{
    // SCR-012 장보기 튜토리얼은 GameTutorialScreen 공용 템플릿 사용 (2-5 확정: 템플릿 1 + 데이터 3벌)

    /// SCR-013 장보기 플레이 — 종료 이동처만 모드 분기 (개별 014 / 스토리 015).
    /// 무대(점포·발판·장바구니·HUD)는 ShoppingDemo가 조립한다. 일시정지는 상단 좌측(2-4 확정 예외 · 시안 x58 y79).
    public class ShoppingPlayScreen : ScreenBase
    {
        ShoppingDemo _game;

        protected override void BuildUi()
        {
            UiKit.Background(transform, "SCR-013-Bg");
            var pause = UiKit.NavButton(transform, "PauseButton", 58, 79, "일시정지", "Icon-Pause", PausePopup.Open);
            pause.gameObject.AddComponent<SafeAreaExempt>().Reason = "장보기 일시정지 — 상단 좌측 (2-4 확정 예외)";
        }

        protected override void OnEnter()
        {
            if (_game == null)
            {
                _game = gameObject.AddComponent<ShoppingDemo>();
                _game.Finished += OnFinished;
            }
            _game.Begin(Rect);
            transform.Find("PauseButton").SetAsLastSibling();
        }

        protected override void OnExit() => _game?.End();

        void OnFinished(Save.PlayRecord record, IReadOnlyList<string> lines)
        {
            Flow.CommitPlay(record);
            ShoppingResultScreen.LastPlay = record;
            ShoppingStoryResultScreen.LastPlay = record;
            Flow.Go(Flow.Mode == GameMode.Story ? ScreenId.SCR_015 : ScreenId.SCR_014);
        }
    }

    /// SCR-014 장보기 개별 결과 (5-14 확정 · 재료 단위 20개 · 시안 SCR-014).
    /// 요약 카드 3종: 제대로 산 재료 · 다르게 산 재료 · 전체 활동 시간 (합 20 · 분모 미표기 · 힌트 도착도 제대로에 포함).
    /// 나열: 종류 단위 묶음 + 개수 배지 · 위 = 제대로(초록 상자) / 아래 = 다르게(주황 상자 · 기울임) · 라운드 번호 미표시.
    /// 힌트·도착 시간·좌우 편차 등 상세 수치는 ADM 전용. 「잘못·틀린」 대신 「다르게 산」(확정).
    public class ShoppingResultScreen : ScreenBase
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
            UiKit.Background(transform, "SCR-013-Bg");
            _header = ResultKit.BuildHeader(transform, "장보기를 마쳤어요", "사 오신 재료를 살펴보세요");
            _panel = ResultKit.BuildPanel(transform);

            _cardValues = new Text[3];
            _cardValues[0] = ResultKit.SummaryCard(_panel, "Correct", 0, ResultKit.CardKind.Proper, "제대로 산 재료");
            _cardValues[1] = ResultKit.SummaryCard(_panel, "Different", 1, ResultKit.CardKind.Other, "다르게 산 재료");
            _cardValues[2] = ResultKit.SummaryCard(_panel, "Duration", 2, ResultKit.CardKind.Duration, "전체 활동 시간");

            _properBox = ResultKit.MaterialBox(_panel, "CorrectMaterials", 233, proper: true);
            _otherBox = ResultKit.MaterialBox(_panel, "DifferentMaterials", 457, proper: false);
            ResultKit.Ribbon(_panel, "GreenRibbon", 487, 211, true, "제대로 산 재료");
            ResultKit.Ribbon(_panel, "OrangeRibbon", 483, 435, false, "다르게 산 재료");

            _recordsButton = ResultKit.BuildButtons(transform,
                () => Flow.Go(ScreenId.SCR_013), () => Flow.Go(ScreenId.SCR_005), () => Flow.Go(ScreenId.SCR_023));
        }

        protected override void OnEnter()
        {
            foreach (var go in _items)
                if (go != null)
                    Destroy(go);
            _items.Clear();

            ResultKit.ApplyHeader(_header, Flow);
            int correct = 0, total = 0;
            var pairs = new List<(string, bool)>();
            if (LastPlay != null)
                foreach (var r in LastPlay.ShoppingItems)
                {
                    total++;
                    if (r.Correct)
                        correct++;
                    pairs.Add((r.BoughtItem, r.Correct));
                }
            var groups = ResultKit.Group(pairs);

            _cardValues[0].text = ResultKit.CountText(correct);
            _cardValues[1].text = ResultKit.CountText(total - correct);
            _cardValues[2].text = ResultKit.DurationText(LastPlay != null ? LastPlay.DurationSec : 0f);

            PlaceRow(_properBox, groups.FindAll(g => g.proper), proper: true);
            PlaceRow(_otherBox, groups.FindAll(g => !g.proper), proper: false);

            _recordsButton.SetActive(!Flow.IsGuest);
        }

        void PlaceRow(RectTransform box, List<(string label, int count, bool proper)> row, bool proper)
        {
            float pitch = ResultKit.Pitch(row.Count, 1180, 110, 134);
            for (int i = 0; i < row.Count; i++)
                _items.Add(ResultKit.MaterialItem(box, i, pitch, proper, row[i].label,
                    ArtCatalog.Get(ArtCatalog.Ingredient, row[i].label), row[i].count));
        }
    }
}
