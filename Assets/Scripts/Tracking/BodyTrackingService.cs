using UnityEngine;

namespace Shinmyeong.Tracking
{
    /// 게임이 소비하는 트래킹 최상위 창구.
    /// 양손을 독립 파이프라인으로 추적한다 — 활성 손 선택(스위칭) 없음.
    /// 판정기는 어느 손이든 받는다. 몸 중심 가로 위치는 장보기 판정용(07 문서 5-8).
    ///
    /// 손마다: 이상치 제거 → One Euro 필터(새 추론 프레임에서만) → 렌더 프레임 보간
    public class BodyTrackingService : MonoBehaviour
    {
        public static BodyTrackingService Instance { get; private set; }

        [Header("제공자 (둘 다 있으면 MoveNet 우선)")]
        [SerializeField] MoveNetPoseProvider _moveNet;
        [SerializeField] MockPoseProvider _mock;

        [Header("판정 기준값 (시작값 · 밸런스 문서에서 확정)")]
        [Tooltip("키포인트를 신뢰하는 최소 score (추적 획득 문턱)")]
        [SerializeField] float _minKeypointScore = 0.3f;
        [Tooltip("손 추적 유지 중 허용하는 최소 score (획득 문턱보다 낮게) — score가 문턱 주변에서 오르내릴 때 점멸 방지")]
        [SerializeField] float _handSustainScore = 0.2f;
        [Tooltip("사람 존재 판정에 쓰는 핵심 관절 평균 score 문턱")]
        [SerializeField] float _personScoreThreshold = 0.35f;
        [SerializeField] float _personAppearDelay = 0.3f;
        [SerializeField] float _personLostDelay = 1.5f;

        [Header("표시")]
        [Tooltip("거울 모드: 사용자가 오른쪽으로 움직이면 화면에서도 오른쪽으로")]
        [SerializeField] bool _mirror = true;

        [Header("커서 리치 존 — 손목 카메라 영역 일부를 화면 전체로 매핑 (기준안 · 실환경 튜닝)")]
        [Tooltip("화면 하단 버튼에 닿으려고 몸을 숙이면 손목 추적을 잃는 문제 대응(2026-08-31 실사용 발견).\n서서 허리~머리 높이 손 움직임만으로 화면 전체에 닿게 한다. 끄면 기존 1:1 매핑")]
        [SerializeField] bool _handZoneEnabled = true;
        [Tooltip("리치 존 좌하단 (뷰포트 좌표 · y 위로) — 이 지점이 화면 (0,0)이 된다")]
        [SerializeField] Vector2 _handZoneMin = new Vector2(0.10f, 0.40f);
        [Tooltip("리치 존 우상단 — 이 지점이 화면 (1,1)이 된다")]
        [SerializeField] Vector2 _handZoneMax = new Vector2(0.90f, 0.85f);

        [Header("스무딩 (One Euro) — 낮은 minCutoff = 강한 스무딩, 높은 beta = 빠른 움직임 반응")]
        [SerializeField] float _handMinCutoff = 0.5f;
        [SerializeField] float _handBeta = 0.1f;
        [SerializeField] float _bodyMinCutoff = 0.4f;
        [SerializeField] float _bodyBeta = 0.1f;

        [Header("출력 보간 · 이상치 제거")]
        [Tooltip("필터 목표값을 따라가는 속도(1/s). 높을수록 즉각 반응, 낮을수록 부드러움")]
        [SerializeField] float _followSharpness = 12f;
        [Tooltip("이 score 미만이면서 좌표가 maxJump 이상 튀면 그 샘플을 버린다")]
        [SerializeField] float _weakScore = 0.45f;
        [SerializeField] float _maxJump = 0.25f;
        [Tooltip("연속으로 버릴 수 있는 최대 샘플 수 (실제 큰 이동을 계속 막지 않도록)")]
        [SerializeField] int _maxConsecutiveRejects = 3;
        [Tooltip("신뢰도가 잠깐 떨어져도 마지막 위치를 유지하는 유예 시간(초) — 커서 점멸·dwell 게이지 리셋 방지")]
        [SerializeField] float _lostGrace = 0.2f;

