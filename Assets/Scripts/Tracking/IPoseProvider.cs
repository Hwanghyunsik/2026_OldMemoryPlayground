namespace Shinmyeong.Tracking
{
    public interface IPoseProvider
    {
        bool IsRunning { get; }

        /// 마지막으로 추정된 포즈를 frame에 복사한다. 새 데이터 여부와 무관하게 최신 값을 준다.
        bool TryGetLatest(PoseFrame frame);

        /// 미리보기용 카메라 텍스처 (Mock 등 없는 경우 null)
        UnityEngine.Texture PreviewTexture { get; }
    }
}
