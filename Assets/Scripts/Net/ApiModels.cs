using System;
using System.Collections.Generic;

// 서버 API 요청·응답 모양 (Docs/신명놀이터_API.md 1장). JsonUtility 직렬화라 필드명은 명세의 snake_case 그대로 둔다.
// JsonUtility 한계: null 문자열은 "" 로 나간다(서버가 "" 를 null 로 봄 · 1-8) · bool 은 항상 나간다 · 다형 리스트 불가(활동별 본문 클래스 분리).
namespace Shinmyeong.Net
{
    /// 실패 응답 공통 봉투 `{ ok:false, message, issues }` + 일부 경로의 보조 플래그
    [Serializable]
    public class ApiErrorBody
    {
        public bool ok;
        public string message;
        public string[] issues;
        public bool in_use;       // D3 409
        public string server_time; // 400 — 서버 지금 시각 (2026-10-19 반영) · A2 started_at 이 7일 넘게 미래면 이 값으로 보정해 재전송
    }

    // ---- 설치 (D1~D3) ----

    [Serializable] public class LoginRequest { public string username; public string password; }

    [Serializable] public class OperatorDto { public string id; public string name; public string role; public string organization_id; }
    [Serializable] public class OrganizationDto { public string id; public string name; }

    /// D1 응답. `operator` 는 C# 예약어라 필드로 둘 수 없다 — JsonUtility 는 필드명 그대로만 매핑하므로
    /// 운영자 이름·역할이 필요해지면 `@operator` 필드명(C# 축자 식별자)으로 받는다. 지금은 토큰·기관만 쓴다
    [Serializable]
    public class LoginResponse
    {
        public bool ok;
        public string token;       // op.… 운영자 토큰 (30분 · 저장하지 않는다)
        public string expires_at;
        public OperatorDto @operator;
        public OrganizationDto organization;
    }

    [Serializable] public class DeviceDto { public string id; public string name; public bool in_use; public string last_seen_at; public string app_version; }
    [Serializable] public class DevicesResponse { public bool ok; public List<DeviceDto> devices = new List<DeviceDto>(); }

    [Serializable] public class DeviceTokenRequest { public bool replace; }
    [Serializable] public class DeviceBrief { public string id; public string name; }
    [Serializable] public class DeviceTokenResponse { public bool ok; public string token; public DeviceBrief device; public bool replaced; }

    // ---- 카드 (A1) ----

    [Serializable]
    public class ParticipantDto
    {
        public string id;
        public string name;
        public string gender;         // M | F
        public int avatar_index;      // 0~7
        public int card_color_index;  // 0~7
        public string device_id;
    }

    [Serializable] public class ParticipantsResponse { public bool ok; public List<ParticipantDto> participants = new List<ParticipantDto>(); }

    // ---- 기기 상태 · 설정 · 명령 (B1 · B2 · 명령) ----

    [Serializable]
    public class DeviceStatusRequest
    {
        public bool camera_connected;
        public bool person_detected;
        public string game_state;        // 표시용 ≤40자 (현재 화면 코드 등)
        public string app_version;
        public long storage_free_bytes;
        public string device_time;       // 시간대 붙은 ISO
    }

    [Serializable] public class OkResponse { public bool ok; }

    [Serializable]
    public class DeviceSettingsResponse
    {
        public bool ok;
        public DeviceBrief device;
        public int volume_level;     // 1~3
        public bool voice_enabled;
        public string updated_at;
    }

    [Serializable] public class CommandsResponse { public bool ok; public string shutdown_requested_at; }
    [Serializable] public class CommandAckRequest { public string type = "shutdown"; }

    // ---- 참여 기록 (A2) — 활동별 본문 ----

