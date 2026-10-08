using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Save;
using Shinmyeong.UI;

namespace Shinmyeong.Flow.Screens
{
    /// 기기 선택 (기획 화면 코드 없음 · 93 문서 D2·D3) — **더미**. 운영자 로그인 다음 화면.
    /// 관리자 웹에 등록된 기관의 기기 중 이 PC가 맡을 1대를 고른다(기기 생성은 관리자 웹 몫).
    /// 목록에서 고르고 「이 기기로 시작」으로 확정 → SCR-001.
    /// 다른 PC가 쓰는 기기(「다른 PC에서 사용 중」)는 확인 단계를 한 번 더 거쳐야 옮긴다 — 공유본 5-1
    public class DeviceSelectScreen : ScreenBase
    {
        const int MaxRows = 5; // 한 기관 기기 수는 몇 대 수준 — 넘치면 스크롤 목록으로 바꾼다
        const string StartText = "이 기기로 시작";

        RectTransform _list;
        Text _empty;
        Text _notice;
        Text _startLabel;
        Image _startFace;
        readonly List<(Image face, Text label, Text tag)> _rows = new List<(Image, Text, Text)>();
        IReadOnlyList<DeviceInfo> _devices;
        int _selected = -1;
        bool _confirmPending; // 사용 중인 기기 — 한 번 더 눌러야 옮긴다

        protected override void BuildUi()
        {
            AdminUi.EnsureEventSystem();
            UiKit.Background(transform, "SCR-002-bg");

            var panel = UiKit.Frame(transform, "Panel", 560, 150, 800, 800, Skin.Face, Skin.Outline, shadow: true);
            UiKit.Txt(panel, "Title", 0, 50, 800, 56, "기기 선택", 48, 7, Skin.Ink);
            UiKit.Txt(panel, "Subtitle", 0, 122, 800, 32, "이 PC에서 쓸 기기를 골라 주세요", 26, 5, Skin.Muted);

            _list = UiKit.Node(panel, "List", 100, 190, 600, MaxRows * 84);
            _empty = UiKit.Txt(_list, "Empty", 0, 0, 600, MaxRows * 84,
                "등록된 기기가 없습니다\n관리자 웹에서 기기를 먼저 등록해 주세요", 26, 5, Skin.Muted);

            _notice = UiKit.Txt(panel, "Notice", 40, 600, 720, 60, "", 23, 5, Skin.Orange, wrap: true);
            _startLabel = AdminUi.Button(panel, "StartButton", 210, 680, 380, 84, StartText, Skin.Green, Color.white, Confirm);
            _startFace = _startLabel.transform.parent.GetComponent<Image>();
            AdminUi.Button(transform, "BackButton", 760, 970, 400, 56, "다른 계정으로 로그인", Skin.Dim, Color.white,
                () => Flow.GoBack(), 24);
        }

        protected override void OnEnter()
        {
            HandCursorUI.Hidden = true;
            if (!DeviceAuth.IsLoggedIn)
            {
                Flow.Go(ScreenId.LOGIN); // 로그인 없이 들어올 수 없다
                return;
            }
            _devices = DeviceAuth.LoadDevices();
            RebuildRows();
            Select(_devices.Count == 1 ? 0 : -1);
        }

        protected override void OnExit() => HandCursorUI.Hidden = false;

        void RebuildRows()
        {
            foreach (var row in _rows)
                Destroy(row.face.gameObject);
            _rows.Clear();
            _empty.gameObject.SetActive(_devices.Count == 0);

            for (int i = 0; i < _devices.Count && i < MaxRows; i++)
            {
                int index = i;
                var label = AdminUi.Button(_list, "Device" + i, 0, i * 84, 600, 70, _devices[i].Name,
                    Color.white, Skin.Brown, () => Select(index), 30);
                UiKit.Place(label.rectTransform, 30, 0, 540, 70);
                label.alignment = TextAnchor.MiddleLeft;
                var face = label.transform.parent.GetComponent<Image>();
                Text tag = null;
                if (_devices[i].InUse)
                    tag = UiKit.Txt(face.transform, "InUse", 300, 0, 270, 70, "다른 PC에서 사용 중", 22, 6, Skin.Orange, TextAnchor.MiddleRight);
                _rows.Add((face, label, tag));
            }
        }

        void Select(int index)
        {
            _selected = index;
            _confirmPending = false;
            for (int i = 0; i < _rows.Count; i++)
            {
                bool on = i == index;
                _rows[i].face.color = on ? Skin.Blue : Color.white;
                _rows[i].label.color = on ? Color.white : Skin.Brown;
                if (_rows[i].tag != null)
                    _rows[i].tag.color = on ? Color.white : Skin.Orange;
            }
            bool ready = index >= 0;
            _startFace.color = ready ? Skin.Green : Skin.Track;
            _startFace.GetComponent<Button>().interactable = ready;
            _startLabel.text = StartText;
            _notice.color = Skin.Muted;
            _notice.text = ready && _devices[index].InUse
                ? "다른 PC에 연결된 기기입니다. 이 PC로 옮기면 그 PC는 연결이 끊어집니다."
                : "";
        }

        void Confirm()
        {
            if (_selected < 0)
                return;
            var device = _devices[_selected];
            if (device.InUse && !_confirmPending)
            {
                // 확인 단계 — 실수로 다른 PC의 연결을 끊지 않게 한 번 더 누르게 한다
                _confirmPending = true;
                _notice.color = Skin.Orange;
                _notice.text = $"「{device.Name}」을(를) 이 PC로 옮길까요? 기존 PC는 연결이 끊어집니다.\n옮기려면 아래 버튼을 한 번 더 눌러 주세요.";
                _startFace.color = Skin.Orange;
                _startLabel.text = "이 PC로 옮기기";
                return;
            }
            if (!DeviceAuth.SelectDevice(device, replace: _confirmPending))
                return;
            Flow.Go(ScreenId.SCR_001);
        }
    }
}
