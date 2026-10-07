using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Flow;

namespace Shinmyeong.UI
{
    /// 플레이 화면 공통 HUD (07 문서 2-3·2-5 · 시안 SCR-008/013/018) — 게임 3종이 공유한다.
    ///   상단 종이 헤더(Title-bg 1184×182 · x329 y50): 목표 제시 — 문구 + 목표 그림, 장보기는 GoalRoot에 칩 3개
    ///   상단 우측 경과 시간(Time-Bg 285×127 · x1590 y65): 제한 시간 아님 · 숫자만
    ///   우측 세로 진행 레일(122×705 · x1752 y255): 1~10 · 현재 라운드 초록 강조 · 지난 라운드 옅은 초록
    /// 전부 비인터랙션 표시 요소 — 안전 영역(2-4) 적용 대상이 아니다. 진행 표시는 이 레일 하나뿐(중복 배치 금지).
    /// 스테이지 루트에 붙여 게임 End(루트 파괴)와 수명을 같이한다.
    public class PlayHud : MonoBehaviour
    {
        public RectTransform GoalRoot { get; private set; }

        Text _goalText;
        Image _goalIcon;
        Text _elapsedText;
        readonly Image[] _cells = new Image[10];
        readonly Text[] _cellTexts = new Text[10];
        float _clockStart;

        static readonly Color CellFuture = Skin.Tan;
        static readonly Color CellDone = Skin.Hex("9fbfa9");
        static readonly Color CellCurrent = Skin.Green;

        public static PlayHud Create(Transform parent)
        {
            var go = new GameObject("PlayHud");
            go.transform.SetParent(parent, false);
            UiKit.Stretch(go);
            var hud = go.AddComponent<PlayHud>();
            hud.Build();
            return hud;
        }

        void Build()
        {
            // 목표 제시 — 종이 헤더 (x329 y50 1184×182)
            GoalRoot = UiKit.HeaderPaper(transform, 329, 50);
            _goalIcon = UiKit.ImgFit(GoalRoot, "TargetIcon", null, 286, 32, 113, 110);
            _goalIcon.gameObject.SetActive(false);
            _goalText = UiKit.Txt(GoalRoot, "Instruction", 0, 60, 1184, 55, "", 40, 6, Skin.Ink);

            // 경과 시간 (x1590 y65 285×127) — 제한 아님(5-9)
            var elapsed = UiKit.Node(transform, "ElapsedTime", 1590, 65, 285, 127);
            UiKit.Img(elapsed, "Box", "Time-Bg", 1, -1, 283, 129);
            UiKit.Txt(elapsed, "Label", 110, 20, 150, 35, "경과 시간", 25, 6, Skin.BrownDark);
            _elapsedText = UiKit.Txt(elapsed, "Value", 110, 57, 150, 48, "00 : 00", 38, 7, Skin.Green);

            // 진행 레일 (x1752 y255 122×705) — 1이 위
            var rail = UiKit.Node(transform, "StageProgress", 1752, 255, 122, 705);
            UiKit.Img(rail, "Frame", "Box-Round-28", 0, 0, 122, 705, Color.white, sliced: true);
            // 시안 좌표는 안쪽 면이 0.5px 왼쪽, 칸 묶음이 2.5px 위로 치우쳐(위 여백 22 · 아래 27) 우상단으로 쏠려 보였다 → 정중앙 배치
            const float FaceX = (122f - 113f) * 0.5f, FaceY = 5f, FaceH = 695f;
            const float CellSize = 55f, CellPitch = 65.7f;
            float cellTop = FaceY + (FaceH - (CellPitch * 9f + CellSize)) * 0.5f;
            float cellX = FaceX + (113f - CellSize) * 0.5f;
            UiKit.Img(rail, "Face", "Box-Round-23", FaceX, FaceY, 113, FaceH, Skin.Paper, sliced: true);
            UiKit.Img(rail, "Outline", "Box-Round-23-Outline", FaceX, FaceY, 113, FaceH, Skin.Outline, sliced: true);
            for (int i = 0; i < 10; i++)
            {
                float y = cellTop + i * CellPitch;
                var cell = UiKit.Img(rail, $"Stage{i + 1}", "Circle-52", cellX, y, CellSize, CellSize, CellFuture);
                _cells[i] = cell;
                _cellTexts[i] = UiKit.Txt(cell.transform, "Number", 0, 0, CellSize, CellSize, (i + 1).ToString(), 30, 6, Skin.Muted);
                _cellTexts[i].alignByGeometry = true; // 폰트 기준선 대신 글자 모양으로 원 정중앙에 맞춘다 (숫자가 위로 뜨지 않게)
            }

            _clockStart = Time.time;
        }

        void Update()
        {
            // timeScale=0 일시정지 중에는 Time.time이 멈춰 「일시정지 시간 제외」가 자동 충족된다
            int sec = Mathf.FloorToInt(Time.time - _clockStart);
            _elapsedText.text = $"{sec / 60:00} : {sec % 60:00}";
        }

        /// 목표 제시 패널 내용 (게임별 데이터 + 8-6-1 확정 안내 문구). 목표 그림이 있으면 왼쪽에 함께 보인다
        public void SetGoal(string text, Sprite icon = null)
        {
            _goalText.text = text;
            bool hasIcon = icon != null;
            _goalIcon.gameObject.SetActive(hasIcon);
            if (hasIcon)
            {
                _goalIcon.sprite = icon;
                UiKit.Place(_goalText.rectTransform, 425, 60, 700, 55);
                _goalText.alignment = TextAnchor.MiddleLeft;
            }
            else
            {
                UiKit.Place(_goalText.rectTransform, 0, 60, 1184, 55);
                _goalText.alignment = TextAnchor.MiddleCenter;
            }
        }

        /// 기본 문구 표시를 끄고 GoalRoot에 게임이 직접 그린다 (장보기 목표 칩)
        public void UseCustomGoal()
        {
            _goalText.gameObject.SetActive(false);
            _goalIcon.gameObject.SetActive(false);
        }

        /// 현재 라운드(1~10) 강조 — 지난 라운드는 완료 색 (성패 무관 동일 색 · Q3 확정)
        public void SetRound(int round)
        {
            for (int i = 0; i < 10; i++)
            {
                bool current = i == round - 1;
                _cells[i].color = current ? CellCurrent : i < round - 1 ? CellDone : CellFuture;
                _cellTexts[i].color = current ? Color.white : i < round - 1 ? Color.white : Skin.Muted;
            }
        }
    }
}