        /// 한 손의 독립 추적 파이프라인
        class HandPipeline
        {
            public bool Valid;
            /// 유예 홀드 중: 신뢰도가 잠깐 꺼져 마지막 위치를 유지하는 상태
            public bool Coasting;
            public Vector2 Pos = new Vector2(0.5f, 0.5f);

            Vector2 _target;
            OneEuroFilter2 _filter;
            Vector2 _lastAcceptedRaw;
            bool _tracking;
            int _rejects;
            float _lostTime;

            public void Rebuild(float minCutoff, float beta)
            {
                _filter = new OneEuroFilter2(minCutoff, beta);
                _tracking = false;
                Valid = false;
                Coasting = false;
                _lostTime = 0f;
            }

            public void UpdateTarget(PoseKeypoint kp, bool personPresent, float dt, bool mirror,
                float minScore, float sustainScore, float weakScore, float maxJump, int maxRejects, float lostGrace)
            {
                if (!personPresent)
                {
                    Drop();
                    return;
                }

                // score 히스테리시스: 획득은 minScore, 추적 유지 중에는 sustainScore까지 실좌표를 계속 쓴다.
                // 낮은 score 좌표의 튐은 이상치 제거(weakScore/maxJump)와 One Euro가 막는다
                float threshold = _tracking ? sustainScore : minScore;
                if (kp.Score < threshold)
                {
                    // 짧은 신뢰도 하락(점멸)은 유예 시간 동안 마지막 위치를 유지한 채 무시한다.
                    // 외삽하지 않는다 — 당기기 판정에 가짜 하강을 만들 수 있음
                    _lostTime += dt;
                    if (_tracking && _lostTime <= lostGrace)
                    {
                        Coasting = true;
                        return;
                    }
                    Drop();
                    return;
                }
                _lostTime = 0f;
                Coasting = false;

                // 카메라 좌표(y 아래로 증가) → 뷰포트 좌표(y 위로 증가), 미러 적용
                float vx = mirror ? 1f - kp.Position.x : kp.Position.x;
                float vy = 1f - kp.Position.y;
                var raw = new Vector2(Mathf.Clamp01(vx), Mathf.Clamp01(vy));

                // 이상치 제거: 신뢰도가 낮은데 좌표가 크게 튀면 그 샘플은 버린다
                if (_tracking && kp.Score < weakScore
                    && Vector2.Distance(raw, _lastAcceptedRaw) > maxJump
                    && _rejects < maxRejects)
                {
                    _rejects++;
                    return;
                }
                _rejects = 0;
                _lastAcceptedRaw = raw;

                _target = _filter.Filter(raw, dt);
                if (!_tracking)
                {
                    // 추적 재개 첫 샘플은 글라이드 없이 그 자리에서 시작
                    _tracking = true;
                    Pos = _target;
                }
                Valid = true;
            }

            public void Interpolate(float lerpK)
            {
                if (Valid)
                    Pos = Vector2.Lerp(Pos, _target, lerpK);
            }

            void Drop()
            {
                Valid = false;
                Coasting = false;
                _tracking = false;
                _lostTime = 0f;
                _filter.Reset();
            }
        }

        readonly PoseFrame _frame = new PoseFrame();
        readonly HandPipeline _leftHand = new HandPipeline();
        readonly HandPipeline _rightHand = new HandPipeline();
        OneEuroFilter _bodyFilter;
        float _personTimer;
        double _lastFrameTimestamp;
        float _bodyTarget;
        bool _bodyTracking;
        float _bodyLostTime;

        // ---- 게임에서 읽는 값 ----

        /// 사람이 카메라 앞에 있는가 (히스테리시스 적용)
        public bool PersonPresent { get; private set; }

        /// 손별 커서 위치, 뷰포트 좌표 (0~1 · y는 위가 1) — 리치 존 매핑 적용 후 값.
        /// 파이프라인(필터·이상치 제거·유예)은 원시 카메라 공간에서 돌고, 매핑은 출력에서만 한다
        public bool GetHandValid(HandSide side) => Pipeline(side).Valid;
        public Vector2 GetHandPos(HandSide side) => MapHandZone(Pipeline(side).Pos);

