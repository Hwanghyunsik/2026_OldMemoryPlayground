using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Shinmyeong.Net;
using Shinmyeong.Save;

namespace Shinmyeong.EditorTools
{
    /// 서버 연동 준비용 에디터 메뉴 — 서버 없이도 본문 모양을 확인하고, 주소가 맞는지 확인한다
    public static class ApiDevMenu
    {
        /// 발신함 — 아직 서버로 못 보낸 참여 기록(A2 본문 파일)을 보여 주고, 기기 토큰이 있으면 지금 보낸다
        [MenuItem("Shinmyeong/API/발신함 보기 · 지금 보내기")]
        static async void FlushOutbox()
        {
            var files = Outbox.Pending();
            Debug.Log($"[Api] 발신함 {files.Length}건" + (files.Length > 0 ? "\n  " + string.Join("\n  ", System.Array.ConvertAll(files, Path.GetFileName)) : ""));
            if (files.Length == 0) return;
            if (!DeviceAuth.IsRegistered) { Debug.LogWarning("[Api] 기기 토큰이 없어 보내지 못함"); return; }
            var last = await Outbox.Flush(DeviceAuth.Token);
            Debug.Log($"[Api] 발신함 처리 — 마지막 결과 {last} · 남은 {Outbox.Count}건");
        }

        [System.Serializable]
        class TestConfig
        {
            public string test_username;
            public string test_password;
            public string test_device; // D3 까지 시험할 기기 이름 (비우면 D2 까지만 · 사용 중인 기기는 빼앗지 않는다)
        }