    [Serializable]
    public class PlaySessionBase
    {
        public string id;               // 게임이 만든 UUID · 멱등 키
        public string participant_id;
        public string activity;         // harvest | shopping | cooking
        public string mode;             // story | free
        public string story_run_id;     // 스토리면 UUID · 개별은 "" (서버가 null 로 봄)
        public string started_at;       // 시간대 붙은 ISO
        public float duration_sec;
        public float paused_sec;
        public int success_count;       // 참고값 — 서버가 rounds 로 다시 계산
        public int hint_count;
        public string app_version;
    }

    [Serializable]
    public class HarvestRoundDto
    {
        public int round_no;
        public string target_item;
        public string picked_item;
        public string result;           // target | other | bug
        public float reaction_sec;
        public int hint_count;
        public float round_sec;         // 4-8 요청 2 — 선택이지만 보낸다
    }

    [Serializable] public class HarvestSessionBody : PlaySessionBase { public List<HarvestRoundDto> rounds = new List<HarvestRoundDto>(); }

    [Serializable]
    public class ShoppingItemDto
    {
        public int seq;                 // 1~3
        public string target_item;
        public string target_zone;      // L2 L1 C R1 R2
        public string bought_item;
        public string bought_zone;
        public bool is_matched;
        public float arrive_sec;
        public int hint_count;
    }

    [Serializable]
    public class ShoppingRoundDto
    {
        public int round_no;
        public float round_sec;
        public int hint_count;          // 라운드 수준 — 중앙 복귀 유도 힌트 (재료별 items[].hint_count 와 별도) · 서버 합산 반영 2026-10-19 (Docs/97)
        public List<ShoppingItemDto> items = new List<ShoppingItemDto>();
    }

    [Serializable]
    public class ShoppingSessionBody : PlaySessionBase
    {
        public int move_left_count;
        public int move_right_count;
        public List<ShoppingRoundDto> rounds = new List<ShoppingRoundDto>();
    }

    [Serializable] public class CookingPickDto { public int seq; public string item; public bool is_matched; }

    [Serializable]
    public class CookingRoundDto
    {
        public int round_no;
        public string food;
        public float round_sec;
        public float reaction_sec;
        public int review_count;
        public int hint_count;
        public List<string> answers = new List<string>();
        public List<CookingPickDto> picks = new List<CookingPickDto>();
    }

    [Serializable] public class CookingSessionBody : PlaySessionBase { public List<CookingRoundDto> rounds = new List<CookingRoundDto>(); }

    [Serializable]
    public class PlaySessionResponse
    {
        public bool ok;
        public string id;
        public bool duplicate;      // 200 — 같은 id 가 이미 있음 (정상)
        public int success_count;   // 201 — 서버 계산값
        public int hint_count;
        public bool clock_suspect;  // 201 — started_at 이 서버보다 1시간 넘게 미래 (저장은 됨 · 관리자 점검 화면에 24시간 「날짜·시각 주의」)
        public string server_time;  // clock_suspect 일 때 서버 지금 시각
    }

    // ---- 내 기록 (A3) ----

    [Serializable] public class ParticipantBrief { public string id; public string name; }
    [Serializable] public class PlaySummaryDto { public int count; public int days; public float total_sec; public string last_played_at; }

    [Serializable]
    public class PlaySessionSummaryDto
    {
        public string id;
        public string activity;
        public string mode;
        public string story_run_id;
        public string started_at;
        public float duration_sec;
        public int success_count;    // 라운드 성공 수 0~10
        public int accuracy_count;   // 지표 1번 — 장보기는 제대로 산 재료 수(재료 단위) · 그 밖은 success_count 와 같음 (2026-10-19 추가)
        public float avg_reaction_sec;
        public int hint_count;
        public string device_id;
    }

    [Serializable]
    public class MyRecordsResponse
    {
        public bool ok;
        public ParticipantBrief participant;
        public string activity;      // `?activity=` 로 거른 활동 · 안 거르면 null → "" (summary 는 늘 전체 활동 기준)
        public PlaySummaryDto summary;
        public List<PlaySessionSummaryDto> sessions = new List<PlaySessionSummaryDto>();
    }
}
