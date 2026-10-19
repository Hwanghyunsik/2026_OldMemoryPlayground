using System;
using UnityEngine;
using Shinmyeong.Save;

namespace Shinmyeong.Net
{
    /// 내 기록 A3 `GET participants/{id}/play-sessions` 호출 모음 (명세 1-9 · 2026-10-19 `?activity=` 반영).
    /// 화면(SCR-005 최근 참여 · SCR-023)은 여기서 받은 DTO 를 바로 그린다 — 로컬 기록 파일은 더 쓰지 않는다
    public static class MyRecords
    {
        public static Awaitable<ApiResult<MyRecordsResponse>> FetchAll(string participantId, int limit = 20) =>
            ApiClient.Get<MyRecordsResponse>($"participants/{participantId}/play-sessions?limit={Mathf.Clamp(limit, 1, 50)}", DeviceAuth.Token);

        /// activityKey 는 게임 표기(Harvest · Shopping · Cooking)
        public static Awaitable<ApiResult<MyRecordsResponse>> FetchByActivity(string participantId, string activityKey, int limit = 5) =>
            ApiClient.Get<MyRecordsResponse>($"participants/{participantId}/play-sessions?activity={PlaySessionMapper.ActivityCode(activityKey)}&limit={Mathf.Clamp(limit, 1, 50)}", DeviceAuth.Token);

        /// 서버 활동 코드 → 게임 표기 (harvest → Harvest)
        public static string ActivityKey(string code)
        {
            switch (code)
            {
                case "harvest": return "Harvest";
                case "shopping": return "Shopping";
                case "cooking": return "Cooking";
                default: return code ?? "";
            }
        }

        /// 서버 ISO 시각(UTC `Z` 또는 시간대 포함) → 이 PC 로컬 "yyyy-MM-dd HH:mm:ss" (기존 화면 날짜 표기와 같은 형식)
        public static string ToLocal(string iso)
        {
            if (DateTimeOffset.TryParse(iso, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var t))
                return t.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            return iso ?? "";
        }
    }
}
