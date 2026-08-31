using UnityEngine;

namespace Shinmyeong.Interaction
{
    /// 일시정지 전역 상태 (POP-001 · 07 문서 8-7).
    /// timeScale=0으로 게임 시간(Time.time) 자체를 멈춰서, 반응·힌트·경과 시간이 전부 함께 정지하고
    /// 「일시정지 시간은 활동 시간·반응 시간에서 제외」(확정) 규칙이 자동으로 지켜진다.
    /// 커서·dwell 등 팝업 조작 계통은 unscaled 시간으로 돌므로 일시정지 중에도 살아 있다.
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }

        /// 이번 판에서 누적된 일시정지 실시간(초) — 참여 기록의 PausedSec 원시값
        public static float AccumulatedSec { get; private set; }

        static float _pauseStartRealtime;

        public static void Pause()
        {
            if (IsPaused)
                return;
            IsPaused = true;
            _pauseStartRealtime = Time.realtimeSinceStartup;
            Time.timeScale = 0f;
        }

        public static void Resume()
        {
            if (!IsPaused)
                return;
            IsPaused = false;
            AccumulatedSec += Time.realtimeSinceStartup - _pauseStartRealtime;
            Time.timeScale = 1f;
        }

        /// 게임 시작(Begin) 시 호출 — 판 단위 누적 초기화
        public static void ResetAccumulated() => AccumulatedSec = 0f;
    }
}