        /// 디버그 오버레이용 리치 존 읽기
        public bool HandZoneEnabled => _handZoneEnabled;
        public Vector2 HandZoneMin => _handZoneMin;
        public Vector2 HandZoneMax => _handZoneMax;

        /// 손 세로 매핑 배율 — 리치 존이 켜지면 카메라 공간의 세로 거리가 화면 공간에서 1/(존 높이)배로 늘어난다.
        /// 팔 움직임 물리 거리로 정의된 임계값(수확 당김 등)을 화면 공간으로 환산할 때 곱한다
        public float HandMapScaleY => !_handZoneEnabled || ActiveProvider is MockPoseProvider
            ? 1f
            : 1f / Mathf.Max(0.05f, _handZoneMax.y - _handZoneMin.y);

        Vector2 MapHandZone(Vector2 raw)
        {
            // Mock(마우스)은 화면 좌표 그대로가 자연스럽다 — 실카메라(MoveNet)일 때만 리치 존 적용
            if (!_handZoneEnabled || ActiveProvider is MockPoseProvider)
                return raw;
            return new Vector2(
                Mathf.Clamp01(Mathf.InverseLerp(_handZoneMin.x, _handZoneMax.x, raw.x)),
                Mathf.Clamp01(Mathf.InverseLerp(_handZoneMin.y, _handZoneMax.y, raw.y)));
        }
        public bool AnyHandValid => _leftHand.Valid || _rightHand.Valid;

        /// 유예 홀드 중인가 (신뢰도가 잠깐 꺼져 마지막 위치 유지 · 디버그용)
        public bool GetHandCoasting(HandSide side) => Pipeline(side).Coasting;
        public bool BodyCoasting { get; private set; }

        /// 몸 중심(골반) 가로 위치 0~1 · 장보기 발판 판정용
        public float BodyCenterX01 { get; private set; } = 0.5f;
        public bool BodyValid { get; private set; }

        /// 원본 포즈 (디버그·확장용)
        public PoseFrame LatestFrame => _frame;
        public IPoseProvider ActiveProvider { get; private set; }
        public bool Mirror => _mirror;

        public event System.Action OnPersonAppeared;
        public event System.Action OnPersonLost;

        HandPipeline Pipeline(HandSide side) => side == HandSide.Left ? _leftHand : _rightHand;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_moveNet == null)
                _moveNet = GetComponent<MoveNetPoseProvider>();
            if (_mock == null)
                _mock = GetComponent<MockPoseProvider>();

