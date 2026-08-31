using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Tracking;

namespace Shinmyeong.Flow.Screens
{
    /// SCR-002 사용자 감지·보정 (설계서 p6 확정) — 위치 안내 + 개인 기준값 보정을 한 화면에 통합.
    /// 요소: 안내 헤더 · 실시간 카메라 영상 + 전신 정렬 가이드(사각형+스켈레톤) · 가변 보정 메시지 ·
    ///   권장 거리 안내 · 4단계 체크리스트(쉬운 말 · 기술 용어 금지) · 완료 시 SCR-003 자동 이동(버튼 없음) ·
    ///   처음으로(dwell 3초 — 입력 방식 개발 판단 ⑧ 기준안).
    /// 여기서 확보한 기준값(Calibration)이 이후 판정 영역 산출의 기준이 된다(5-8).
    public class CalibrationScreen : ScreenBase
    {
        const float HoldSeconds = 2f;     // 4단계 유지 시간 (기준안)
        const float CenterTolerance = 0.15f;

        static readonly string[] StepLabels =
        {
            "화면에 보여요",
            "머리부터 발까지 보여요",
            "가운데에 서 있어요",
            "준비 완료",
        };

        RawImage _preview;
        Text _previewFallback;
        Text _message;
        Image[] _stepDots;
        Text[] _stepTexts;
        Image _holdFill;
        readonly Image[] _skeletonDots = new Image[PoseFrame.JointCount];
        RectTransform _previewRect;

