using UnityEngine;
using UnityEngine.UI;

namespace Shinmyeong.Flow.Screens
{
    /// 튜토리얼 화면 공용 템플릿 (SCR-007·012·017 · 2-5 확정: 템플릿 1개 + 데이터 3벌).
    /// 확정 좌표 — 헤더 x48 y26 1824×126 · 동영상 x110 y180 1040×580 + 재생 진행 표시 ·
    ///   단계별 설명 x1190 y180 620×580(현재 단계 강조) · 하단 버튼 개별 3종 / 스토리 2종.
    /// 실습 없음 · 건너뛰기 없음(확정 7장). 영상은 자산 대기 — 자리 표시 + 단계 강조 자동 순환
    /// (영상 도입 시 영상 타이밍과 동기화). TTS 허용 구간 — 음성 도입 시 연결.
    public class GameTutorialScreen : ScreenBase
    {
        const float StepSeconds = 3f; // 단계 강조 순환 주기 (영상 자리 기준안)

        string _gameName = "";
        string[] _steps = System.Array.Empty<string>();
        ScreenId _playScreen;

        GameObject _freeButtons;
        GameObject _storyButtons;
        Image[] _stepPanels;
        Text[] _stepTexts;
        Image _progressFill;
        float _timer;
        int _currentStep;

        public GameTutorialScreen Setup(string gameName, string[] steps, ScreenId playScreen)
        {
            _gameName = gameName;
            _steps = steps;
            _playScreen = playScreen;
            return this;
        }

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.12f, 0.14f, 0.13f));

            // 헤더 x48 y26 1824×126 (게임명 치환)
            var header = UiKit.Panel(transform, "Header", new Color(1f, 1f, 1f, 0.07f));
            header.rectTransform.SetSizeWithAnchors(new Vector2(0.5f, 0.918f), new Vector2(1824, 126));
            UiKit.Label(header.transform, "Title", new Vector2(0.5f, 0.5f), new Vector2(1200, 80), 46,
                $"{_gameName} — 이렇게 해요");

            // 튜토리얼 동영상 x110 y180 1040×580 (자리 · 자산 대기)
            var video = UiKit.Panel(transform, "VideoArea", new Color(0.17f, 0.19f, 0.18f));
            video.rectTransform.SetSizeWithAnchors(new Vector2(0.328f, 0.565f), new Vector2(1040, 580));
            UiKit.Label(video.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(600, 80), 30, "튜토리얼 영상 (자리)");

            // 재생 진행 표시 — 영상 아래
            var progressBg = UiKit.Panel(transform, "ProgressBg", new Color(1f, 1f, 1f, 0.12f));
            progressBg.rectTransform.SetSizeWithAnchors(new Vector2(0.328f, 0.275f), new Vector2(1040, 12));
            var fill = UiKit.Panel(progressBg.transform, "Fill", new Color(0.95f, 0.85f, 0.45f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            _progressFill = fill;

            // 단계별 설명 x1190 y180 620×580 — 현재 단계 강조
            var stepsPanel = UiKit.Panel(transform, "Steps", new Color(1f, 1f, 1f, 0.05f));
            stepsPanel.rectTransform.SetSizeWithAnchors(new Vector2(0.781f, 0.565f), new Vector2(620, 580));
            _stepPanels = new Image[_steps.Length];
            _stepTexts = new Text[_steps.Length];
            for (int i = 0; i < _steps.Length; i++)
            {
                var row = UiKit.Panel(stepsPanel.transform, $"Step_{i}", new Color(1f, 1f, 1f, 0.06f));
                var rect = row.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f - (i + 0.5f) / _steps.Length);
                rect.sizeDelta = new Vector2(580, 560f / _steps.Length - 14f);
                _stepPanels[i] = row;
                var label = UiKit.Label(row.transform, "Label", new Vector2(0.54f, 0.5f), new Vector2(480, 100), 29,
                    $"{i + 1}. {_steps[i]}");
                label.alignment = TextAnchor.MiddleLeft;
                _stepTexts[i] = label;
            }

            // 하단 버튼 — 개별 모드 3종
            _freeButtons = new GameObject("FreeButtons");
            _freeButtons.transform.SetParent(transform, false);
            UiKit.Stretch(_freeButtons);
            UiKit.Button(_freeButtons.transform, "Back", new Vector2(0.28f, 0.145f), new Vector2(220, 90), "이전으로",
                () => Flow.Go(ScreenId.SCR_005));
            UiKit.Button(_freeButtons.transform, "Start", new Vector2(0.5f, 0.145f), new Vector2(280, 100), "시작하기",
                () => Flow.Go(_playScreen), color: new Color(0.3f, 0.6f, 0.35f));
            UiKit.Button(_freeButtons.transform, "ChangeUser", new Vector2(0.71f, 0.145f), new Vector2(240, 90), "사용자 변경",
                () => Flow.Go(ScreenId.SCR_003));

            // 하단 버튼 — 스토리 모드 2종 (나가기 = SCR-004 복귀)
            _storyButtons = new GameObject("StoryButtons");
            _storyButtons.transform.SetParent(transform, false);
            UiKit.Stretch(_storyButtons);
            UiKit.Button(_storyButtons.transform, "Exit", new Vector2(0.35f, 0.145f), new Vector2(220, 90), "나가기",
                () => Flow.Go(ScreenId.SCR_004));
            UiKit.Button(_storyButtons.transform, "Start", new Vector2(0.62f, 0.145f), new Vector2(280, 100), "시작하기",
                () => Flow.Go(_playScreen), color: new Color(0.3f, 0.6f, 0.35f));
        }

        protected override void OnEnter()
        {
            bool story = Flow.Mode == GameMode.Story;
            _freeButtons.SetActive(!story);
            _storyButtons.SetActive(story);
            _timer = 0f;
            _currentStep = 0;
            ApplyHighlight();
        }

        void Update()
        {
            // 영상 자리: 단계 강조를 자동 순환하며 진행 막대를 채운다 (영상 도입 시 영상 타이밍과 동기화)
            _timer += Time.deltaTime;
            if (_timer >= StepSeconds)
            {
                _timer = 0f;
                _currentStep = (_currentStep + 1) % _steps.Length;
                ApplyHighlight();
            }
            if (_progressFill != null)
                _progressFill.fillAmount = (_currentStep + Mathf.Clamp01(_timer / StepSeconds)) / _steps.Length;
        }

        void ApplyHighlight()
        {
            for (int i = 0; i < _stepPanels.Length; i++)
            {
                bool current = i == _currentStep;
                _stepPanels[i].color = current ? new Color(0.95f, 0.85f, 0.45f, 0.25f) : new Color(1f, 1f, 1f, 0.06f);
                _stepTexts[i].color = current ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            }
        }
    }
}
