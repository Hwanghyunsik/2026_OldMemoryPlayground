using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Shinmyeong.Net
{
    /// 한 번의 호출 결과. Ok 가 아니면 Status 로 명세 3-5 「오류별로 게임이 할 일」을 따른다.
    public class ApiResult<T>
    {
        public bool Ok;           // 2xx 이고 본문 파싱 성공
        public long Status;       // HTTP 상태 · 0 = 서버에 닿지 못함(네트워크 · 시간 초과 · 인증서)
        public string Message;    // 서버 message(화면에 그대로 보여도 되는 한국어) 또는 전송 오류 설명
        public string[] Issues;   // 400 값 오류 위치 (`rounds.3.items.0.arrive_sec: …`)
        public bool InUse;        // D3 409
        public string ServerTime; // 400 응답의 server_time (시계 보정용 · 2026-10-19 반영)
        public T Data;
        public string Raw;

        public bool Unreachable => Status == 0;
        /// 잠시 후 다시 보내면 될 수 있는 실패 — 네트워크 · 5xx · 429. 400/401/403/404/409 는 재시도하지 않는다
        public bool Retryable => Status == 0 || Status >= 500 || Status == 429;

        public override string ToString() => Ok ? $"OK {Status}" : $"실패 {Status} {Message}";
    }

    /// 서버 API 호출기 (UnityWebRequest · JSON · Bearer 토큰). 경로는 `auth/login` 처럼 `/api/` 뒤만 적는다.
    /// 화면·게임 코드는 이 클래스를 직접 부르지 않고 DeviceAuth(D1~D3) · 데이터 계층(A1~A3 · B1 · B2 · 명령)을 통해 쓴다.
    public static class ApiClient
    {
        public static Awaitable<ApiResult<T>> Get<T>(string path, string token = null, bool quiet = false) => Send<T>("GET", path, null, token, quiet);
        public static Awaitable<ApiResult<T>> Post<T>(string path, object body, string token = null, bool quiet = false) => Send<T>("POST", path, body, token, quiet);
        public static Awaitable<ApiResult<T>> Put<T>(string path, object body, string token = null, bool quiet = false) => Send<T>("PUT", path, body, token, quiet);

        /// body 는 [Serializable] 객체(JsonUtility) 또는 이미 만든 JSON 문자열 · quiet 면 실패를 콘솔에 쓰지 않는다(주기 호출용)
        public static async Awaitable<ApiResult<T>> Send<T>(string method, string path, object body, string token, bool quiet = false)
        {
            var result = new ApiResult<T>();
            string url = ApiConfig.Url(path);
            using (var req = new UnityWebRequest(url, method))
            {
                if (body != null)
                {
                    string json = body as string ?? JsonUtility.ToJson(body);
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Accept", "application/json");
                if (!string.IsNullOrEmpty(token))
                    req.SetRequestHeader("Authorization", "Bearer " + token);
                req.timeout = ApiConfig.TimeoutSec;

                try
                {
                    await req.SendWebRequest();
                }
                catch (Exception e)
                {
                    result.Message = $"요청 실패: {e.Message}";
                    if (!quiet) Debug.LogWarning($"[Api] {method} {url} — {result.Message}");
                    return result;
                }

                result.Status = req.responseCode;
                result.Raw = req.downloadHandler.text;

                if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.DataProcessingError)
                {
                    result.Status = 0;
                    result.Message = "서버에 연결할 수 없습니다 (" + req.error + ")";
                    if (!quiet) Debug.LogWarning($"[Api] {method} {url} — {result.Message}");
                    return result;
                }

                if (result.Status >= 200 && result.Status < 300)
                {
                    try
                    {
                        result.Data = JsonUtility.FromJson<T>(string.IsNullOrEmpty(result.Raw) ? "{}" : result.Raw);
                        result.Ok = true;
                    }
                    catch (Exception e)
                    {
                        result.Message = "응답을 읽지 못했습니다: " + e.Message;
                        Debug.LogError($"[Api] {method} {url} {result.Status} — {result.Message}\n{result.Raw}");
                    }
                    return result;
                }

                // 실패 봉투 { ok:false, message, issues } — 500 도 JSON (명세 공통)
                try
                {
                    var err = JsonUtility.FromJson<ApiErrorBody>(result.Raw);
                    if (err != null)
                    {
                        result.Message = err.message;
                        result.Issues = err.issues;
                        result.InUse = err.in_use;
                        result.ServerTime = err.server_time;
                    }
                }
                catch { /* JSON 이 아닌 응답(프록시 오류 등) */ }
                if (string.IsNullOrEmpty(result.Message))
                    result.Message = DefaultMessage(result.Status);
                if (!quiet)
                    Debug.LogWarning($"[Api] {method} {url} → {result.Status} {result.Message}"
                        + (result.Issues != null && result.Issues.Length > 0 ? "\n  " + string.Join("\n  ", result.Issues) : ""));
                // 기기 토큰으로 부른 호출이 401 이면 기기 연결이 끊긴 것 — 설치 화면 복귀는 DeviceLink/FlowManager 가 맡는다
                if (result.Status == 401 && !string.IsNullOrEmpty(token) && token == Shinmyeong.Save.DeviceAuth.Token)
                    DeviceLink.NoteUnauthorized();
                return result;
            }
        }

        static string DefaultMessage(long status)
        {
            switch (status)
            {
                case 400: return "보낸 값이 올바르지 않습니다";
                case 401: return "인증이 필요합니다";
                case 403: return "이 요청을 할 수 없는 계정입니다";
                case 404: return "대상을 찾을 수 없습니다";
                case 405: return "허용되지 않는 방식입니다";
                case 409: return "이미 사용 중입니다";
                case 429: return "잠시 후 다시 시도해 주세요";
                default: return status >= 500 ? "서버 오류 — 잠시 후 다시 시도해 주세요" : $"오류 {status}";
            }
        }
    }
}
