using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;
using Shinmyeong.UI;

namespace Shinmyeong.Flow.Screens
{
    /// 튜토리얼 단계 데이터 — 한 줄 동작 + 보조 설명 + 삽화(선택)
    public class TutorialStep
    {
        public string Action;
        public string Explanation;
        public string Icon;

        public TutorialStep(string action, string explanation, string icon = null)
        {
            Action = action;
            Explanation = explanation;
            Icon = icon;
        }
    }

    /// 튜토리얼 화면 공용 템플릿 (SCR-007·012·017 · 2-5 확정: 템플릿 1개 + 데이터 3벌 · 시안 좌표).
    /// 헤더(게임명 간판 + 「이렇게 해 보세요」) · 튜토리얼 영상 창 x209 y290 934×620 + 재생 진행 표시 ·
    /// 순서 판 x1148 y289 563×622(현재 단계 강조) · 버튼: 개별 = 왼쪽 2단(이전으로·사용자 변경)+시작 / 스토리 = 나가기+시작.
    /// 실습 없음 · 건너뛰기 없음(확정 7장). 튜토리얼 영상(`Assets/Video/튜토리얼 영상`)을 반복 재생하고
    /// 단계 강조는 영상 진행을 단계 수로 고르게 나눠 따라간다(단계별 시점 미확정 — 기준안).
    /// 영상이 없으면 자리 표시 + 3초 자동 순환. TTS 허용 구간 — 음성 도입 시 연결.
    public class GameTutorialScreen : ScreenBase
    {
        const float StepSeconds = 3f; // 영상이 없을 때 단계 강조 순환 주기

        string _titleSprite = "SCR-007-Title";
        float _titleWidth = 192;
        string _bgSprite = "SCR-007-Bg";
        TutorialStep[] _steps = System.Array.Empty<TutorialStep>();
        string _clip;
        ScreenId _playScreen;

        GameObject _freeButtons;
        GameObject _storyButtons;
        Image[] _faces;
        Image[] _borders;
        Image[] _badges;
        Text[] _actions;
        Image _progressFill;
        Text _placeholder;
        VideoView _video;
        float _timer;
        int _currentStep;

        public GameTutorialScreen Setup(string titleSprite, float titleWidth, string bgSprite, TutorialStep[] steps, string clip, ScreenId playScreen)
        {
            _titleSprite = titleSprite;
            _titleWidth = titleWidth;
            _bgSprite = bgSprite;
            _steps = steps;
            _clip = clip;
            _playScreen = playScreen;
            return this;
        }