        /// 기초 테스트 — persistentDataPath/api_config.json 의 test_username · test_password(· test_device)로
        /// D1 로그인 → D2 기기 목록 → (test_device 가 있고 사용 중이 아니면 D3) → 기기 토큰이 있으면 A1 · B2 · 명령 · B1 까지 실제 호출
        [MenuItem("Shinmyeong/API/기초 테스트 (D1→D2→[D3]→A1·B2·명령·B1)")]
        static async void BasicTest()
        {
            var log = new System.Text.StringBuilder("[Api] 기초 테스트 — " + ApiConfig.BaseUrl + "\n");
            TestConfig cfg = null;
            try { if (File.Exists(ApiConfig.ConfigPath)) cfg = JsonUtility.FromJson<TestConfig>(File.ReadAllText(ApiConfig.ConfigPath)); } catch { }
            if (cfg == null || string.IsNullOrEmpty(cfg.test_username))
            {
                Debug.LogWarning($"[Api] {ApiConfig.ConfigPath} 에 test_username · test_password 를 넣어 주세요");
                return;
            }

            // D1
            Debug.Log($"[Api] 기초 테스트 시작 — {ApiConfig.BaseUrl} · 계정 {cfg.test_username}");
            var login = await DeviceAuth.Login(cfg.test_username, cfg.test_password);
            Debug.Log($"[Api] D1 응답 {login}");
            log.AppendLine($"D1 로그인: {login} — 운영자 {DeviceAuth.OperatorName} · 기관 {DeviceAuth.OrganizationName}");
            if (!login.Ok) { Debug.LogWarning(log.ToString()); return; }

            // D2
            var devices = await DeviceAuth.LoadDevices();
            log.AppendLine($"D2 기기 목록: {devices} — {DeviceAuth.LastDevices.Count}대");
            foreach (var d in DeviceAuth.LastDevices)
                log.AppendLine($"   · {d.Name} ({d.Id}) {(d.InUse ? "사용 중" : "비어 있음")} · 마지막 보고 {d.LastSeenAt ?? "-"} · v{d.AppVersion ?? "-"}");
            if (!devices.Ok) { Debug.LogWarning(log.ToString()); return; }

            // D3 — 이름이 맞고 비어 있는 기기만 (다른 PC 의 기기를 빼앗지 않는다)
            if (!DeviceAuth.IsRegistered && !string.IsNullOrEmpty(cfg.test_device))
            {
                int idx = -1;
                for (int i = 0; i < DeviceAuth.LastDevices.Count; i++)
                    if (DeviceAuth.LastDevices[i].Name == cfg.test_device) idx = i;
                if (idx < 0) log.AppendLine($"D3 건너뜀: 「{cfg.test_device}」 기기가 목록에 없음");
                else if (DeviceAuth.LastDevices[idx].InUse) log.AppendLine($"D3 건너뜀: 「{cfg.test_device}」 은 다른 PC 에서 사용 중 (replace 는 화면에서 확인 후)");
                else
                {
                    var tok = await DeviceAuth.SelectDevice(DeviceAuth.LastDevices[idx]);
                    log.AppendLine($"D3 기기 토큰: {tok} — {(tok.Ok ? "저장됨 (" + DeviceAuth.DeviceName + ")" : tok.Message)}");
                }
            }
            if (!DeviceAuth.IsRegistered)
            {
                log.AppendLine("기기 토큰 없음 — A1·B2·명령·B1 은 기기 선택 뒤에 (api_config.json 의 test_device 또는 플레이 모드 화면)");
                Debug.Log(log.ToString());
                return;
            }
            string token = DeviceAuth.Token;
            log.AppendLine($"연결된 기기: {DeviceAuth.DeviceName} ({DeviceAuth.DeviceId})");

            // A1
            var parts = await ApiClient.Get<ParticipantsResponse>("participants", token);
            log.AppendLine($"A1 사용자 목록: {parts} — {(parts.Ok ? parts.Data.participants.Count + "명" : parts.Message)}");
            if (parts.Ok)
                foreach (var p in parts.Data.participants)
                    log.AppendLine($"   · {p.name} {p.gender} 그림{p.avatar_index} 색{p.card_color_index} ({p.id})");

            // B2
            var set = await ApiClient.Get<DeviceSettingsResponse>("devices/me/settings", token);
            log.AppendLine($"B2 설정: {set} — {(set.Ok ? $"소리 {set.Data.volume_level} · 음성 {set.Data.voice_enabled} · 기기 {set.Data.device?.name}" : set.Message)}");

            // 명령
            var cmd = await ApiClient.Get<CommandsResponse>("devices/me/commands", token);
            log.AppendLine($"명령: {cmd} — shutdown_requested_at {(cmd.Ok ? (string.IsNullOrEmpty(cmd.Data.shutdown_requested_at) ? "없음" : cmd.Data.shutdown_requested_at) : cmd.Message)}");

            // B1
            var status = new DeviceStatusRequest
            {
                camera_connected = false, person_detected = false, game_state = "editor-test",
                app_version = ApiConfig.AppVersion, storage_free_bytes = FreeBytes(), device_time = ApiConfig.NowIso(),
            };
            var st = await ApiClient.Put<OkResponse>("devices/me/status", status, token);
            log.AppendLine($"B1 상태 보고: {st}");

            if (login.Ok && devices.Ok && parts.Ok && set.Ok && cmd.Ok && st.Ok) Debug.Log(log.ToString());
            else Debug.LogWarning(log.ToString());
        }

        static long FreeBytes()
        {
            try { return new DriveInfo(Path.GetPathRoot(Application.persistentDataPath)).AvailableFreeSpace; }
            catch { return 0; }
        }

        /// 서버 주소 확인 — 토큰 없이 부를 수 있는 경로에 GET 을 보내 405(경로 있음 · 메서드 다름) JSON 봉투가 오면 연결·주소가 맞다
        [MenuItem("Shinmyeong/API/서버 연결 확인 (GET auth/login → 405 기대)")]
        static async void CheckServer()
        {
            Debug.Log($"[Api] 주소: {ApiConfig.BaseUrl} (설정 파일 {ApiConfig.ConfigPath})");
            var res = await ApiClient.Get<OkResponse>("auth/login");
            if (res.Status == 405)
                Debug.Log($"[Api] 연결 OK — 405 '{res.Message}' (명세대로 JSON 봉투)");
            else if (res.Unreachable)
                Debug.LogWarning($"[Api] 서버에 닿지 못함 — {res.Message}");
            else
                Debug.LogWarning($"[Api] 예상과 다른 응답 {res.Status} '{res.Message}'\n{res.Raw}");
        }
    }
}
