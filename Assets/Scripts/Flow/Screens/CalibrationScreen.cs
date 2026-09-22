using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;
using Shinmyeong.Tracking;
using Shinmyeong.UI;

namespace Shinmyeong.Flow.Screens
{
    /// SCR-002 사용자 감지·보정 (설계서 p6 확정 · 시안 SCR-002) — 위치 안내 + 개인 기준값 보정을 한 화면에 통합.
    /// 요소: 안내 헤더 · 실시간 카메라 영상 + 전신 정렬 가이드(점선 사각형+스켈레톤) · 가변 보정 메시지 ·
    ///   권장 거리 안내 · 4단계 체크리스트(쉬운 말 · 기술 용어 금지) · 유지 게이지 · 완료 시 SCR-003 자동 이동(버튼 없음) ·
    ///   처음으로(dwell 3초 — 입력 방식 개발 판단 ⑧ 기준안).
    /// 여기서 확보한 기준값(Calibration)이 이후 판정 영역 산출의 기준이 된다(5-8).
    public class CalibrationScreen : ScreenBase
    {
        const float HoldSeconds = 2f;     // 4단계 유지 시간 (기준안)
        const float CenterTolerance = 0.15f;

        static readonly string[] StepLabels =
        {
            "사람이 확인되었습니다",
            "온몸이 화면에 들어왔습니다",
            "가운데에 서 계십니다",
            "준비를 마무리하는 중입니다",
        };

        RawImage _preview;
        Text _previewFallback;
        Text _message;
        GameObject _messageBox;
        GameObject[] _stepOn;
        GameObject[] _stepOff;
        Image _holdFill;
        readonly Image[] _skeletonDots = new Image[PoseFrame.JointCount];
        RectTransform _previewRect;

        float _holdTimer;
        // 기준값 표본 누적 (유지 구간 동안)
        int _sampleCount;
        float _sumCenterX, _sumHeadY, _sumShoulderY, _sumHipY, _sumAnkleY;

