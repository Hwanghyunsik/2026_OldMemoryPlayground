using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Shinmyeong.Save;

namespace Shinmyeong.Net
{
    /// 게임 기록(PlayRecord) → A2 `POST /api/play-sessions` 본문 (명세 1-8 · 활동별 rounds 모양).
    /// 값 변환: Activity/Mode 소문자 코드 · 수확 Result 한글 → target/other/bug · StartedAt(로컬 "yyyy-MM-dd HH:mm:ss") → 시간대 붙은 ISO ·
    /// 장보기 재료별 기록(평탄) → 라운드별 items(seq 1~3) · 요리 picks 에 seq 부여.
    public static class PlaySessionMapper
    {
        public const string LocalTimeFormat = "yyyy-MM-dd HH:mm:ss";

        public static string ActivityCode(string activity)
        {
            switch (activity)
            {
                case "Harvest": return "harvest";
                case "Shopping": return "shopping";
                case "Cooking": return "cooking";
                default: return (activity ?? "").ToLowerInvariant();
            }
        }

        public static string ModeCode(string mode) => mode == "Story" ? "story" : "free";

        public static string ResultCode(string result)
        {
            switch (result)
            {
                case "정답": return "target";
                case "벌레": return "bug";
                default: return "other"; // "다른 선택"
            }
        }

        /// 로컬 시각 문자열(시간대 없음)을 이 PC 시간대를 붙인 ISO 로. 읽지 못하면 지금 시각
        public static string ToIso(string localStartedAt)
        {
            if (DateTime.TryParseExact(localStartedAt, LocalTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local))
                return ApiConfig.ToIso(new DateTimeOffset(local));
            Debug.LogWarning($"[Api] StartedAt 해석 실패 '{localStartedAt}' — 지금 시각으로 대체");
            return ApiConfig.NowIso();
        }

        /// 본문 JSON. participantId 는 A1 의 id(서버 ObjectId) — 로컬 사용자 Id 가 아니다
        public static string ToJson(PlayRecord r, string participantId)
        {
            switch (r.Activity)
            {
                case "Harvest": return JsonUtility.ToJson(Harvest(r, participantId));
                case "Shopping": return JsonUtility.ToJson(Shopping(r, participantId));
                case "Cooking": return JsonUtility.ToJson(Cooking(r, participantId));
                default:
                    Debug.LogError($"[Api] 알 수 없는 활동 '{r.Activity}' — A2 본문을 만들 수 없음");
                    return null;
            }
        }

        static void FillBase(PlaySessionBase b, PlayRecord r, string participantId)
        {
            b.id = string.IsNullOrEmpty(r.SessionId) ? Guid.NewGuid().ToString() : r.SessionId;
            b.participant_id = participantId;
            b.activity = ActivityCode(r.Activity);
            b.mode = ModeCode(r.Mode);
            b.story_run_id = b.mode == "story" ? (r.StoryRunId ?? "") : "";
            b.started_at = ToIso(r.StartedAt);
            b.duration_sec = r.DurationSec;
            b.paused_sec = r.PausedSec;
            b.success_count = r.SuccessCount;
            b.hint_count = r.HintCount;
            b.app_version = ApiConfig.AppVersion;
        }

        public static HarvestSessionBody Harvest(PlayRecord r, string participantId)
        {
            var b = new HarvestSessionBody();
            FillBase(b, r, participantId);
            foreach (var h in r.HarvestRounds)
                b.rounds.Add(new HarvestRoundDto
                {
                    round_no = h.Round,
                    target_item = h.Target ?? "",
                    picked_item = h.Picked ?? "",
                    result = ResultCode(h.Result),
                    reaction_sec = h.ReactionSec,
                    hint_count = h.HintCount,
                    round_sec = h.RoundSec,
                });
            WarnRounds(b.rounds.Count, r);
            return b;
        }

        public static ShoppingSessionBody Shopping(PlayRecord r, string participantId)
        {
            var b = new ShoppingSessionBody();
            FillBase(b, r, participantId);
            b.move_left_count = r.MoveLeftCount;
            b.move_right_count = r.MoveRightCount;

            var byRound = new SortedDictionary<int, ShoppingRoundDto>();
            foreach (var it in r.ShoppingItems)
            {
                if (!byRound.TryGetValue(it.Round, out var round))
                {
                    round = new ShoppingRoundDto
                    {
                        round_no = it.Round,
                        round_sec = it.Round >= 1 && it.Round - 1 < r.ShoppingRoundSecs.Count ? r.ShoppingRoundSecs[it.Round - 1] : 0f,
                        hint_count = it.Round >= 1 && it.Round - 1 < r.ShoppingRoundHints.Count ? r.ShoppingRoundHints[it.Round - 1] : 0,
                    };
                    byRound[it.Round] = round;
                }
                round.items.Add(new ShoppingItemDto
                {
                    seq = round.items.Count + 1,
                    target_item = it.TargetItem ?? "",
                    target_zone = it.TargetZone ?? "",
                    bought_item = it.BoughtItem ?? "",
                    bought_zone = it.BoughtZone ?? "",
                    is_matched = it.Correct,
                    arrive_sec = it.ArriveSec,
                    hint_count = it.HintCount,
                });
            }
            b.rounds.AddRange(byRound.Values);
            WarnRounds(b.rounds.Count, r);
            return b;
        }

        public static CookingSessionBody Cooking(PlayRecord r, string participantId)
        {
            var b = new CookingSessionBody();
            FillBase(b, r, participantId);
            foreach (var c in r.CookingRounds)
            {
                var dto = new CookingRoundDto
                {
                    round_no = c.Round,
                    food = c.Food ?? "",
                    round_sec = c.RoundSec,
                    reaction_sec = c.ReactionSec,
                    review_count = c.ReviewCount,
                    hint_count = c.HintCount,
                    answers = new List<string>(c.Answers),
                };
                for (int i = 0; i < c.Picks.Count; i++)
                    dto.picks.Add(new CookingPickDto { seq = i + 1, item = c.Picks[i].Item ?? "", is_matched = c.Picks[i].Correct });
                b.rounds.Add(dto);
            }
            WarnRounds(b.rounds.Count, r);
            return b;
        }

        static void WarnRounds(int count, PlayRecord r)
        {
            if (count != 10)
                Debug.LogWarning($"[Api] {r.Activity} 기록의 라운드가 {count}개 — 서버는 정확히 10개만 받는다 (400)");
        }
    }
}
