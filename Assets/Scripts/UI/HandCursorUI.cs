using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Tracking;

namespace Shinmyeong.UI
{
    /// 캔버스 위에서 트래킹 손 위치를 따라다니는 커서 (한 손용 · 좌우 각 1개 배치).
    public class HandCursorUI : MonoBehaviour
    {
        public HandSide Side;
        [SerializeField] Image _image;

        RectTransform _rect;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            if (_image == null)
                _image = GetComponent<Image>();
        }

        void LateUpdate()
        {
            var svc = BodyTrackingService.Instance;
            bool show = svc != null && svc.GetHandValid(Side);
            if (_image != null && _image.enabled != show)
                _image.enabled = show;
            if (!show)
                return;

            // 앵커를 뷰포트 좌표에 직접 두면 해상도와 무관하게 같은 지점을 가리킨다
            _rect.anchorMin = _rect.anchorMax = svc.GetHandPos(Side);
            _rect.anchoredPosition = Vector2.zero;
        }
    }
}
