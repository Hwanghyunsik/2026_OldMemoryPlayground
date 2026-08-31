using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;
using Shinmyeong.Tracking;

namespace Shinmyeong.Flow.Screens
{
    /// SCR-001 대기 · 타이틀 — 조작 요소 없음 · 사람 감지 시 자동 진입
    public class IdleScreen : ScreenBase
    {
        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.10f, 0.12f, 0.16f));
            UiKit.Label(transform, "Title", new Vector2(0.5f, 0.62f), new Vector2(1200, 120), 80, "신명놀이터 (가제)");
            UiKit.Label(transform, "Guide", new Vector2(0.5f, 0.42f), new Vector2(1000, 60), 36, "화면 앞 3m 발자국 위치에 서 주세요");
        }

        void Update()
        {
            var svc = BodyTrackingService.Instance;
            if (svc != null && svc.PersonPresent)
                Flow.Go(ScreenId.SCR_002);
        }
    }

    // SCR-002 사용자 감지·보정은 CalibrationScreen.cs (정식 구현 · 2026-08-31)

    /// SCR-003 사용자 선택 — 저장소의 등록 사용자 카드 + 비회원.
    /// 사용자 등록·수정은 관리자 화면(FN-20 · 타 팀 웹) 몫 — 여기서는 선택만.
    /// 페이징(2-5 확정): 쪽당 8명 · 그리드 바깥 좌우 세로 버튼 + 현재 쪽 표시 · 그리드 안에 이동 카드 금지 ·
    /// 첫/마지막 쪽에서는 해당 방향 버튼 자체를 표시하지 않는다(선택 불가 요소는 화면에서 뺀다 · 8-5 원칙).
    public class UserSelectScreen : ScreenBase
    {
        const int UsersPerPage = 8;
        // 아바타 8종 자리 — 카드 배경색으로만 구분 (플레이스홀더 · SCR-023 헤더 아바타도 같은 색을 쓴다)
        internal static readonly Color[] CardColors =
        {
            new Color(0.25f, 0.5f, 0.45f), new Color(0.5f, 0.4f, 0.25f),
            new Color(0.3f, 0.4f, 0.55f), new Color(0.5f, 0.3f, 0.4f),
            new Color(0.35f, 0.5f, 0.3f), new Color(0.45f, 0.35f, 0.5f),
            new Color(0.5f, 0.45f, 0.3f), new Color(0.3f, 0.45f, 0.5f),
        };

        GameObject _cardsRoot;
        Text _emptyText;
        int _page;

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.12f, 0.11f, 0.16f));
            UiKit.Label(transform, "Header", new Vector2(0.5f, 0.72f), new Vector2(900, 80), 48, "누구신가요?");
            _emptyText = UiKit.Label(transform, "Empty", new Vector2(0.5f, 0.5f), new Vector2(900, 100), 30,
                "등록된 분이 아직 없어요\n(등록은 관리자 화면에서 합니다)");

            UiKit.Button(transform, "Guest", new Vector2(0.5f, 0.24f), new Vector2(380, 100),
                "처음이신가요? 이름 없이 시작", () =>
                {
                    Flow.IsGuest = true;
                    Flow.UserId = "";
                    Flow.UserName = "";
                    Flow.Go(ScreenId.SCR_004);
                }, color: new Color(0.35f, 0.35f, 0.4f));

            UiKit.Button(transform, "Home", new Vector2(0.28f, 0.145f), new Vector2(220, 90), "처음으로",
                () => Flow.Go(ScreenId.SCR_001));

            UiKit.Label(transform, "Hint", new Vector2(0.5f, 0.85f), new Vector2(900, 40), 26, "손을 3초간 올려두면 선택됩니다");

