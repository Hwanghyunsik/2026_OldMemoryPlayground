using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;

namespace Shinmyeong.Flow
{
    /// POP-001 일시정지 · POP-002 그만두기 확인 (07 문서 8-7 확정 · 플레이 3종 공용 오버레이).
    /// 뒤 화면은 유지한 채 딤으로 덮고(알아볼 수 있는 정도), 게임 진행·경과 시간은 GamePause(timeScale=0)로 멈춘다.
    /// 팝업 표시 중 DwellJudge.ModalRoot로 뒤 화면 dwell 조작을 차단한다.
    /// 문구는 8-6-1 확정 통합표를 따른다. TTS는 이 구간 허용(플레이 정지 상태) — 음성 도입 시 여기서 재생.
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
            var popup = go.AddComponent<PausePopup>();
            popup.BuildUi();
            return popup;
        }

        void BuildUi()
        {
            // 딤 — 뒤 화면이 무엇이었는지 알아볼 수 있는 정도로만 (확정)
            UiKit.Panel(transform, "Dim", new Color(0f, 0f, 0f, 0.6f));

            _pop1 = BuildPanel("POP-001", "잠시 쉬어 갈까요?", null,
                ("계속하기", Close, true),
                ("그만두기", ShowQuitConfirm, false));

            // 2단계 확인 — 고령 사용자의 오조작 종료 방지 (확정)
            _pop2 = BuildPanel("POP-002", "그만두시겠어요?", "지금 하시던 활동은 기록되지 않아요",
                ("계속하기", Close, true),
                ("그만두기", Quit, false));
        }

        GameObject BuildPanel(string name, string title, string guide,
            params (string label, System.Action action, bool primary)[] buttons)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            UiKit.Stretch(root);

            // 패널은 상호작용 안전 영역(x420~1500 / y240~980) 안 (확정)
            var panel = UiKit.Panel(root.transform, "Panel", new Color(0.16f, 0.16f, 0.2f));
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.55f);
            rect.sizeDelta = new Vector2(880, 460);

            UiKit.Label(root.transform, "Title", new Vector2(0.5f, 0.68f), new Vector2(800, 90), 54, title);
            if (guide != null)
                UiKit.Label(root.transform, "Guide", new Vector2(0.5f, 0.58f), new Vector2(800, 60), 34, guide);

            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                float x = 0.5f + (i - (buttons.Length - 1) * 0.5f) * 0.16f;
                UiKit.Button(root.transform, b.label, new Vector2(x, 0.45f), new Vector2(280, 110),
                    b.label, b.action.Invoke,
                    color: b.primary ? new Color(0.3f, 0.6f, 0.35f) : new Color(0.4f, 0.4f, 0.45f));
            }

            root.SetActive(false);
            return root;
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
