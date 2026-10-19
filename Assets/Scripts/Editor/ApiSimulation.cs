using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Shinmyeong.Net;
using Shinmyeong.Save;

namespace Shinmyeong.EditorTools
{
    /// 게임 시뮬레이션 — 연결된 기기 토큰으로 서버의 사용자(A1)마다 게임 3종 참여 기록을 만들어 A2 로 보내고(스토리 1판 + 개별 4판 · 최근 7일),
    /// 상태 보고(B1)로 게임 진행 상태를 흉내 낸 뒤, A3 로 되읽어 관리자 화면과 대조할 수 있게 콘솔에 요약을 남긴다.
    /// 기록 값은 게임 규칙(04 구성표 · 라운드 10 · 장보기 목표 1~3개 · 레시피 10종)대로 만들고 실제 게임과 같은 변환기(PlaySessionMapper)를 거친다.
    public static class ApiSimulation
    {
        static readonly string[] Crops = { "호박", "오이", "가지", "토마토", "고추", "대추", "감", "밤" };

        // 장보기 점포 (ShoppingDemo.StoreDefs) — zone 0 L2 · 1 L1 · 3 R1 · 4 R2
        static readonly (string zone, string[] pool)[] Stores =
        {
            ("L2", new[] { "쌀가루", "밀가루", "팥", "깨", "밤" }),
            ("L1", new[] { "호박", "시금치", "당근", "파", "버섯" }),
            ("R1", new[] { "소고기", "갈비", "달걀", "두부" }),
            ("R2", new[] { "미역", "김", "김치", "간장", "당면", "곶감" }),
        };

        static readonly (string food, string[] answers)[] Recipes =
        {
            ("두부조림", new[] { "두부" }), ("수정과", new[] { "곶감" }), ("김밥", new[] { "김", "밥" }), ("호박죽", new[] { "호박", "팥" }),
            ("미역국", new[] { "미역", "소고기" }), ("김치전", new[] { "김치", "밀가루", "달걀" }), ("잡채", new[] { "당면", "시금치", "당근" }),
            ("산적", new[] { "소고기", "파", "버섯" }), ("송편", new[] { "쌀가루", "깨", "솔잎" }), ("갈비찜", new[] { "갈비", "간장", "밤" }),
        };
        static readonly string[] AllIngredients =
        {
            "두부", "곶감", "김", "밥", "호박", "팥", "미역", "소고기", "김치", "밀가루", "달걀", "당면", "시금치", "당근", "파", "버섯", "쌀가루", "깨", "솔잎", "갈비", "간장", "밤",
        };

