using System;
using UnityEngine;
using Shinmyeong.Save;
using Shinmyeong.Tracking;

namespace Shinmyeong.Net
{
    /// 기기 상주 연결 — 게임이 켜져 있는 동안 서버와 주고받는 기기 호출을 한곳에서 돈다 (명세 1-6 · 1-7 · B2 · 3-3 · Docs/94 3장 2단계).
    ///   · B1 상태 보고 5초마다 (카메라 · 사람 인식 · 현재 화면 · 버전 · 남은 저장 공간 · PC 시각) — 60초 안에 보고가 없으면 관리자 화면은 꺼진 것으로 본다
    ///   · 명령 조회 5초마다 — `shutdown_requested_at` 이 게임을 켠 시각보다 뒤면 ack 후 종료
    ///   · B2 설정 60초마다 — 소리 크기(1~3) · 음성 안내 여부
    ///   · 발신함(Outbox) 비우기 — 시작 때 · 30초마다 · 기록이 들어온 직후
    ///   · 401(기기 토큰 거부) — 바로 화면을 바꾸지 않는다(18-1 어르신 화면에 관리 화면 금지). 표시만 해 두고 SCR-001 복귀 때 FlowManager 가 로그인 화면으로 보낸다
    /// 플레이 모드가 시작되면 스스로 생긴다(씬 배치 불필요).
    public class DeviceLink : MonoBehaviour
    {
        public static DeviceLink Instance { get; private set; }

        /// 기기 토큰이 거부됨(401) — 관리자 웹에서 연결 해제됐거나 다른 PC 로 옮겨짐. SCR-001 복귀 때 로그인 화면으로
        public static bool TokenRejected { get; private set; }
        /// 마지막 상태 보고가 성공했는가 (화면에서 「서버 연결 안 됨」 표시용)
        public static bool Online { get; private set; }
        /// B2 — 소리 크기 1~3 (기본 2) · 음성 안내 켬 (TTS 도입 시 사용)
        public static int VolumeLevel { get; private set; } = 2;
        public static bool VoiceEnabled { get; private set; } = true;
        /// 게임을 켠 시각 — 이보다 앞선 종료 요청은 지난 것으로 무시 (명세 1-7)
        public static DateTimeOffset StartedAt { get; private set; }

        float _nextStatus, _nextCommand, _nextSettings, _nextFlush;
        bool _statusBusy, _commandBusy, _settingsBusy, _flushBusy, _quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("DeviceLink");
            DontDestroyOnLoad(go);
            go.AddComponent<DeviceLink>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            StartedAt = DateTimeOffset.Now;
            TokenRejected = false;
            _nextStatus = _nextCommand = _nextSettings = _nextFlush = 0f; // 시작하자마자 한 번씩
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (_quitting || TokenRejected || !DeviceAuth.IsRegistered) return;
            float t = Time.unscaledTime;
            if (t >= _nextStatus && !_statusBusy) { _nextStatus = t + ApiConfig.StatusIntervalSec; ReportStatus(); }
            if (t >= _nextCommand && !_commandBusy) { _nextCommand = t + ApiConfig.CommandIntervalSec; PollCommands(); }
            if (t >= _nextSettings && !_settingsBusy) { _nextSettings = t + ApiConfig.SettingsIntervalSec; LoadSettings(); }
            if (t >= _nextFlush && !_flushBusy) { _nextFlush = t + ApiConfig.QueueRetrySec; FlushOutbox(); }
        }

        /// 기록이 발신함에 들어왔을 때 — 다음 프레임에 바로 보낸다
        public void FlushSoon() => _nextFlush = 0f;

        /// 설치 화면에서 기기를 새로 연결했을 때 (DeviceAuth.SelectDevice)
        public static void OnDeviceLinked()
        {
            TokenRejected = false;
            if (Instance != null) Instance._nextStatus = Instance._nextCommand = Instance._nextSettings = Instance._nextFlush = 0f;
        }

        /// FlowManager 가 로그인 화면으로 보낸 뒤 호출 — 표시를 지운다
        public static void ClearRejected() => TokenRejected = false;

