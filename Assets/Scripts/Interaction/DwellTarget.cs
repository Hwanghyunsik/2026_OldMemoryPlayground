using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Shinmyeong.Interaction
{
    /// dwell로 선택 가능한 UI 요소에 붙인다. 유지 시간은 07 문서 2-1:
    /// UI 버튼 3초 · 게임 오브젝트 2초 · 요리하기 재료 1~2초(기준안).
    public class DwellTarget : MonoBehaviour
    {
        public static readonly List<DwellTarget> Active = new List<DwellTarget>();

        [SerializeField] float _dwellSeconds = 3f;
        [Tooltip("판정 영역 배율 — 시각 오브젝트보다 넓게 잡는다 (07 문서 2-1 설계 원칙)")]
        [SerializeField] float _hitAreaScale = 1.2f;
        [SerializeField] UnityEvent _onSelected;

        public event System.Action<DwellTarget> Selected;

        public float DwellSeconds => _dwellSeconds;

        /// 런타임 조립용 — 요리 재료 1~2초(기준안) 등 대상별 유지 시간 지정
        public void SetDwellSeconds(float seconds) => _dwellSeconds = seconds;

        /// 완료 후 커서가 영역을 벗어나기 전까지 재발동하지 않는다
        public bool IsArmed { get; private set; } = true;

        RectTransform _rect;

        void Awake() => _rect = GetComponent<RectTransform>();

        void OnEnable() => Active.Add(this);

        void OnDisable()
        {
            Active.Remove(this);
            IsArmed = true;
        }

        public bool ContainsScreenPoint(Vector2 screenPos, Camera uiCamera)
        {
            if (_rect == null)
                return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenPos, uiCamera, out var local))
                return false;
            var r = _rect.rect;
            return Mathf.Abs(local.x - r.center.x) <= r.width * _hitAreaScale * 0.5f
                && Mathf.Abs(local.y - r.center.y) <= r.height * _hitAreaScale * 0.5f;
        }

        public void NotifySelected()
        {
            IsArmed = false;
            _onSelected?.Invoke();
            Selected?.Invoke(this);
        }

        public void Rearm() => IsArmed = true;
    }
}
