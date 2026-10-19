using System;
using System.Collections.Generic;
using UnityEngine;
using Shinmyeong.Net;

namespace Shinmyeong.Save
{
    /// 관리자 웹에 등록된 기기 1대 (D2 `devices[]`)
    public struct DeviceInfo
    {
        public string Id;
        public string Name;
        public bool InUse;          // 다른 PC에 이미 연결됨 (D2 `in_use`)
        public string LastSeenAt;   // 마지막 상태 보고 시각 (없으면 null)
        public string AppVersion;

        public DeviceInfo(string id, string name, bool inUse = false, string lastSeenAt = null, string appVersion = null)
        {
            Id = id;
            Name = name;
            InUse = inUse;
            LastSeenAt = lastSeenAt;
            AppVersion = appVersion;
        }
    }

    /// 기기 연결 — 명세 1장 설치 흐름 D1~D3 (Docs/신명놀이터_API.md · 93-1 §5).
    /// 설치 때 운영자가 username·password로 로그인(D1 · 운영자 토큰 30분 · 메모리에만) → 기관의 기기 목록(D2)에서
    /// 이 PC가 맡을 기기를 고름 → 서버가 그 기기의 토큰을 발급(D3) → PlayerPrefs 에 보관. 이후 모든 기기 호출은 이 토큰만 쓴다.
    /// 기기 생성(이름 등록)은 관리자 웹 몫이다.
    public static class DeviceAuth
    {
        const string TokenKey = "Device.Token";
        const string IdKey = "Device.Id";
        const string NameKey = "Device.Name";
        const string OperatorKey = "Device.ActivatedBy";

        // 운영자 토큰 — 기기 선택이 끝나면 버린다 (게임은 기기 토큰만 쓴다 · 서버에도 저장되지 않는다)
        static string _operatorToken;
        static DateTime _operatorExpiresUtc;
        static string _operatorName;

        public static bool IsRegistered => !string.IsNullOrEmpty(PlayerPrefs.GetString(TokenKey, ""));
        /// 저장된 기기 토큰(`dev_…`) — 설치 뒤 모든 기기 호출의 Bearer
        public static string Token => PlayerPrefs.GetString(TokenKey, "");
        public static string DeviceId => PlayerPrefs.GetString(IdKey, "");
        public static string DeviceName => PlayerPrefs.GetString(NameKey, "");

        public static bool IsLoggedIn => !string.IsNullOrEmpty(_operatorToken) && DateTime.UtcNow < _operatorExpiresUtc;
        public static string OperatorName => _operatorName;
        public static string OrganizationName { get; private set; }

        /// D2 로 마지막에 받은 목록 (화면 표시용)
        public static IReadOnlyList<DeviceInfo> LastDevices { get; private set; } = Array.Empty<DeviceInfo>();

        /// D1 운영자 로그인. 실패 사유는 `result.Message`(서버가 준 한국어) — 401 틀림 · 403 막힌 계정/정지된 기관/전체관리자 · 429 잠시 잠김
        public static async Awaitable<ApiResult<LoginResponse>> Login(string username, string password)
        {
            Logout();
            var res = await ApiClient.Post<LoginResponse>("auth/login", new LoginRequest { username = username, password = password });
            if (res.Ok && !string.IsNullOrEmpty(res.Data.token))
            {
                _operatorToken = res.Data.token;
                _operatorExpiresUtc = ParseUtc(res.Data.expires_at, DateTime.UtcNow.AddMinutes(30));
                _operatorName = res.Data.@operator != null ? res.Data.@operator.name : username;
                OrganizationName = res.Data.organization != null ? res.Data.organization.name : "";
                Debug.Log($"[DeviceAuth] 운영자 로그인 — {_operatorName} / {OrganizationName} (만료 {_operatorExpiresUtc:HH:mm} UTC)");
            }
            else if (res.Ok)
            {
                res.Ok = false;
                res.Message = "로그인 응답에 토큰이 없습니다";
            }
            return res;
        }

        /// D2 로그인한 운영자 기관의 기기 목록 → `LastDevices`
        public static async Awaitable<ApiResult<DevicesResponse>> LoadDevices()
        {
            var res = await ApiClient.Get<DevicesResponse>("devices", _operatorToken);
            if (res.Ok)
            {
                var list = new List<DeviceInfo>();
                foreach (var d in res.Data.devices)
                    list.Add(new DeviceInfo(d.id, d.name, d.in_use, d.last_seen_at, d.app_version));
                LastDevices = list;
            }
            return res;
        }

        /// D3 고른 기기의 토큰 발급 · 저장. 다른 PC가 쓰는 기기는 replace일 때만 옮긴다(그 PC의 토큰은 서버에서 무효).
        /// replace 없이 사용 중인 기기를 고르면 409 (`result.InUse`)
        public static async Awaitable<ApiResult<DeviceTokenResponse>> SelectDevice(DeviceInfo device, bool replace = false)
        {
            var res = await ApiClient.Post<DeviceTokenResponse>($"devices/{device.Id}/token", new DeviceTokenRequest { replace = replace }, _operatorToken);
            if (res.Ok && !string.IsNullOrEmpty(res.Data.token))
            {
                PlayerPrefs.SetString(TokenKey, res.Data.token);
                PlayerPrefs.SetString(IdKey, res.Data.device != null ? res.Data.device.id : device.Id);
                PlayerPrefs.SetString(NameKey, res.Data.device != null ? res.Data.device.name : device.Name);
                PlayerPrefs.SetString(OperatorKey, _operatorName ?? "");
                PlayerPrefs.Save();
                Debug.Log($"[DeviceAuth] 기기 연결 — {device.Name} (운영자 {_operatorName} · 교체 {res.Data.replaced})");
                Logout();
                DeviceLink.OnDeviceLinked();
            }
            else if (res.Ok)
            {
                res.Ok = false;
                res.Message = "기기 토큰 응답이 비어 있습니다";
            }
            return res;
        }

        public static void Logout()
        {
            _operatorToken = null;
            _operatorName = null;
            _operatorExpiresUtc = DateTime.MinValue;
        }

        /// 기기 연결 해제 — 다음 실행 때 로그인 화면부터 시작 (실행 인자 `-relogin` · 에디터 메뉴 · 서버 401)
        public static void Clear()
        {
            Logout();
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.DeleteKey(IdKey);
            PlayerPrefs.DeleteKey(NameKey);
            PlayerPrefs.DeleteKey(OperatorKey);
            PlayerPrefs.Save();
            Debug.Log("[DeviceAuth] 기기 연결 해제");
        }

        static DateTime ParseUtc(string iso, DateTime fallback)
        {
            if (DateTimeOffset.TryParse(iso, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var t))
                return t.UtcDateTime;
            return fallback;
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Shinmyeong/기기 연결 해제 (로그인 화면 다시 보기)")]
        static void ClearMenu() => Clear();
#endif
    }
}