        /// 기기 토큰 호출이 401 을 받았을 때 (ApiClient 가 호출) — 표시만 하고 화면 전환은 FlowManager 가 SCR-001 복귀 때
        public static void NoteUnauthorized()
        {
            if (TokenRejected) return;
            TokenRejected = true;
            Debug.LogWarning("[DeviceLink] 기기 토큰이 거부됨(401) — 대기 화면으로 돌아갈 때 로그인 화면으로 보냅니다 (기록은 발신함에 보관)");
        }

        // ---- B1 상태 보고 ----

        async void ReportStatus()
        {
            _statusBusy = true;
            try
            {
                var svc = BodyTrackingService.Instance;
                bool camera = svc != null && svc.ActiveProvider is MoveNetPoseProvider mn && mn.IsRunning;
                var body = new DeviceStatusRequest
                {
                    camera_connected = camera,
                    person_detected = svc != null && svc.PersonPresent,
                    game_state = Flow.FlowManager.Instance != null ? Flow.FlowManager.Instance.CurrentId.ToString() : "boot",
                    app_version = ApiConfig.AppVersion,
                    storage_free_bytes = FreeBytes(),
                    device_time = ApiConfig.NowIso(),
                };
                var res = await ApiClient.Put<OkResponse>("devices/me/status", body, DeviceAuth.Token, quiet: true);
                bool was = Online;
                Online = res.Ok;
                if (was != Online) Debug.Log(Online ? "[DeviceLink] 서버 연결됨 — 상태 보고 시작" : $"[DeviceLink] 상태 보고 실패 — {res}");
            }
            finally { _statusBusy = false; }
        }

        static long FreeBytes()
        {
            try { return new System.IO.DriveInfo(System.IO.Path.GetPathRoot(Application.persistentDataPath)).AvailableFreeSpace; }
            catch { return 0; }
        }

        // ---- 명령 (프로그램 종료) ----

        async void PollCommands()
        {
            _commandBusy = true;
            try
            {
                var res = await ApiClient.Get<CommandsResponse>("devices/me/commands", DeviceAuth.Token, quiet: true);
                if (!res.Ok || string.IsNullOrEmpty(res.Data.shutdown_requested_at)) return;
                if (!DateTimeOffset.TryParse(res.Data.shutdown_requested_at, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var at)) return;
                if (at <= StartedAt) return; // 지난 요청 (이번 실행보다 앞) — 무시 (명세 1-7)
                if (_quitting) return;
                _quitting = true;
                Debug.Log($"[DeviceLink] 프로그램 종료 요청 수신 ({at:HH:mm:ss}) — ack 후 종료");
                var ack = await ApiClient.Post<OkResponse>("devices/me/commands/ack", new CommandAckRequest(), DeviceAuth.Token);
                if (!ack.Ok) Debug.LogWarning($"[DeviceLink] 종료 ack 실패 {ack} — 그래도 종료합니다");
                Quit();
            }
            finally { _commandBusy = false; }
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- B2 설정 ----

        async void LoadSettings()
        {
            _settingsBusy = true;
            try
            {
                var res = await ApiClient.Get<DeviceSettingsResponse>("devices/me/settings", DeviceAuth.Token, quiet: true);
                if (!res.Ok) return;
                int level = Mathf.Clamp(res.Data.volume_level, 1, 3);
                if (level != VolumeLevel || res.Data.voice_enabled != VoiceEnabled)
                    Debug.Log($"[DeviceLink] 설정 — 소리 {level} · 음성 {(res.Data.voice_enabled ? "켬" : "끔")}");
                VolumeLevel = level;
                VoiceEnabled = res.Data.voice_enabled;
                AudioListener.volume = level == 1 ? 0.4f : level == 2 ? 0.7f : 1f;
            }
            finally { _settingsBusy = false; }
        }

        // ---- 발신함 ----

        async void FlushOutbox()
        {
            _flushBusy = true;
            try
            {
                if (Outbox.Count == 0) return;
                await Outbox.Flush(DeviceAuth.Token); // 401 은 ApiClient → NoteUnauthorized
            }
            finally { _flushBusy = false; }
        }
    }
}
