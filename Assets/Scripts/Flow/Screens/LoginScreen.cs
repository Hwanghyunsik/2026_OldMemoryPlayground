using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Shinmyeong.Net;
using Shinmyeong.Save;
using Shinmyeong.UI;

namespace Shinmyeong.Flow.Screens
{
    /// 운영자 로그인 (기획 화면 코드 없음 · 명세 D1 `POST auth/login`). 성공하면 기기 선택(DEVICE_SELECT)으로 간다.
    /// 설치 때 운영자가 한 번만 보는 관리 화면이라 어르신 조작(dwell)이 아니라 키보드·마우스로 입력한다.
    /// 기기가 연결된 PC는 실행 시 이 화면을 건너뛴다. Tab = 다음 칸 · Enter = 로그인.
    /// 그림은 디자이너 프리팹 `Assets/Prefabs/SCR-000-Login`(UiCatalog.Screens)을 그대로 쓰고, 코드는 입력 칸·버튼 동작만 붙인다.
    /// 「아이디/비밀번호 찾기」는 관리자 웹(ApiConfig.WebUrl)을 브라우저로 연다 — 계정 관리는 웹 몫.
    public class LoginScreen : ScreenBase
    {
        const string PrefabName = "SCR-000-Login";

        InputField _username;
        InputField _password;
        Text _status;
        bool _busy;

        protected override void BuildUi()
        {
            AdminUi.EnsureEventSystem();

            var prefab = UiCatalog.Instance != null ? UiCatalog.Instance.FindScreen(PrefabName) : null;
            if (prefab != null)
                BuildFromPrefab(prefab);
            else
            {
                Debug.LogWarning($"[Login] 화면 프리팹 {PrefabName} 없음 — 메뉴 「신명/UI 카탈로그 재생성」 확인 · 임시 레이아웃으로 표시");
                BuildFallback();
            }

            UiKit.Txt(transform, "Server", 0, 1038, 1920, 30, $"서버 {ApiConfig.BaseUrl}", 18, 4, Skin.Muted);
        }

        // ---- 디자이너 프리팹: 배경 · 패널 · 「로그인」 제목 · 아이디/비밀번호 칸 · 로그인 버튼 · 「아이디/비밀번호 찾기」 ----

        void BuildFromPrefab(GameObject prefab)
        {
            var root = Instantiate(prefab, transform, false);
            root.name = "Design";
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            var panel = Find(root, "MainPanel");
            _username = MakeField(Find(root, "MainPanel/Input/ID/Input_ID"), password: false);
            _password = MakeField(Find(root, "MainPanel/Input/PW/Input_ID"), password: true);
            MakeButton(Find(root, "MainPanel/Input/BT_Login"), Submit);
            MakeButton(Find(root, "MainPanel/ID_PW"), OpenAccountHelp);

            // 안내 문구 — 패널 아래쪽 빈 줄 (ID_PW 줄 아래)
            _status = UiKit.Txt(panel != null ? panel : (RectTransform)root.transform, "Status", 0, 365, 896, 30, "", 20, 5, Skin.Orange);
        }

        static RectTransform Find(GameObject root, string path)
        {
            var t = root.transform.Find(path);
            if (t == null)
                Debug.LogError($"[Login] 프리팹 {PrefabName} 에 '{path}' 가 없음 — 디자이너 프리팹 구조가 바뀌었으면 LoginScreen 경로를 맞출 것");
            return t as RectTransform;
        }

        /// 디자이너의 흰 입력 상자(Image)에 InputField 를 붙인다
        static InputField MakeField(RectTransform box, bool password)
        {
            if (box == null)
                return null;
            var img = box.GetComponent<Image>();
            if (img != null)
                img.raycastTarget = true;

            float w = box.rect.width, h = box.rect.height;
            var text = UiKit.Txt(box, "Text", 18, 0, w - 36, h, "", 24, 5, Skin.Brown, TextAnchor.MiddleLeft);
            text.supportRichText = false;              // InputField 요구 사항
            text.horizontalOverflow = HorizontalWrapMode.Wrap;

            var field = box.gameObject.AddComponent<InputField>();
            field.targetGraphic = img;
            field.textComponent = text;
            field.lineType = InputField.LineType.SingleLine;
            field.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            field.characterLimit = 40;
            field.caretColor = Skin.Brown;
            field.customCaretColor = true;
            return field;
        }

        /// 디자이너 요소(버튼 그림 · 글자)를 클릭 버튼으로 — 자식 글자도 클릭을 받아 올린다
        static void MakeButton(RectTransform node, Action onClick)
        {
            if (node == null)
                return;
            var graphic = node.GetComponent<Graphic>();
            if (graphic != null)
                graphic.raycastTarget = true;
            foreach (var g in node.GetComponentsInChildren<Graphic>())
                g.raycastTarget = true;
            var button = node.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.onClick.AddListener(() => onClick());
        }