        [MenuItem("Shinmyeong/API/게임 시뮬레이션 (사용자별 기록 7건 A2 전송 → A3 확인)")]
        static async void Run()
        {
            if (!DeviceAuth.IsRegistered)
            {
                Debug.LogWarning("[Api] 기기 토큰이 없습니다 — 먼저 로그인 → 기기 선택(또는 기초 테스트의 test_device)");
                return;
            }
            string token = DeviceAuth.Token;
            var log = new StringBuilder($"[Api] 게임 시뮬레이션 — {ApiConfig.BaseUrl} · 기기 {DeviceAuth.DeviceName}\n");

            var parts = await ApiClient.Get<ParticipantsResponse>("participants", token);
            if (!parts.Ok) { Debug.LogWarning(log + $"A1 실패 {parts}"); return; }
            log.AppendLine($"A1 사용자 {parts.Data.participants.Count}명");

            var counts = new Dictionary<UploadOutcome, int>();
            foreach (UploadOutcome o in Enum.GetValues(typeof(UploadOutcome))) counts[o] = 0;
            string lastJson = null;
            int mismatch = 0; // 서버 재계산(success_count · hint_count)이 게임 값과 다른 건수 — 97 회신 「주의 3」 확인용

            await Status(token, "SCR_003", false);
            int pIndex = 0;
            foreach (var p in parts.Data.participants)
            {
                var rng = new System.Random(p.id.GetHashCode());
                float skill = 0.6f + 0.3f * pIndex / Math.Max(1, parts.Data.participants.Count - 1); // 사람마다 다른 수준 → 관리자 화면 비교용
                pIndex++;
                log.AppendLine($"── {p.name} ({p.id}) · 성공률 기준 {skill:P0}");

                // 스토리 1판 (2일 전) — 수확 → 장보기 → 요리, 같은 story_run_id
                string storyRun = Guid.NewGuid().ToString();
                var day = DateTime.Now.AddDays(-2).Date.AddHours(10).AddMinutes(rng.Next(0, 50));
                var plan = new List<(PlayRecord rec, string state)>
                {
                    (Harvest(rng, skill, day, "Story", storyRun), "SCR_008"),
                    (Shopping(rng, skill, day.AddMinutes(4), "Story", storyRun), "SCR_013"),
                    (Cooking(rng, skill, day.AddMinutes(9), "Story", storyRun), "SCR_018"),
                    // 개별 4판 — 최근 7일에 흩어 놓는다
                    (Harvest(rng, skill - 0.1f, DateTime.Now.AddDays(-6).Date.AddHours(14), "Free", null), "SCR_008"),
                    (Shopping(rng, skill, DateTime.Now.AddDays(-4).Date.AddHours(11), "Free", null), "SCR_013"),
                    (Harvest(rng, skill + 0.1f, DateTime.Now.AddDays(-1).Date.AddHours(15), "Free", null), "SCR_008"),
                    (Cooking(rng, skill + 0.05f, DateTime.Now.AddMinutes(-30), "Free", null), "SCR_018"),
                };

                foreach (var (rec, state) in plan)
                {
                    await Status(token, state, true);
                    string json = PlaySessionMapper.ToJson(rec, p.id);
                    var (outcome, res) = await PlaySessionUploader.UploadJson(json, token);
                    counts[outcome]++;
                    lastJson = json;
                    string serverSum = res.Ok ? $"서버 성공 {res.Data.success_count} · 도움 {res.Data.hint_count}" : $"{res.Status} {res.Message}";
                    log.AppendLine($"   A2 {rec.Activity,-8} {rec.Mode,-5} {rec.StartedAt} 게임 성공 {rec.SuccessCount}/10 도움 {rec.HintCount} → {outcome} · {serverSum}");
                    if (res.Issues != null) foreach (var i in res.Issues) log.AppendLine("      ! " + i);
                    if (res.Ok && (res.Data.success_count != rec.SuccessCount || res.Data.hint_count != rec.HintCount))
                    {
                        mismatch++;
                        log.AppendLine($"      ! 서버 재계산 불일치 — 성공 {rec.SuccessCount}→{res.Data.success_count} · 도움 {rec.HintCount}→{res.Data.hint_count}");
                    }
                    if (res.Ok && res.Data.clock_suspect) log.AppendLine($"      ! clock_suspect · server_time {res.Data.server_time}");
                }
            }

            // 멱등 확인 — 마지막 본문을 그대로 한 번 더 (200 duplicate 기대)
            if (lastJson != null)
            {
                var (o2, r2) = await PlaySessionUploader.UploadJson(lastJson, token);
                log.AppendLine($"재전송(멱등): {o2} · {r2.Status} duplicate={(r2.Ok && r2.Data.duplicate)}");
            }
            await Status(token, "SCR_001", false);

            log.AppendLine($"결과: 저장 {counts[UploadOutcome.Stored]} · 중복 {counts[UploadOutcome.Duplicate]} · 거절 {counts[UploadOutcome.Rejected]} · 인증 {counts[UploadOutcome.Unauthorized]} · 재시도 {counts[UploadOutcome.Retry]} · 서버 재계산 불일치 {mismatch}");
            if (counts[UploadOutcome.Rejected] + counts[UploadOutcome.Unauthorized] + counts[UploadOutcome.Retry] + mismatch == 0) Debug.Log(log.ToString());
            else Debug.LogWarning(log.ToString());

            // A3 되읽기 — 사용자별로 따로 로그 (콘솔 한 항목이 길면 잘린다)
            await ReadBackAll(token, parts.Data.participants);
        }

        /// A3 만 — 서버에 쌓인 내 기록 요약·목록을 사용자별로 출력 (관리자 화면 ADM-003 과 대조)
        [MenuItem("Shinmyeong/API/A3 되읽기 (사용자별 내 기록 요약)")]
        static async void ReadBack()
        {
            if (!DeviceAuth.IsRegistered) { Debug.LogWarning("[Api] 기기 토큰이 없습니다"); return; }
            var parts = await ApiClient.Get<ParticipantsResponse>("participants", DeviceAuth.Token);
            if (!parts.Ok) { Debug.LogWarning($"[Api] A1 실패 {parts}"); return; }
            await ReadBackAll(DeviceAuth.Token, parts.Data.participants);
        }

