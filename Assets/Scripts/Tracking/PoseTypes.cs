using UnityEngine;

namespace Shinmyeong.Tracking
{
    /// MoveNet 17 키포인트 인덱스 (출력 텐서 순서와 동일)
    public enum PoseJoint
    {
        Nose = 0,
        LeftEye = 1,
        RightEye = 2,
        LeftEar = 3,
        RightEar = 4,
        LeftShoulder = 5,
        RightShoulder = 6,
        LeftElbow = 7,
        RightElbow = 8,
        LeftWrist = 9,
        RightWrist = 10,
        LeftHip = 11,
        RightHip = 12,
        LeftKnee = 13,
        RightKnee = 14,
        LeftAnkle = 15,
        RightAnkle = 16,
    }

    public enum HandSide
    {
        Left = 0,
        Right = 1,
    }

    public struct PoseKeypoint
    {
        /// 카메라 프레임 기준 정규화 좌표 (x: 0=좌, 1=우 / y: 0=상, 1=하)
        public Vector2 Position;
        public float Score;
    }

    public class PoseFrame
    {
        public const int JointCount = 17;

        public readonly PoseKeypoint[] Keypoints = new PoseKeypoint[JointCount];
        public double Timestamp;
        public bool Valid;

        public PoseKeypoint Get(PoseJoint joint) => Keypoints[(int)joint];

        public void CopyTo(PoseFrame other)
        {
            System.Array.Copy(Keypoints, other.Keypoints, JointCount);
            other.Timestamp = Timestamp;
            other.Valid = Valid;
        }
    }
}
