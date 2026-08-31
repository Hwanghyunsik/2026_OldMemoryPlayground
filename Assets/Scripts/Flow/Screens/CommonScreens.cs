using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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

    /// SCR-002 사용자 감지 · 보정 — 전신 확인 후 자동 진입 (체크리스트는 플레이스홀더)
    public class CalibrationScreen : ScreenBase
    {
        const float HoldSeconds = 2f;

        Text _status;
        float _timer;

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.10f, 0.14f, 0.14f));
            UiKit.Label(transform, "Title", new Vector2(0.5f, 0.68f), new Vector2(1000, 80), 52, "잠시만 기다려 주세요");
            _status = UiKit.Label(transform, "Status", new Vector2(0.5f, 0.45f), new Vector2(1000, 120), 34, "");
        }

        protected override void OnEnter() => _timer = 0f;

        void Update()
        {
            var svc = BodyTrackingService.Instance;
            bool ok = svc != null && svc.PersonPresent && svc.BodyValid;
            _timer = ok ? _timer + Time.deltaTime : 0f;
            if (_status != null)
                _status.text = ok
                    ? $"몸이 잘 보여요 ({Mathf.CeilToInt(HoldSeconds - _timer)})"
                    : "전신이 화면에 들어오도록 서 주세요";
            if (_timer >= HoldSeconds)
                Flow.Go(ScreenId.SCR_003);
        }
    }

    /// SCR-003 사용자 선택 — 저장소의 등록 사용자 카드 + 비회원.
    /// 사용자 등록·수정은 관리자 화면(FN-20) 몫 — 여기서는 선택만. 카드 아바타·페이징은 정식 구현에서.
    public class UserSelectScreen : ScreenBase
    {
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

            UiKit.Button(transform, "Home", new Vector2(0.26f, 0.13f), new Vector2(220, 90), "처음으로",
                () => Flow.Go(ScreenId.SCR_001));

            UiKit.Label(transform, "Hint", new Vector2(0.5f, 0.85f), new Vector2(900, 40), 26, "손을 3초간 올려두면 선택됩니다");

#if UNITY_EDITOR
            // 에디터 전용: 관리자 화면(FN-20) 구현 전 저장 검증용 사용자 등록
            UiKit.Button(transform, "DevAddUser", new Vector2(0.9f, 0.13f), new Vector2(200, 70),
                "+ 테스트 사용자", () =>
                {
                    int n = Save.SaveStore.LoadUsers().Users.Count + 1;
                    Save.SaveStore.AddUser($"테스트{n}", n % 2 == 0 ? "남" : "여", (n - 1) % 8, (n - 1) % 8);
                    RebuildCards();
                }, dwellSeconds: 1f, color: new Color(0.3f, 0.3f, 0.3f));
#endif
        }

        protected override void OnEnter() => RebuildCards();

        void RebuildCards()
        {
            if (_cardsRoot != null)
                Destroy(_cardsRoot);
            _cardsRoot = new GameObject("Cards");
            _cardsRoot.transform.SetParent(transform, false);
            UiKit.Stretch(_cardsRoot);

            var users = Save.SaveStore.LoadUsers().Users;
            _emptyText.gameObject.SetActive(users.Count == 0);

            // 한 줄 최대 4장 · 두 줄까지 (8명 초과 표시는 페이징과 함께 정식 구현에서)
            int count = Mathf.Min(users.Count, 8);
            for (int i = 0; i < count; i++)
            {
                var user = users[i];
                int row = i / 4;
                int colsInRow = row == 0 ? Mathf.Min(count, 4) : count - 4;
                float x = 0.5f + (i % 4 - (colsInRow - 1) * 0.5f) * 0.15f;
                float y = count <= 4 ? 0.5f : row == 0 ? 0.57f : 0.4f;
                var size = count <= 4 ? new Vector2(240, 280) : new Vector2(230, 140);
                var color = CardColors[Mathf.Abs(user.CardColorIndex) % CardColors.Length];
                UiKit.Button(_cardsRoot.transform, $"User_{i}", new Vector2(x, y), size, user.Name, () =>
                {
                    Flow.IsGuest = false;
                    Flow.UserId = user.Id;
                    Flow.UserName = user.Name;
                    Flow.Go(ScreenId.SCR_004);
                }, color: color);
            }
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

            UiKit.Button(transform, "Home", new Vector2(0.26f, 0.13f), new Vector2(220, 90), "처음으로",
                () => Flow.Go(ScreenId.SCR_001));
            UiKit.Button(transform, "ChangeUser", new Vector2(0.74f, 0.13f), new Vector2(260, 90), "사용자 변경",
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

            UiKit.Button(transform, "Back", new Vector2(0.26f, 0.13f), new Vector2(220, 90), "이전으로",
                () => Flow.Go(ScreenId.SCR_004));
            var records = UiKit.Button(transform, "Records", new Vector2(0.74f, 0.13f), new Vector2(260, 90),
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

    /// 영상 화면 자리 (SCR-006 · 011 · 016 · 021 공통 틀) — 영상 자산 도입 전: 자동 진행 + 건너뛰기만 구현
    public class VideoPlaceholderScreen : ScreenBase
    {
        const float AutoSeconds = 5f;

        string _title = "영상 (자리)";
        ScreenId _next = ScreenId.SCR_007;

        public VideoPlaceholderScreen Setup(string title, ScreenId next)
        {
            _title = title;
            _next = next;
            return this;
        }

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.08f, 0.08f, 0.1f));
            UiKit.Panel(transform, "VideoArea", new Color(0.16f, 0.16f, 0.2f))
                .rectTransform.SetSizeWithAnchors(new Vector2(0.5f, 0.55f), new Vector2(1280, 720));
            UiKit.Label(transform, "Title", new Vector2(0.5f, 0.55f), new Vector2(900, 80), 40, _title);
            // 건너뛰기 — 영상 영역 밖 우측 상단 · 안전 영역 예외 (확정)
            UiKit.Button(transform, "Skip", new Vector2(0.88f, 0.92f), new Vector2(220, 80), "건너뛰기",
                () => Flow.Go(_next));
        }

        protected override void OnEnter() => StartCoroutine(AutoNext());

        protected override void OnExit() => StopAllCoroutines();

        IEnumerator AutoNext()
        {
            yield return new WaitForSeconds(AutoSeconds);
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
