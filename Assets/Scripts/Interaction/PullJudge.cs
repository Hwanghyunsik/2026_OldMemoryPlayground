using UnityEngine;
using Shinmyeong.Tracking;

namespace Shinmyeong.Interaction
{
    /// 수확하기 당기기 판정 (07 문서 4-4 · 개발 판단 C8). 양손 독립 —
    /// 각 손이 따로 무장·당김을 진행하며, 어느 손으로든 수확할 수 있다.
    ///
    ///   ① 손이 작물 영역 진입 → 무장 지연(0.3~0.5초) 동안 머무르면 무장
    ///   ② 무장 후 손이 아래로 하강 임계 거리만큼 이동 → 당김 확정
    ///
    /// 게이지 링 없음.
    public class PullJudge : MonoBehaviour
    {
        public static PullJudge Instance { get; private set; }

        [Header("판정 기준값 (C8 기준안 · 실환경 테스트로 확정)")]
        [Tooltip("영역 진입 후 무장까지의 지연 (검토안 0.3~0.5초)")]
        [SerializeField] float _armDelaySeconds = 0.4f;
        [Tooltip("당김으로 인정하는 최소 하강 거리 (뷰포트 비율 · 0.11 ≈ 120px@1080)")]
        [SerializeField] float _pullDistanceViewport = 0.11f;

        /// 라운드 연출 중에는 게임이 꺼 둔다
        public bool JudgingEnabled = true;

        public event System.Action<PullTarget> Pulled;

        class HandState
        {
            public PullTarget Current;
            public bool Armed;
            public float ArmTimer;
            public float PeakY;
            public PullTarget BlockedUntilExit;

            public void Reset()
            {
                Current = null;
                Armed = false;
                ArmTimer = 0f;
            }
        }

        readonly HandState _left = new HandState();
        readonly HandState _right = new HandState();

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
            if (!JudgingEnabled || GamePause.IsPaused || svc == null)
            {
                // 일시정지 중 커서는 살아 있으므로(unscaled) 손 하강이 당김으로 잡히지 않게 막는다
                _left.Reset();
                _right.Reset();
                return;
            }

            Step(svc, _left, HandSide.Left);
            Step(svc, _right, HandSide.Right);
        }

        void Step(BodyTrackingService svc, HandState state, HandSide side)
        {
            if (!svc.GetHandValid(side))
            {
                state.Reset();
                return;
            }

            var vp = svc.GetHandPos(side);
            var screenPos = new Vector2(vp.x * Screen.width, vp.y * Screen.height);
            float viewportY = vp.y;

            // 당김 직후에는 그 손이 해당 작물 영역을 벗어나기 전까지 재판정하지 않는다
            if (state.BlockedUntilExit != null)
            {
                if (!state.BlockedUntilExit.isActiveAndEnabled || !state.BlockedUntilExit.ContainsEntry(screenPos))
                    state.BlockedUntilExit = null;
                else
                    return;
            }

            if (!state.Armed)
            {
                PullTarget hit = null;
                foreach (var target in PullTarget.Active)
                {
                    if (target.isActiveAndEnabled && target.ContainsEntry(screenPos))
                    {
                        hit = target;
                        break;
                    }
                }

                if (hit != state.Current)
                {
                    state.Current = hit;
                    state.ArmTimer = 0f;
                }
                if (state.Current == null)
                    return;

                state.ArmTimer += Time.deltaTime;
                if (state.ArmTimer >= _armDelaySeconds)
                {
                    state.Armed = true;
                    state.PeakY = viewportY;
                }
            }
            else
            {
                if (state.Current == null || !state.Current.isActiveAndEnabled || !state.Current.WithinLateral(screenPos.x))
                {
                    state.Reset();
                    return;
                }

                // 하강 거리는 무장 이후 최고점 기준으로 잰다
                state.PeakY = Mathf.Max(state.PeakY, viewportY);
                if (state.PeakY - viewportY >= _pullDistanceViewport)
                {
                    var pulled = state.Current;
                    state.Reset();
                    state.BlockedUntilExit = pulled;
                    pulled.NotifyPulled();
                    Pulled?.Invoke(pulled);
                }
            }
        }
    }
}
