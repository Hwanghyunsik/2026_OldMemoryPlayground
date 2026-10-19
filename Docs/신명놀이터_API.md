# 신명놀이터 서버 API

기준: `admin/` (Next.js 16) · 2026-10-16 API 하나로 통일 · 조이랩 서버 설계 `93-1_서버_ERD_공유본` v0.2 §5 반영 (D1~D3 · A1~A3 · B1 · B2) — ERD 와 다른 점은 [4장](#4-erd93-1-v02-대조표)
주소: 개발 서버 `https://dev.joylab.kr:33780` · 같은 PC 에서 돌리면 `http://localhost:3000`

**API 는 `/api/…` 하나입니다.** 게임 기기 · 게임 설치 화면 · 관리자 화면(브라우저)이 모두 같은 API 를 부르고, 누가 불렀는지(토큰 · 쿠키)에 따라 쓸 수 있는 경로가 정해집니다. 따로 있던 「브릿지」 API · 서버 액션 · 접두사 없는 경로는 없어졌습니다.

| 장 | 내용 |
|---|---|
| [1. 게임(기기) API](#1-게임기기-api) | 설치(D1~D3) · 카드(A1) · 참여 기록(A2) · 내 기록(A3) · 상태 보고(B1) · 설정(B2) · 명령 |
| [2. 관리자 화면 API](#2-관리자-화면-api) | 로그인 · 계정 · 기기 · 사용자 · 참여 기록 · 통계 |
| [3. 다른 언어로 붙이기](#3-다른-언어로-붙이기) | Unity 가 아닐 때의 동작 순서 · Python 예시 (Unity 는 `신명놀이터_Unity_연동가이드.md`) |
| [4. ERD 대조표](#4-erd93-1-v02-대조표) | 93-1 항목별 구현 상태 · 조이랩에 요청할 것 |

---

## 공통

| 항목 | 내용 |
|---|---|
| 경로 | 모두 `/api/…` (예: `/api/auth/login` · `/api/participants`). 93-1 의 `/auth/login` 은 **기본 주소를 `…/api` 로** 잡으면 그대로 맞는다 |
| 본문 | JSON · `Content-Type: application/json` |
| 필드명 | 93-1 대로 snake_case (`camera_connected` · `participant_id`) |
| 시각 | ISO 8601, **시간대 필수** — `2026-10-08T10:12:00+09:00` 또는 `…Z`. 시간대가 없으면 400 · 응답 시각은 UTC(`…Z`) |
| 성공 | `{ "ok": true, ... }` |
| 실패 | `{ "ok": false, "message": "...", "issues": ["필드: 이유", ...] }` (`issues`는 값 오류일 때만) · `message` 는 화면에 그대로 보여 줄 수 있는 한국어 |

### 누가 부르나 (호출자)

| 호출자 | 보내는 것 | 쓸 수 있는 경로 |
|---|---|---|
| 기기 (게임 PC) | `Authorization: Bearer dev_…` (D3 에서 받은 기기 토큰) | `/api/devices/me/…` · A1 `GET /api/participants` · A2 `POST /api/play-sessions` · A3 |
| 설치 화면 | `Authorization: Bearer op.…` (D1 운영자 토큰 · 30분) | D2 `GET /api/devices` · D3 `POST /api/devices/{id}/token` · `GET /api/me` |
| 관리자 화면 (브라우저) | 로그인 쿠키 `sm_admin` (`POST /api/auth/session` 이 줌) | 2장 전부 + A1 · A3 · D2 · D3 (역할에 따라 — 2장 표의 「누가」) |
| 없음 | — | `POST /api/auth/login` · `POST/DELETE /api/auth/session` |

- 맞지 않는 호출자는 401(토큰 · 쿠키 없음 · 틀림) 또는 403(있지만 이 경로를 쓸 수 없음 · 예: 기기 토큰으로 `/api/organizations`)
- **쿠키로 부르는 변경 요청(POST · PUT · PATCH · DELETE)은 `Origin` 확인** — 브라우저 주소와 서버 주소가 다르면 403 「다른 사이트에서 온 요청」. 서버가 프록시 뒤에 있으면 `.env.local` 의 `ALLOWED_ORIGINS=호스트:포트` (쉼표로 여러 개). `Origin` 이 없는 호출(브라우저 밖)은 통과
- 역할: `super` 전체관리자(조이랩 · 계정 관리만) · `owner` 기관관리자 · `staff` 운영자. 기관관리자와 운영자는 자기 기관의 기기 · 사용자 · 기록을 함께 본다

| 코드 | 뜻 |
|---|---|
| 200 / 201 | 성공 / 새로 저장됨 |
| 400 | 값 오류 — `issues` 확인. 다시 보내도 같은 결과 |
| 401 | 토큰 없음 · 틀림 · 만료 · 로그인 안 됨 · 비밀번호가 바뀌어 끊긴 로그인 |
| 403 | 막힌 계정 · 정지된 기관 · 전체관리자로 D1 · 이 호출자가 쓸 수 없는 경로 · 다른 사이트(Origin) |
| 404 | 대상 없음 (지운 사용자 · 다른 기관 기기 · 없는 경로) |
| 405 | 경로는 있는데 메서드가 다름 |
| 409 | 이미 다른 PC 가 쓰는 기기 (D3) · 다른 기관이 이미 쓴 참여 기록 id (A2) · 같은 이름 기기 · 이미 쓰는 아이디 · 사용자가 남은 기기 삭제 · 연결 안 된 기기 종료 |
| 429 | 로그인(또는 지금 비밀번호) 연속 실패로 잠시 막힘 (5번째 실패부터 60초) |
| 500 | 서버 오류 — 잠시 후 재시도. 이때도 본문은 JSON |

---

## 1. 게임(기기) API

### 설치 흐름 (PC 1대당 1회 · 93-1 §5)

```
D1 POST /api/auth/login (운영자 아이디·비밀번호) ─▶ 운영자 토큰(30분)
D2 GET  /api/devices (운영자 토큰) ─▶ 기관의 기기 목록 · in_use 표시
D3 POST /api/devices/{id}/token (운영자 토큰) ─▶ 기기 토큰 ─▶ 게임 PC 가 보관 · 이후 모든 호출에 사용
```

- 기기는 **관리자 웹(사용자 관리 → 기기 등록)에서 먼저 만든다.** 게임 PC 는 고르기만 한다
- 기기 토큰은 계정이 아니라 기기에 묶인다 — 발급한 운영자가 막히거나 비밀번호를 바꿔도 그대로 유효. 사라지는 경우: 같은 기기를 다시 고름(D3 replace) · 관리자 웹 「연결 해제」 · 기기 삭제
- 운영자 토큰은 서버에 저장하지 않는다 — 같은 계정이 여러 PC 에서 로그인해도 서로 영향 없음

| # | 메서드 | 경로 | 토큰 | 용도 · 시점 |
|---|---|---|---|---|
| D1 | POST | `/api/auth/login` | 없음 | 설치 시 운영자 로그인 |
| D2 | GET | `/api/devices` | 운영자 | 설치 시 기기 목록 |
| D3 | POST | `/api/devices/{id}/token` | 운영자 | 설치 시 기기 토큰 발급 |
| A1 | GET | `/api/participants` | 기기 | 사용자 선택 화면 진입마다 |
| A2 | POST | `/api/play-sessions` | 기기 | 10라운드를 마쳤을 때 |
| A3 | GET | `/api/participants/{id}/play-sessions?limit=5` | 기기 | 내 기록 보기 진입 |
| B1 | PUT | `/api/devices/me/status` | 기기 | 켜 있는 동안 5초마다 |
| B2 | GET | `/api/devices/me/settings` | 기기 | 켤 때 · 주기(1분) |
| — | GET | `/api/devices/me/commands` | 기기 | 5초마다 (종료 요청 확인) |
| — | POST | `/api/devices/me/commands/ack` | 기기 | 명령 처리 직전 |

### 1-1. D1 `POST /api/auth/login`

```json
{ "username": "manager", "password": "········" }
```

```json
{
  "ok": true,
  "token": "op.6ac7….1791…​.9f3a…",
  "expires_at": "2026-10-08T12:30:00.000Z",
  "operator": { "id": "6ac705d3…", "name": "김관리", "role": "owner", "organization_id": "6ac705d2…" },
  "organization": { "id": "6ac705d2…", "name": "조이복지관" }
}
```

- 401 틀림 · 403 막힌 계정/정지된 기관/전체관리자 · 429 같은 곳에서 5번 연속 틀리면 60초 잠금
- 토큰은 30분 뒤 만료 — 설치 흐름(D2 · D3) 안에서만 쓴다

### 1-2. D2 `GET /api/devices`

```json
{ "ok": true, "devices": [
  { "id": "6ac7…", "name": "1층 프로그램실", "in_use": true,  "last_seen_at": "2026-10-08T02:54:17.000Z", "app_version": "0.9.0" },
  { "id": "6ac7…", "name": "2층 강당",       "in_use": false, "last_seen_at": null, "app_version": null }
]}
```

`in_use` 가 true 면 다른 PC 가 이미 토큰을 받은 기기 — 설치 화면에 「다른 PC 에서 사용 중」으로 표시하고, 운영자가 확인한 뒤에만 D3 에 `replace: true` 로 보낸다.

### 1-3. D3 `POST /api/devices/{id}/token`

```json
{ "replace": false }
```

| 응답 | 본문 |
|---|---|
| 201 | `{ "ok": true, "token": "dev_…", "device": { "id", "name" }, "replaced": false }` — **토큰 원문은 이 응답에만** |
| 409 | `{ "ok": false, "message": "다른 PC 에서 사용 중인 기기입니다 …", "in_use": true }` — `replace: true` 로 다시 보내면 교체 (기존 PC 토큰 무효) · 동시에 두 PC 가 보내도 한쪽만 성공 |
| 404 | 없는 기기 · 다른 기관 기기 |

### 1-4. A1 `GET /api/participants`

기관 전체 사용자, 이름순 (93-1 원칙 2 — 어느 기기에서 참여해도 기록이 이어진다). `?device=me` 를 붙이면 이 기기로 등록된 사용자만.

```json
{ "ok": true, "participants": [
  { "id": "6abb…", "name": "박순덕", "gender": "F", "avatar_index": 0, "card_color_index": 0, "device_id": "6ac7…" }
]}
```

| 필드 | 뜻 |
|---|---|
| `id` | 사용자 ID → 참여 기록(A2)의 `participant_id` |
| `name` | 카드 이름 (≤5자) · 같은 이름이 있을 수 있음 |
| `gender` | `M` / `F` |
| `avatar_index` | 그림 0~7 (관리자 화면의 그림 1~8 에서 1 뺀 값) |
| `card_color_index` | 카드 배경색 0~7 — `0 #F6DCD2` · `1 #FBEAB8` · `2 #DBEECB` · `3 #CFE6EF` · `4 #DED8F1` · `5 #F4D6E6` · `6 #E8DFD0` · `7 #D6E2DA` |
| `device_id` | 등록할 때 고른 「주로 쓰는 기기」 (우리 쪽 추가) |

### 1-5. B1 `PUT /api/devices/me/status`

```json
{ "camera_connected": true, "person_detected": false, "game_state": "idle",
  "app_version": "0.9.0", "storage_free_bytes": 42949672960, "device_time": "2026-10-08T11:54:17+09:00" }
```

| 필드 | 필수 | 뜻 |
|---|---|---|
| `camera_connected` | ✔ | 카메라 영상이 들어오는지 |
| `person_detected` | | 사람 인식 결과 · 모르면 생략/null |
| `game_state` | | 표시용 (≤40자) |
| `app_version` | | 게임 버전 — 관리자 기기 표에 표시 · 길면 40자로 잘라 받음 |
| `storage_free_bytes` | | 게임 PC 남은 저장 공간 — 점검 「저장 공간」 (1GB 미만 문제 · 5GB 미만 주의). 93-1 에 없는 제안 |
| `device_time` | | 게임 PC 지금 시각(시간대 붙은 ISO · `""` 는 없음으로 봄) — 서버와 5분 넘게 다르면 점검 「날짜 · 시각」 주의. 93-1 에 없는 제안 |

→ `{ "ok": true }`. `devices.status` 를 덮어쓴다. **60초 안에 보고가 없으면 관리자 화면은 게임이 꺼진 것으로 본다** (93-1 의 「예: 1분」보다 짧게 · 30초 이하 권장). 미리보기 영상은 받지 않는다(제외 결정). `POST` 로 보내도 같다.

### 1-6. B2 `GET /api/devices/me/settings`

```json
{ "ok": true, "device": { "id": "6ac7…", "name": "1층 프로그램실" }, "volume_level": 2, "voice_enabled": true, "updated_at": null }
```

관리자 웹 설정(ADM-008)에서 기기마다 바꾼다. 켤 때와 1분마다 읽어 적용한다. 관리 번호(93-1 `admin_pin_hash`)는 §6-1 협의 전이라 없다.

### 1-7. 명령 `GET /api/devices/me/commands` · `POST /api/devices/me/commands/ack`

93-1 §6-6 「기기가 주기적으로 명령을 조회」 방식.

```json
{ "ok": true, "shutdown_requested_at": "2026-10-08T03:10:00.000Z" }
```

`shutdown_requested_at` 이 **게임을 켠 시각보다 뒤**면 `POST /api/devices/me/commands/ack` 에 `{ "type": "shutdown" }` 을 보낸 뒤 종료한다. 앞이면 지난 요청이니 무시한다. ack → `{ "ok": true }` (요청 표시가 지워진다).

### 1-8. A2 `POST /api/play-sessions`

10라운드를 다 마쳤을 때만. 중간에 그만두면 보내지 않습니다. 본문은 `play_sessions` 문서와 같은 모양(93-1 §5-3)이고, 서버가 `id → _id`, `organization_id` · `device_id`(기기 토큰) · `uploaded_at` 을 채웁니다.
**같은 `id` 를 다시 받으면 저장하지 않고 성공(200 `duplicate`)** — 네트워크 장애 후 재전송해도 한 건만 남습니다.

```json
{
  "id": "6f1c2a9e-3b4d-4f61-9c2e-1a7b8d9e0f12",
  "participant_id": "6702f1a9c3b4d5e6f7a8b9c0",
  "activity": "harvest",
  "mode": "free",
  "story_run_id": null,
  "started_at": "2026-10-07T14:03:21+09:00",
  "duration_sec": 312.4,
  "paused_sec": 0.0,
  "success_count": 9,
  "hint_count": 2,
  "app_version": "0.9.0",
  "rounds": [
    { "round_no": 1, "target_item": "감", "picked_item": "감", "result": "target", "reaction_sec": 4.2, "hint_count": 0, "round_sec": 31.5 },
    { "round_no": 2, "target_item": "고추", "picked_item": "가지", "result": "other", "reaction_sec": 6.8, "hint_count": 1, "round_sec": 44.0 }
  ]
}
```

공통 필드:

| 필드 | 필수 | 규칙 |
|---|---|---|
| `id` | ✔ | **게임이 만든 UUID** — 멱등 키. 재전송할 때 같은 값 |
| `participant_id` | ✔ | A1 의 `id` · 다른 기관 사용자면 404 |
| `activity` | ✔ | `harvest` \| `shopping` \| `cooking` |
| `mode` | ✔ | `story` \| `free` |
| `story_run_id` | 스토리면 ✔ | 스토리 3종(수확→장보기→요리)을 묶는 UUID · 개별 게임은 `null`/생략/`""` (빈 문자열은 null 로 본다 — Unity JsonUtility) |
| `started_at` | ✔ | **시간대가 붙은** ISO 시각 · 시간대 없음 · null · 숫자는 400 · 서버보다 1시간 넘게 미래면 400 |
| `duration_sec` | ✔ | 걸린 시간(초) · 일시정지 제외 · `duration_sec + paused_sec` 이 3시간을 넘으면 400 |
| `paused_sec` | | 일시정지 시간(초) · 기본 0 |
| `success_count` · `hint_count` | | 참고값 — **저장하지 않고 서버가 `rounds` 로 다시 계산**한다 (93-1 원칙 4) · 응답에 서버 값이 온다 · `success_count` 는 0~10 이 아니면 400 |
| `move_left_count` · `move_right_count` | | 장보기만 · 다른 활동이면 버린다 |
| `app_version` | | 길면 40자로 잘라 받음 |
| `rounds` | ✔ | **정확히 10개** · `round_no` 1~10 겹치지 않게 · 모양은 `activity` 에 따라 아래 · 서버는 검사한 값(기본값 채움 · 앞뒤 공백 제거 · `""` → null · 모르는 키 제거 · 번호순)으로 저장 |

`rounds[]` — 수확하기 (`harvest`):

| 필드 | 필수 | 뜻 |
|---|---|---|
| `round_no` | ✔ | 1~10 |
| `target_item` · `picked_item` | ✔ · | 목표 작물 · 고른 작물(없으면 null) |
| `result` | ✔ | `target` 목표 작물 · `other` 다른 작물 · `bug` 벌레 먹은 작물 |
| `reaction_sec` | ✔ | 목표 제시 → 선택 확정(초) · 일시정지 제외 |
| `hint_count` | | 자동 힌트 횟수 · 기본 0 |
| `round_sec` | 권장 | 라운드 전체 시간(초). **93-1 에 없어 선택이지만 보내 주세요** — 없으면 관리자 화면의 라운드 시간이 「–」로 나오고 긴 라운드를 짚지 못합니다 |

`rounds[]` — 장보기 (`shopping`): `{ round_no, round_sec, items: [ { seq 1~3, target_item, target_zone, bought_item, bought_zone, is_matched, arrive_sec, hint_count } ] }` · `zone` 은 `L2` `L1` `C` `R1` `R2` · 재료 1~3개

`rounds[]` — 요리하기 (`cooking`): `{ round_no, food, round_sec, reaction_sec, review_count, hint_count, answers: ["재료", …], picks: [ { seq, item, is_matched } ] }`

| 응답 | 본문 |
|---|---|
| 201 | `{ "ok": true, "id": "…", "success_count": 9, "hint_count": 1 }` — 서버가 계산한 요약 |
| 200 | `{ "ok": true, "id": "…", "duplicate": true }` — 같은 `id` 가 이미 있음 |
| 400 | 값 오류 · `issues` 에 `rounds.3.items.0.arrive_sec: …` 처럼 위치가 붙는다 |
| 404 | 없는 사용자 |
| 409 | 다른 기관이 이미 쓴 `id` — 새 UUID 로 보낸다 |

관리자 화면 지표(정확하게 선택 · 평균 반응 시간 · 다른 선택 · 도움 횟수)는 서버가 활동별 라운드에서 변환합니다. 변환 규칙(가정)은 `admin/src/lib/playSession.ts` — 수확: `result === "target"` 이 정확 · 장보기: 재료가 모두 `is_matched` 면 정확, 반응은 `arrive_sec` 평균 · 요리: 담은 재료가 모두 `is_matched` 면 정확, `review_count` 는 도움과 별개로 셈.

### 1-9. A3 `GET /api/participants/{id}/play-sessions?limit=5`

게임의 「내 기록 보기」. 최근 참여 목록(최신순 · `limit` 1~50 · 생략하면 5 · 범위 밖은 1 또는 50)과 누적 요약. 다른 기관 사용자는 404.

```json
{
  "ok": true,
  "participant": { "id": "6702f1a9…", "name": "김순자" },
  "summary": { "count": 24, "days": 11, "total_sec": 7420, "last_played_at": "2026-10-08T02:54:20.000Z" },
  "sessions": [
    { "id": "6f1c2a9e-…", "activity": "harvest", "mode": "free", "story_run_id": null, "started_at": "2026-10-08T02:50:00.000Z",
      "duration_sec": 312.4, "success_count": 9, "hint_count": 1, "device_id": "6ac7…" }
  ]
}
```

| 요약 | 뜻 |
|---|---|
| `count` | 전체 참여 횟수 |
| `days` | 함께한 날 수 (참여한 날짜 수) |
| `total_sec` | 전체 활동 시간(초 · 일시정지 제외 합) |
| `last_played_at` | 마지막 참여 시각 · 없으면 null |

---

## 2. 관리자 화면 API

관리자 화면(브라우저)이 부르는 API 입니다. 로그인 쿠키로 부르고, 변경 요청은 `Origin` 을 확인합니다(공통 참고). 화면은 처음 그릴 때 서버 안에서 같은 API 를 바로 부르고(`admin/src/lib/api-server.ts`), 버튼을 누르면 브라우저가 `fetch("/api/…")` 합니다(`admin/src/lib/admin-api.ts`). 응답 타입 전체는 `admin/src/lib/api-types.ts`.

**누가**: 전 = 전체관리자(super) · 기 = 기관관리자(owner) · 운 = 운영자(staff). 변경 응답은 따로 적지 않으면 `{ "ok": true }`.

### 2-1. 로그인 · 내 계정

| 메서드 | 경로 | 누가 | 본문 → 응답 |
|---|---|---|---|
| POST | `/api/auth/session` | 누구나 | `{ username, password }` → `{ home }` + 쿠키 `sm_admin`(HttpOnly · SameSite=Lax · HTTPS 면 Secure · 브라우저를 닫으면 사라짐). 401 틀림 · 403 막힌 계정/정지된 기관 · 429 5번 연속 실패 후 60초 |
| DELETE | `/api/auth/session` | 누구나 | 쿠키 지움 (로그아웃 · 화면 잠그기) |
| GET | `/api/me` | 전 기 운 · 운영자 토큰 | `{ operator: { id, username, name, role }, organization: { id, name } \| null, home, features: { delete_keeps_records } }` — `home` 은 역할별 첫 화면(전체관리자 `/accounts` · 그 외 `/check`) |
| PUT | `/api/me/password` | 전 기 운 | `{ current_password, new_password(8자 이상) }` → 새 쿠키. 지금 비밀번호가 틀리면 400 · 5번 틀리면 429 |

비밀번호를 바꾸거나(이 경로) 재설정하면(2-2) **그 전에 받은 로그인은 모두 끊긴다**(다음 요청 401) — 바꾼 화면만 새 쿠키로 이어진다. 막힌 계정 · 정지된 기관은 다음 요청부터 401.

### 2-2. 계정 관리 — 기관 · 운영자

| 메서드 | 경로 | 누가 | 본문 → 응답 |
|---|---|---|---|
| GET | `/api/organizations` | 전 | → `{ organizations: [{ id, name, status, created_at, device_count, user_count, operators: [운영자] }] }` |
| POST | `/api/organizations` | 전 | `{ name(≤30자), owner: { username, password, name } }` → 201 `{ id }` — 기관과 기관관리자 계정을 함께 만든다 |
| PATCH | `/api/organizations/{id}` | 전 | `{ status: "active" \| "suspended" }` — 정지하면 그 기관의 로그인 · D1 · 기기 호출이 403 으로 막힌다 (기기 토큰은 지우지 않아 풀면 그대로) |
| GET | `/api/operators` | 전 기 | 기관관리자는 자기 기관 · 전체관리자는 `?organization_id=` → `{ operators: [운영자] }` |
| POST | `/api/operators` | 전 기 | `{ username, password, name, role: "owner" \| "staff", organization_id }` → 201 `{ id }`. 기관관리자는 자기 기관의 `staff` 만(`organization_id` 생략 가능) · 아니면 403 |
| PATCH | `/api/operators/{id}` | 전 기 | `{ is_active }` — 막기 · 풀기 (지우지 않는다) |
| PUT | `/api/operators/{id}/password` | 전 기 | `{ password }` — 재설정 · 그 계정의 기존 로그인은 끊긴다 |

- 운영자: `{ id, username, name, role, is_active, last_login_at, created_at }`
- 아이디: 영문 소문자 · 숫자 · 밑줄 3~20자 (이미 쓰면 409) · 비밀번호 8자 이상 · 이름 1~10자
- 기관관리자가 다른 기관 계정 · 기관관리자 계정 · 전체관리자 계정을 고치려 하면 404 (없는 것처럼)

### 2-3. 기기

| 메서드 | 경로 | 누가 | 본문 → 응답 |
|---|---|---|---|
| GET | `/api/devices` | 기 운 · 운영자 토큰(D2) | → `{ devices: [기기] }` |
| POST | `/api/devices` | 기 운 | `{ name(설치 위치 · ≤20자) }` → 201 `{ id }` · 같은 이름이면 409 |
| PATCH | `/api/devices/{id}` | 기 운 | `{ name?, volume_level?: 1\|2\|3, voice_enabled? }` — 이름 · 소리 크기 · 음성 안내 (게임은 B2 로 읽는다) |
| DELETE | `/api/devices/{id}` | 기 운 | 이 기기로 등록된 사용자가 있으면 409 `{ user_count }` · 지우면 기기 토큰도 무효 |
| POST | `/api/devices/{id}/token` | 기 운 · 운영자 토큰(D3) | 1-3 과 같음 |
| DELETE | `/api/devices/{id}/token` | 기 운 | 연결 해제 — 기기 토큰을 지운다. 그 PC 는 다음 호출에서 401 → 설치 화면 |
| POST | `/api/devices/{id}/shutdown` | 기 운 | 프로그램 종료 요청 — 게임이 5초 안에 명령(1-7)으로 받아 꺼진다 · 연결 안 된 기기면 409 |
| GET | `/api/devices/{id}/status` | 기 운 | 점검 화면(ADM-001)이 2초마다 — 아래 |

기기: `{ id, name, in_use(토큰 발급됨), online(최근 보고 60초 안), last_seen_at, app_version, volume_level, voice_enabled, user_count, shutdown_requested_at, created_at }`

`GET /api/devices/{id}/status`:

```json
{ "ok": true, "fresh": true, "linked": true, "camera_connected": true, "person_detected": true, "game_state": "idle",
  "storage": "ok", "clock": "ok", "reported_at": "2026-10-08T02:56:10.000Z" }
```

`storage` · `clock` 은 `ok | warn | bad | null`(보고 없음) — 저장 공간 1GB 미만 `bad` · 5GB 미만 `warn` / 기기 시계가 서버와 5분 넘게 다르면 `warn`. 60초 안에 보고가 없으면 `{ "fresh": false, "linked", "reported_at" }` 만.

### 2-4. 사용자 (어르신)

| 메서드 | 경로 | 누가 | 본문 → 응답 |
|---|---|---|---|
| GET | `/api/participants` | 기 운 · 기기(A1) | `?q=`(이름) · `?sort=name\|recent` · `?page=`(주면 쪽 나눔) · `?page_size=`(기본 7 · 최대 100) · `?device=me\|{기기 id}` → `{ participants: [사용자], total, matched, page, page_count }` · `page` 가 없으면 전부 |
| POST | `/api/participants` | 기 운 | `{ name(≤5자), gender: "M"\|"F", avatar_index: 0~7, device_id }` → 201 `{ id }` · 카드색은 서버가 고른다 |
| GET | `/api/participants/{id}` | 기 운 | 상세(ADM-003) → `{ participant, summary: { days, count, total_sec }, activities: [{ activity, count, status: good\|similar\|insufficient }], recent: [최근 5건] }` |
| PATCH | `/api/participants/{id}` | 기 운 | POST 와 같은 본문 (전부 보냄) |
| DELETE | `/api/participants/{id}` | 기 운 | → `{ records_kept: false, deleted_play_sessions }` (기록도 삭제) · `DELETE_KEEPS_RECORDS=1` 이면 `{ records_kept: true }` (기록은 「지운 사용자」로 남음) |
| GET | `/api/participants/{id}/trend` | 기 운 | 변화 추이(ADM-003-2) · `?activity=harvest\|shopping\|cooking` · `?period=day\|week\|month` → `{ participant, buckets, graph(최근 5구간), changes: { 지표: { status, first, last, text } } }` |
| GET | `/api/participants/{id}/play-sessions` | 기 운 · 기기(A3) | 1-9 와 같음 |

사용자: `{ id, name, gender, avatar_index(0~7), card_color_index, device_id, device_name, created_at, last_played_at, play_count }` — A1 도 같은 모양(1-4 의 필드 + 관리용 필드).

구간(bucket): `{ key, label, count, users, duration_sec, avg: { accuracy, reaction, other, hint } }` — 지표 값은 기록이 없으면 null.

### 2-5. 참여 기록 · 통계

| 메서드 | 경로 | 누가 | 본문 → 응답 |
|---|---|---|---|
| GET | `/api/play-sessions` | 기 운 | 목록(ADM-004) · `?from=` `?to=`(YYYY-MM-DD) · `?participant_id=` · `?activity=` · `?mode=story\|free` · `?sort=new\|old` · `?page=`(한 쪽 20건) → `{ has_any, total, page, page_count, play_sessions: [{ id, participant_id, participant_name, device_name, activity, mode, started_at, duration_sec }] }` |
| GET | `/api/play-sessions/{id}` | 기 운 | 1회 상세(ADM-005) → 목록 한 줄 + `ended_at, paused_sec, correct_count, avg_reaction_sec, hint_count, round_stats: [{ no, correct, reaction_sec, round_sec, hints, others }], rounds: [게임이 보낸 원시 라운드]` |
| GET | `/api/stats/today` | 기 운 | 오늘 → `{ count, users, last: { name, at } \| null }` |
| GET | `/api/stats` | 기 운 | 통계(ADM-006) · `?from=&to=` 또는 `?preset=week\|month\|3m`(기본 이번 달) → `{ from, to, summary: { users, count, per_user }, activities: [{ activity, users, count }], weeks: [구간] }` |
| GET | `/api/stats/activities/{activity}` | 기 운 | 활동별 통계(ADM-006-2) · 기간은 위와 같음 → `{ from, to, summary: { users, count, total_sec, avg_sec }, avg, users: [{ participant_id, name, deleted, count, accuracy, reaction }], weeks }` |

참여 기록 저장(A2)은 기기만 한다 — 관리자 화면에서 기록을 만들거나 고치는 API 는 없다.

---

## 3. 다른 언어로 붙이기

Unity 로 만든다면 `신명놀이터_Unity_연동가이드.md` 를 보세요 (`unity/Shinmyeong.Api` 스크립트가 아래 흐름을 다 해 줍니다). 이 장은 다른 언어 · HTTP 수준에서 확인할 때의 **동작 순서**입니다. 게임은 DB 를 직접 만지지 않고 이 API 만 부릅니다.

```
기관 A ─ 게임 기기 1 ─┐
       ─ 게임 기기 2  ├─ HTTPS · Authorization: Bearer <기기 토큰> ─▶ 서버 /api/… ─▶ MongoDB
기관 B ─ 게임 기기 3 ─┘                                               ▲ 관리자 화면(브라우저 · 쿠키)도 같은 /api/…
```

### 3-1. 설치 (처음 한 번)

1. 설치 화면에서 운영자 아이디 · 비밀번호를 받아 **D1** → 운영자 토큰 (30분 유효 · 저장하지 않는다)
2. **D2** 로 기관의 기기 목록을 보여 준다. `in_use: true` 는 「다른 PC 에서 사용 중」으로 표시
3. 운영자가 고른 기기로 **D3** (`{}`) → `201 { token }` 을 **PC 에 저장**한다 (파일 · 레지스트리 등). `409 in_use` 면 운영자에게 확인한 뒤 `{ "replace": true }` 로 다시 보낸다 (기존 PC 토큰 무효)
4. 이후 모든 호출에 기기 토큰을 쓴다. 발급한 운영자 계정이 막히거나 비밀번호를 바꿔도 토큰은 유효하다

기기는 관리자 화면(사용자 관리 → 기기 등록)에서 먼저 만들어 둔다. 게임 PC 는 고르기만 한다.

### 3-2. 켤 때

1. 저장된 기기 토큰이 없으면 설치 화면 (3-1)
2. `startedAt = 지금` 을 기억해 둔다 (종료 요청 판정)
3. **B2** 설정을 읽어 소리 크기 · 음성 안내 적용
4. **A1** 카드 목록 → 시작 화면 (시작 화면으로 돌아올 때마다 다시)
5. 5초 타이머 **B1** · 5초 타이머 명령 확인 · 1분 타이머 **B2**

서버에 닿지 않으면 마지막으로 받은 카드 목록으로 시작 화면을 보여 준다. 서버 장애가 게임 이용 불가로 이어지지 않는다 (93-1 §5-2).

### 3-3. 활동을 마쳤을 때 — A2

1. `id = UUID` 를 만든다 (멱등 키)
2. 활동별 라운드 10개를 담아 **A2**. 응답 `201` 저장 · `200 duplicate` 이미 있음(정상)
3. 네트워크 · 5xx 실패면 **로컬 큐(파일)에 저장**해 두고 30초마다, 다음 실행 때 같은 본문으로 다시 보낸다 — 같은 `id` 라 두 번 저장되지 않는다
4. `400`(값 오류) · `404`(지운 사용자) · `409`(쓸 수 없는 id)는 재시도하지 않는다
5. `401` 이면 기기 토큰이 거부된 것 — 설치 화면으로 (기록은 큐에 남겨 둔다)

### 3-4. 종료 요청

5초마다 `GET /api/devices/me/commands` → `shutdown_requested_at` 이 있고 **`startedAt` 보다 뒤**면 `POST /api/devices/me/commands/ack {"type":"shutdown"}` 후 종료. 앞이면 지난 요청이니 무시.

### 3-5. 오류별로 게임이 할 일

| 코드 | 게임이 할 일 |
|---|---|
| `400` | 보내는 값을 고친다 (`issues` 에 「경로: 이유」). 재시도해도 같은 결과 |
| `401` | 운영자 토큰이면 다시 로그인 · 기기 토큰이면 설치 화면 |
| `403` | 운영자 계정 확인 (막힌 계정 · 정지된 기관 · 전체관리자로 D1) |
| `404` | 카드 목록을 다시 받는다 |
| `409` | D3: 운영자 확인 뒤 `replace: true` · A2: 버린다 |
| `429` | 1분 뒤 |
| `500` | 잠시 후 재시도 (참여 기록은 큐) |

### 3-6. Python 예시 (requests)

```python
import json, uuid, requests
from datetime import datetime, timezone

API = "https://dev.joylab.kr:33780/api"     # 모든 경로가 /api/… — 기본 주소에 /api 까지 넣어 두면 93-1 경로 그대로
TOKEN_FILE = "device_token.json"
started_at = datetime.now(timezone.utc)

def install(username, password, device_name, replace=False):
    r = requests.post(f"{API}/auth/login", json={"username": username, "password": password}, timeout=5)
    if r.status_code != 200:
        raise RuntimeError(r.json().get("message"))      # 401 틀림 · 429 잠시 잠김 · 403 막힌 계정
    op = r.json()["token"]
    devices = requests.get(f"{API}/devices", headers={"Authorization": f"Bearer {op}"}, timeout=5).json()["devices"]
    device = next((d for d in devices if d["name"] == device_name), None)
    if device is None:                                    # 이름이 틀리면 아무 기기나 고르지 않는다 (다른 PC 의 기기를 빼앗지 않게)
        raise RuntimeError("기기 이름을 확인해 주세요: " + ", ".join(d["name"] for d in devices))
    r = requests.post(f"{API}/devices/{device['id']}/token", json={"replace": replace}, headers={"Authorization": f"Bearer {op}"}, timeout=5)
    if r.status_code == 409:
        raise RuntimeError("다른 PC 에서 사용 중 — 운영자 확인 뒤 replace=True")
    json.dump({"token": r.json()["token"], "device": device["name"]}, open(TOKEN_FILE, "w"))

def H():
    return {"Authorization": "Bearer " + json.load(open(TOKEN_FILE))["token"]}

def participants():
    return requests.get(f"{API}/participants", headers=H(), timeout=5).json()["participants"]

def settings():
    return requests.get(f"{API}/devices/me/settings", headers=H(), timeout=5).json()   # volume_level · voice_enabled

def report_status(camera_ok, person=None, state="idle"):
    body = {"camera_connected": camera_ok, "person_detected": person, "game_state": state,
            "device_time": datetime.now(timezone.utc).isoformat()}
    requests.put(f"{API}/devices/me/status", json=body, headers=H(), timeout=5)

def save_play_session(participant_id, activity, mode, begin, duration_sec, rounds, story_run_id=None, paused_sec=0):
    # begin 은 시간대가 있는 datetime (예: datetime.now(timezone.utc)) — 시간대가 없으면 서버가 400 으로 거절한다
    body = {"id": str(uuid.uuid4()), "participant_id": participant_id, "activity": activity, "mode": mode,
            "story_run_id": story_run_id, "started_at": begin.astimezone().isoformat(), "duration_sec": duration_sec,
            "paused_sec": paused_sec, "app_version": "0.9.0", "rounds": rounds}      # rounds 모양은 1-8 (활동별)
    r = requests.post(f"{API}/play-sessions", json=body, headers=H(), timeout=5)
    if r.status_code in (200, 201):
        return r.json()["id"]
    if r.status_code in (400, 404, 409):
        return None                        # 재시도하지 않는다 (409 = 다른 기관이 쓴 id)
    raise RuntimeError(r.text)             # 큐에 넣고 나중에 같은 body 로 재시도

def my_records(participant_id):
    return requests.get(f"{API}/participants/{participant_id}/play-sessions?limit=5", headers=H(), timeout=5).json()

def check_shutdown():
    c = requests.get(f"{API}/devices/me/commands", headers=H(), timeout=5).json()
    at = c.get("shutdown_requested_at")
    if at and datetime.fromisoformat(at.replace("Z", "+00:00")) > started_at:
        requests.post(f"{API}/devices/me/commands/ack", json={"type": "shutdown"}, headers=H(), timeout=5)
        raise SystemExit(0)
```

---

## 점검 도구

| 명령 (`admin/`에서) | 내용 |
|---|---|
| `npm run api:check -- http://localhost:3000` | 설치 흐름(D1~D3) · 기기 API(A1~A3 · B1 · B2 · 명령) · 관리자 API(쿠키 로그인 · Origin 403 · 기기/사용자 CRUD · 기록 · 통계 · 404/405)를 실제로 호출해 65개 항목 검사 (기관관리자 `CHECK_USERNAME`/`CHECK_PASSWORD`, 기본 manager/manager1234) |
| `npm run e2e` | 브라우저(엣지)로 관리자 화면 전체 52항목 — 새 seed + game:sim 이 돌고 있을 때 (`E2E_BASE` 로 주소) |
| `npm run game:sim -- http://localhost:3000 "1층 프로그램실"` | 가짜 게임 — 설치 흐름 → 카드 · 설정 읽기 · 5초 상태 보고 · 40초마다 참여 기록(A2) · 내 기록 보기(A3) · 종료 요청 처리 |

> ⚠️ api:check · game:sim 은 **실제 기기 토큰을 새로 발급**한다. api:check 는 사용 중이 아닌 기기를 고르지만 모두 사용 중이면 첫 기기를 가져가고, 끝나면 연결을 해제한다. 운영 서버에서는 돌리지 않는다 (점검이 필요하면 점검용 기기를 따로 등록).

## curl 예시

```bash
B=http://localhost:3000/api
# 게임 기기
OP=$(curl -s -H "content-type: application/json" -d '{"username":"manager","password":"manager1234"}' $B/auth/login | jq -r .token)
curl -s -H "authorization: Bearer $OP" $B/devices
DEV=$(curl -s -X POST -H "authorization: Bearer $OP" -H "content-type: application/json" -d '{}' $B/devices/<기기id>/token | jq -r .token)
curl -s -H "authorization: Bearer $DEV" $B/participants
curl -s -X PUT -H "authorization: Bearer $DEV" -H "content-type: application/json" -d '{"camera_connected":true,"person_detected":true}' $B/devices/me/status
curl -s -H "authorization: Bearer $DEV" $B/devices/me/settings
curl -s -H "authorization: Bearer $DEV" $B/devices/me/commands
curl -s -H "authorization: Bearer $DEV" "$B/participants/<사용자id>/play-sessions?limit=5"
# 관리자 화면과 같은 호출 (쿠키)
curl -s -c jar -H "content-type: application/json" -d '{"username":"manager","password":"manager1234"}' $B/auth/session
curl -s -b jar $B/me
curl -s -b jar "$B/participants?page=1&q=김"
curl -s -b jar "$B/stats?preset=month"
```

DB 구조는 `admin/docs/SCHEMA.md`, 조이랩 설계 원본은 `93-1_서버_ERD_공유본 (1).md`.

---

## 4. ERD(93-1 v0.2) 대조표

조이랩 `93-1_서버_ERD_공유본 (1).md` 의 항목을 하나씩 구현과 맞춰 본 결과 (2026-10-15 전수점검).
✅ 같음 · ⚠️ 다름(조이랩과 맞춰야 함) · ➕ 우리 쪽 추가 · ❌ 아직 없음

### 4-1. §5 API

| ERD | 내용 | 구현 | 비고 |
|---|---|---|---|
| 경로 | `/auth/login` · `/devices` … (접두사 없음) | ⚠️ | **모두 `/api/…`** — 관리자 화면과 한 API 로 합쳐 접두사를 붙였다. 게임의 기본 주소를 `https://…:33780/api` 로 잡으면 93-1 경로 그대로 |
| D1 `POST /auth/login` | 운영자 username · password → 운영자 토큰(짧은 유효) | ✅ | 30분 · 연속 5회 실패 시 60초 잠금(➕ 429) · 전체관리자는 403 |
| D2 `GET /devices` | `id` · `name` · `in_use` · `last_seen_at` | ✅ | ➕ `app_version` |
| D3 `POST /devices/{id}/token` | 사용 중이면 `replace: true` 일 때만 교체 · 아니면 409 | ✅ | 확인과 저장을 한 번에 해 동시 요청에도 한 건만 성공 |
| A1 `GET /participants` | `id` · `name` · `gender` · `avatar_index` · `card_color_index` | ✅ | ➕ `device_id` · `?device=me` |
| A2 `POST /play-sessions` | §5-3 본문 · 같은 id 재전송은 저장 않고 성공(멱등) | ✅ | ➕ 다른 기관이 쓴 id 는 409 (남의 기록 존재를 알려 주지 않음) · 응답에 서버 계산 `success_count` · `hint_count` |
| A3 `GET /participants/{id}/play-sessions?limit=5` | 최근 목록 + 함께한 날 수 · 전체 활동 시간 · 마지막 참여일 | ✅ | ➕ `count`(전체 횟수) · `limit` 1~50 |
| B1 `PUT /devices/me/status` | `camera_connected` · `person_detected` 덮어쓰기 · 주기 「예: 1분」 | ⚠️ | **주기**: 관리자 점검 화면은 60초 안에 보고가 없으면 「꺼짐」 — 30초 이하로 보내 달라고 요청 필요. ➕ `game_state` · `app_version` · `storage_free_bytes` · `device_time` |
| B2 `GET /devices/me/settings` | 소리 크기 · 음성 안내 · **관리 번호** | ⚠️ | 관리 번호(`admin_pin_hash`) 없음 — §6-1 미결 |
| (§6-6 협의) 서버 → 기기 명령 | 방식 미정 | ➕ | `GET /devices/me/commands` · `POST …/ack` (기기가 5초마다 조회) · 카메라 미리보기는 제외 결정 |

### 4-2. §5-1 토큰 규칙

| ERD | 구현 |
|---|---|
| 운영자 토큰은 만료로만 사라짐 · 여러 개 동시 가능 · 단일 세션 처리 안 함 | ✅ 서명 토큰 · 서버에 저장 안 함 |
| 기기 토큰은 기기에 묶임 · 계정 로그아웃/비밀번호 변경/비활성/삭제와 무관 | ✅ |
| 기기 토큰이 사라지는 경우: 같은 기기를 다시 고름 · 관리자 웹 연결 해제 · 기기 삭제 | ✅ 세 가지 모두 |
| D2 에 `in_use` · D3 `replace` 없으면 409 | ✅ |
| 운영자 토큰은 JWT 등 서명 토큰이면 저장 불필요 | ✅ HMAC 서명 (`op.<id>.<만료>.<서명>`) |

### 4-3. §5-2 서버 장애 시

| ERD | 구현 |
|---|---|
| 어르신 목록을 기기에 보관 · 서버 없이도 선택 화면 | ✅ Unity `ApiClient.Participants` (마지막 목록) |
| 참여 기록은 쌓아 두었다가 자동 재전송 · A2 멱등 | ✅ Unity 파일 큐 30초 재전송 · 같은 id |

### 4-4. §5-3 A2 본문 필드

| 필드 | ERD | 구현 |
|---|---|---|
| `id` | UUID · 멱등 키 | ✅ UUID 형식 검사 |
| `participant_id` · `activity` · `mode` · `story_run_id` | | ✅ 스토리 모드면 `story_run_id` 필수(➕ 검사) · `""` 는 null |
| `started_at` | | ✅ 시간대가 붙은 ISO 만 (➕ 1시간 넘는 미래 거절) |
| `duration_sec` · `paused_sec` | 일시정지 제외 | ✅ 합이 3시간 넘으면 거절(➕) |
| `success_count` · `hint_count` | 게임이 보냄 | ⚠️ **저장하지 않고 서버가 `rounds` 로 다시 계산** (§1 원칙 4 를 따름) — 게임 화면 숫자와 같은 규칙으로 세야 함 |
| `move_left_count` · `move_right_count` | 장보기만 | ✅ 장보기가 아니면 버림 |
| `app_version` | | ✅ 길면 40자로 잘라 받음 |
| 수확 `rounds` | `round_no` · `target_item` · `picked_item` · `result` · `reaction_sec` · `hint_count` | ✅ ➕ `round_sec`(선택) — ADM-005 라운드 시간 |
| 장보기 `rounds` | `round_no` · `round_sec` · `items[seq, target_item, target_zone, bought_item, bought_zone, is_matched, arrive_sec, hint_count]` | ✅ 재료 1~3개 |
| 요리 `rounds` | `round_no` · `food` · `round_sec` · `reaction_sec` · `review_count` · `hint_count` · `answers[]` · `picks[seq, item, is_matched]` | ✅ |
| 서버가 채우는 값 | `organization_id` · `device_id`(토큰) · `uploaded_at` | ✅ ➕ `endedAt`(= 시작 + 걸린 시간 + 일시정지) · `roundStats`(변환 지표) |

### 4-5. §3 컬렉션 · 필드

| 컬렉션 | 구현 | 다른 점 |
|---|---|---|
| organizations | ✅ `name` · `status` | — |
| operators | ✅ | 비밀번호 필드 이름 `passwordHash` · 역할 ➕ `super`(전체관리자 · 기관 없음) · ➕ `passwordUpdatedAt`(바꾸면 기존 로그인 끊김) |
| devices | ✅ 토큰 해시 · 발급 시각/운영자 · `app_version` · `last_seen_at` · `settings` · `status` | ⚠️ `settings.admin_pin_hash` 없음 · ➕ `status.storageFreeBytes` · `clockOffsetSec` · `shutdownRequestedAt` · `deletedAt` |
| participants | ✅ | 그림 번호를 DB 에는 1~8 로 저장(API 는 0~7) · 카드색은 무작위가 아니라 같은 그림끼리 겹치지 않게 배정 · ➕ `deviceId`(주로 쓰는 기기) |
| play_sessions | ✅ | `_id` 를 UUID **문자열**로 저장(§6-7 · BSON Binary 아님) · 사용자 필드 이름 `user` · ➕ `roundStats` · `endedAt` · `reviewCount` |
| 필드 이름 | snake_case | DB 는 camelCase · **API JSON 은 ERD 대로 snake_case** |
| 인덱스 §3-7 | ✅ 모두 | `operators.username` unique · `devices.tokenHash` partial unique · 기관+기간 · 사용자+기간 · 기관+활동+모드+기간 · `storyRunId` partial |
| `device_status_logs` | ❌ | ERD 도 「필요하면」 — 최근 상태만 둔다 |

### 4-6. §4 지표 계산

| 지표 | ERD | 구현 | |
|---|---|---|---|
| 수확 정확하게 선택 | `success_count` | `result === "target"` 인 라운드 수 | ✅ 같은 값 |
| 장보기 정확하게 선택 | `is_matched` **재료 수** | 재료가 **모두** 맞은 **라운드 수** | ⚠️ 기획서 REF-A-07 「10라운드 중 성공한 라운드 수」를 따름 — ERD 는 최대 30 |
| 요리 정확하게 선택 | picks 가 모두 맞은 라운드 수 | 같음 (담은 재료가 1개 이상) | ✅ |
| 반응 시간 | 수확 · 요리 `reaction_sec` · 장보기 `arrive_sec` 평균 | 같음 | ✅ |
| 수확 다른 선택 | `target` 이 아닌 수 | 같음 | ✅ |
| 장보기 다른 선택 | `is_matched: false` 수 | 같음 | ✅ |
| 요리 다른 선택 | 다른 재료가 **섞인 라운드 수** | 다르게 담은 **재료 수** | ⚠️ 합의 필요 |
| 도움 받은 횟수 | `hint_count` | 같음 · 요리 차림표 다시 보기는 따로 | ✅ |
| 기간 평균 · 기록 없으면 「기록 없음」 | | 같음 | ✅ |

ERD §4 도 장보기 · 요리 세부 정의는 「추후 확정」이다. 바뀌면 `admin/src/lib/playSession.ts` 의 `deriveRoundStat` 하나만 고친다 (원시 라운드가 남아 있어 다시 계산할 수 있다).

### 4-7. §6 함께 정할 것 — 현재 가정

| # | 항목 | 지금 구현 |
|---|---|---|
| 6-1 | 관리 번호 | 두지 않음 (관리자 웹은 아이디 · 비밀번호로 확정) |
| 6-2 | 어르신 삭제 시 기록 | 함께 삭제 (설정 `DELETE_KEEPS_RECORDS=1` 이면 「지운 사용자」로 보존) |
| 6-3 | 개인정보 처리 | 미정 — 기관 동의 · 위탁 절차 확인 필요 |
| 6-4 | 보존 기간 | 없음 (TTL 미적용) |
| 6-5 | 비회원 참여 | 기록하지 않음 |
| 6-6 | 서버 → 기기 명령 | 기기 조회 방식으로 구현 (4-1) · 미리보기 제외 |
| 6-7 | UUID 저장 형식 | 문자열 |

### 4-8. 조이랩에 요청할 것

1. B1 상태 보고를 **30초 이하**(권장 5초) 주기로 — 아니면 점검 화면이 꺼짐/켜짐을 오간다
2. 수확 라운드에 **`round_sec`** 를 보내 주기 — 라운드 시간 · 긴 라운드 표시에 필요
3. B1 에 **`storage_free_bytes` · `device_time`** 를 보내 주기 — 점검의 저장 공간 · 날짜 시각
4. 장보기 「정확하게 선택」 · 요리 「다른 선택」 정의 확정 (4-6)
5. `success_count` · `hint_count` 를 게임 화면에서도 같은 규칙으로 세기
6. §6 미결 항목 결정 (4-7)
7. API 경로는 `/api/…` — 93-1 경로 앞에 `/api` (기본 주소에 넣으면 됨)

