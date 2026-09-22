using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;
using Shinmyeong.UI;

namespace Shinmyeong.Flow
{
    /// POP-001 일시정지 · POP-002 그만두기 확인 (07 문서 8-7 확정 · 플레이 3종 공용 오버레이 · 시안 POPUP).
    /// 뒤 화면은 유지한 채 딤으로 덮고(알아볼 수 있는 정도), 게임 진행·경과 시간은 GamePause(timeScale=0)로 멈춘다.
    /// 팝업 표시 중 DwellJudge.ModalRoot로 뒤 화면 dwell 조작을 차단한다.
    /// 문구는 8-6-1 확정 통합표를 따른다. TTS는 이 구간 허용(플레이 정지 상태) — 음성 도입 시 여기서 재생.
    /// 시안의 그만두기 X 아이콘은 부정 기호 금지(3-3)에 따라 나가기 아이콘으로 바꿨다.
    public class PausePopup : MonoBehaviour
    {
        // 게임 UI(UICanvas 0)와 커서(CursorCanvas 100) 사이
        const int SortingOrder = 50;

        static PausePopup _instance;

        GameObject _pop1;
        GameObject _pop2;

        /// POP-001 열기 — 플레이 화면의 일시정지 버튼이 호출
        public static void Open()
        {
            if (_instance == null)
                _instance = Build();
            _instance.OpenInternal();
        }

        public static bool IsOpen => _instance != null && _instance.gameObject.activeSelf;

        /// 화면 전환 등 외부 요인으로 팝업이 무효해질 때 — timeScale이 0에 갇히지 않게 정리
        public static void CloseIfOpen()
        {
            if (IsOpen)
                _instance.Close();
        }

        static PausePopup Build()
        {
            var go = new GameObject("PausePopup");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var popup = go.AddComponent<PausePopup>();
            popup.BuildUi();
            return popup;
        }

        void BuildUi()
        {
            // 딤 — 뒤 화면이 무엇이었는지 알아볼 수 있는 정도로만 (확정)
            UiKit.Panel(transform, "Dim", new Color(0f, 0f, 0f, 0.7f));

            _pop1 = BuildDialog("POP-001", "Icon-Pause", new Vector2(44, 49), "잠시 쉬어 갈까요?", null,
                ("계속하기", Close), ("그만두기", ShowQuitConfirm));

            // 2단계 확인 — 고령 사용자의 오조작 종료 방지 (확정)
            _pop2 = BuildDialog("POP-002", "Icon-Exit", new Vector2(55, 58), "그만두시겠어요?", "지금 하시던 활동은 기록되지 않아요",
                ("계속하기", Close), ("그만두기", Quit));
        }

        GameObject BuildDialog(string name, string icon, Vector2 iconSize, string title, string guide,
            (string label, System.Action action) primary, (string label, System.Action action) secondary)
        {
            var root = UiKit.Group(transform, name);

            // 대화 상자 880×530 (x520 y275) — 상호작용 안전 영역(x420~1500 / y240~980) 안 (확정)
            var dialog = UiKit.Node(root, "Dialog", 520, 275, 880, 530);
            UiKit.Img(dialog, "Face", "Box-Round-28", 0, 0, 880, 530, Skin.Hex("fffef8"), sliced: true);
            UiKit.Img(dialog, "Face2", "Box-Round-23", 5, 5, 870, 520, Skin.FaceWarm, sliced: true);
            UiKit.Img(dialog, "Outline", "Box-Round-23-Outline", 5, 5, 870, 520, Skin.Hex("c2b286", 0.55f), sliced: true);

            UiKit.Img(dialog, "Circle", "Circle-125", 380, 46, 120, 120, Skin.Hex("fffaec"));
            UiKit.ImgFit(dialog, "Icon", icon, 440 - iconSize.x * 0.5f, 106 - iconSize.y * 0.5f, iconSize.x, iconSize.y, Skin.BrownDark);

            UiKit.Txt(dialog, "Title", 140, 211, 600, 70, title, 58, 6, Skin.BrownDark);
            if (guide != null)
                UiKit.Txt(dialog, "Guide", 100, 290, 680, 46, guide, 30, 5, Skin.Hex("6d655a"));

            // 계속하기(주 · 초록) · 그만두기(상아) — 각 318×104 · dwell 3초
            var cont = UiKit.Node(dialog, "ContinueButton", 103, 368, 318, 104);
            UiKit.Img(cont, "Background", "Bt-Round-Green-03", 0, 0, 318, 104, Color.white, sliced: true);
            UiKit.ImgFit(cont, "PlayIcon", "Icon-Play", 45, 31, 38, 42, Color.white);
            UiKit.Txt(cont, "Label", 89, 19, 183, 63, primary.label, 37, 7, Color.white);
            UiKit.Dwell(cont, 318, 104, 3f, primary.action, withText: false);

            var quit = UiKit.Node(dialog, "QuitButton", 456, 368, 318, 104);
            UiKit.Img(quit, "Background", "Bt-Round-Ivory-02", 0, 0, 318, 104, Color.white, sliced: true);
            UiKit.ImgFit(quit, "ExitIcon", "Icon-Exit", 44, 33, 36, 38, Skin.Hex("382819"));
            UiKit.Txt(quit, "Label", 89, 19, 184, 63, secondary.label, 37, 7, Skin.Hex("3c2919"));
            UiKit.Dwell(quit, 318, 104, 3f, secondary.action, withText: false);

            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        void OpenInternal()
        {
            gameObject.SetActive(true);
            _pop1.SetActive(true);
            _pop2.SetActive(false);
            GamePause.Pause();
            DwellJudge.ModalRoot = transform; // 뒤 화면 dwell 차단 — 팝업 버튼만 판정
        }

        void ShowQuitConfirm()
        {
            _pop1.SetActive(false);
            _pop2.SetActive(true);
        }

        /// 계속하기 — 팝업을 닫고 하던 활동으로 복귀
        void Close()
        {
            DwellJudge.ModalRoot = null;
            GamePause.Resume();
            gameObject.SetActive(false);
        }

        /// POP-002 그만두기 — 기록 없이, 결과 화면을 거치지 않고 개별 SCR-005 / 스토리 SCR-004 (확정)
        void Quit()
        {
            Close();
            FlowManager.Instance.Go(FlowManager.Instance.Mode == GameMode.Story
                ? ScreenId.SCR_004
                : ScreenId.SCR_005);
        }
    }
}