#if UNITY_EDITOR
            // 에디터 전용: 관리자 웹(타 팀) 연동 전 저장 검증용 사용자 등록
            var devAdd = UiKit.Button(transform, "DevAddUser", new Vector2(0.9f, 0.13f), new Vector2(200, 70),
                "+ 테스트 사용자", () =>
                {
                    int n = Save.SaveStore.LoadUsers().Users.Count + 1;
                    Save.SaveStore.AddUser($"테스트{n}", n % 2 == 0 ? "남" : "여", (n - 1) % 8, (n - 1) % 8);
                    RebuildCards();
                }, dwellSeconds: 1f, color: new Color(0.3f, 0.3f, 0.3f));
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
            _cardsRoot = new GameObject("Cards");
            _cardsRoot.transform.SetParent(transform, false);
            UiKit.Stretch(_cardsRoot);

            var users = Save.SaveStore.LoadUsers().Users;
            _emptyText.gameObject.SetActive(users.Count == 0);

            int pages = Mathf.Max(1, Mathf.CeilToInt(users.Count / (float)UsersPerPage));
            _page = Mathf.Clamp(_page, 0, pages - 1);
            bool paged = pages > 1;

            // 한 줄 최대 4장 · 두 줄까지. 페이징 시에는 좌우 세로 버튼 자리를 위해 간격·카드 폭을 줄인다
            // (버튼·카드 모두 안전 영역 x420~1500 안)
            int start = _page * UsersPerPage;
            int count = Mathf.Min(users.Count - start, UsersPerPage);
            float spacing = paged ? 0.115f : 0.14f;
            for (int i = 0; i < count; i++)
            {
                var user = users[start + i];
                int row = i / 4;
                int colsInRow = row == 0 ? Mathf.Min(count, 4) : count - 4;
                float x = 0.5f + (i % 4 - (colsInRow - 1) * 0.5f) * spacing;
                float y = count <= 4 ? 0.5f : row == 0 ? 0.57f : 0.4f;
                var size = count <= 4
                    ? (paged ? new Vector2(210, 280) : new Vector2(240, 280))
                    : (paged ? new Vector2(210, 140) : new Vector2(230, 140));
                var color = CardColors[Mathf.Abs(user.CardColorIndex) % CardColors.Length];
                var button = UiKit.Button(_cardsRoot.transform, $"User_{start + i}", new Vector2(x, y), size, user.Name, () =>
                {
                    Flow.IsGuest = false;
                    Flow.UserId = user.Id;
                    Flow.UserName = user.Name;
                    Flow.Go(ScreenId.SCR_004);
                }, color: color);

                // 아바타 그림이 있으면 카드 상단(세로형)/좌측(가로형)에 얹고 이름을 비켜 배치 (Docs/92)
                var avatar = UI.ArtCatalog.Get(UI.ArtCatalog.Avatar, $"아바타{user.AvatarIndex + 1}");
                if (avatar != null)
                {
                    bool tall = size.y > size.x * 0.8f;
                    var avatarGo = new GameObject("Avatar");
                    avatarGo.transform.SetParent(button.transform, false);
                    var img = avatarGo.AddComponent<Image>();
                    img.sprite = avatar;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    var avatarRect = avatarGo.GetComponent<RectTransform>();
                    avatarRect.anchorMin = avatarRect.anchorMax = tall ? new Vector2(0.5f, 0.62f) : new Vector2(0.27f, 0.5f);
                    avatarRect.sizeDelta = tall ? new Vector2(150, 150) : new Vector2(100, 100);
                    var labelRect = (RectTransform)button.transform.Find("Label");
                    labelRect.anchorMin = labelRect.anchorMax = tall ? new Vector2(0.5f, 0.18f) : new Vector2(0.7f, 0.5f);
                }
            }

            if (!paged)
                return;

            // 목록 이동 — 그리드 바깥 좌우 세로 버튼 + 현재 쪽 표시 (2-5 확정 · 그리드 안에 이동 카드 금지)
            if (_page > 0)
                UiKit.Button(_cardsRoot.transform, "PrevPage", new Vector2(0.245f, 0.5f), new Vector2(90, 260), "◀",
                    () => { _page--; RebuildCards(); });
            if (_page < pages - 1)
                UiKit.Button(_cardsRoot.transform, "NextPage", new Vector2(0.755f, 0.5f), new Vector2(90, 260), "▶",
                    () => { _page++; RebuildCards(); });
            UiKit.Label(_cardsRoot.transform, "PageInfo", new Vector2(0.5f, 0.315f), new Vector2(220, 40), 28,
                $"{_page + 1} / {pages}");
        }
    }

    /// SCR-004 모드 선택 — 스토리 / 개별 분기 · 비회원은 헤더에 이름 미표시(확정)
    public class ModeSelectScreen : ScreenBase
    {
        Text _header;

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.14f, 0.12f, 0.12f));
            _header = UiKit.Label(transform, "Header", new Vector2(0.5f, 0.78f), new Vector2(900, 60), 40, "");

            UiKit.Button(transform, "Story", new Vector2(0.40f, 0.5f), new Vector2(300, 340),
                "스토리 게임\n(수확→장보기→요리)", () =>
                {
                    Flow.Mode = GameMode.Story;
                    Flow.Go(ScreenId.SCR_006);
                }, color: new Color(0.6f, 0.42f, 0.25f));

            UiKit.Button(transform, "Free", new Vector2(0.60f, 0.5f), new Vector2(300, 340),
                "개별 게임\n(하나만 골라서)", () =>
                {
                    Flow.Mode = GameMode.Free;
                    Flow.Go(ScreenId.SCR_005);
                }, color: new Color(0.25f, 0.45f, 0.6f));

            UiKit.Button(transform, "Home", new Vector2(0.28f, 0.145f), new Vector2(220, 90), "처음으로",
                () => Flow.Go(ScreenId.SCR_001));
            UiKit.Button(transform, "ChangeUser", new Vector2(0.71f, 0.145f), new Vector2(260, 90), "사용자 변경",
                () => Flow.Go(ScreenId.SCR_003));
            UiKit.Label(transform, "Hint", new Vector2(0.5f, 0.87f), new Vector2(900, 40), 26, "손을 3초간 올려두면 선택됩니다");
        }

        protected override void OnEnter()
        {
            if (_header != null)
                _header.text = Flow.IsGuest ? "무엇을 해볼까요?" : $"{Flow.UserName} 님, 무엇을 해볼까요?";
        }
    }

    /// SCR-005 콘텐츠 로비 (개별 모드 전용) — 게임 3종 + 내 기록 보기(비회원 미표시)
    public class LobbyScreen : ScreenBase
    {
        GameObject _recordsButton;

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.11f, 0.14f, 0.12f));
            UiKit.Label(transform, "Header", new Vector2(0.5f, 0.76f), new Vector2(900, 70), 44, "무엇을 해볼까요?");

            UiKit.Button(transform, "Harvest", new Vector2(0.35f, 0.5f), new Vector2(250, 300),
                "수확하기\n(팔 운동)", () => Flow.Go(ScreenId.SCR_007), color: new Color(0.35f, 0.55f, 0.3f));
            UiKit.Button(transform, "Shopping", new Vector2(0.50f, 0.5f), new Vector2(250, 300),
                "장보기\n(다리 운동)", () => Flow.Go(ScreenId.SCR_012), color: new Color(0.55f, 0.45f, 0.3f));
            UiKit.Button(transform, "Cooking", new Vector2(0.65f, 0.5f), new Vector2(250, 300),
                "요리하기\n(기억 놀이)", () => Flow.Go(ScreenId.SCR_017), color: new Color(0.5f, 0.35f, 0.45f));

            UiKit.Button(transform, "Back", new Vector2(0.28f, 0.145f), new Vector2(220, 90), "이전으로",
                () => Flow.Go(ScreenId.SCR_004));
            var records = UiKit.Button(transform, "Records", new Vector2(0.71f, 0.145f), new Vector2(260, 90),
                "내 기록 보기", () => Flow.Go(ScreenId.SCR_023));
            _recordsButton = records.gameObject;
        }

        protected override void OnEnter()
        {
            // 비회원에게는 버튼 자체를 표시하지 않는다 (확정 · 인계자료 8-5)
            if (_recordsButton != null)
                _recordsButton.SetActive(!Flow.IsGuest);
        }
    }

    /// 영상 화면 공통 틀 (SCR-006 · 011 · 016 · 021 · 2-5 v2.7 확정 좌표) — 영상 자산 도입 전 자리.
    /// 영상 x320 y160 1280×720 · 자막 x380 y660(내부 하단 · 52px) · 진행 표시 x320 y902 ·
    /// 자동 진행 안내 x660 y944 · 건너뛰기 x1472 y26 400×112(영상 밖 우측 상단 · dwell 3초 · 예외).
    /// 다시 듣기·이야기 흐름 표시·진행 단계 UI를 두지 않는다(확정). TTS 허용 구간 — 음성 도입 시.
    public class VideoPlaceholderScreen : ScreenBase
    {
        const float AutoSeconds = 5f; // 영상 자리 재생 시간 — 실제 영상 길이로 대체된다

        string _title = "영상 (자리)";
        ScreenId _next = ScreenId.SCR_007;
        Image _progressFill;

        public VideoPlaceholderScreen Setup(string title, ScreenId next)
        {
            _title = title;
            _next = next;
            return this;
        }

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.08f, 0.08f, 0.1f));

            // 영상 영역 x320 y160 1280×720 — 화면 전체가 아니라 축소 배치 (조작 버튼과 겹치지 않게 · 확정)
            var video = UiKit.Panel(transform, "VideoArea", new Color(0.16f, 0.16f, 0.2f));
            video.rectTransform.SetSizeWithAnchors(new Vector2(0.5f, 0.5185f), new Vector2(1280, 720));
            UiKit.Label(video.transform, "Title", new Vector2(0.5f, 0.6f), new Vector2(900, 120), 40, _title);

            // 자막 — 영상 내부 하단 오버레이 x380 y660 1160×160 · 52px (대본 확정 시 채움)
            var subtitle = UiKit.Label(transform, "Subtitle", new Vector2(0.5f, 0.3148f), new Vector2(1160, 160), 52, "");
            subtitle.color = new Color(1f, 1f, 1f, 0.9f);

            // 진행 표시 x320 y902 1280×26 — 시간 압박 요소가 아님 (숫자 없음)
            var progressBg = UiKit.Panel(transform, "ProgressBg", new Color(1f, 1f, 1f, 0.14f));
            progressBg.rectTransform.SetSizeWithAnchors(new Vector2(0.5f, 0.1528f), new Vector2(1280, 26));
            var fill = UiKit.Panel(progressBg.transform, "Fill", new Color(0.95f, 0.85f, 0.45f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            _progressFill = fill;

            // 자동 진행 안내 x660 y944 600×66 (8-6-1 확정 문구)
            UiKit.Label(transform, "AutoInfo", new Vector2(0.5f, 0.0954f), new Vector2(600, 66), 26,
                "잠시 후 다음 이야기가 이어져요");

            // 건너뛰기 x1472 y26 400×112 — 영상 밖 우측 상단 · 안전 영역 예외 (확정)
            var skip = UiKit.Button(transform, "Skip", new Vector2(0.8708f, 0.9241f), new Vector2(400, 112), "건너뛰기",
                () => Flow.Go(_next));
            skip.gameObject.AddComponent<SafeAreaExempt>().Reason = "영상 건너뛰기 — 영상 밖 우측 상단 (2-4 확정 예외)";
        }

        protected override void OnEnter() => StartCoroutine(AutoNext());

        protected override void OnExit() => StopAllCoroutines();

        IEnumerator AutoNext()
        {
            float start = Time.time;
            while (Time.time - start < AutoSeconds)
            {
                _progressFill.fillAmount = Mathf.Clamp01((Time.time - start) / AutoSeconds);
                yield return null;
            }
            Flow.Go(_next);
        }
    }

    /// 미구현 화면 자리 — 제목 + 자동 진행 또는 버튼
    public class PlaceholderScreen : ScreenBase
    {
        string _title = "";
        float _autoSeconds;
        ScreenId? _autoNext;
        (string label, Action action)[] _buttons = Array.Empty<(string, Action)>();

        public void Configure(string title, float autoSeconds, ScreenId? autoNext, params (string, Action)[] buttons)
        {
            _title = title;
            _autoSeconds = autoSeconds;
            _autoNext = autoNext;
            _buttons = buttons;
        }

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.12f, 0.12f, 0.12f));
            UiKit.Label(transform, "Title", new Vector2(0.5f, 0.58f), new Vector2(1100, 100), 44, _title);
            for (int i = 0; i < _buttons.Length; i++)
            {
                float x = Mathf.Lerp(0.35f, 0.65f, _buttons.Length == 1 ? 0.5f : i / (float)(_buttons.Length - 1));
                var b = _buttons[i];
                UiKit.Button(transform, $"Btn_{i}", new Vector2(x, 0.2f), new Vector2(260, 95), b.label, () => b.action());
            }
        }

        protected override void OnEnter()
        {
            if (_autoNext.HasValue && _autoSeconds > 0)
                StartCoroutine(AutoNext());
        }

        protected override void OnExit() => StopAllCoroutines();

        IEnumerator AutoNext()
        {
            yield return new WaitForSeconds(_autoSeconds);
            Flow.Go(_autoNext.Value);
        }
    }

    static class RectTransformExt
    {
        public static void SetSizeWithAnchors(this RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
        }
    }
}
