using UnityEngine;

namespace Shinmyeong.Tracking
{
    /// SCR-002에서 확보하는 개인 초기 기준값 (설계서: 이후 전 게임의 판정 영역 산출 기준 · 5-8).
    /// 뷰포트 좌표(0~1 · y는 위가 1) — 세션 단위이며 SCR-001 복귀(새 사용자) 시 다시 잰다.
    /// 현재 소비처: 없음(수집만) — 신체 기준 커서 매핑·장보기 이동 범위 산출에 쓸 예정.
    public static class Calibration
    {
        public static bool Ready { get; private set; }
        public static float CenterX { get; private set; }   // 중앙에 섰을 때 몸 중심 x
        public static float HeadY { get; private set; }     // 코 기준
        public static float ShoulderY { get; private set; } // 양어깨 평균
        public static float HipY { get; private set; }      // 양골반 평균
        public static float AnkleY { get; private set; }    // 양발목 평균 (신뢰 낮으면 근사)

        public static void Set(float centerX, float headY, float shoulderY, float hipY, float ankleY)
        {
            CenterX = centerX;
            HeadY = headY;
            ShoulderY = shoulderY;
            HipY = hipY;
            AnkleY = ankleY;
            Ready = true;
            Debug.Log($"[Calibration] 기준값 확보 — 중심x {centerX:F2} · 머리 {headY:F2} · 어깨 {shoulderY:F2}"
                + $" · 골반 {hipY:F2} · 발목 {ankleY:F2}");
        }

        public static void Clear() => Ready = false;
    }
}
