using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;
using Shinmyeong.Tracking;
using Shinmyeong.UI;

namespace Shinmyeong.Flow.Screens
{
    /// SCR-001 대기 · 타이틀 (시안 SCR-001) — 조작 요소 없음 · 사람 감지 시 자동 진입.
    /// 제목 로고는 배경 그림에 포함 · 안내 2단(카메라 앞 · 발자국 위치) · 좌상단 현재 날짜/시각
    public class IdleScreen : ScreenBase
    {
        Text _dateText;
        static readonly CultureInfo Korean = new CultureInfo("ko-KR");

        protected override void BuildUi()
        {
            UiKit.Background(transform, "SCR-001-bg");

            var camera = UiKit.Img(transform, "Camera", "SCR-001-Camera-bg", 538, 663, 844, 233);
            UiKit.ImgFit(camera.transform, "CameraIcon", "SCR-001-Camera-icon", 107, 57, 121, 95);
            UiKit.Txt(camera.transform, "CameraTitle", 264, 66, 490, 48, "카메라 앞에 서 주세요", 50, 6, Skin.Hex("1e4d40"), TextAnchor.MiddleLeft);
            UiKit.Txt(camera.transform, "CameraSubtitle", 264, 129, 400, 30, "잠시 후 자동으로 시작됩니다", 30, 4, Skin.Hex("1e4d40"), TextAnchor.MiddleLeft);

            var foot = UiKit.Img(transform, "FootGuide", "SCR-001-Foot-bg", 638, 885, 649, 183);
            UiKit.ImgFit(foot.transform, "FootIcon", "SCR-001-Foot-icon", 72, 51, 73, 65);
            UiKit.Txt(foot.transform, "FootLine1", 191, 52, 385, 28, "화면에서 약 3m 떨어진 곳에", 28, 6, Skin.Brown, TextAnchor.MiddleLeft);
            UiKit.Txt(foot.transform, "FootLine2", 191, 88, 385, 28, "발자국 표시를 따라 서 주세요.", 28, 6, Skin.Brown, TextAnchor.MiddleLeft);

            var date = UiKit.Img(transform, "Date", "Box-Trans-Round-25", 24, 31, 448, 86, Color.white, sliced: true);
            _dateText = UiKit.Txt(date.transform, "DateText", 0, 0, 448, 86, "", 25, 5, Color.white);
        }

        protected override void OnEnter() => RefreshDate();

        void Update()
        {
            if (Time.frameCount % 60 == 0)
                RefreshDate();
            var svc = BodyTrackingService.Instance;
            if (svc != null && svc.PersonPresent)
                Flow.Go(ScreenId.SCR_002);
        }

        void RefreshDate()
        {
            var now = DateTime.Now;
            _dateText.text = now.ToString("yyyy년 M월 d일 dddd tt h:mm", Korean);
        }
    }

    // SCR-002 사용자 감지·보정은 CalibrationScreen.cs

    /// SCR-003 사용자 선택 (시안 SCR-003) — 저장소의 등록 사용자 카드 + 비회원.
    /// 사용자 등록·수정은 관리자 화면(FN-20 · 타 팀 웹) 몫 — 여기서는 선택만.
    /// 페이징(2-5 확정): 쪽당 8명(4×2) · 그리드 바깥 좌우 세로 버튼 + 현재 쪽 표시 · 그리드 안에 이동 카드 금지 ·
    /// 첫/마지막 쪽에서는 해당 방향 버튼 자체를 표시하지 않는다(8-5 원칙).
    public class UserSelectScreen : ScreenBase
    {
        const int UsersPerPage = 8;

        // 카드 8색 (시안 User01~08 InnerFill / InnerBorder) — SCR-023 헤더 등 다른 곳도 같은 표를 쓴다
        internal static readonly (Color fill, Color border)[] CardColors =
        {
            (Skin.Hex("f8f6ec"), Skin.Hex("d8cba8")), (Skin.Hex("f5f6e6"), Skin.Hex("d8cba8")),
            (Skin.Hex("faebef"), Skin.Hex("dca6b5")), (Skin.Hex("e1e8f8"), Skin.Hex("acbdde")),
            (Skin.Hex("eceae4"), Skin.Hex("c4c2bd")), (Skin.Hex("f2e7fa"), Skin.Hex("c4a5db")),
            (Skin.Hex("e1f9f3"), Skin.Hex("a1d9cd")), (Skin.Hex("ffe3d4"), Skin.Hex("ffb48a")),
        };

