using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Shinmyeong.Save;
using Shinmyeong.UI;

namespace Shinmyeong.Flow.Screens
{
    /// 운영자 로그인 (기획 화면 코드 없음 · 93 문서 D1) — **더미**. 성공하면 기기 선택(DEVICE_SELECT)으로 간다.
    /// 설치 때 운영자가 한 번만 보는 관리 화면이라 어르신 조작(dwell)이 아니라 키보드·마우스로 입력한다.
    /// 기기가 연결된 PC는 실행 시 이 화면을 건너뛴다. Tab = 다음 칸 · Enter = 로그인.
    public class LoginScreen : ScreenBase
    {
        InputField _username;
        InputField _password;
        Text _status;
        bool _busy;

        protected override void BuildUi()
        {
            AdminUi.EnsureEventSystem();
            UiKit.Background(transform, "SCR-002-bg");

            var panel = UiKit.Frame(transform, "Panel", 560, 240, 800, 600, Skin.Face, Skin.Outline, shadow: true);
            UiKit.Txt(panel, "Title", 0, 50, 800, 56, "운영자 로그인", 48, 7, Skin.Ink);
            UiKit.Txt(panel, "Subtitle", 0, 122, 800, 32, "로그인한 뒤 이 PC에서 쓸 기기를 고릅니다", 26, 5, Skin.Muted);

            _username = Field(panel, "Username", 200, "아이디", "username", false);
            _password = Field(panel, "Password", 300, "비밀번호", "password", true);

            _status = UiKit.Txt(panel, "Status", 0, 392, 800, 30, "", 24, 5, Skin.Orange);
            AdminUi.Button(panel, "LoginButton", 250, 450, 300, 84, "로그인", Skin.Green, Color.white, Submit);

            UiKit.Footer(transform, $"더미 계정: {DeviceAuth.DummyUsername} / {DeviceAuth.DummyPassword} (서버 연동 전)",
                660, 912, 600, 50, 22);
        }

        protected override void OnEnter()
        {
            HandCursorUI.Hidden = true; // 손 조작 화면이 아니다
            DeviceAuth.Logout();
            _busy = false;
            _status.text = "";
            _password.text = "";
            Focus(_username);
        }

        protected override void OnExit() => HandCursorUI.Hidden = false;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _busy)
                return;
            if (kb.tabKey.wasPressedThisFrame)
                Focus(_username.isFocused ? _password : _username);
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                Submit();
        }

        void Submit()
        {
            if (_busy)
                return;
            if (string.IsNullOrEmpty(_username.text) || string.IsNullOrEmpty(_password.text))
            {
                _status.color = Skin.Orange;
                _status.text = "아이디와 비밀번호를 입력해 주세요";
                Focus(string.IsNullOrEmpty(_username.text) ? _username : _password);
                return;
            }
            StartCoroutine(LoginRoutine());
        }

        IEnumerator LoginRoutine()
        {
            _busy = true;
            _status.color = Skin.Muted;
            _status.text = "확인하고 있습니다…";
            yield return new WaitForSeconds(0.6f); // 서버 응답 대기 흉내
            if (DeviceAuth.Login(_username.text, _password.text))
            {
                Flow.Go(ScreenId.DEVICE_SELECT);
                yield break;
            }
            _busy = false;
            _status.color = Skin.Orange;
            _status.text = "아이디 또는 비밀번호를 다시 확인해 주세요";
            _password.text = "";
            Focus(_password);
        }

        static void Focus(InputField field)
        {
            field.Select();
            field.ActivateInputField();
        }

        /// 라벨 + 입력 칸 한 줄 (패널 기준 y)
        static InputField Field(RectTransform panel, string name, float y, string label, string placeholder, bool password)
        {
            UiKit.Txt(panel, name + "Label", 90, y, 160, 70, label, 28, 6, Skin.Brown, TextAnchor.MiddleLeft);

            var box = UiKit.Img(panel, name, "Box-Round-23", 260, y, 450, 70, Color.white, sliced: true);
            box.raycastTarget = true;
            UiKit.Img(box.transform, "Outline", "Box-Round-23-Outline", 0, 0, 450, 70, Skin.Outline, sliced: true);

            var hint = UiKit.Txt(box.transform, "Placeholder", 22, 0, 406, 70, placeholder, 24, 4, Skin.Track, TextAnchor.MiddleLeft);
            var text = UiKit.Txt(box.transform, "Text", 22, 0, 406, 70, "", 28, 5, Skin.Brown, TextAnchor.MiddleLeft);
            text.supportRichText = false;              // InputField 요구 사항
            text.horizontalOverflow = HorizontalWrapMode.Wrap;

            var field = box.gameObject.AddComponent<InputField>();
            field.targetGraphic = box;
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = InputField.LineType.SingleLine;
            field.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            field.characterLimit = 40;
            field.caretColor = Skin.Brown;
            field.customCaretColor = true;
            return field;
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