        void OpenAccountHelp()
        {
            string url = ApiConfig.WebUrl;
            Debug.Log($"[Login] 아이디/비밀번호 찾기 → 브라우저 {url}");
            Application.OpenURL(url);
        }

        // ---- 프리팹이 없을 때의 임시 레이아웃 (코드 조립) ----

        void BuildFallback()
        {
            UiKit.Background(transform, "SCR-002-bg");
            var panel = UiKit.Frame(transform, "Panel", 560, 240, 800, 600, Skin.Face, Skin.Outline, shadow: true);
            UiKit.Txt(panel, "Title", 0, 50, 800, 56, "로그인", 48, 7, Skin.Ink);
            UiKit.Txt(panel, "Subtitle", 0, 122, 800, 32, "로그인한 뒤 이 PC에서 쓸 기기를 고릅니다", 26, 5, Skin.Muted);
            _username = FallbackField(panel, "Username", 200, "아이디", false);
            _password = FallbackField(panel, "Password", 300, "비밀번호", true);
            _status = UiKit.Txt(panel, "Status", 0, 392, 800, 30, "", 24, 5, Skin.Orange);
            AdminUi.Button(panel, "LoginButton", 250, 450, 300, 84, "로그인", Skin.Green, Color.white, Submit);
            var help = UiKit.Txt(panel, "AccountHelp", 0, 545, 800, 34, "아이디/비밀번호 찾기", 22, 5, Skin.Muted);
            help.raycastTarget = true;
            help.gameObject.AddComponent<Button>().onClick.AddListener(OpenAccountHelp);
        }

        static InputField FallbackField(RectTransform panel, string name, float y, string label, bool password)
        {
            UiKit.Txt(panel, name + "Label", 90, y, 160, 70, label, 28, 6, Skin.Brown, TextAnchor.MiddleLeft);
            var box = UiKit.Img(panel, name, "Box-Round-23", 260, y, 450, 70, Color.white, sliced: true);
            UiKit.Img(box.transform, "Outline", "Box-Round-23-Outline", 0, 0, 450, 70, Skin.Outline, sliced: true);
            return MakeField((RectTransform)box.transform, password);
        }

        // ---- 동작 ----

        protected override void OnEnter()
        {
            HandCursorUI.Hidden = true; // 손 조작 화면이 아니다
            DeviceAuth.Logout();
            _busy = false;
            _status.text = "";
            if (_password != null) _password.text = "";
            Focus(_username);
        }

        protected override void OnExit() => HandCursorUI.Hidden = false;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _busy || _username == null || _password == null)
                return;
            if (kb.tabKey.wasPressedThisFrame)
                Focus(_username.isFocused ? _password : _username);
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                Submit();
        }

        void Submit()
        {
            if (_busy || _username == null || _password == null)
                return;
            if (string.IsNullOrEmpty(_username.text) || string.IsNullOrEmpty(_password.text))
            {
                _status.color = Skin.Orange;
                _status.text = "아이디와 비밀번호를 입력해 주세요";
                Focus(string.IsNullOrEmpty(_username.text) ? _username : _password);
                return;
            }
            RunLogin();
        }

        async void RunLogin()
        {
            _busy = true;
            _status.color = Skin.Muted;
            _status.text = "확인하고 있습니다…";
            var res = await DeviceAuth.Login(_username.text, _password.text);
            if (Flow.CurrentId != Id)
                return; // 기다리는 사이 화면이 바뀜
            if (res.Ok)
            {
                Flow.Go(ScreenId.DEVICE_SELECT);
                return;
            }
            _busy = false;
            _status.color = Skin.Orange;
            // 401 틀림 · 403 막힌 계정/정지된 기관/전체관리자 · 429 잠시 잠김 — 서버 message 는 화면에 그대로 보여도 되는 한국어(명세 공통)
            _status.text = res.Unreachable ? "서버에 연결할 수 없습니다. 네트워크를 확인해 주세요"
                : res.Status == 401 ? "아이디 또는 비밀번호를 다시 확인해 주세요"
                : res.Message;
            if (res.Status == 401)
                _password.text = "";
            Focus(res.Status == 401 ? _password : _username);
        }

        static void Focus(InputField field)
        {
            if (field == null)
                return;
            field.Select();
            field.ActivateInputField();
        }
    }

    /// 관리 화면(로그인 · 기기 선택) 공용 — 마우스 클릭 UI
    static class AdminUi
    {
        /// 클릭 버튼 (둥근 면 + 라벨). 라벨 Text를 돌려준다
        public static Text Button(Transform parent, string name, float x, float y, float w, float h,
            string label, Color face, Color labelColor, Action onClick, int size = 34)
        {
            var img = UiKit.Pill(parent, name, x, y, w, h, face);
            img.raycastTarget = true;
            img.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());
            return UiKit.Txt(img.transform, "Label", 0, 0, w, h, label, size, 7, labelColor);
        }

        /// 다른 화면은 손 커서(dwell)만 써서 씬에 EventSystem이 없다 — 관리 화면의 클릭·입력용으로 만든다
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
