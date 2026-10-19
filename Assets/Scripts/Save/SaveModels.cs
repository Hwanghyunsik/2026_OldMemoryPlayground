using System;
using System.Collections.Generic;

namespace Shinmyeong.Save
{
    /// 데이터 모델 (JsonUtility 직렬화). 2026-10-19 부터 원본은 서버 — 사용자는 A1 캐시(`ParticipantStore`), 참여 기록은 발신함(`Net/Outbox`)을 거쳐 A2.
    /// 참여 항목: 날짜·활동·모드·소요 시간·라운드별 기록. 저장하지 않는 것(6-2): 카메라 원본 · 게임 중간 진행 상태 — 중도 종료한 판은 기록을 남기지 않는다.
    /// 지표(FN-22 정확하게 선택·반응 시간·다른 선택·도움)는 게임이 저장하지 않고 서버가 이 원시 데이터에서 계산한다.

    [Serializable]
    public class UserProfile
    {
        public string Id;
        public string Name;
        public string Gender;      // 통계 전용 · 조회 조건으로 쓰지 않음 (FN-20)
        public int AvatarIndex;    // 그림 8종 (0~7)
        public int CardColorIndex; // 카드 배경색
        public string CreatedAt;   // (서버 목록에는 없음 · 비움)
    }

    /// A1 사용자 목록 캐시 파일 모양 (participants_cache.json)
    [Serializable]
    public class UserDatabase
    {
        public int Version = 1;
        public List<UserProfile> Users = new List<UserProfile>();
    }

    /// 1회 참여(10라운드) 기록 — FN-22 지표 계산의 단위
    [Serializable]
    public class PlayRecord
    {
        public string SessionId;  // 게임이 만든 UUID — 서버 A2 멱등 키(같은 값으로 재전송) · 2026-10-19 추가
        public string StoryRunId; // 스토리 3종(수확→장보기→요리)을 묶는 UUID · 개별은 "" · FlowManager.CommitPlay 가 채움
        public string Activity;   // Harvest | Shopping | Cooking
        public string Mode;       // Story | Free — 저장(커밋) 시점에 FlowManager가 채운다
        public string StartedAt;  // yyyy-MM-dd HH:mm:ss
        public float DurationSec; // 전체 활동 시간
        public float PausedSec;   // 일시정지 누적 — 반응 시간에서 제외(FN-22) · POP-001 구현 시 채움
        public int SuccessCount;  // 성공 라운드 수 · 힌트 후 성공 포함(확정) — 게임별 정의는 9-1 확정값으로 재검토
        public int HintCount;     // 도움 받은 횟수 합

        // 수확하기
        public List<HarvestRoundRecord> HarvestRounds = new List<HarvestRoundRecord>();

        // 장보기
        public List<ShoppingItemRecord> ShoppingItems = new List<ShoppingItemRecord>(); // 재료별 구매 판정 (~20건)
        public List<float> ShoppingRoundSecs = new List<float>();                       // 라운드 수행 시간
        public List<int> ShoppingRoundHints = new List<int>();                          // 라운드별 중앙 복귀 유도 힌트 수 — 재료 힌트와 별도 (2026-10-19 추가 · 서버 rounds[].hint_count)
        public int MoveLeftCount;                                                        // 좌우 이동 방향별 횟수
        public int MoveRightCount;

        // 요리하기
        public List<CookingRoundRecord> CookingRounds = new List<CookingRoundRecord>();

        public static PlayRecord Start(string activity) => new PlayRecord
        {
            SessionId = Guid.NewGuid().ToString(),
            Activity = activity,
            StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        };
    }

    [Serializable]
    public class HarvestRoundRecord
    {
        public int Round;
        public string Target;     // 그 라운드의 목표 작물 — 「목표 / 고른 것」 비교용 (2026-10-07 추가)
        public string Picked;     // 선택 작물
        public string Result;     // 정답 | 다른 선택 | 벌레
        public float ReactionSec; // 목표 강조 종료(t=0)부터 당김 확정까지
        public int HintCount;
        public float RoundSec;    // 라운드 시작부터 당김 확정까지 — 서버 round_sec(명세 4-8 요청 2) · 2026-10-19 추가
    }

    [Serializable]
    public class ShoppingItemRecord
    {
        public int Round;
        public string TargetItem;
        public string TargetZone; // L2 | L1 | C | R1 | R2
        public string BoughtItem;
        public string BoughtZone;
        public bool Correct;      // 목표 점포 그대로 구매 여부 — 다르게 산 것도 실패가 아니다(확정)
        public float ArriveSec;   // 점등부터 도착 판정까지
        public int HintCount;
    }

    [Serializable]
    public class CookingPick
    {
        public string Item;
        public bool Correct;
    }

    [Serializable]
    public class CookingRoundRecord
    {
        public int Round;
        public string Food;
        public List<string> Answers = new List<string>(); // 기록 시점의 레시피 재료 — 레시피가 바뀌어도 옛 기록 해석 가능 (2026-10-07 추가)
        public List<CookingPick> Picks = new List<CookingPick>();
        public float RoundSec;    // 최초 팝업 닫힘부터 슬롯 완성까지
        public float ReactionSec; // 최초 팝업 닫힘부터 첫 재료 선택까지 — 반응 시간 끝점(18-4 확정)
        public int ReviewCount;   // 차림표 다시 보기 (내부 데이터로만 기록 · 확정)
        public int HintCount;
    }
}
