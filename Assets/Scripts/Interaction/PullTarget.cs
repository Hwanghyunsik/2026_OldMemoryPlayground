using System.Collections.Generic;
using UnityEngine;

namespace Shinmyeong.Interaction
{
    /// 수확하기 당기기 판정 대상 (넝쿨의 작물 1개).
    /// dwell이 아니다 — 게이지 링을 표시하지 않는다 (07 문서 2-1 · 4-4).
    public class PullTarget : MonoBehaviour
    {
        public static readonly List<PullTarget> Active = new List<PullTarget>();

        [Tooltip("진입 판정 영역 배율 — 시각 오브젝트보다 넓게")]
        [SerializeField] float _hitAreaScale = 1.15f;
        [Tooltip("당기는 동안 허용되는 가로 범위 배율 (손이 비스듬히 내려가도 유지)")]
        [SerializeField] float _lateralToleranceScale = 1.8f;

        public event System.Action<PullTarget> Pulled;

        /// 게임이 붙이는 임의 데이터 (작물 정보 등)
        public object Payload;

        RectTransform _rect;

        void Awake() => _rect = GetComponent<RectTransform>();

        void OnEnable() => Active.Add(this);

        void OnDisable() => Active.Remove(this);

        /// 화면 픽셀 사각형 — 캔버스가 Screen Space Camera면 그 카메라로 월드 코너를 화면 좌표로 바꾼다
        Rect GetScreenRect()
        {
            var corners = new Vector3[4];
            _rect.GetWorldCorners(corners);
            var canvas = _rect.GetComponentInParent<Canvas>();
            var cam = canvas != null ? UI.UiCamera.Of(canvas) : null;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public bool ContainsEntry(Vector2 screenPos)
        {
            var r = GetScreenRect();
            return Mathf.Abs(screenPos.x - r.center.x) <= r.width * _hitAreaScale * 0.5f
                && Mathf.Abs(screenPos.y - r.center.y) <= r.height * _hitAreaScale * 0.5f;
        }

        public bool WithinLateral(float screenX)
        {
            var r = GetScreenRect();
            return Mathf.Abs(screenX - r.center.x) <= r.width * _lateralToleranceScale * 0.5f;
        }

        public void NotifyPulled() => Pulled?.Invoke(this);
    }
}