        GameObject _cardsRoot;
        Text _emptyText;
        int _page;

        protected override void BuildUi()
        {
            UiKit.Background(transform, "SCR-002-bg");
            var header = UiKit.HeaderPaper(transform);
            UiKit.ImgFit(header, "Title", "SCR-003-Title-Text", 456, 49, 271, 50);
            UiKit.Txt(header, "Instruction", 100, 112, 984, 34, "손을 이름 위에 <b>3초간</b> 올려 두시면 선택됩니다.", 28, 5, Skin.Brown);

            // 카드 판 (어두운 반투명 상자)
            UiKit.Img(transform, "Box", "SCR-003-User-Bg-Trans", 340, 275, 1236, 530, Skin.Dim, sliced: true);
            _emptyText = UiKit.Txt(transform, "Empty", 340, 275, 1236, 530,
                "등록된 분이 아직 없어요\n(등록은 관리자 화면에서 합니다)", 30, 5, Color.white);

            // 비회원 — 우측 「처음 오셨나요?」 + 「기록은 저장되지 않습니다」 안내
            var guest = UiKit.Node(transform, "GuestButton", 1637, 443, 223, 203);
            UiKit.Img(guest, "Background", "Box-Round-30", 0, 0, 223, 203, Skin.Dim, sliced: true);
            UiKit.Img(guest, "Dotline", "Dot-Line-1.5", 5, 5, 213, 193);
            UiKit.Img(guest, "PlusCircle", "Circle-125", 86, 46, 50, 50, Color.white);
            UiKit.ImgFit(guest, "Plus", "Icon-Plus", 96, 56, 30, 30, Skin.IconTint);
            UiKit.Txt(guest, "Title", 8, 115, 207, 34, "처음 오셨나요?", 25, 6, Color.white);
            UiKit.Txt(guest, "Subtitle", 8, 151, 207, 30, "이름 없이 바로 시작", 18, 5, Skin.FaceWarm);
            UiKit.Dwell(guest, 223, 203, 3f, () =>
            {
                Flow.IsGuest = true;
                Flow.UserId = "";
                Flow.UserName = "";
                Flow.Go(ScreenId.SCR_004);
            }).gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(우측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";
            UiKit.ImgFit(transform, "GuestNotice", "SCR-003-Notice", 1656, 649, 186, 79);

            UiKit.NavButton(transform, "HomeButton", 84, 477, "처음으로", "Icon-Home", () => Flow.Go(ScreenId.SCR_001))
                .gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(좌측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";

#if UNITY_EDITOR
            // 에디터 전용: 관리자 웹(타 팀) 연동 전 저장 검증용 사용자 등록
            var devAdd = UiKit.NavButton(transform, "DevAddUser", 1700, 900, "+ 테스트 사용자", null, () =>
            {
                int n = Save.SaveStore.LoadUsers().Users.Count + 1;
                Save.SaveStore.AddUser($"테스트{n}", n % 2 == 0 ? "남" : "여", (n - 1) % 8, (n - 1) % 8);
                RebuildCards();
            }, w: 180, h: 70, dwellSeconds: 1f, labelSize: 20);
            devAdd.gameObject.AddComponent<SafeAreaExempt>().Reason = "에디터 전용 개발 버튼 — 빌드 미포함";
#endif
        }

        protected override void OnEnter()
        {
            _page = 0;
            RebuildCards();
        }

        void RebuildCards()
        {
            if (_cardsRoot != null)
                Destroy(_cardsRoot);
            _cardsRoot = UiKit.Group(transform, "Cards").gameObject;

            var users = Save.SaveStore.LoadUsers().Users;
            _emptyText.gameObject.SetActive(users.Count == 0);

            int pages = Mathf.Max(1, Mathf.CeilToInt(users.Count / (float)UsersPerPage));
            _page = Mathf.Clamp(_page, 0, pages - 1);
            bool paged = pages > 1;

            // 4×2 카드 236×234 · 가로 간격 246 · 세로 242 (시안 User01~08)
            int start = _page * UsersPerPage;
            int count = Mathf.Min(users.Count - start, UsersPerPage);
            for (int i = 0; i < count; i++)
            {
                var user = users[start + i];
                int row = i / 4, col = i % 4;
                int colsInRow = row == 0 ? Mathf.Min(count, 4) : count - 4;
                float rowWidth = colsInRow * 236 + (colsInRow - 1) * 10;
                float x = 958 - rowWidth * 0.5f + col * 246;
                float y = count <= 4 ? 423 : (row == 0 ? 302 : 544);
                var (fill, border) = CardColors[Mathf.Abs(user.CardColorIndex) % CardColors.Length];

                var card = UiKit.Node(_cardsRoot.transform, $"User_{start + i}", x, y, 236, 234);
                UiKit.Img(card, "Face", "Box-Round-28", 1, 2, 233, 230, Color.white, sliced: true);
                UiKit.Img(card, "InnerFill", "Box-Round-23", 7, 7, 223, 220, fill, sliced: true);
                UiKit.Img(card, "InnerBorder", "Box-Round-23-Outline", 7, 7, 223, 220, border, sliced: true);
                var avatar = ArtCatalog.Get(ArtCatalog.Avatar, $"아바타{user.AvatarIndex + 1}");
                if (avatar != null)
                {
                    var img = UiKit.ImgFit(card, "Avatar", null, 42, 23, 151, 151);
                    img.sprite = avatar;
                }
                else
                    UiKit.Txt(card, "AvatarPlaceholder", 42, 23, 151, 151, $"그림 {user.AvatarIndex + 1}", 24, 5, Skin.Muted);
                UiKit.Txt(card, "Name", 8, 178, 220, 42, user.Name, 32, 7, Skin.Hex("2d2119"));
                UiKit.Dwell(card, 236, 234, 3f, () =>
                {
                    Flow.IsGuest = false;
                    Flow.UserId = user.Id;
                    Flow.UserName = user.Name;
                    Flow.Go(ScreenId.SCR_004);
                });
            }

            if (!paged)
                return;

            // 목록 이동 — 그리드 바깥 좌우 세로 버튼 93×149 + 현재 쪽 표시 (2-5 확정 · 그리드 안에 이동 카드 금지)
            if (_page > 0)
                PageButton(353, "PreviousPage", "이전", flip: true, () => { _page--; RebuildCards(); });
            if (_page < pages - 1)
                PageButton(1469, "NextPage", "다음", flip: false, () => { _page++; RebuildCards(); });
            var pill = UiKit.Node(_cardsRoot.transform, "Pagination", 854, 867, 223, 56);
            UiKit.Pill(pill, "Background", 0, 0, 223, 56, Skin.Dim);
            UiKit.Txt(pill, "PageNumber", 0, 0, 223, 56, $"{_page + 1} / {pages}", 26, 6, Color.white);
        }

        void PageButton(float x, string name, string label, bool flip, Action onSelected)
        {
            var root = UiKit.Node(_cardsRoot.transform, name, x, 475, 93, 149);
            UiKit.Img(root, "Background", "Bt-Round-Ivory", 0, 0, 93, 149, Color.white, sliced: true);
            var arrow = UiKit.ImgFit(root, "Arrow", "Icon-Next", 32, 33, 29, 47, Skin.IconTint);
            if (flip)
                arrow.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            UiKit.Txt(root, "Label", 4, 95, 85, 34, label, 23, 6, Skin.ButtonLabel);
            UiKit.Dwell(root, 93, 149, 3f, onSelected, withText: false)
                .gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(카드 판 가장자리) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";
        }
    }

    /// SCR-004 모드 선택 (시안 SCR-004) — 스토리 / 개별 분기 · 비회원은 헤더에 이름·아바타 미표시(확정)
    public class ModeSelectScreen : ScreenBase
    {
        Text _greeting;
        Image _avatar;

        protected override void BuildUi()
        {
            UiKit.Background(transform, "SCR-002-bg");
            var header = UiKit.HeaderPaper(transform);
            _avatar = UiKit.ImgFit(header, "Avatar", "Avatar_01", 89, 35, 116, 116);
            _greeting = UiKit.Txt(header, "Greeting", 230, 48, 760, 54, "", 40, 6, Skin.Ink, TextAnchor.MiddleLeft);
            UiKit.Txt(header, "Instruction", 230, 105, 840, 38, "무엇을 하시겠어요? 손을 3초간 올려주시면 선택됩니다", 28, 5, Skin.Brown, TextAnchor.MiddleLeft);

            // 스토리 게임 652×607
            var story = UiKit.Node(transform, "StoryGame", 304, 264, 652, 607);
            UiKit.Img(story, "Background", "SCR-004-StoryGame", 0, 0, 652, 607);
            UiKit.Txt(story, "Description", 70, 360, 512, 42, "세 가지 활동을 이야기로 이어서 합니다", 25, 5, Skin.Brown);
            UiKit.ImgFit(story, "Activity0", "Icon-Harvest", 111, 416, 98, 98);
            UiKit.ImgFit(story, "ActivityLabel0", "Icon-Harvest-Text", 108, 494, 103, 39);
            UiKit.ImgFit(story, "Arrow0", "Icon-Next", 228, 443, 26, 41, Skin.Muted);
            UiKit.ImgFit(story, "Activity1", "Icon-Shopping", 275, 416, 98, 98);
            UiKit.ImgFit(story, "ActivityLabel1", "Icon-Shopping-Text", 284, 494, 80, 39);
            UiKit.ImgFit(story, "Arrow1", "Icon-Next", 390, 443, 26, 41, Skin.Muted);
            UiKit.ImgFit(story, "Activity2", "Icon-Cooking", 439, 416, 98, 98);
            UiKit.ImgFit(story, "ActivityLabel2", "Icon-Cooking-Text", 436, 494, 103, 39);
            UiKit.Dwell(story, 652, 607, 3f, () =>
            {
                Flow.Mode = GameMode.Story;
                Flow.Go(ScreenId.SCR_006);
            });

            // 개별 게임 652×597 — 안의 활동 3칸은 미리보기(선택은 SCR-005 로비에서 · 이동 맵)
            var free = UiKit.Node(transform, "IndividualGame", 967, 272, 652, 597);
            UiKit.Img(free, "Background", "SCR-004-IndividualGame", 0, 0, 652, 597);
            MiniActivity(free, 49, "Icon-Harvest", "Icon-Harvest-Text", 132, 50, Skin.Hex("3d7a26"));
            MiniActivity(free, 235, "Icon-Shopping", "Icon-Shopping-Text", 104, 51, Skin.Hex("bd642d"));
            MiniActivity(free, 421, "Icon-Cooking", "Icon-Cooking-Text", 132, 50, Skin.Hex("65338d"));
            UiKit.Txt(free, "Description", 37, 422, 578, 100,
                "원하는 활동을 하나만 골라서 합니다\n활동을 마치면 결과를 자세히 볼 수 있습니다", 25, 5, Skin.Brown);
            UiKit.Dwell(free, 652, 597, 3f, () =>
            {
                Flow.Mode = GameMode.Free;
                Flow.Go(ScreenId.SCR_005);
            });

            UiKit.NavButton(transform, "HomeButton", 79, 477, "처음으로", "Icon-Home", () => Flow.Go(ScreenId.SCR_001))
                .gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(좌측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";
            UiKit.NavButton(transform, "ChangeUserButton", 1683, 478, "사용자 변경", "Icon-User", () => Flow.Go(ScreenId.SCR_003), labelSize: 22)
                .gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(우측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";
            UiKit.Footer(transform, "손을 3초간 올려두면 선택됩니다.");
        }

        static void MiniActivity(Transform parent, float x, string icon, string label, float labelW, float labelH, Color border)
        {
            var card = UiKit.Node(parent, icon, x, 174, 178, 192);
            UiKit.Img(card, "Fill", "Box-Round-23", 2, 2, 174, 188, Skin.Cream, sliced: true);
            UiKit.Img(card, "Border", "Box-Round-23-Outline", 0, 0, 178, 192, border, sliced: true);
            UiKit.ImgFit(card, "ActivityIcon", icon, 18, 12, 142, 142);
            UiKit.ImgFit(card, "ActivityLabel", label, (178 - labelW) * 0.5f, 132, labelW, labelH);
        }

        protected override void OnEnter()
        {
            _greeting.text = Flow.IsGuest ? "반갑습니다" : $"{Flow.UserName} 님, 반갑습니다";
            ApplyAvatar(_avatar, Flow);
        }

        /// 헤더 아바타 — 비회원이면 숨긴다
        internal static void ApplyAvatar(Image avatar, FlowManager flow)
        {
            var profile = flow.IsGuest ? null : Save.SaveStore.LoadUsers().Users.Find(u => u.Id == flow.UserId);
            var sprite = profile != null ? ArtCatalog.Get(ArtCatalog.Avatar, $"아바타{profile.AvatarIndex + 1}") : null;
            avatar.gameObject.SetActive(sprite != null);
            if (sprite != null)
                avatar.sprite = sprite;
        }
    }

    /// SCR-005 콘텐츠 로비 (시안 SCR-005 · 개별 모드 전용) — 게임 3종 카드 + 내 기록 보기(비회원 미표시)
    public class LobbyScreen : ScreenBase
    {
        GameObject _recordsButton;
        Image _avatar;
        GameObject _userBadge;
        Text _userName;
        readonly Text[] _recent = new Text[3];

        protected override void BuildUi()
        {
            UiKit.Background(transform, "SCR-002-bg");
            var header = UiKit.HeaderPaper(transform);
            _avatar = UiKit.ImgFit(header, "Avatar", "Avatar_01", 89, 35, 116, 116);
            UiKit.Txt(header, "Title", 230, 48, 660, 54, "무엇을 해 볼까요?", 40, 6, Skin.Ink, TextAnchor.MiddleLeft);
            UiKit.Txt(header, "Instruction", 230, 105, 690, 38, "하고 싶은 활동에 손을 3초간 올려 두세요", 28, 5, Skin.Brown, TextAnchor.MiddleLeft);
            _userBadge = UiKit.Img(header, "UserBadge", "Box-Round-23", 936, 57, 193, 71, Skin.Cream, sliced: true).gameObject;
            _userName = UiKit.Txt(_userBadge.transform, "UserName", 0, 0, 193, 71, "", 28, 6, Skin.Brown);

            _recent[0] = ActivityCard(318, "SCR-005-Harvest-Box", "Icon-Harvest", "수확하기", "팔을 뻗어 재료를 거둡니다", Skin.BrownDark, ScreenId.SCR_007);
            _recent[1] = ActivityCard(752, "SCR-005-Shopping-Box", "Icon-Shopping", "장보기", "옆으로 옮겨 재료를 고릅니다", Skin.Orange, ScreenId.SCR_012);
            _recent[2] = ActivityCard(1186, "SCR-005-Cooking-Box", "Icon-Cooking", "요리하기", "재료를 기억해 음식을 만듭니다", Skin.Purple, ScreenId.SCR_017);

            UiKit.NavButton(transform, "BackButton", 79, 478, "이전으로", "Icon-Arrow", () => Flow.Go(ScreenId.SCR_004))
                .gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(좌측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";
            var records = UiKit.NavButton(transform, "RecordsButton", 1677, 478, "내 기록 보기", "Icon-Record", () => Flow.Go(ScreenId.SCR_023), labelSize: 21);
            records.gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(우측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";
            _recordsButton = records.gameObject;
            UiKit.Footer(transform, "손을 3초간 올려두면 선택됩니다.", 696, 933);
        }

        Text ActivityCard(float x, string box, string icon, string title, string desc, Color titleColor, ScreenId next)
        {
            var card = UiKit.Node(transform, title, x, 275, 421, 597);
            UiKit.Img(card, "Background", box, 0, 0, 421, 597);
            UiKit.ImgFit(card, "ActivityIcon", icon, 102, 58, 218, 215);
            UiKit.Txt(card, "ActivityTitle", 24, 382, 373, 58, title, 40, 7, titleColor);
            UiKit.Txt(card, "Description", 20, 445, 381, 43, desc, 25, 5, Skin.Brown);
            var recent = UiKit.Txt(card, "RecentParticipation", 20, 505, 381, 38, "", 22, 5, Skin.Muted);
            UiKit.Dwell(card, 421, 597, 3f, () => Flow.Go(next));
            return recent;
        }

        protected override void OnEnter()
        {
            // 비회원에게는 기록 버튼 자체를 표시하지 않는다 (확정 · 인계자료 8-5)
            _recordsButton.SetActive(!Flow.IsGuest);
            _userBadge.SetActive(!Flow.IsGuest);
            _userName.text = $"{Flow.UserName} 님";
            ModeSelectScreen.ApplyAvatar(_avatar, Flow);

            // 활동별 최근 참여일 — 기록이 있을 때만
            string[] keys = { "Harvest", "Shopping", "Cooking" };
            var records = Flow.IsGuest || string.IsNullOrEmpty(Flow.UserId)
                ? null : Save.SaveStore.LoadRecords(Flow.UserId).Records;
            for (int i = 0; i < 3; i++)
            {
                string last = null;
                if (records != null)
                    for (int r = records.Count - 1; r >= 0; r--)
                        if (records[r].Activity == keys[i]) { last = records[r].StartedAt; break; }
                _recent[i].text = last != null ? $"최근 참여 {RecordsScreen.FormatDate(last)}" : "";
            }
        }
    }

    /// 영상 화면 공통 틀 (SCR-006 · 011 · 016 · 021 · 시안 좌표) — 영상 자산 도입 전 자리.
    /// 제목 간판 그림 · 영상 창 x420 y222 1082×608 · 자막 자리(창 내부 하단 · 52px) · 재생 진행 표시 x458 y865 ·
    /// 자동 진행 안내 · 건너뛰기 x1581 y216(영상 밖 우측 상단 · dwell 3초 · 2-4 확정 예외).
    /// 다시 듣기·이야기 흐름 표시·진행 단계 UI를 두지 않는다(확정). TTS 허용 구간 — 음성 도입 시.
    public class VideoPlaceholderScreen : ScreenBase
    {
        const float AutoSeconds = 5f; // 영상 자리 재생 시간 — 실제 영상 길이로 대체된다

        string _titleSprite = "SCR-006-Title";
        Rect _titleRect = new Rect(805, 88, 310, 65);
        ScreenId _next = ScreenId.SCR_007;
        Image _progressFill;

        public VideoPlaceholderScreen Setup(string titleSprite, Rect titleRect, ScreenId next)
        {
            _titleSprite = titleSprite;
            _titleRect = titleRect;
            _next = next;
            return this;
        }

        protected override void BuildUi()
        {
            UiKit.Background(transform, "SCR-006-Video-Bg");
            UiKit.ImgFit(transform, "Title", _titleSprite, _titleRect.x, _titleRect.y, _titleRect.width, _titleRect.height);

            // 영상 창 — 흰 둥근 테 + 어두운 화면 (영상 도입 시 VideoPlayer RawImage로 교체)
            var viewport = UiKit.Node(transform, "VideoViewport", 420, 222, 1082, 608);
            UiKit.Img(viewport, "RoundedShape", "Box-Round-38", 0, 0, 1082, 608, Color.white, sliced: true);
            UiKit.Img(viewport, "VideoImage", "Box-Round-38", 20, 20, 1042, 568, Skin.Hex("2d2926"), sliced: true);
            UiKit.Txt(viewport, "Placeholder", 0, 0, 1082, 608, "비디오", 32, 7, Color.white);
            // 자막 — 창 내부 하단 오버레이 · 52px (대본 확정 시 채움)
            UiKit.Txt(viewport, "Subtitle", 60, 430, 962, 140, "", 52, 6, Color.white, wrap: true);

            // 재생 진행 표시 (숫자 없음 · 시간 압박 요소 아님)
            _progressFill = UiKit.Bar(transform, "PlaybackProgress", 458, 865, 1004, 27, Skin.Track, Skin.Teal);

            var skip = UiKit.NavButton(transform, "SkipButton", 1581, 216, "건너뛰기", "Icon-Skip", () => Flow.Go(_next));
            skip.gameObject.AddComponent<SafeAreaExempt>().Reason = "영상 건너뛰기 — 영상 밖 우측 상단 (2-4 확정 예외)";

            UiKit.Footer(transform, "잠시 후 다음 이야기가 이어져요", 712, 979, 496, 56);
        }

        protected override void OnEnter() => StartCoroutine(AutoNext());

        protected override void OnExit() => StopAllCoroutines();

        IEnumerator AutoNext()
        {
            float start = Time.time;
            while (Time.time - start < AutoSeconds)
            {
                UiKit.SetBar(_progressFill, (Time.time - start) / AutoSeconds);
                yield return null;
            }
            Flow.Go(_next);
        }
    }
}
