using UnityEngine;
using UnityEngine.InputSystem;

namespace Shinmyeong.Tracking
{
    /// 트래킹 상태를 화면에 그리는 개발용 오버레이. F1로 토글.
    public class TrackingDebugView : MonoBehaviour
    {
        [SerializeField] BodyTrackingService _service;
        [SerializeField] bool _visible = false; // 기본 숨김 — F1로 켠다 (2026-09-22)
        [SerializeField] float _previewHeight = 240f;

        static readonly Color HandColor = new Color(1f, 0.85f, 0.1f);
        static readonly Color BodyColor = new Color(0.2f, 0.9f, 1f);

        void Awake()
        {
            if (_service == null)
                _service = GetComponent<BodyTrackingService>();
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                _visible = !_visible;
        }

        void OnGUI()
        {
            if (!_visible || _service == null)
                return;

            DrawPreview();
            DrawBodyCenterLine();
            DrawStatusText();
        }

        void DrawPreview()
        {
            var provider = _service.ActiveProvider;
            var tex = provider?.PreviewTexture;
            if (tex == null)
                return;

            float aspect = (float)tex.width / tex.height;
            var rect = new Rect(10, 10, _previewHeight * aspect, _previewHeight);
            // 미러 모드면 미리보기도 좌우 반전해 거울처럼 보이게
            var coords = _service.Mirror ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1);
            GUI.DrawTextureWithTexCoords(rect, tex, coords);

            // 커서 리치 존 — 이 사각형 안의 손목 움직임이 화면 전체로 매핑된다 (튜닝용)
            if (_service.HandZoneEnabled && _service.ActiveProvider is MoveNetPoseProvider)
            {
                var zMin = _service.HandZoneMin; // 뷰포트(y 위로) → 미리보기(y 아래로) 변환
                var zMax = _service.HandZoneMax;
                float zx = rect.x + zMin.x * rect.width;
                float zy = rect.y + (1f - zMax.y) * rect.height;
                float zw = (zMax.x - zMin.x) * rect.width;
                float zh = (zMax.y - zMin.y) * rect.height;
                DrawRectBorder(new Rect(zx, zy, zw, zh), 2f, HandColor);
            }

            var frame = _service.LatestFrame;
            if (frame.Valid)
            {
                for (int i = 0; i < PoseFrame.JointCount; i++)
                {
                    var kp = frame.Keypoints[i];
                    bool isWrist = i == (int)PoseJoint.LeftWrist || i == (int)PoseJoint.RightWrist;
                    // 손목은 유지 문턱(0.2)까지 표시 — score 0.2~0.3 구간(히스테리시스 유지 샘플)은 흐리게
                    float drawMin = isWrist ? 0.2f : 0.3f;
                    if (kp.Score < drawMin)
                        continue;
                    float px = _service.Mirror ? 1f - kp.Position.x : kp.Position.x;
                    var p = new Vector2(rect.x + px * rect.width, rect.y + kp.Position.y * rect.height);
                    var color = isWrist ? HandColor : Color.green;
                    if (kp.Score < 0.3f)
                        color.a = 0.4f;
                    DrawDot(p, 6f, color);
                }
            }
        }

        void DrawBodyCenterLine()
        {
            if (!_service.BodyValid)
                return;
            float x = _service.BodyCenterX01 * Screen.width;
            var prev = GUI.color;
            GUI.color = BodyColor;
            GUI.DrawTexture(new Rect(x - 2, Screen.height - 80, 4, 80), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void DrawStatusText()
        {
            string provider = _service.ActiveProvider == null ? "없음"
                : _service.ActiveProvider is MoveNetPoseProvider m ? $"MoveNet ({m.LastInferenceMs:F1}ms)"
                : "Mock(마우스)";
            string left = HandLabel(HandSide.Left);
            string right = HandLabel(HandSide.Right);
            string body = !_service.BodyValid ? "무효"
                : _service.BodyCenterX01.ToString("F2") + (_service.BodyCoasting ? " [유예]" : "");
            string text = $"제공자: {provider}\n"
                + $"사람: {(_service.PersonPresent ? "감지됨" : "없음")}\n"
                + $"왼손: {left} · 오른손: {right}\n"
                + $"몸 중심 x: {body}\n"
                + "F1 오버레이 · P 사람 토글(Mock)";
            GUI.Label(new Rect(10, _previewHeight + 20, 400, 120), text);
        }

        string HandLabel(HandSide side)
        {
            if (!_service.GetHandValid(side))
                return "무효";
            return _service.GetHandPos(side).ToString("F2")
                + (_service.GetHandCoasting(side) ? " [유예]" : "");
        }

        static void DrawRectBorder(Rect r, float thickness, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static void DrawDot(Vector2 center, float size, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - size / 2, center.y - size / 2, size, size), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