        protected override void BuildUi()
        {
            UiKit.Background(transform, _bgSprite);

            // 헤더 x329 y50 — 「[게임명 간판] · 이렇게 해 보세요」를 가운데 정렬
            var header = UiKit.HeaderPaper(transform, 329, 50);
            float total = _titleWidth + 24 + 12 + 24 + 300;
            float tx = (1184 - total) * 0.5f;
            UiKit.ImgFit(header, "Title", _titleSprite, tx, 48, _titleWidth, 53);
            UiKit.Img(header, "Divider", "Circle-25", tx + _titleWidth + 24, 70, 12, 12, Skin.Green);
            UiKit.Txt(header, "Instruction", tx + _titleWidth + 60, 48, 300, 55, "이렇게 해 보세요", 40, 6, Skin.Ink, TextAnchor.MiddleLeft);
            UiKit.Txt(header, "Subtitle", 0, 105, 1184, 42, "짧은 영상을 보시면 바로 아실 수 있습니다", 28, 5, Skin.Brown);

            // 튜토리얼 영상 창 — 둥근 화면(Mask) 안에 영상 반복 재생
            var video = UiKit.Frame(transform, "TutorialVideo", 209, 290, 934, 620, Skin.Paper, Skin.Outline, shadow: true);
            var mask = UiKit.Img(video, "VideoMask", "Box-Round-20", 29, 25, 875, 495, Skin.Hex("2d2926"), sliced: true);
            _placeholder = UiKit.Txt(video, "PlaceholderLabel", 29, 25, 875, 495, "튜토리얼 영상 (자리)", 32, 7, Color.white);
            _video = VideoView.Create(mask);
            _progressFill = UiKit.Bar(video, "PlaybackProgress", 29, 549, 875, 29, Skin.Track, Skin.Green, "Box-Round-38", "Box-Round-38");
            foreach (var img in video.Find("PlaybackProgress").GetComponentsInChildren<Image>())
                img.pixelsPerUnitMultiplier = 2.62f;
            UiKit.ImgFit(transform, "TutorialTitle", "SCR-007-Tutorial", 458, 211, 459, 184);

            // 순서 판 — 현재 단계 강조
            var steps = UiKit.Frame(transform, "Steps", 1148, 289, 563, 622, Skin.Paper, Skin.Outline, shadow: true);
            UiKit.ImgFit(steps, "SequenceTitle", "SCR-007-Sequence", 110, -45, 361, 113);
            int n = _steps.Length;
            bool four = n >= 4;
            float rowH = four ? 130 : 145;
            float pitch = four ? 133.7f : 160f;
            float top = four ? 67f : 120f;
            int actionSize = four ? 25 : 32;
            int explainSize = four ? 20 : 25;
            _faces = new Image[n];
            _borders = new Image[n];
            _badges = new Image[n];
            _actions = new Text[n];
            for (int i = 0; i < n; i++)
            {
                float y = top + i * pitch;
                var row = UiKit.Node(steps, $"Step{i + 1:00}", 33, y, 498, rowH);
                _faces[i] = UiKit.Img(row, "Face", "Box-Round-23", 1, 0, 495, rowH, Skin.FaceLight, sliced: true);
                _borders[i] = UiKit.Img(row, "Border", "Box-Round-23-Outline", 1, 0, 495, rowH, Skin.Hex("d5b888"), sliced: true);
                _badges[i] = UiKit.Img(row, "NumberBadge", "Circle-125", 30, rowH * 0.5f - 30, 60, 60, Skin.Amber);
                UiKit.Txt(_badges[i].transform, "Number", 0, 0, 60, 60, (i + 1).ToString(), 40, 7, Color.white);
                _actions[i] = UiKit.Txt(row, "Action", 112, rowH * 0.5f - 42, 290, 45, _steps[i].Action, actionSize, 7, Skin.Hex("302b22"), TextAnchor.MiddleLeft);
                UiKit.Txt(row, "Explanation", 112, rowH * 0.5f + 5, 290, 34, _steps[i].Explanation, explainSize, 5, Skin.Muted, TextAnchor.MiddleLeft);
                if (!string.IsNullOrEmpty(_steps[i].Icon))
                    UiKit.ImgFit(row, "Illustration", _steps[i].Icon, four ? 412 : 375, four ? 20 : 8, four ? 75 : 115, rowH - (four ? 40 : 16));
            }

            // 하단 버튼 — 개별 모드: 왼쪽 2단(이전으로 · 사용자 변경) + 시작하기
            _freeButtons = UiKit.Group(transform, "FreeButtons").gameObject;
            var stack = UiKit.ButtonStack(_freeButtons.transform, "IndividualLeftButtons", 9, 342);
            UiKit.StackButton(stack, "BackButton", 12, "이전으로", "Icon-Arrow", () => Flow.Go(ScreenId.SCR_005));
            UiKit.StackButton(stack, "ChangeUserButton", 260, "사용자 변경", "Icon-User", () => Flow.Go(ScreenId.SCR_003), labelSize: 22);
            stack.gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(좌측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";
            UiKit.NavButton(_freeButtons.transform, "StartButton", 1732, 478, "시작하기", "Icon-Play", () => Flow.Go(_playScreen), green: true)
                .gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(우측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";

            // 스토리 모드: 나가기(SCR-004 복귀) + 시작하기
            _storyButtons = UiKit.Group(transform, "StoryButtons").gameObject;
            UiKit.NavButton(_storyButtons.transform, "ExitButton", 24, 478, "나가기", "Icon-Exit", () => Flow.Go(ScreenId.SCR_004))
                .gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(좌측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";
            UiKit.NavButton(_storyButtons.transform, "StartButton", 1732, 478, "시작하기", "Icon-Play", () => Flow.Go(_playScreen), green: true)
                .gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(우측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";
        }

        protected override void OnEnter()
        {
            bool story = Flow.Mode == GameMode.Story;
            _freeButtons.SetActive(!story);
            _storyButtons.SetActive(story);
            _timer = 0f;
            _currentStep = 0;
            ApplyHighlight();
            bool playing = !string.IsNullOrEmpty(_clip) && _video.Play(_clip, loop: true);
            _placeholder.gameObject.SetActive(!playing);
        }

        protected override void OnExit() => _video.Stop();

        void Update()
        {
            int n = Mathf.Max(1, _steps.Length);
            if (_video.HasClip)
            {
                // 영상 진행을 단계 수로 나눠 현재 단계를 강조 — 반복 재생 시 처음 단계로 돌아간다
                float progress = _video.Progress;
                int step = Mathf.Min(n - 1, (int)(progress * n));
                if (step != _currentStep)
                {
                    _currentStep = step;
                    ApplyHighlight();
                }
                if (_progressFill != null)
                    UiKit.SetBar(_progressFill, progress);
                return;
            }

            // 영상 없음: 단계 강조를 자동 순환하며 진행 막대를 채운다
            _timer += Time.deltaTime;
            if (_timer >= StepSeconds)
            {
                _timer = 0f;
                _currentStep = (_currentStep + 1) % Mathf.Max(1, _steps.Length);
                ApplyHighlight();
            }
            if (_progressFill != null && _steps.Length > 0)
                UiKit.SetBar(_progressFill, (_currentStep + Mathf.Clamp01(_timer / StepSeconds)) / _steps.Length);
        }

        void ApplyHighlight()
        {
            for (int i = 0; i < _faces.Length; i++)
            {
                bool current = i == _currentStep;
                _faces[i].color = current ? Skin.FaceGreen : Skin.FaceLight;
                _borders[i].color = current ? Skin.Green : Skin.Hex("d5b888");
                _badges[i].color = current ? Skin.Green : Skin.Amber;
                _actions[i].color = current ? Skin.Green : Skin.Hex("302b22");
            }
        }
    }
}
