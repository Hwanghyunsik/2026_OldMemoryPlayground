using UnityEngine;
using UnityEngine.InputSystem;

namespace Shinmyeong.Tracking
{
    /// 웹캠 없이 개발할 때 쓰는 마우스 시뮬레이션.
    /// 마우스 = 오른손 손목, 마우스 x = 몸 중심. P 키로 사람 존재 여부 토글.
    public class MockPoseProvider : MonoBehaviour, IPoseProvider
    {
        [SerializeField] bool _personPresent = true;

        readonly PoseFrame _latest = new PoseFrame();

        public bool IsRunning => enabled;
        public Texture PreviewTexture => null;

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
                _personPresent = !_personPresent;

            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);

            // 마우스 화면 좌표 → 카메라 프레임 정규화 좌표로 변환.
            // 실제 카메라는 미러가 아니므로, 서비스에서 미러링(1-x)한 결과가
            // 마우스를 그대로 따르도록 미리 x를 뒤집어 둔다.
            float mx = 1f - Mathf.Clamp01(mousePos.x / Screen.width);
            float my = 1f - Mathf.Clamp01(mousePos.y / Screen.height); // 카메라 y는 위가 0

            float score = _personPresent ? 0.95f : 0.0f;

            for (int i = 0; i < PoseFrame.JointCount; i++)
                _latest.Keypoints[i] = new PoseKeypoint { Position = new Vector2(mx, my), Score = 0f };

            Set(PoseJoint.Nose, mx, 0.2f, score);
            Set(PoseJoint.LeftShoulder, mx + 0.08f, 0.35f, score);
            Set(PoseJoint.RightShoulder, mx - 0.08f, 0.35f, score);
            Set(PoseJoint.LeftHip, mx + 0.06f, 0.6f, score);
            Set(PoseJoint.RightHip, mx - 0.06f, 0.6f, score);
            Set(PoseJoint.RightWrist, mx, my, score);
            Set(PoseJoint.LeftWrist, mx + 0.2f, 0.55f, score * 0.5f);

            _latest.Timestamp = Time.realtimeSinceStartupAsDouble;
            _latest.Valid = true;
        }

        void Set(PoseJoint joint, float x, float y, float score)
        {
            _latest.Keypoints[(int)joint] = new PoseKeypoint
            {
                Position = new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y)),
                Score = score,
            };
        }

        public bool TryGetLatest(PoseFrame frame)
        {
            if (!_latest.Valid)
                return false;
            _latest.CopyTo(frame);
            return true;
        }
    }
}
