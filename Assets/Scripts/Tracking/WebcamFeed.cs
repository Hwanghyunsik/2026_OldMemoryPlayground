using UnityEngine;

namespace Shinmyeong.Tracking
{
    /// 웹캠 캡처 관리. 다른 컴포넌트는 이 클래스의 Texture만 참조한다.
    public class WebcamFeed : MonoBehaviour
    {
        [Tooltip("비우면 첫 번째 장치를 사용")]
        [SerializeField] string _deviceName = "";
        [SerializeField] int _requestedWidth = 1280;
        [SerializeField] int _requestedHeight = 720;
        [SerializeField] int _requestedFps = 30;

        WebCamTexture _texture;

        public WebCamTexture Texture => _texture;
        public bool IsReady => _texture != null && _texture.isPlaying && _texture.width > 16;

        void OnEnable()
        {
            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogWarning("[WebcamFeed] 연결된 웹캠이 없습니다.");
                return;
            }

            string name = _deviceName;
            if (string.IsNullOrEmpty(name))
                name = devices[0].name;

            _texture = new WebCamTexture(name, _requestedWidth, _requestedHeight, _requestedFps);
            _texture.Play();
            Debug.Log($"[WebcamFeed] 시작: {name}");
        }

        void OnDisable()
        {
            if (_texture != null)
            {
                _texture.Stop();
                Destroy(_texture);
                _texture = null;
            }
        }
    }
}
