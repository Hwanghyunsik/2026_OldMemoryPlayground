using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Games.Harvest;
using Shinmyeong.Interaction;
using Shinmyeong.UI;

namespace Shinmyeong.Flow.Screens
{
    // SCR-007 수확 튜토리얼은 GameTutorialScreen 공용 템플릿 사용 (2-5 확정: 템플릿 1 + 데이터 3벌)

    /// SCR-008 수확 플레이 — 그림 한 벌을 두 모드가 공유, 종료 이동처만 분기(-S 규칙).
    /// 무대 그림(배경·넝쿨·바구니·HUD)은 HarvestDemo가 조립한다. 일시정지는 시안 위치(좌측 상단 x61 y255).
    public class HarvestPlayScreen : ScreenBase
    {
        HarvestDemo _game;

        protected override void BuildUi()
        {
            UiKit.Background(transform, "SCR-008-Bg");
            var pause = UiKit.NavButton(transform, "PauseButton", 61, 255, "일시정지", "Icon-Pause", PausePopup.Open);
            pause.gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(좌측 상단) — 확정 위치(하단 중앙 220×100)와 다름 · 기획 확인 대기(Q5)";
        }

        protected override void OnEnter()
        {
            if (_game == null)
            {
                _game = gameObject.AddComponent<HarvestDemo>();
                _game.Finished += OnFinished;
            }
            _game.Begin(Rect);
            transform.Find("PauseButton").SetAsLastSibling(); // 무대 위에 버튼
        }

        protected override void OnExit() => _game?.End();

        void OnFinished(Save.PlayRecord record, IReadOnlyList<string> lines)
        {
            Flow.CommitPlay(record); // 비회원 여부는 FlowManager가 판단
            ResultScreen.LastPlay = record;
            HarvestStoryResultScreen.LastPlay = record;
            Flow.Go(Flow.Mode == GameMode.Story ? ScreenId.SCR_010 : ScreenId.SCR_009);
        }
    }

    /// SCR-009 수확 개별 결과 (8-2 · 설계서 p28 확정 · 시안 SCR-009).
    /// 쉬운 지표 3종(정확하게 선택 · 다른 선택 · 전체 활동 시간 — 분모 미표기) + 라운드 1~10 순서 수확물 나열.
    /// 구분: 정확 = 선반 위 / 다른 선택 = 흙 단에 기울임 / 벌레 = 벌레 아이콘 병기 (텍스트·O/X·색상 단독 구분 금지).
    /// 반응 시간·힌트 등 상세 수치는 ADM 전용 — 표시하지 않는다. 힌트 도움 성공도 정확한 선택.
    public class ResultScreen : ScreenBase
    {
        public static Save.PlayRecord LastPlay;

        ResultKit.Header _header;
        RectTransform _panel;
        Text[] _cardValues;
        GameObject _recordsButton;
        readonly List<GameObject> _items = new List<GameObject>();

        protected override void BuildUi()
        {
            UiKit.Background(transform, "SCR-008-Bg");
            _header = ResultKit.BuildHeader(transform, "수확을 마쳤어요", "담아오신 것을 살펴보세요");
            _panel = ResultKit.BuildPanel(transform);

            _cardValues = new Text[3];
            _cardValues[0] = ResultKit.SummaryCard(_panel, "Correct", 0, ResultKit.CardKind.Proper, "정확하게 선택");
            _cardValues[1] = ResultKit.SummaryCard(_panel, "Different", 1, ResultKit.CardKind.Other, "다른 선택");
            _cardValues[2] = ResultKit.SummaryCard(_panel, "Duration", 2, ResultKit.CardKind.Duration, "전체 활동 시간");

            // 리본 + 점선 / 선반(흙 단) + 라운드 번호 띠
            var l = UiKit.Img(_panel, "DividerLeft", "Dot-Line-3", 49, 269, 444, 3, Skin.Outline2);
            l.type = Image.Type.Tiled;
            var r = UiKit.Img(_panel, "DividerRight", "Dot-Line-3", 824, 269, 443, 3, Skin.Outline2);
            r.type = Image.Type.Tiled;
            ResultKit.Ribbon(_panel, "Ribbon", 487, 243, true, "담아 오신 것");
            UiKit.ImgFit(_panel, "Ground", "SCR-009-Ground", 39, 435, 1236, 207);
            var strip = UiKit.Img(_panel, "NumberStrip", "Box-Round-5", 74, 579, 1165, 38, Skin.Hex("d2c4ab", 0.45f), sliced: true);
            for (int i = 0; i < 10; i++)
                UiKit.Txt(strip.transform, $"Number{i + 1}", 5 + i * 116, 0, 110, 38, (i + 1).ToString(), 28, 7, Skin.BrownDark);

            _recordsButton = ResultKit.BuildButtons(transform,
                () => Flow.Go(ScreenId.SCR_008), () => Flow.Go(ScreenId.SCR_005), () => Flow.Go(ScreenId.SCR_023));
        }

        protected override void OnEnter()
        {
            foreach (var go in _items)
                if (go != null)
                    Destroy(go);
            _items.Clear();

            ResultKit.ApplyHeader(_header, Flow);
            int correct = 0, total = 0;
            if (LastPlay != null)
            {
                total = LastPlay.HarvestRounds.Count;
                for (int i = 0; i < total; i++)
                {
                    var round = LastPlay.HarvestRounds[i];
                    bool proper = round.Result == "정답";
                    if (proper)
                        correct++;
                    PlaceCrop(i, round.Picked, proper, round.Result == "벌레");
                }
            }
            _cardValues[0].text = ResultKit.CountText(correct);
            _cardValues[1].text = ResultKit.CountText(total - correct);
            _cardValues[2].text = ResultKit.DurationText(LastPlay != null ? LastPlay.DurationSec : 0f);

            _recordsButton.SetActive(!Flow.IsGuest);
        }

        /// 라운드 순서대로 같은 칸(번호 위): 정확 = 선반 위 y337 · 다른 선택 = 흙 단 y442에 기울여 놓는다 (8-2)
        void PlaceCrop(int slot, string name, bool proper, bool bug)
        {
            float x = 79 + slot * 116;
            float y = proper ? 337 : 442;
            var card = UiKit.Node(_panel, $"Crop{slot + 1}", x, y, 110, 110);
            UiKit.Img(card, "Face", "Box-Round-23", 0, 0, 110, 110, Skin.Face, sliced: true);
            UiKit.Img(card, "Outline", "Box-Round-23-Outline", 0, 0, 110, 110, Skin.Outline, sliced: true);
            var art = ArtCatalog.Get(bug ? ArtCatalog.CropBug : ArtCatalog.CropLaid, name);
            if (art != null)
                UiKit.ImgFit(card, "Crop", null, 17, 17, 76, 76).sprite = art;
            else
                UiKit.Txt(card, "Label", 5, 5, 100, 100, name, 24, 6, Skin.Brown);
            if (bug)
                UiKit.ImgFit(card, "Bug", "Bug", 64, 73, 31, 34); // 벌레 먹은 것 병기 (상시 식별 표식)
            if (!proper)
            {
                UiKit.PlaceCentered(card, x, y, 110, 110);
                card.localRotation = Quaternion.Euler(0, 0, slot % 2 == 0 ? -12f : 12f);
            }
            _items.Add(card.gameObject);
        }
    }
}