        static async Awaitable ReadBackAll(string token, List<ParticipantDto> participants)
        {
            foreach (var p in participants)
            {
                // 전체 (한 번에 확인) + 활동별 최근 5건 (게임 SCR-023 이 실제로 쓸 호출 · 2026-10-19 서버 반영)
                var my = await ApiClient.Get<MyRecordsResponse>($"participants/{p.id}/play-sessions?limit=50", token);
                if (!my.Ok) { Debug.LogWarning($"[Api] A3 {p.name}: 실패 {my}"); continue; }
                var s = my.Data.summary;
                var sb = new StringBuilder($"[Api] A3 {p.name}: 참여 {s.count}회 · 함께한 날 {s.days}일 · 활동 {s.total_sec:F0}초 · 마지막 {s.last_played_at} · 목록 {my.Data.sessions.Count}건 · activity={(string.IsNullOrEmpty(my.Data.activity) ? "(전체)" : my.Data.activity)}\n");
                foreach (var ss in my.Data.sessions)
                    sb.AppendLine($"   {Line(ss)}");
                foreach (var act in new[] { "harvest", "shopping", "cooking" })
                {
                    var byAct = await ApiClient.Get<MyRecordsResponse>($"participants/{p.id}/play-sessions?activity={act}&limit=5", token);
                    if (!byAct.Ok) { sb.AppendLine($"   ?activity={act}: 실패 {byAct}"); continue; }
                    sb.AppendLine($"   ?activity={act}&limit=5 → activity={byAct.Data.activity} · {byAct.Data.sessions.Count}건 · summary 참여 {byAct.Data.summary.count}회(전체 기준이어야 함)");
                    foreach (var ss in byAct.Data.sessions)
                        if (ss.activity != act) sb.AppendLine($"      ! 다른 활동이 섞임: {Line(ss)}");
                        else sb.AppendLine($"      {Line(ss)}");
                }
                Debug.Log(sb.ToString());
            }

            string Line(PlaySessionSummaryDto ss) =>
                $"{ss.started_at} {ss.activity,-8} {ss.mode,-5} 성공 {ss.success_count} 정확 {ss.accuracy_count} 반응 {ss.avg_reaction_sec:F1}초 도움 {ss.hint_count} {ss.duration_sec:F0}초 story={(string.IsNullOrEmpty(ss.story_run_id) ? "-" : ss.story_run_id.Substring(0, 8))}";
        }

        static async Awaitable Status(string token, string state, bool person)
        {
            var body = new DeviceStatusRequest
            {
                camera_connected = true, person_detected = person, game_state = state,
                app_version = ApiConfig.AppVersion, device_time = ApiConfig.NowIso(),
                storage_free_bytes = 50L * 1024 * 1024 * 1024,
            };
            await ApiClient.Put<OkResponse>("devices/me/status", body, token);
        }

        // ---- 기록 생성 (게임 규칙대로) ----

        static PlayRecord Base(string activity, DateTime startedAt, string mode, string storyRun)
        {
            var r = PlayRecord.Start(activity);
            r.StartedAt = startedAt.ToString("yyyy-MM-dd HH:mm:ss");
            r.Mode = mode;
            r.StoryRunId = storyRun ?? "";
            return r;
        }

        static float Rand(System.Random rng, float min, float max) => (float)(min + rng.NextDouble() * (max - min));

        static PlayRecord Harvest(System.Random rng, float skill, DateTime at, string mode, string storyRun)
        {
            var r = Base("Harvest", at, mode, storyRun);
            float total = 0f;
            for (int round = 1; round <= 10; round++)
            {
                string target = Crops[rng.Next(Crops.Length)];
                float reaction = Rand(rng, 2f, 9f);
                int hint = reaction > 6.5f ? 1 : 0; // 자동 힌트 주기 뒤에 당김
                bool ok = rng.NextDouble() < skill;
                bool bug = !ok && round >= 4 && rng.NextDouble() < 0.4;
                string picked = ok ? target : bug ? target : Crops[(Array.IndexOf(Crops, target) + 1 + rng.Next(Crops.Length - 1)) % Crops.Length];
                string result = ok ? "정답" : bug ? "벌레" : "다른 선택";
                float roundSec = reaction + 1.0f;
                r.HarvestRounds.Add(new HarvestRoundRecord { Round = round, Target = target, Picked = picked, Result = result, ReactionSec = reaction, HintCount = hint, RoundSec = roundSec });
                r.HintCount += hint;
                if (ok) r.SuccessCount++;
                total += roundSec + 2.5f; // 담김 연출 · 다음 라운드 강조
            }
            r.DurationSec = total;
            return r;
        }

