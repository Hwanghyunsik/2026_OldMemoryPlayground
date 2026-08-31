using UnityEngine;

namespace Shinmyeong.UI
{
    /// 상호작용 안전 영역 (07 문서 2-4 확정 · 1920×1080 기준).
    /// 핵심 영역 x420~1500 / y240~980(위에서 아래) — 모든 인터랙션 요소(선택 대상·버튼)는 이 안에 둔다.
    /// 예외(확정): 영상 건너뛰기(우측 상단) · 장보기 일시정지(상단 좌측) — SafeAreaExempt로 표시.
    /// 장보기 발판·점포는 좌우 이동이 게임 목적이라 적용 대상이 아니다(판정도 Dwell/Pull이 아님).
    /// 비인터랙션 표시 요소(목표 패널·경과 시간·진행 레일·결과 나열·배경)는 제한 없음.
    public static class SafeArea
    {
        // 화면 픽셀(위에서 아래 y) 기준 확정값
        public const float XMin = 420f;
        public const float XMax = 1500f;
        public const float YTop = 240f;
        public const float YBottom = 980f;

        /// 캔버스 좌표(아래에서 위 y) 기준 핵심 영역
        public static Rect CoreRectBottomUp => new Rect(XMin, 1080f - YBottom, XMax - XMin, YBottom - YTop);

#if UNITY_EDITOR
        /// 개발 검증: root 아래 모든 인터랙션 요소(DwellTarget·PullTarget)가 핵심 영역 안에 있는지 확인.
        /// 위반은 경고 로그로 알린다 — 배치 수정용이며 런타임 동작에는 관여하지 않는다.
        /// 비활성 오브젝트(모드별 버튼 그룹 등)도 검사한다.
        public static void ValidateInteractables(Transform root, string context)
        {
            var canvas = root.GetComponentInParent<Canvas>();
            if (canvas == null)
                return;
            var canvasRect = canvas.rootCanvas.GetComponent<RectTransform>();
            var core = CoreRectBottomUp;

            foreach (var target in CollectInteractables(root))
            {
                if (target.GetComponentInParent<Interaction.SafeAreaExempt>(true) != null)
                    continue;
                var rect = target.transform as RectTransform;
                if (rect == null)
                    continue;

                var bounds = UnityEngine.RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, rect);
                // 캔버스 로컬(중심 원점) → 캔버스 픽셀(좌하단 원점)
                float px = canvasRect.rect.width * canvasRect.pivot.x;
                float py = canvasRect.rect.height * canvasRect.pivot.y;
                float xMin = bounds.min.x + px;
                float xMax = bounds.max.x + px;
                float yMin = bounds.min.y + py;
                float yMax = bounds.max.y + py;

                if (xMin < core.xMin - 0.5f || xMax > core.xMax + 0.5f ||
                    yMin < core.yMin - 0.5f || yMax > core.yMax + 0.5f)
                {
                    Debug.LogWarning(
                        $"[SafeArea] {context} · 「{Path(target.transform)}」 안전 영역 이탈 — "
                        + $"x {xMin:F0}~{xMax:F0} / y(아래부터) {yMin:F0}~{yMax:F0} "
                        + $"(핵심 x {core.xMin:F0}~{core.xMax:F0} / y {core.yMin:F0}~{core.yMax:F0}) "
                        + "· 예외 대상이면 SafeAreaExempt를 붙일 것 (2-4 확정 예외 2건 한정)");
                }
            }
        }

        static System.Collections.Generic.List<MonoBehaviour> CollectInteractables(Transform root)
        {
            var list = new System.Collections.Generic.List<MonoBehaviour>();
            list.AddRange(root.GetComponentsInChildren<Interaction.DwellTarget>(true));
            list.AddRange(root.GetComponentsInChildren<Interaction.PullTarget>(true));
            return list;
        }

        static string Path(Transform t)
        {
            string path = t.name;
            while (t.parent != null && t.parent.GetComponent<Canvas>() == null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
#endif
    }
}

namespace Shinmyeong.Interaction
{
    /// 안전 영역 검증 제외 표식 — 2-4 확정 예외(영상 건너뛰기 · 장보기 일시정지)와
    /// 에디터 전용 개발 버튼에만 붙인다. 그 외 요소에 붙여 검증을 피하지 않는다.
    public class SafeAreaExempt : MonoBehaviour
    {
        [Tooltip("예외 사유 — 확정 근거 조항을 적는다")]
        public string Reason = "";
    }
}