        float _holdTimer;
        // 기준값 표본 누적 (유지 구간 동안)
        int _sampleCount;
        float _sumCenterX, _sumHeadY, _sumShoulderY, _sumHipY, _sumAnkleY;

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.10f, 0.14f, 0.14f));
            UiKit.Label(transform, "Title", new Vector2(0.5f, 0.92f), new Vector2(1000, 70), 46, "잠시만 기다려 주세요");
            UiKit.Label(transform, "Sub", new Vector2(0.5f, 0.855f), new Vector2(1100, 44), 30,
                "화면 앞 발자국 위치에 편하게 서 주세요");

            // 실시간 카메라 영상 (거울 방향) — 비인터랙션 표시 요소
            var frame = UiKit.Panel(transform, "PreviewFrame", new Color(0f, 0f, 0f, 0.45f));
            frame.rectTransform.SetSizeWithAnchors(new Vector2(0.35f, 0.51f), new Vector2(660, 500));
            var previewGo = new GameObject("Preview");
            previewGo.transform.SetParent(frame.transform, false);
            _preview = previewGo.AddComponent<RawImage>();
            _preview.raycastTarget = false;
            _previewRect = previewGo.GetComponent<RectTransform>();
            _previewRect.anchorMin = new Vector2(0.02f, 0.02f);
            _previewRect.anchorMax = new Vector2(0.98f, 0.98f);
            _previewRect.sizeDelta = Vector2.zero;
            _previewFallback = UiKit.Label(frame.transform, "Fallback", new Vector2(0.5f, 0.5f), new Vector2(400, 60), 28,
                "카메라 영상");

            // 전신 정렬 가이드 — 점선 대용 프레임 (자산 도입 시 점선+실루엣으로 교체)
            var guide = new GameObject("BodyGuide");
            guide.transform.SetParent(frame.transform, false);
            var guideRect = guide.AddComponent<RectTransform>();
            guideRect.anchorMin = new Vector2(0.28f, 0.06f);
            guideRect.anchorMax = new Vector2(0.72f, 0.96f);
            guideRect.sizeDelta = Vector2.zero;
            BuildBorder(guideRect, new Color(1f, 0.9f, 0.4f, 0.55f));

            // 스켈레톤 점 풀
            for (int i = 0; i < PoseFrame.JointCount; i++)
            {
                var dotGo = new GameObject($"Joint_{i}");
                dotGo.transform.SetParent(frame.transform, false);
                var dot = dotGo.AddComponent<Image>();
                dot.raycastTarget = false;
                dot.color = new Color(0.4f, 1f, 0.6f, 0.9f);
                var rect = dotGo.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(14, 14);
                dotGo.SetActive(false);
                _skeletonDots[i] = dot;
            }

            // 가변 보정 메시지 — 프리뷰 아래
            _message = UiKit.Label(transform, "Message", new Vector2(0.35f, 0.20f), new Vector2(700, 56), 34, "");
            _message.color = new Color(1f, 0.93f, 0.6f);

            // 우측: 4단계 체크리스트 (완료 = 초록 체크 / 대기 = 회색)
            _stepDots = new Image[StepLabels.Length];
            _stepTexts = new Text[StepLabels.Length];
            for (int i = 0; i < StepLabels.Length; i++)
            {
                var row = UiKit.Panel(transform, $"Step_{i}", new Color(1f, 1f, 1f, 0.07f));
                row.rectTransform.SetSizeWithAnchors(new Vector2(0.71f, 0.655f - 0.095f * i), new Vector2(430, 78));
                var dot = UiKit.Panel(row.transform, "Dot", new Color(0.45f, 0.45f, 0.45f));
                dot.rectTransform.SetSizeWithAnchors(new Vector2(0.1f, 0.5f), new Vector2(40, 40));
                _stepDots[i] = dot;
                var label = UiKit.Label(row.transform, "Label", new Vector2(0.58f, 0.5f), new Vector2(330, 60), 30, StepLabels[i]);
                label.alignment = TextAnchor.MiddleLeft;
                _stepTexts[i] = label;
            }

            // 4단계 유지 게이지 (숫자 없음)
            var gaugeBg = UiKit.Panel(transform, "HoldBg", new Color(1f, 1f, 1f, 0.12f));
            gaugeBg.rectTransform.SetSizeWithAnchors(new Vector2(0.71f, 0.255f), new Vector2(430, 14));
            var fill = UiKit.Panel(gaugeBg.transform, "Fill", new Color(0.4f, 0.85f, 0.5f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            _holdFill = fill;

            // 권장 거리 안내 + 자동 이동 안내
            UiKit.Label(transform, "Distance", new Vector2(0.71f, 0.19f), new Vector2(500, 40), 26,
                "화면에서 세 걸음(약 3m) 떨어져 주세요");
            UiKit.Label(transform, "AutoInfo", new Vector2(0.5f, 0.075f), new Vector2(700, 36), 26,
                "준비가 끝나면 저절로 넘어가요");

            // 처음으로 — dwell 3초 (입력 방식 개발 판단 ⑧: UI 버튼 공통 규칙과 통일 · 기준안)
            UiKit.Button(transform, "Home", new Vector2(0.28f, 0.145f), new Vector2(220, 90), "처음으로",
                () => Flow.Go(ScreenId.SCR_001));
        }

        static void BuildBorder(RectTransform parent, Color color)
        {
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"Border_{i}");
                go.transform.SetParent(parent, false);
                var img = go.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = false;
                var rect = go.GetComponent<RectTransform>();
                bool horizontal = i < 2;
                rect.anchorMin = new Vector2(0f, horizontal ? (i == 0 ? 0f : 1f) : 0f);
                rect.anchorMax = new Vector2(horizontal ? 1f : (i == 2 ? 0f : 1f), horizontal ? rect.anchorMin.y : 1f);
                if (!horizontal)
                    rect.anchorMin = new Vector2(i == 2 ? 0f : 1f, 0f);
                rect.sizeDelta = horizontal ? new Vector2(0, 4) : new Vector2(4, 0);
            }
        }

        protected override void OnEnter()
        {
            _holdTimer = 0f;
            _sampleCount = 0;
            _sumCenterX = _sumHeadY = _sumShoulderY = _sumHipY = _sumAnkleY = 0f;
            Calibration.Clear(); // 새 사용자·재진입 시 다시 잰다
        }

        void Update()
        {
            var svc = BodyTrackingService.Instance;
            bool mock = svc != null && svc.ActiveProvider is MockPoseProvider;

            UpdatePreview(svc);

            // 단계 판정 — 체크리스트는 상태를 쉬운 말로 (기술 용어 금지 · 설계서)
            bool present = svc != null && svc.PersonPresent;
            bool fullBody = present && (mock ? svc.BodyValid : FullBodyVisible(svc));
            bool centered = fullBody && svc.BodyValid && Mathf.Abs(svc.BodyCenterX01 - 0.5f) <= CenterTolerance;

            if (centered)
            {
                _holdTimer += Time.deltaTime;
                AccumulateSample(svc, mock);
            }
            else
            {
                _holdTimer = 0f;
                _sampleCount = 0;
                _sumCenterX = _sumHeadY = _sumShoulderY = _sumHipY = _sumAnkleY = 0f;
            }
            bool done = _holdTimer >= HoldSeconds;

            SetStep(0, present);
            SetStep(1, fullBody);
            SetStep(2, centered);
            SetStep(3, done);
            _holdFill.fillAmount = Mathf.Clamp01(_holdTimer / HoldSeconds);

            _message.text = !present ? "화면 앞 발자국 위치에 서 주세요"
                : !fullBody ? "한 걸음 뒤로 — 머리부터 발까지 화면에 들어오게 서 주세요"
                : !centered ? (svc.BodyCenterX01 > 0.5f ? "조금 왼쪽으로 와 주세요" : "조금 오른쪽으로 와 주세요")
                : "좋아요, 그대로 잠깐 계세요";

            if (done)
            {
                CommitCalibration();
                Flow.Go(ScreenId.SCR_003);
            }
        }

        static bool FullBodyVisible(BodyTrackingService svc)
        {
            var frame = svc.LatestFrame;
            if (frame == null || !frame.Valid)
                return false;
            bool head = frame.Get(PoseJoint.Nose).Score >= 0.3f;
            bool shoulders = frame.Get(PoseJoint.LeftShoulder).Score >= 0.3f
                && frame.Get(PoseJoint.RightShoulder).Score >= 0.3f;
            // 발목은 신뢰도가 낮게 나오는 관절이라 문턱을 낮추고 한쪽만 있어도 인정 (기준안)
            bool ankle = frame.Get(PoseJoint.LeftAnkle).Score >= 0.25f
                || frame.Get(PoseJoint.RightAnkle).Score >= 0.25f;
            return head && shoulders && ankle && svc.BodyValid;
        }

        void AccumulateSample(BodyTrackingService svc, bool mock)
        {
            _sumCenterX += svc.BodyCenterX01;
            if (mock)
            {
                _sampleCount++;
                return;
            }
            var frame = svc.LatestFrame;
            float Y(PoseJoint a, PoseJoint b)
            {
                var ka = frame.Get(a);
                var kb = frame.Get(b);
                float sum = 0f;
                int n = 0;
                if (ka.Score >= 0.2f) { sum += 1f - ka.Position.y; n++; }
                if (kb.Score >= 0.2f) { sum += 1f - kb.Position.y; n++; }
                return n > 0 ? sum / n : -1f;
            }
            _sumHeadY += Mathf.Max(0f, 1f - frame.Get(PoseJoint.Nose).Position.y);
            float shoulder = Y(PoseJoint.LeftShoulder, PoseJoint.RightShoulder);
            float hip = Y(PoseJoint.LeftHip, PoseJoint.RightHip);
            float ankle = Y(PoseJoint.LeftAnkle, PoseJoint.RightAnkle);
            _sumShoulderY += shoulder >= 0f ? shoulder : 0f;
            _sumHipY += hip >= 0f ? hip : 0f;
            _sumAnkleY += ankle >= 0f ? ankle : 0f;
            _sampleCount++;
        }

        void CommitCalibration()
        {
            if (_sampleCount <= 0)
                return;
            float n = _sampleCount;
            Calibration.Set(_sumCenterX / n, _sumHeadY / n, _sumShoulderY / n, _sumHipY / n, _sumAnkleY / n);
        }

        void SetStep(int index, bool on)
        {
            _stepDots[index].color = on ? new Color(0.35f, 0.8f, 0.45f) : new Color(0.45f, 0.45f, 0.45f);
            _stepTexts[index].color = on ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        }

        void UpdatePreview(BodyTrackingService svc)
        {
            var tex = svc != null && svc.ActiveProvider != null ? svc.ActiveProvider.PreviewTexture : null;
            bool hasTex = tex != null;
            _preview.enabled = hasTex;
            _previewFallback.gameObject.SetActive(!hasTex);
            if (hasTex)
            {
                _preview.texture = tex;
                // 거울 방향 — 사용자가 오른쪽으로 움직이면 화면에서도 오른쪽으로
                _preview.uvRect = svc.Mirror ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
            }

            // 스켈레톤 점 — 전신 진입 유도 보조 (개발 표현 · 자산 도입 시 정리)
            var frame = svc != null ? svc.LatestFrame : null;
            for (int i = 0; i < PoseFrame.JointCount; i++)
            {
                bool show = hasTex && frame != null && frame.Valid && frame.Keypoints[i].Score >= 0.25f;
                _skeletonDots[i].gameObject.SetActive(show);
                if (!show)
                    continue;
                var kp = frame.Keypoints[i];
                float px = svc.Mirror ? 1f - kp.Position.x : kp.Position.x;
                var rect = _skeletonDots[i].rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(
                    Mathf.Lerp(0.02f, 0.98f, px),
                    Mathf.Lerp(0.02f, 0.98f, 1f - kp.Position.y));
                rect.anchoredPosition = Vector2.zero;
            }
        }
    }
}
