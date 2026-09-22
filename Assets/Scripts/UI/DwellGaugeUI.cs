using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;
using Shinmyeong.Tracking;

namespace Shinmyeong.UI
{
    /// 손 커서에 붙는 원형 채움 게이지 링 (한 손용).
    /// 그 손이 dwell을 진행 중일 때만 표시 — 수확·장보기에는 게이지 링이 없다(07 문서 2-1).
    /// 모양은 시안 선택 진행 링과 동일: 회색 트랙 위에 파란 링이 12시부터 시계 방향으로 찬다.
    public class DwellGaugeUI : MonoBehaviour
    {
        public HandSide Side;
        [SerializeField] Image _fillImage;

        Image _track;

        void Awake()
        {
            if (_fillImage == null)
                _fillImage = GetComponent<Image>();
            if (_fillImage == null)
                return;

            var ring = Skin.Sprite("Circle-outline-15");
            if (ring != null)
            {
                _fillImage.sprite = ring;
                _fillImage.color = Skin.Blue;

                // 트랙은 자식으로 두면 채움(부모) 위에 그려지므로, 같은 부모 아래 바로 앞 순서의 형제로 만든다
                var go = new GameObject("DwellGaugeTrack");
                go.transform.SetParent(transform.parent, false);
                _track = go.AddComponent<Image>();
                _track.sprite = ring;
                _track.color = Skin.Hex("cccccc", 0.9f);
                _track.raycastTarget = false;
                var src = (RectTransform)transform;
                var r = _track.rectTransform;
                r.anchorMin = src.anchorMin;
                r.anchorMax = src.anchorMax;
                r.pivot = src.pivot;
                r.sizeDelta = src.sizeDelta;
                r.anchoredPosition = src.anchoredPosition;
                go.transform.SetSiblingIndex(transform.GetSiblingIndex());
            }

            _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Radial360;
            _fillImage.fillOrigin = (int)Image.Origin360.Top;
            _fillImage.fillClockwise = true;
        }

        void LateUpdate()
        {
            var judge = DwellJudge.Instance;
            bool show = judge != null && judge.CurrentTarget != null
                && judge.ActiveHand == Side && judge.CurrentTarget.IsArmed;
            if (_fillImage.enabled != show)
            {
                _fillImage.enabled = show;
                if (_track != null)
                    _track.enabled = show;
            }
            if (show)
                _fillImage.fillAmount = judge.Progress01;
        }
    }
}
