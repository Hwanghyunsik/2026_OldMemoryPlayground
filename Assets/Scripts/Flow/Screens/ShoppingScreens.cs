using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Games.Shopping;
using Shinmyeong.Interaction;

namespace Shinmyeong.Flow.Screens
{
    // SCR-012 장보기 튜토리얼은 GameTutorialScreen 공용 템플릿 사용 (2-5 확정: 템플릿 1 + 데이터 3벌)

    /// SCR-013 장보기 플레이 — 종료 이동처만 모드 분기 (개별 014 / 스토리 015)
    public class ShoppingPlayScreen : ScreenBase
    {
        ShoppingDemo _game;

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.12f, 0.13f, 0.11f));
            // 일시정지 — 장보기만 상단 좌측 220×116 (안전 영역 예외 · 확정 07 문서 2-4: x48 y34)
            var pause = UiKit.Button(transform, "Pause", new Vector2(0.082f, 0.915f), new Vector2(220, 116),
                "일시정지", PausePopup.Open, color: new Color(0.3f, 0.3f, 0.35f));
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

    /// SCR-014 장보기 개별 결과 (5-14 확정 · 재료 단위 20개).
    /// 요약 카드 3종: 제대로 산 재료 · 다르게 산 재료 · 전체 활동 시간 (합 20 · 분모 미표기 · 힌트 도착도 제대로에 포함).
    /// 나열: 종류 단위 묶음 + 개수 배지 · 위 = 제대로 / 아래 = 다르게(기울임) · 라운드 번호 미표시.
    /// 힌트·도착 시간·좌우 편차 등 상세 수치는 ADM 전용. 「잘못·틀린」 대신 「다르게 산」(확정).
    public class ShoppingResultScreen : ScreenBase
    {
        public static Save.PlayRecord LastPlay;

        Text[] _cardValues;
        GameObject _recordsButton;
        readonly List<GameObject> _items = new List<GameObject>();

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.11f, 0.13f, 0.12f));
            UiKit.Label(transform, "Header", new Vector2(0.5f, 0.9f), new Vector2(900, 70), 50, "장보기를 마쳤어요");
            UiKit.Label(transform, "Sub", new Vector2(0.5f, 0.84f), new Vector2(900, 44), 30, "사 오신 재료를 살펴보세요");

            string[] cardNames = { "제대로 산 재료", "다르게 산 재료", "전체 활동 시간" };
            _cardValues = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var card = UiKit.Panel(transform, $"Card_{i}", new Color(1f, 1f, 1f, 0.08f));
                card.rectTransform.SetSizeWithAnchors(new Vector2(0.34f + 0.16f * i, 0.72f), new Vector2(260, 110));
                UiKit.Label(card.transform, "Name", new Vector2(0.5f, 0.74f), new Vector2(240, 36), 26, cardNames[i]);
                _cardValues[i] = UiKit.Label(card.transform, "Value", new Vector2(0.5f, 0.32f), new Vector2(240, 48), 36, "");
            }

            UiKit.Panel(transform, "Shelf", new Color(1f, 1f, 1f, 0.10f))
                .rectTransform.SetSizeWithAnchors(new Vector2(0.5f, 0.505f), new Vector2(1080, 12));
            UiKit.Panel(transform, "Ground", new Color(0f, 0f, 0f, 0.22f))
                .rectTransform.SetSizeWithAnchors(new Vector2(0.5f, 0.31f), new Vector2(1080, 12));

            UiKit.Button(transform, "Retry", new Vector2(0.3f, 0.145f), new Vector2(240, 95), "다시 하기",
                () => Flow.Go(ScreenId.SCR_013));
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

            int correct = 0, total = 0;
            var groups = new List<(string label, int count, bool proper)>();
            if (LastPlay != null)
                foreach (var r in LastPlay.ShoppingItems)
                {
                    total++;
                    if (r.Correct)
                        correct++;
                    int idx = groups.FindIndex(g => g.label == r.BoughtItem && g.proper == r.Correct);
                    if (idx >= 0)
                        groups[idx] = (r.BoughtItem, groups[idx].count + 1, r.Correct);
                    else
                        groups.Add((r.BoughtItem, 1, r.Correct));
                }

            _cardValues[0].text = $"{correct} 개";
            _cardValues[1].text = $"{total - correct} 개";
            _cardValues[2].text = FormatDuration(LastPlay != null ? LastPlay.DurationSec : 0f);

            PlaceRow(groups.FindAll(g => g.proper), 0.575f, tilt: false);
            PlaceRow(groups.FindAll(g => !g.proper), 0.38f, tilt: true);

            _recordsButton.SetActive(!Flow.IsGuest);
        }

        void PlaceRow(List<(string label, int count, bool proper)> row, float y, bool tilt)
        {
            for (int i = 0; i < row.Count; i++)
            {
                float x = 0.5f + (i - (row.Count - 1) * 0.5f) * 0.085f;
                var card = UiKit.Panel(transform, $"Item_{row[i].label}", tilt
                    ? new Color(0.45f, 0.42f, 0.38f)
                    : new Color(0.55f, 0.6f, 0.45f));
                card.rectTransform.SetSizeWithAnchors(new Vector2(x, y), new Vector2(125, 115));
                if (tilt)
                    card.rectTransform.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? -8f : 8f);
                if (!UI.ArtCatalog.TryAddIcon(card, UI.ArtCatalog.Ingredient, row[i].label))
                    UiKit.Label(card.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(115, 100), 26, row[i].label);
                if (row[i].count > 1)
                {
                    var badge = UiKit.Label(card.transform, "Badge", new Vector2(0.85f, 0.85f), new Vector2(56, 32), 24,
                        $"×{row[i].count}");
                    badge.color = new Color(1f, 0.9f, 0.5f);
                }
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
