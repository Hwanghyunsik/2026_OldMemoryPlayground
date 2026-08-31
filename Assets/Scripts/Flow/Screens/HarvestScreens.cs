using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Games.Harvest;

namespace Shinmyeong.Flow.Screens
{
    // SCR-007 수확 튜토리얼은 GameTutorialScreen 공용 템플릿 사용 (2-5 확정: 템플릿 1 + 데이터 3벌)

    /// SCR-008 수확 플레이 — 그림 한 벌을 두 모드가 공유, 종료 이동처만 분기(-S 규칙)
    public class HarvestPlayScreen : ScreenBase
    {
        HarvestDemo _game;

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.10f, 0.14f, 0.10f));
            // 일시정지 — 하단 중앙 220×100 (확정 · 07 문서 2-4) · 안전 영역 하한(y980) 안
            UiKit.Button(transform, "Pause", new Vector2(0.5f, 0.14f), new Vector2(220, 100),
                "일시정지", PausePopup.Open, color: new Color(0.3f, 0.3f, 0.35f));
        }

        protected override void OnEnter()
        {
            if (_game == null)
            {
                _game = gameObject.AddComponent<HarvestDemo>();
                _game.Finished += OnFinished;
            }
            _game.Begin(Rect);
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

    /// SCR-009 수확 개별 결과 (8-2 · 설계서 p28 확정).
    /// 쉬운 지표 3종(정확하게 선택 · 다른 선택 · 전체 활동 시간 — 분모 미표기) + 라운드 1~10 순서 수확물 나열.
    /// 구분: 정확 = 선반 위 / 다른 선택 = 흙 단에 기울임 / 벌레 = 아이콘 병기 (텍스트·O/X·색상 단독 구분 금지).
    /// 반응 시간·힌트 등 상세 수치는 ADM 전용 — 표시하지 않는다. 힌트 도움 성공도 정확한 선택.
    public class ResultScreen : ScreenBase
    {
        public static Save.PlayRecord LastPlay;

        Text[] _cardValues;
        GameObject _recordsButton;
        readonly List<GameObject> _items = new List<GameObject>();

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.13f, 0.13f, 0.10f));
            UiKit.Label(transform, "Header", new Vector2(0.5f, 0.9f), new Vector2(900, 70), 50, "수확을 마쳤어요");
            UiKit.Label(transform, "Sub", new Vector2(0.5f, 0.84f), new Vector2(900, 44), 30, "담아 오신 것을 살펴보세요");

            string[] cardNames = { "정확하게 선택", "다른 선택", "전체 활동 시간" };
            _cardValues = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var card = UiKit.Panel(transform, $"Card_{i}", new Color(1f, 1f, 1f, 0.08f));
                card.rectTransform.SetSizeWithAnchors(new Vector2(0.34f + 0.16f * i, 0.72f), new Vector2(260, 110));
                UiKit.Label(card.transform, "Name", new Vector2(0.5f, 0.74f), new Vector2(240, 36), 26, cardNames[i]);
                _cardValues[i] = UiKit.Label(card.transform, "Value", new Vector2(0.5f, 0.32f), new Vector2(240, 48), 36, "");
            }

            // 선반 / 흙 단
            UiKit.Panel(transform, "Shelf", new Color(1f, 1f, 1f, 0.10f))
                .rectTransform.SetSizeWithAnchors(new Vector2(0.5f, 0.505f), new Vector2(1080, 12));
            UiKit.Panel(transform, "Ground", new Color(0f, 0f, 0f, 0.22f))
                .rectTransform.SetSizeWithAnchors(new Vector2(0.5f, 0.31f), new Vector2(1080, 12));

            UiKit.Button(transform, "Retry", new Vector2(0.3f, 0.145f), new Vector2(240, 95), "다시 하기",
                () => Flow.Go(ScreenId.SCR_008));
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

            int correct = 0;
            var top = new List<Save.HarvestRoundRecord>();
            var bottom = new List<Save.HarvestRoundRecord>();
            if (LastPlay != null)
                foreach (var r in LastPlay.HarvestRounds)
                {
                    if (r.Result == "정답")
                    {
                        correct++;
                        top.Add(r);
                    }
                    else
                        bottom.Add(r);
                }

            _cardValues[0].text = $"{correct} 개";
            _cardValues[1].text = $"{(LastPlay != null ? LastPlay.HarvestRounds.Count - correct : 0)} 개";
            _cardValues[2].text = FormatDuration(LastPlay != null ? LastPlay.DurationSec : 0f);

            PlaceRow(top, 0.575f, tilt: false);
            PlaceRow(bottom, 0.38f, tilt: true);

            _recordsButton.SetActive(!Flow.IsGuest);
        }

        void PlaceRow(List<Save.HarvestRoundRecord> row, float y, bool tilt)
        {
            for (int i = 0; i < row.Count; i++)
            {
                float x = 0.5f + (i - (row.Count - 1) * 0.5f) * 0.082f;
                var card = UiKit.Panel(transform, $"Crop_{i}", tilt
                    ? new Color(0.45f, 0.42f, 0.38f)
                    : new Color(0.55f, 0.6f, 0.45f));
                card.rectTransform.SetSizeWithAnchors(new Vector2(x, y), new Vector2(115, 110));
                if (tilt)
                    card.rectTransform.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? -8f : 8f);
                // 놓인 작물 그림이 있으면 교체 · 벌레 먹은 것만 아이콘 병기 (자산 도입 시 큰 벌레 아이콘)
                bool hasArt = UI.ArtCatalog.TryAddIcon(card, UI.ArtCatalog.CropLaid, row[i].Picked);
                if (!hasArt)
                {
                    string label = row[i].Result == "벌레" ? $"{row[i].Picked}\n🐛" : row[i].Picked;
                    UiKit.Label(card.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(108, 100), 26, label);
                }
                else if (row[i].Result == "벌레")
                    UiKit.Label(card.transform, "Bug", new Vector2(0.8f, 0.2f), new Vector2(44, 40), 30, "🐛");
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