            RebuildFilters();
        }

        void OnValidate()
        {
            // 플레이 중 인스펙터로 스무딩 값을 조정하면 즉시 반영 (필터 상태는 초기화됨)
            if (Application.isPlaying && _bodyFilter != null)
                RebuildFilters();
        }

        void RebuildFilters()
        {
            _leftHand.Rebuild(_handMinCutoff, _handBeta);
            _rightHand.Rebuild(_handMinCutoff, _handBeta);
            _bodyFilter = new OneEuroFilter(_bodyMinCutoff, _bodyBeta);
            _bodyTracking = false;
            _bodyLostTime = 0f;
            BodyCoasting = false;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            ActiveProvider = SelectProvider();
            if (ActiveProvider == null || !ActiveProvider.TryGetLatest(_frame))
            {
                UpdatePersonPresence(false, Time.unscaledDeltaTime);
                _leftHand.Valid = false;
                _leftHand.Coasting = false;
                _rightHand.Valid = false;
                _rightHand.Coasting = false;
                BodyValid = false;
                BodyCoasting = false;
                return;
            }

            UpdatePersonPresence(ComputePersonScore() >= _personScoreThreshold, Time.unscaledDeltaTime);

            // 필터는 새 추론 결과가 왔을 때만, 실제 추론 간격을 dt로 써서 갱신한다
            if (_frame.Timestamp != _lastFrameTimestamp)
            {
                float frameDt = _lastFrameTimestamp > 0
                    ? Mathf.Clamp((float)(_frame.Timestamp - _lastFrameTimestamp), 0.001f, 0.25f)
                    : Time.deltaTime;
                _lastFrameTimestamp = _frame.Timestamp;

                _leftHand.UpdateTarget(_frame.Get(PoseJoint.LeftWrist), PersonPresent, frameDt, _mirror,
                    _minKeypointScore, _handSustainScore, _weakScore, _maxJump, _maxConsecutiveRejects, _lostGrace);
                _rightHand.UpdateTarget(_frame.Get(PoseJoint.RightWrist), PersonPresent, frameDt, _mirror,
                    _minKeypointScore, _handSustainScore, _weakScore, _maxJump, _maxConsecutiveRejects, _lostGrace);
                UpdateBodyTarget(frameDt);
            }

            // 렌더 프레임 보간: 추론 프레임 사이를 부드럽게 잇는다 (unscaled — 일시정지 중에도 커서는 움직인다)
            float k = 1f - Mathf.Exp(-_followSharpness * Time.unscaledDeltaTime);
            _leftHand.Interpolate(k);
            _rightHand.Interpolate(k);
            if (BodyValid)
                BodyCenterX01 = Mathf.Lerp(BodyCenterX01, _bodyTarget, k);
        }

        IPoseProvider SelectProvider()
        {
            if (_moveNet != null && _moveNet.IsRunning)
                return _moveNet;
            if (_mock != null && _mock.IsRunning)
                return _mock;
            return null;
        }

        float ComputePersonScore()
        {
            float sum = 0f;
            sum += _frame.Get(PoseJoint.Nose).Score;
            sum += _frame.Get(PoseJoint.LeftShoulder).Score;
            sum += _frame.Get(PoseJoint.RightShoulder).Score;
            sum += _frame.Get(PoseJoint.LeftHip).Score;
            sum += _frame.Get(PoseJoint.RightHip).Score;
            return sum / 5f;
        }

        void UpdatePersonPresence(bool detectedNow, float dt)
        {
            if (detectedNow == PersonPresent)
            {
                _personTimer = 0f;
                return;
            }

            _personTimer += dt;
            float required = detectedNow ? _personAppearDelay : _personLostDelay;
            if (_personTimer < required)
                return;

            _personTimer = 0f;
            PersonPresent = detectedNow;
            if (PersonPresent)
                OnPersonAppeared?.Invoke();
            else
            {
                RebuildFilters();
                OnPersonLost?.Invoke();
            }
        }

        void UpdateBodyTarget(float dt)
        {
            var lh = _frame.Get(PoseJoint.LeftHip);
            var rh = _frame.Get(PoseJoint.RightHip);

            float x;
            if (lh.Score >= _minKeypointScore && rh.Score >= _minKeypointScore)
                x = (lh.Position.x + rh.Position.x) * 0.5f;
            else
            {
                // 골반이 가려지면 어깨 중심으로 대체 (몸통 중심 판정 허용 · 07 문서 5-8)
                var ls = _frame.Get(PoseJoint.LeftShoulder);
                var rs = _frame.Get(PoseJoint.RightShoulder);
                if (ls.Score < _minKeypointScore || rs.Score < _minKeypointScore)
                {
                    // 짧은 신뢰도 하락은 유예 시간 동안 마지막 위치를 유지 (손과 동일한 점멸 방지)
                    _bodyLostTime += dt;
                    if (_bodyTracking && _bodyLostTime <= _lostGrace)
                    {
                        BodyCoasting = true;
                        return;
                    }
                    BodyValid = false;
                    BodyCoasting = false;
                    _bodyTracking = false;
                    _bodyLostTime = 0f;
                    _bodyFilter.Reset();
                    return;
                }
                x = (ls.Position.x + rs.Position.x) * 0.5f;
            }
            _bodyLostTime = 0f;
            BodyCoasting = false;

            if (!PersonPresent)
            {
                BodyValid = false;
                _bodyTracking = false;
                return;
            }

            if (_mirror)
                x = 1f - x;

            _bodyTarget = _bodyFilter.Filter(Mathf.Clamp01(x), dt);
            if (!_bodyTracking)
            {
                _bodyTracking = true;
                BodyCenterX01 = _bodyTarget;
            }
            BodyValid = true;
        }
    }
}
