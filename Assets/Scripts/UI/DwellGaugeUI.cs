using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;
using Shinmyeong.Tracking;

namespace Shinmyeong.UI
{
    /// 손 커서에 붙는 원형 채움 게이지 링 (한 손용).
    /// 그 손이 dwell을 진행 중일 때만 표시 — 수확·장보기에는 게이지 링이 없다(07 문서 2-1).
    public class DwellGaugeUI : MonoBehaviour
    {
        public HandSide Side;
        [SerializeField] Image _fillImage;

        void Awake()
        {
            if (_fillImage == null)
                _fillImage = GetComponent<Image>();
            if (_fillImage != null)
            {
                _fillImage.type = Image.Type.Filled;
                _fillImage.fillMethod = Image.FillMethod.Radial360;
                _fillImage.fillOrigin = (int)Image.Origin360.Top;
                _fillImage.fillClockwise = true;
            }
        }

        void LateUpdate()
        {
            var judge = DwellJudge.Instance;
            bool show = judge != null && judge.CurrentTarget != null
                && judge.ActiveHand == Side && judge.CurrentTarget.IsArmed;
            if (_fillImage.enabled != show)
                _fillImage.enabled = show;
            if (show)
                _fillImage.fillAmount = judge.Progress01;
        }
    }
}
