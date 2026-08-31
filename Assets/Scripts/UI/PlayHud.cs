using UnityEngine;
using UnityEngine.UI;

namespace Shinmyeong.UI
{
    /// 플레이 화면 공통 HUD (07 문서 2-3·2-5 확정) — 게임 3종이 공유한다.
    ///   상단 중앙: 목표 제시 패널 1000×116
    ///   상단 우측: 경과 시간 372×116 (제한 시간 아님 · 숫자만, 게이지·경고 없음)
    ///   우측 세로: 진행 레일 220×800 · 1~10 공통 · 현재 라운드 강조
    /// 전부 비인터랙션 표시 요소 — 안전 영역(2-4) 적용 대상이 아니다.
    /// 진행 표시는 이 레일 하나뿐 — 두 곳에 중복 배치하지 않는다(확정).
    /// 스테이지 루트에 붙여 게임 End(루트 파괴)와 수명을 같이한다.
    public class PlayHud : MonoBehaviour
    {
        Text _goalText;
        Text _elapsedText;
        readonly Image[] _cells = new Image[10];
        readonly Text[] _cellTexts = new Text[10];
        float _clockStart;

        static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.35f);
        static readonly Color CellFuture = new Color(1f, 1f, 1f, 0.10f);
        static readonly Color CellDone = new Color(0.5f, 0.62f, 0.42f, 0.85f);
        static readonly Color CellCurrent = new Color(1f, 0.85f, 0.25f);

        public static PlayHud Create(Transform parent)
        {
            var go = new GameObject("PlayHud");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            var hud = go.AddComponent<PlayHud>();
            hud.Build();
            return hud;
        }

        void Build()
        {
            // 목표 제시 패널 — 상단 중앙 1000×116 (2-5 확정)
            var goal = Panel(transform, "GoalPanel", new Vector2(0.5f, 0.922f), new Vector2(1000, 116));
            _goalText = Label(goal.transform, "Text", new Vector2(480, 100), 38, "");

            // 경과 시간 — 상단 우측 372×116 · 제한 아님(확정 5-9)
            var elapsed = Panel(transform, "ElapsedPanel", new Vector2(0.878f, 0.922f), new Vector2(372, 116));
            _elapsedText = Label(elapsed.transform, "Text", new Vector2(340, 90), 40, "00:00");

            // 진행 레일 — 우측 세로 220×800 · 1이 위 · 경과 시간 아래로 내려 겹치지 않게
            var rail = Panel(transform, "ProgressRail", new Vector2(0.918f, 0.48f), new Vector2(220, 800));
            for (int i = 0; i < 10; i++)
            {
                var cellGo = new GameObject($"Round_{i + 1}");
                cellGo.transform.SetParent(rail.transform, false);
                var img = cellGo.AddComponent<Image>();
                img.raycastTarget = false;
                img.color = CellFuture;
                var cellRect = cellGo.GetComponent<RectTransform>();
                cellRect.anchorMin = cellRect.anchorMax = new Vector2(0.5f, 1f - (i + 0.5f) / 10f);
                cellRect.sizeDelta = new Vector2(64, 64);
                _cells[i] = img;
                _cellTexts[i] = Label(cellGo.transform, "Num", new Vector2(60, 40), 24, (i + 1).ToString());
            }

            _clockStart = Time.time;
        }

        void Update()
        {
            // timeScale=0 일시정지 중에는 Time.time이 멈춰 「일시정지 시간 제외」가 자동 충족된다
            int sec = Mathf.FloorToInt(Time.time - _clockStart);
            _elapsedText.text = $"{sec / 60:00}:{sec % 60:00}";
        }

        /// 목표 제시 패널 내용 (게임별 데이터 + 8-6-1 확정 안내 문구)
        public void SetGoal(string text) => _goalText.text = text;

        /// 현재 라운드(1~10) 강조 — 지난 라운드는 완료 색
        public void SetRound(int round)
        {
            for (int i = 0; i < 10; i++)
            {
                bool current = i == round - 1;
                _cells[i].color = current ? CellCurrent : i < round - 1 ? CellDone : CellFuture;
                _cellTexts[i].color = current ? new Color(0.2f, 0.15f, 0f) : Color.white;
                _cells[i].rectTransform.localScale = current ? Vector3.one * 1.15f : Vector3.one;
            }
        }

        // ---- 조립 헬퍼 (X-Box 단계 · 자산 도입 시 프리팹 교체) ----

        static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = PanelColor;
            img.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            return img;
        }

        static Text Label(Transform parent, string name, Vector2 size, int fontSize, string content)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.text = content;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return text;
        }
    }
}
