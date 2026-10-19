using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using Shinmyeong.Save;

namespace Shinmyeong.Net
{
    /// 참여 기록 발신함 — A2 본문을 파일로 두고 보낼 수 있을 때 보낸다 (명세 3-3 · Docs/94 3장 4단계).
    /// persistentDataPath/Outbox/<시각>_<SessionId>.json (본문 JSON 그대로 · 재전송도 같은 본문 → 같은 `id` 로 멱등).
    /// 결과별: 201/200 → 지움 · 400/404/409 → rejected/ 로 옮겨 보관(재시도 금지) · 401 → 멈춤(토큰 거부) · 네트워크/5xx/429 → 멈추고 다음 회차에.
    /// 400 이 `started_at` 미래 때문이고 응답에 `server_time` 이 있으면(2026-10-19 서버 반영) 시계 차이만큼 보정해 한 번 다시 보낸다.
    public static class Outbox
    {
        static string Dir => Path.Combine(Application.persistentDataPath, "Outbox");
        static string RejectedDir => Path.Combine(Dir, "rejected");
        static readonly Regex StartedAtRx = new Regex("\"started_at\"\\s*:\\s*\"([^\"]*)\"");
        static readonly HashSet<string> _clockFixed = new HashSet<string>(); // 이번 실행에서 이미 보정한 파일 — 무한 반복 방지

        public static int Count
        {
            get { try { return Directory.Exists(Dir) ? Directory.GetFiles(Dir, "*.json").Length : 0; } catch { return 0; } }
        }

        /// 보낼 파일 목록 (오래된 것부터)
        public static string[] Pending()
        {
            try
            {
                if (!Directory.Exists(Dir)) return Array.Empty<string>();
                var files = Directory.GetFiles(Dir, "*.json");
                Array.Sort(files, StringComparer.Ordinal);
                return files;
            }
            catch { return Array.Empty<string>(); }
        }

        /// 완주한 기록을 발신함에 넣는다 (FlowManager.CommitPlay). 변환이 안 되는 기록(라운드 수 등)은 경고만 — 보내지 않는다
        public static bool Enqueue(PlayRecord record, string participantId)
        {
            string json = PlaySessionMapper.ToJson(record, participantId);
            if (json == null)
            {
                Debug.LogWarning($"[Outbox] 변환 실패 — {record.Activity} {record.StartedAt} 기록을 보내지 않음");
                return false;
            }
            try
            {
                Directory.CreateDirectory(Dir);
                string path = Path.Combine(Dir, $"{DateTime.Now:yyyyMMddHHmmss}_{record.SessionId}.json");
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                Debug.Log($"[Outbox] 기록 보관 → {Path.GetFileName(path)} (대기 {Count}건)");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Outbox] 보관 실패: {e.Message}");
                return false;
            }
        }

        /// 대기 중인 기록을 오래된 것부터 보낸다. 마지막으로 처리한 결과를 돌려준다(없으면 null).
        /// 401 · 네트워크 · 5xx · 429 를 만나면 그 자리에서 멈춘다(순서 보존 · 다음 회차에 같은 파일부터)
        public static async Awaitable<UploadOutcome?> Flush(string deviceToken)
        {
            UploadOutcome? last = null;
            foreach (var path in Pending())
            {
                string json;
                try { json = File.ReadAllText(path); }
                catch (Exception e) { Debug.LogWarning($"[Outbox] 읽기 실패 {Path.GetFileName(path)}: {e.Message}"); continue; }

                var (outcome, res) = await PlaySessionUploader.UploadJson(json, deviceToken);
                last = outcome;

                if (outcome == UploadOutcome.Rejected && res.Status == 400 && !string.IsNullOrEmpty(res.ServerTime)
                    && MentionsStartedAt(res.Issues) && !_clockFixed.Contains(path))
                {
                    // 이 PC 시계가 서버보다 앞섬 — 차이만큼 started_at 을 당겨 한 번만 다시 보낸다
                    string fixedJson = ShiftStartedAt(json, res.ServerTime);
                    _clockFixed.Add(path);
                    if (fixedJson != null)
                    {
                        Debug.LogWarning($"[Outbox] started_at 시계 보정 후 재전송 — 서버 {res.ServerTime}");
                        try { File.WriteAllText(path, fixedJson); } catch { }
                        (outcome, res) = await PlaySessionUploader.UploadJson(fixedJson, deviceToken);
                        last = outcome;
                    }
                }

                switch (outcome)
                {
                    case UploadOutcome.Stored:
                    case UploadOutcome.Duplicate:
                        TryDelete(path);
                        break;
                    case UploadOutcome.Rejected:
                        MoveToRejected(path, res);
                        break;
                    default: // Unauthorized · Retry — 멈춤
                        return last;
                }
            }
            return last;
        }

        static bool MentionsStartedAt(string[] issues)
        {
            if (issues == null) return false;
            foreach (var i in issues) if (i != null && i.Contains("started_at")) return true;
            return false;
        }

        static string ShiftStartedAt(string json, string serverTimeIso)
        {
            var m = StartedAtRx.Match(json);
            if (!m.Success) return null;
            if (!DateTimeOffset.TryParse(m.Groups[1].Value, null, System.Globalization.DateTimeStyles.None, out var started)) return null;
            if (!DateTimeOffset.TryParse(serverTimeIso, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var server)) return null;
            var offset = server - DateTimeOffset.Now; // 음수면 이 PC 가 앞선 것
            var corrected = started + offset;
            return json.Substring(0, m.Groups[1].Index) + ApiConfig.ToIso(corrected) + json.Substring(m.Groups[1].Index + m.Groups[1].Length);
        }

        static void TryDelete(string path)
        {
            try { File.Delete(path); } catch (Exception e) { Debug.LogWarning($"[Outbox] 삭제 실패 {Path.GetFileName(path)}: {e.Message}"); }
        }

        static void MoveToRejected(string path, ApiResult<PlaySessionResponse> res)
        {
            try
            {
                Directory.CreateDirectory(RejectedDir);
                string dest = Path.Combine(RejectedDir, Path.GetFileName(path));
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(path, dest);
                File.WriteAllText(dest + ".reason.txt", $"{res.Status} {res.Message}\n{(res.Issues != null ? string.Join("\n", res.Issues) : "")}");
                Debug.LogWarning($"[Outbox] 서버가 거절한 기록 보관 → rejected/{Path.GetFileName(path)} ({res.Status} {res.Message})");
            }
            catch (Exception e) { Debug.LogWarning($"[Outbox] rejected 이동 실패: {e.Message}"); }
        }
    }
}
