using UnityEngine;
using Shinmyeong.Save;

namespace Shinmyeong.Net
{
    /// 참여 기록 1건 전송 (A2 `POST play-sessions` · 명세 1-8 · 3-3).
    /// 결과 분류: Stored(201) · Duplicate(200 — 같은 id 가 이미 있음 · 정상) · Rejected(400 값 오류 · 404 지운 사용자 · 409 다른 기관 id — 재시도 금지) ·
    /// Unauthorized(401 — 기기 토큰 거부 · 설치 화면으로) · Retry(네트워크 · 5xx · 429 — 큐에 두고 나중에 같은 본문으로).
    public enum UploadOutcome { Stored, Duplicate, Rejected, Unauthorized, Retry }

    public static class PlaySessionUploader
    {
        public static UploadOutcome Classify<T>(ApiResult<T> res, bool duplicate = false)
        {
            if (res.Ok) return duplicate ? UploadOutcome.Duplicate : UploadOutcome.Stored;
            if (res.Status == 401) return UploadOutcome.Unauthorized;
            if (res.Status == 400 || res.Status == 404 || res.Status == 409) return UploadOutcome.Rejected;
            return UploadOutcome.Retry;
        }

        /// 이미 만든 본문 JSON 을 보낸다 (큐 재전송도 같은 본문을 그대로 — 멱등 키 `id` 유지)
        public static async Awaitable<(UploadOutcome outcome, ApiResult<PlaySessionResponse> result)> UploadJson(string bodyJson, string deviceToken)
        {
            var res = await ApiClient.Post<PlaySessionResponse>("play-sessions", bodyJson, deviceToken);
            var outcome = Classify(res, res.Ok && res.Data != null && res.Data.duplicate);
            if (outcome == UploadOutcome.Stored)
            {
                Debug.Log($"[Api] A2 저장 {res.Data.id} · 서버 집계 성공 {res.Data.success_count} · 도움 {res.Data.hint_count}");
                if (res.Data.clock_suspect)
                    Debug.LogWarning($"[Api] A2 clock_suspect — 이 PC 시계가 서버보다 1시간 넘게 앞섭니다 (서버 {res.Data.server_time}) · 기록은 저장됨");
            }
            else if (outcome == UploadOutcome.Duplicate)
                Debug.Log($"[Api] A2 이미 저장됨(duplicate) {res.Data.id}");
            else
                Debug.LogWarning($"[Api] A2 {outcome} — {res.Status} {res.Message}"
                    + (res.Issues != null && res.Issues.Length > 0 ? "\n  " + string.Join("\n  ", res.Issues) : ""));
            return (outcome, res);
        }

        /// 게임 기록을 변환해 보낸다. participantId 는 서버 사용자 id(A1)
        public static Awaitable<(UploadOutcome outcome, ApiResult<PlaySessionResponse> result)> Upload(PlayRecord record, string participantId, string deviceToken)
        {
            string json = PlaySessionMapper.ToJson(record, participantId);
            return UploadJson(json, deviceToken);
        }
    }
}
