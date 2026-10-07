using UnityEngine;

namespace Shinmyeong.UI
{
    /// 물건 그림(작물·재료)을 파티클 이펙트 앞에 그린다 — 자체 정렬 캔버스(overrideSorting)를 쓴다.
    /// 비활성 상태에서 만든 캔버스는 overrideSorting이 켜지지 않으므로 활성화될 때마다 다시 적용한다.
    [RequireComponent(typeof(Canvas))]
    public class InFrontOfEffects : MonoBehaviour
    {
        public const int SortingOrder = 10; // 이펙트 파티클 sortingOrder 1~3 · UICanvas 0 보다 위

        void OnEnable()
        {
            var canvas = GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
        }
    }
}