        static PlayRecord Shopping(System.Random rng, float skill, DateTime at, string mode, string storyRun)
        {
            var r = Base("Shopping", at, mode, storyRun);
            float total = 0f;
            int lastZone = 2; // 중앙
            for (int round = 1; round <= 10; round++)
            {
                int count = round <= 2 ? 1 : round <= 7 ? 2 : 3; // ShoppingDemo.TargetCount
                bool allOk = true;
                float roundSec = 0f;
                int returnHints = 0;
                for (int i = 0; i < count; i++)
                {
                    int ti = rng.Next(Stores.Length);
                    var (zone, pool) = Stores[ti];
                    string item = pool[rng.Next(pool.Length)];
                    bool ok = rng.NextDouble() < skill;
                    int bi = ok ? ti : (ti + 1 + rng.Next(Stores.Length - 1)) % Stores.Length;
                    string bought = ok ? item : Stores[bi].pool[rng.Next(Stores[bi].pool.Length)];
                    float arrive = Rand(rng, 2f, 8f);
                    int hint = arrive > 6.5f ? 1 : 0;
                    r.ShoppingItems.Add(new ShoppingItemRecord
                    {
                        Round = round, TargetItem = item, TargetZone = zone, BoughtItem = bought, BoughtZone = Stores[bi].zone,
                        Correct = ok, ArriveSec = arrive, HintCount = hint,
                    });
                    r.HintCount += hint;
                    allOk &= ok;
                    int zoneIdx = bi >= 2 ? bi + 1 : bi; // 0,1,3,4
                    if (zoneIdx > lastZone) r.MoveRightCount++; else r.MoveLeftCount++;
                    if (zoneIdx > 2) r.MoveLeftCount++; else r.MoveRightCount++; // 중앙 복귀
                    lastZone = 2;
                    float back = Rand(rng, 2f, 6f);
                    if (back > 5.5f) { returnHints++; r.HintCount++; } // 복귀 유도 힌트 (라운드 수준)
                    roundSec += arrive + back + 1.5f;
                }
                r.ShoppingRoundSecs.Add(roundSec);
                r.ShoppingRoundHints.Add(returnHints);
                if (allOk) r.SuccessCount++;
                total += roundSec + 1f;
            }
            r.DurationSec = total;
            return r;
        }

        static PlayRecord Cooking(System.Random rng, float skill, DateTime at, string mode, string storyRun)
        {
            var r = Base("Cooking", at, mode, storyRun);
            float total = 0f;
            for (int round = 1; round <= 10; round++)
            {
                var (food, answers) = Recipes[round - 1];
                var rec = new CookingRoundRecord { Round = round, Food = food, Answers = new List<string>(answers) };
                bool allOk = true;
                float t = 0f;
                for (int i = 0; i < answers.Length; i++)
                {
                    bool ok = rng.NextDouble() < skill;
                    string item = answers[i];
                    if (!ok)
                    {
                        do item = AllIngredients[rng.Next(AllIngredients.Length)]; while (Array.IndexOf(answers, item) >= 0);
                    }
                    rec.Picks.Add(new CookingPick { Item = item, Correct = ok });
                    allOk &= ok;
                    float dwell = Rand(rng, 2.5f, 7f);
                    if (i == 0) rec.ReactionSec = dwell;
                    t += dwell;
                }
                rec.ReviewCount = rng.NextDouble() < 0.25 ? 1 : 0;
                rec.HintCount = rec.ReactionSec > 6f ? 1 : 0;
                rec.RoundSec = t + rec.ReviewCount * 5f;
                r.CookingRounds.Add(rec);
                r.HintCount += rec.HintCount;
                if (allOk) r.SuccessCount++;
                total += rec.RoundSec + 6f; // 차림표 팝업 · 결과 연출
            }
            r.DurationSec = total;
            if (mode == "Free") r.PausedSec = 20f; // 일시정지 한 번 (paused_sec 전송 확인용)
            return r;
        }
    }
}
