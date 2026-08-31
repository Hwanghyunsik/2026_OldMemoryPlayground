using UnityEngine;
using Shinmyeong.Tracking;

namespace Shinmyeong.Interaction
{
    /// 손 커서 위치로 DwellTarget들의 체류 시간을 판정한다. 양손 대응 —
    /// 어느 손이든 대상 위에 머무르면 진행되고, 같은 대상 위에서는 손이 바뀌어도 이어진다.
    /// 규칙(07 문서 2-1): 영역 이탈 시 즉시 리셋 · 동시에 하나의 대상만 진행.
    public class DwellJudge : MonoBehaviour
    {
        public static DwellJudge Instance { get; private set; }

        [Tooltip("Screen Space Overlay 캔버스면 비워 둔다")]
        [SerializeField] Camera _uiCamera;

        /// 설정되면 이 트랜스폼 하위의 대상만 판정한다 — 팝업(POP-001 등)이 뒤 화면 조작을 차단할 때 사용
        public static Transform ModalRoot { get; set; }

        public DwellTarget CurrentTarget { get; private set; }
        public HandSide ActiveHand { get; private set; } = HandSide.Right;
        public float Progress01 { get; private set; }

        float _timer;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            var svc = BodyTrackingService.Instance;
            if (svc == null || !svc.AnyHandValid)
            {
                SetTarget(null, ActiveHand);
                return;
            }

            // 진행 중이던 대상 위에 아직 손이 있으면 유지 — 같은 대상이면 손 인계 허용
            if (CurrentTarget != null)
            {
                if (TargetUnderHand(svc, ActiveHand) != CurrentTarget)
                {
                    var other = Other(ActiveHand);
                    if (TargetUnderHand(svc, other) == CurrentTarget)
                        ActiveHand = other;
                    else
                        SetTarget(null, ActiveHand);
                }
            }

            // 새 대상 탐색 (오른손 우선 — 우선순위일 뿐 기능 차이는 없다)
            if (CurrentTarget == null)
            {
                foreach (var side in new[] { HandSide.Right, HandSide.Left })
                {
                    var hit = TargetUnderHand(svc, side);
                    if (hit != null)
                    {
                        SetTarget(hit, side);
                        break;
                    }
                }
            }

            if (CurrentTarget == null || !CurrentTarget.IsArmed)
                return;

            // unscaled: 일시정지(timeScale=0) 중에도 팝업 버튼 dwell이 동작해야 한다
            _timer += Time.unscaledDeltaTime;
            Progress01 = Mathf.Clamp01(_timer / CurrentTarget.DwellSeconds);
            if (_timer >= CurrentTarget.DwellSeconds)
            {
                CurrentTarget.NotifySelected();
                _timer = 0f;
                Progress01 = 0f;
            }
        }

        DwellTarget TargetUnderHand(BodyTrackingService svc, HandSide side)
        {
            if (!svc.GetHandValid(side))
                return null;
            var pos = svc.GetHandPos(side);
            var screenPos = new Vector2(pos.x * Screen.width, pos.y * Screen.height);
            foreach (var target in DwellTarget.Active)
            {
                if (ModalRoot != null && !target.transform.IsChildOf(ModalRoot))
                    continue;
                if (target.isActiveAndEnabled && target.ContainsScreenPoint(screenPos, _uiCamera))
                    return target;
            }
            return null;
        }

        static HandSide Other(HandSide side) => side == HandSide.Left ? HandSide.Right : HandSide.Left;

        void SetTarget(DwellTarget target, HandSide hand)
        {
            if (target == CurrentTarget && hand == ActiveHand)
                return;
            if (target != CurrentTarget)
            {
                // 이전 대상은 이탈 → 즉시 리셋, 완료된 대상은 재무장
                CurrentTarget?.Rearm();
                _timer = 0f;
                Progress01 = 0f;
            }
            CurrentTarget = target;
            ActiveHand = hand;
        }
    }
}