        protected override void BuildUi()
        {
            UiKit.Background(transform, "SCR-002-bg");

            var header = UiKit.HeaderPaper(transform);
            UiKit.ImgFit(header, "Title", "SCR-002-Title-Text", 357, 48, 470, 50);
            var leafL = UiKit.ImgFit(header, "LeafLeft", "Icon-Leaf", 302, 53, 37, 42);
            leafL.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            leafL.rectTransform.anchoredPosition += new Vector2(37, 0);
            UiKit.ImgFit(header, "LeafRight", "Icon-Leaf", 841, 52, 36, 41);
            UiKit.Txt(header, "Subtitle", 0, 114, 1184, 28, "화면 속 사각형 안에 온 몸이 들어오도록 서 주세요.", 28, 5, Skin.Brown);

            // 큰 판
            var main = UiKit.Node(transform, "MainPanel", 244, 253, 1432, 663);
            UiKit.Img(main, "OuterRim", "Box-Round-23", 0, 0, 1432, 663, Skin.Cream, sliced: true);
            UiKit.Img(main, "Fill", "Box-Round-23", 6, 7, 1420, 650, Skin.FaceWarm, sliced: true);
            UiKit.Img(main, "Border", "Box-Round-23-Outline", 4, 5, 1424, 652, Skin.Outline2, sliced: true);

            // 카메라 영상 (거울 방향) — 비인터랙션 표시 요소
            var cam = UiKit.Node(transform, "CameraPanel", 266, 276, 695, 617);
            UiKit.Img(cam, "Fill", "Box-Round-20", 0, 0, 695, 617, Skin.Cream, sliced: true);
            UiKit.Img(cam, "Border", "Box-Round-20-Outline", 0, 0, 695, 617, Skin.Outline2, sliced: true);
            var previewGo = new GameObject("Preview");
            previewGo.transform.SetParent(cam, false);
            _preview = previewGo.AddComponent<RawImage>();
            _preview.raycastTarget = false;
            _previewRect = UiKit.Place(previewGo.GetComponent<RectTransform>(), 12, 12, 671, 593);
            _previewFallback = UiKit.Txt(cam, "CameraPlaceholder", 0, 0, 695, 617, "카메라 영상", 28, 6, Skin.Hex("182027"));
            // 전신 정렬 가이드 — 점선 사각형
            UiKit.Img(cam, "BodyGuide", "Dot-Line-4", 175, 45, 344, 527, Skin.Green, sliced: true);
            for (int i = 0; i < PoseFrame.JointCount; i++)
            {
                var dot = UiKit.Img(cam, $"Joint_{i}", "Circle-25", 0, 0, 14, 14, new Color(0.3f, 0.9f, 0.5f, 0.9f));
                dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                dot.gameObject.SetActive(false);
                _skeletonDots[i] = dot;
            }
            // 가변 보정 메시지 — 영상 아래쪽 어두운 띠
            _messageBox = UiKit.Img(cam, "MoveBackNotice", "Box-Round-20", 49, 454, 607, 123, new Color(0f, 0f, 0f, 0.9f), sliced: true).gameObject;
            _message = UiKit.Txt(_messageBox.transform, "MoveBackText", 0, 0, 607, 123, "", 28, 6, Skin.Cream);

            // 권장 거리 안내
            var dist = UiKit.Node(transform, "DistanceGuide", 983, 276, 672, 247);
            UiKit.Img(dist, "Fill", "Box-Round-20", 0, 0, 672, 247, Skin.Cream, sliced: true);
            UiKit.Img(dist, "Border", "Box-Round-20-Outline", 0, 0, 672, 247, Skin.Outline2, sliced: true);
            UiKit.Txt(dist, "Title", 34, 28, 260, 35, "권장 거리 안내", 35, 6, Skin.Brown, TextAnchor.MiddleLeft);
            UiKit.ImgFit(dist, "TV", "SCR-002-TV", 80, 91, 125, 95);
            UiKit.ImgFit(dist, "DistanceArrow", "SCR-002-Dot-Line", 237, 140, 250, 13);
            UiKit.ImgFit(dist, "Footprints", "SCR-001-Foot-icon", 515, 90, 93, 83);
            UiKit.Txt(dist, "DistanceValue", 314, 107, 100, 25, "약 3M", 25, 7, Skin.Brown, TextAnchor.MiddleLeft);
            UiKit.Txt(dist, "ScreenLabel", 124, 199, 70, 22, "화면", 22, 5, Skin.Brown, TextAnchor.MiddleLeft);
            UiKit.Txt(dist, "UserLabel", 508, 199, 130, 22, "사용자 위치", 22, 5, Skin.Brown, TextAnchor.MiddleLeft);

            // 인식·보정 상태 — 4단계 체크리스트
            var status = UiKit.Node(transform, "RecognitionStatus", 982, 533, 672, 359);
            UiKit.Img(status, "Fill", "Box-Round-20", 0, 0, 672, 359, Skin.Cream, sliced: true);
            UiKit.Img(status, "Border", "Box-Round-20-Outline", 0, 0, 672, 359, Skin.Outline2, sliced: true);
            UiKit.Txt(status, "Title", 35, 33, 295, 35, "인식·보정 상태", 35, 6, Skin.Brown, TextAnchor.MiddleLeft);
            _stepOn = new GameObject[StepLabels.Length];
            _stepOff = new GameObject[StepLabels.Length];
            for (int i = 0; i < StepLabels.Length; i++)
            {
                float y = 94 + i * 61.7f;
                var row = UiKit.Node(status, $"Step{i + 1}", 29, y, 590, 52);
                var on = UiKit.Node(row, "On", 0, 0, 53, 52);
                UiKit.Img(on, "Circle", "Circle-outline", 0, 0, 53, 52, Skin.Green);
                UiKit.ImgFit(on, "Check", "Icon-Check", 12, 13, 29, 25, Skin.Green);
                var off = UiKit.Node(row, "Off", 0, 0, 53, 52);
                UiKit.Img(off, "PendingFill", "Circle-25", 0, 0, 53, 52, Skin.Outline2);
                UiKit.Img(off, "PendingRing", "Circle-outline", 0, 0, 53, 52, Skin.Hex("6c6252"));
                UiKit.Txt(row, "Label", 73, 9, 520, 30, StepLabels[i], 30, 5, Skin.Brown, TextAnchor.MiddleLeft);
                _stepOn[i] = on.gameObject;
                _stepOff[i] = off.gameObject;
            }
            // 4단계 유지 게이지 (숫자 없음)
            _holdFill = UiKit.Bar(status, "HoldGauge", 35, 330, 600, 12, Skin.Track3, Skin.Green, "TimeBar", "TimeBar");

            UiKit.NavButton(transform, "HomeButton", 44, 477, "처음으로", "Icon-Home", () => Flow.Go(ScreenId.SCR_001))
                .gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(좌측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";
            UiKit.Footer(transform, "준비가 끝나면 자동으로 넘어갑니다", 699, 970, 496, 54, 25);
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
            UiKit.SetBar(_holdFill, Mathf.Clamp01(_holdTimer / HoldSeconds));

            _message.text = !present ? "화면 앞 발자국 위치에 서 주세요."
                : !fullBody ? "조금만 뒤로 물러나 주세요."
                : !centered ? (svc.BodyCenterX01 > 0.5f ? "조금 왼쪽으로 와 주세요." : "조금 오른쪽으로 와 주세요.")
                : "좋아요, 그대로 잠깐 계세요.";

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
            if (_stepOn[index].activeSelf != on)
                _stepOn[index].SetActive(on);
            if (_stepOff[index].activeSelf == on)
                _stepOff[index].SetActive(!on);
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

            // 스켈레톤 점 — 전신 진입 유도 보조
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
                rect.anchoredPosition = new Vector2(12f + 671f * px, -(12f + 593f * kp.Position.y));
            }
        }
    }
}
