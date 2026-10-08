using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shinmyeong.Save
{
    /// 관리자 웹에 등록된 기기 1대
    public struct DeviceInfo
    {
        public string Id;
        public string Name;
        public bool InUse; // 다른 PC에 이미 연결됨 (D2 `in_use`)

        public DeviceInfo(string id, string name, bool inUse = false)
        {
            Id = id;
            Name = name;
            InUse = inUse;
        }
    }

    /// 기기 연결(93 문서 D1~D3) — **더미**.
    /// 설치 때 운영자가 username·password로 로그인 → 기관에 등록된 기기 목록에서 이 PC가 맡을 기기를 고름 →
    /// 서버가 그 기기의 토큰을 발급한다는 흐름만 흉내 낸다. 기기 생성(이름 등록)은 관리자 웹 몫이다.
    /// 서버 연동 전까지는 고정 더미 계정만 통과시키고 고정 목록·가짜 토큰을 쓴다.
    public static class DeviceAuth
    {
        public const string DummyUsername = "admin";
        public const string DummyPassword = "1234";

        static readonly DeviceInfo[] DummyDevices =
        {
            new DeviceInfo("6702f1a9c3b4d5e6f7a8b901", "1층 프로그램실"),
            new DeviceInfo("6702f1a9c3b4d5e6f7a8b902", "2층 강당"),
            new DeviceInfo("6702f1a9c3b4d5e6f7a8b903", "경로당 거실", inUse: true),
        };

        const string TokenKey = "Device.Token";
        const string IdKey = "Device.Id";
        const string NameKey = "Device.Name";
        const string OperatorKey = "Device.ActivatedBy";

        /// 로그인한 운영자 (기기 선택이 끝나면 버린다 — 게임은 기기 토큰만 쓴다)
        static string _operator;

        public static bool IsRegistered => !string.IsNullOrEmpty(PlayerPrefs.GetString(TokenKey, ""));
        public static bool IsLoggedIn => !string.IsNullOrEmpty(_operator);
        public static string DeviceId => PlayerPrefs.GetString(IdKey, "");
        public static string DeviceName => PlayerPrefs.GetString(NameKey, "");

        /// D1 운영자 로그인. 서버 연동 시 이 함수 내부만 API 호출로 바꾼다
        public static bool Login(string username, string password)
        {
            if (username != DummyUsername || password != DummyPassword)
                return false;
            _operator = username;
            return true;
        }

        /// D2 로그인한 운영자 기관의 기기 목록
        public static IReadOnlyList<DeviceInfo> LoadDevices() => IsLoggedIn ? DummyDevices : Array.Empty<DeviceInfo>();

        /// D3 고른 기기의 토큰 발급 · 저장. 다른 PC가 쓰는 기기는 replace일 때만 옮긴다(그 PC의 토큰은 서버에서 무효) —
        /// replace 없이 사용 중인 기기를 고르면 서버가 409로 거절하는 것을 흉내 내 false
        public static bool SelectDevice(DeviceInfo device, bool replace = false)
        {
            if (device.InUse && !replace)
                return false;
            PlayerPrefs.SetString(TokenKey, "dummy-" + Guid.NewGuid().ToString("N"));
            PlayerPrefs.SetString(IdKey, device.Id);
            PlayerPrefs.SetString(NameKey, device.Name);
            PlayerPrefs.SetString(OperatorKey, _operator ?? "");
            PlayerPrefs.Save();
            Debug.Log($"[DeviceAuth] 더미 기기 연결 — {device.Name} (운영자 {_operator})");
            _operator = null;
            return true;
        }

        public static void Logout() => _operator = null;

        /// 기기 연결 해제 — 다음 실행 때 로그인 화면부터 시작 (실행 인자 `-relogin` · 에디터 메뉴)
        public static void Clear()
        {
            _operator = null;
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.DeleteKey(IdKey);
            PlayerPrefs.DeleteKey(NameKey);
            PlayerPrefs.DeleteKey(OperatorKey);
            PlayerPrefs.Save();
            Debug.Log("[DeviceAuth] 기기 연결 해제");
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Shinmyeong/기기 연결 해제 (로그인 화면 다시 보기)")]
        static void ClearMenu() => Clear();
#endif
    }
}
