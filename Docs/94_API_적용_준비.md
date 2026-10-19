# 서버 API 적용 준비 — 명세 ↔ 게임 코드 대조 · 작업 순서

> 작성: 2026-10-19 · 근거: `Docs/신명놀이터_API.md`(관리자 웹 팀 · 2026-10-16 · 이하 「명세」) · `93-1_서버_ERD_공유본` v0.2
> 상태: **준비 완료 — 적용(화면·저장 계층 교체) 착수 전.** 이 문서의 3장 순서대로 적용한다.
> 2026-10-19 추가: 명세를 기획 확정(07 문서)과 대조한 결과 서버 쪽 수정이 필요한 항목 6건 → **7장 · 회신 문서 `Docs/95_API_회신_관리자웹팀.md`**
> 2026-10-19 진행: **3장 1단계(설치 D1~D3) 완료** — `DeviceAuth`·`LoginScreen`·`DeviceSelectScreen` 실호출 전환, 개발 서버에서 로그인 → 기기 「1층」 연결 → A1·B2·명령·B1 모두 200 확인(6장 기초 테스트 메뉴)

> **2026-10-19 오후 — 3장 1~5단계 적용 완료.** 게임 본편이 서버를 쓴다: 설치(D1~D3) · 기기 상주 `DeviceLink`(B1·명령·B2·발신함) · SCR-003 서버 사용자 목록 · 완주 기록 → 발신함 → A2 · SCR-005/023 서버 A3. 로컬 users.json·records/ 는 더 쓰지 않는다. 적용 결과는 3장 끝 「적용 결과」, 1장 표의 「적용 시 바꿀 것」은 그대로 반영됨(차이는 거기 표기).

## 0. 준비된 것 (2026-10-19)

| 항목 | 내용 |
|---|---|
| 호출 계층 | `Assets/Scripts/Net/ApiClient.cs` — UnityWebRequest · JSON · Bearer · 5초 시간 초과 · 실패 봉투(`message`·`issues`) 해석 · `Retryable`(0/5xx/429) 구분. `await` 기반(Unity Awaitable) |
| 접속 설정 | `Net/ApiConfig.cs` — 기본 `https://dev.joylab.kr:33780/api` · 실행 인자 `-api <url>` · `persistentDataPath/api_config.json` · 호출 주기 상수(B1 5초 · 명령 5초 · B2 60초 · 큐 30초) · ISO 시각(시간대 포함) |
| 요청·응답 모양 | `Net/ApiModels.cs` — D1~D3 · A1 · A2(활동별 본문 3종) · A3 · B1 · B2 · 명령. 필드명은 명세 snake_case 그대로(JsonUtility) |
| 기록 변환 | `Net/PlaySessionMapper.cs` — `PlayRecord` → A2 본문. 값 변환 규칙은 2장 |
| 기록 모델 보강 | `PlayRecord.SessionId`(UUID · 게임 시작 시 생성 · 멱등 키) · `PlayRecord.StoryRunId`(스토리 선택 시 `FlowManager.StoryRunId` 생성 → CommitPlay 가 채움) · `HarvestRoundRecord.RoundSec`(명세 4-8 요청 2) |
| 개발 도구 | 에디터 메뉴 `Shinmyeong/API/` — `발신함 보기 · 지금 보내기`(로컬 기록 미리보기 메뉴는 로컬 기록 폐기로 교체) · `서버 연결 확인`(GET auth/login → 405 기대) · `기초 테스트` · `게임 시뮬레이션` · `A3 되읽기` |
| 검증 | 컴파일 오류 0 · 로컬 기록 3종 변환 결과가 명세 1-8 모양과 일치 · 개발 서버 응답 `405 「이 경로는 POST 만 받습니다」` — 주소·HTTPS 인증서·JSON 봉투 확인됨 |

## 1. 게임이 부르는 호출 ↔ 현재 코드

| # | 호출 | 시점 | 현재 코드 | 적용 시 바꿀 것 |
|---|---|---|---|---|
| D1 | `POST auth/login` | 설치 · 운영자 로그인 | `DeviceAuth.Login` — 더미 `admin/1234` | `ApiClient.Post<LoginResponse>` · 운영자 토큰은 메모리에만(30분) · 401/403/429 `message` 를 `LoginScreen` 안내 문구로 |
| D2 | `GET devices` | 설치 · 기기 목록 | `DeviceAuth.LoadDevices` — 더미 3대 | `DevicesResponse.devices` → `DeviceInfo(id, name, in_use)` (화면 코드 변경 없음) |
| D3 | `POST devices/{id}/token` | 설치 · 기기 선택 | `DeviceAuth.SelectDevice(device, replace)` — 가짜 토큰 PlayerPrefs | `{replace}` 전송 · 201 → 토큰 저장 · 409 `in_use` → false(현재 「이 PC로 옮기기」 확인 단계와 그대로 맞물림) |
| A1 | `GET participants` | SCR-003 진입마다 | `SaveStore.LoadUsers()` — 로컬 users.json | 서버 목록을 `UserProfile` 로 변환(2-1) 하고 **마지막 목록을 로컬에 캐시**(서버 불통 시 캐시 · 93-1 §5-2). 에디터 「+ 테스트 사용자」 버튼은 서버 등록이 웹 몫이라 제거 |
| A2 | `POST play-sessions` | 10라운드 완주 | `FlowManager.CommitPlay → SaveStore.AppendRecord` | 본문을 **발신함(파일 큐)** 에 먼저 쓰고 전송 → 201/200 이면 삭제 · `Retryable` 이면 30초마다·다음 실행 때 재전송 · 400/404/409 는 버리고 로그 · 401 은 기기 연결 해제 → 로그인 화면(큐는 보존) |
| A3 | `GET participants/{id}/play-sessions?limit=…` | SCR-023 진입 | `SaveStore.LoadRecords` — 로컬 전체 기록 | `summary`(days · total_sec · last_played_at) 로 요약 3칸 · `sessions` 로 활동별 최근 5회·최근 활동 5건. **활동별 최근 5회를 뽑으려면 `limit=5` 로는 부족** → `limit=50` 으로 받아 게임에서 활동별로 자른다(5장 Q4) |
| B1 | `PUT devices/me/status` | 5초마다 | 없음 | 새 `Net/DeviceLink.cs`(상주 MonoBehaviour): `camera_connected` ← `WebcamFeed.IsReady` · `person_detected` ← `BodyTrackingService.PersonPresent` · `game_state` ← `FlowManager.CurrentId` · `storage_free_bytes` ← persistentDataPath 드라이브 여유 · `device_time` ← `ApiConfig.NowIso()` |
| B2 | `GET devices/me/settings` | 켤 때 · 60초 | 없음 | `DeviceLink` 가 읽어 정적 `DeviceSettings.VolumeLevel/VoiceEnabled` 에 보관 → 사운드·TTS 도입 시 적용(지금은 적용 대상 없음) |
| — | `GET devices/me/commands` · `POST …/ack` | 5초마다 | 없음 | `shutdown_requested_at > 게임 켠 시각` 이면 ack 후 `Application.Quit()` |
| 공통 | 401(기기 토큰) | 아무 호출 | — | `DeviceAuth.Clear()` → `ScreenId.LOGIN` (명세 3-5) |

## 2. 기록 변환 규칙 (`PlaySessionMapper`)

| 게임(PlayRecord) | 서버(A2) | 변환 |
|---|---|---|
| `SessionId` | `id` | 그대로(UUID). 비어 있으면 새로 생성 |
| 로컬 사용자 `Id` | `participant_id` | **서버 A1 의 `id`** — A1 적용 후 `UserProfile.Id` 가 서버 id 가 되므로 그대로 |
| `Activity` Harvest/Shopping/Cooking | `activity` | `harvest` / `shopping` / `cooking` |
| `Mode` Story/Free | `mode` | `story` / `free` |
| `StoryRunId` | `story_run_id` | 스토리만 UUID · 개별은 `""`(서버가 null 로 봄) |
| `StartedAt` `yyyy-MM-dd HH:mm:ss`(로컬) | `started_at` | 이 PC 시간대를 붙인 ISO(`…+09:00`). 로컬 포맷은 SCR-023 날짜 파싱이 쓰므로 유지 |
| `DurationSec` · `PausedSec` · `SuccessCount` · `HintCount` | 같은 이름 | 그대로(서버는 success/hint 를 다시 계산) |
| 수확 `Result` 정답/다른 선택/벌레 | `result` | `target` / `other` / `bug` |
| 수확 `RoundSec` | `round_sec` | 라운드 시작(작물 배치)부터 당김 확정까지 — 담김 연출 제외 |
| 장보기 `ShoppingItems`(재료별 평탄 ≤20건) + `ShoppingRoundSecs` + `ShoppingRoundHints` | `rounds[].items[]` · `rounds[].round_sec` · `rounds[].hint_count` | `Round` 로 묶고 `seq` 1~3 부여 · 라운드 시간과 **복귀 유도 힌트 수(라운드 수준)** 는 같은 번호의 목록에서 · 라운드 `hint_count` 는 서버 합산 반영됨(2026-10-19 · 97) |
| 장보기 `Correct` | `is_matched` | 그대로 |
| 요리 `Picks` | `picks[]` | `seq` 1~3 부여 · `Correct → is_matched` |
| 요리 `Answers` | `answers[]` | 그대로 (2026-10-07 이전 로컬 기록은 비어 있음 — 테스트 데이터라 무시) |
| — | `app_version` | `Application.version`(현재 `0.1.0` — 5장 Q5) |

### 2-1. 사용자(A1 → `UserProfile`)

| 서버 | 게임 | 비고 |
|---|---|---|
| `id` | `Id` | 서버 ObjectId 문자열 |
| `name` | `Name` | |
| `gender` `M`/`F` | `Gender` | 게임은 표시에 쓰지 않음(통계 전용) — 서버 값 그대로 보관 |
| `avatar_index` 0~7 | `AvatarIndex` | 같은 범위 |
| `card_color_index` 0~7 | `CardColorIndex` | `UserSelectScreen.CardColors` 8색과 명세 1-4 색표 대조 필요(5장 Q6) |
| `device_id` | (없음) | 지금은 쓰지 않음 (`?device=me` 도 쓰지 않음 — 기관 전체 사용자 표시 · 93-1 원칙 2) |

### 2-2. 게임 화면 성공 횟수 ↔ 서버 계산 (명세 4-6 · 4-8 요청 5)

| 활동 | 게임 `SuccessCount` | 서버 `success_count` | 일치 |
|---|---|---|---|
| 수확 | 목표 작물을 당긴 라운드 수(힌트 후 포함) | `result === "target"` 라운드 수 | ✅ |
| 장보기 | 목표를 전부 그대로 산 라운드 수(`roundAllCorrect`) | 재료가 모두 `is_matched` 인 라운드 수 | ✅ (5-14 #9 라운드 성공 0~10 — SCR-022 · SCR-023 입력값) |
| 요리 | 담은 재료가 모두 맞은 라운드 수 | picks 가 모두 `is_matched` 인 라운드 수 | ✅ |

→ `success_count` 는 일치. **그러나 ADM 활동별 지표는 다르다**(2026-10-19 07 문서 18-3-1 · 5-14 대조): 장보기 1번 지표 「제대로 산 재료」는 **재료 단위(최대 20)**, 요리 3번 지표 「재료가 달랐던 음식」은 **라운드 단위** — 명세 구현은 둘 다 반대로 돼 있어 서버 수정 요청(95 R1 · R2). 장보기 도움 횟수도 복귀 유도 힌트가 빠짐(95 R3).

## 3. 적용 작업 순서

각 단계는 독립적으로 플레이 확인이 가능하도록 끊었다. 1→2 는 테스트 계정·기기가 있어야 한다(5장 Q2).

1. **설치 흐름 D1~D3** — `DeviceAuth` 내부를 `ApiClient` 호출로 교체(동기 bool → `Awaitable<ApiResult>`), `LoginScreen`·`DeviceSelectScreen` 은 대기 표시·`message` 안내만 추가. 더미 계정 푸터 제거
2. **기기 상주 연결 `DeviceLink`** — B1 5초 · 명령 5초 · B2 60초 · 401 처리 · 게임 켠 시각 보관. FlowManager 옆에 1개 상주
3. **사용자 목록 A1** — `SaveStore.LoadUsers` 를 「서버 목록 → 캐시 갱신, 실패 시 캐시」로. SCR-003 진입이 비동기가 되므로 `UserSelectScreen.OnEnter` 에서 캐시로 먼저 그리고 응답 오면 다시 그림. `AddUser/UpdateUser/DeleteUser` 와 에디터 버튼 제거
4. **참여 기록 A2 + 발신함** — `FlowManager.CommitPlay` → `Outbox.Enqueue(record, participantId)`(파일 `persistentDataPath/Outbox/<SessionId>.json`) → `DeviceLink` 가 즉시·30초마다·시작 때 비움. 로컬 `records/<id>.json` 은 A3 전환 뒤 제거
5. **내 기록 A3** — `RecordsScreen` 이 `MyRecordsResponse` 로 그림(요약 3칸 · 활동별 최근 5회는 **`?activity=…&limit=5`** 3회 호출(2026-10-19 서버 반영 · `accuracy_count`·`avg_reaction_sec` 포함) · 최근 5건·스토리 묶기는 전체 `limit` 호출에서 `story_run_id`). 실패 시 「지금은 기록을 불러올 수 없어요」 류 중립 문구(평가 표현 금지 · 기획 금지 사항)
6. **정리** — `SaveStore` 를 캐시·발신함 전용으로 축소 · 93/93-1/91 문서에 적용 결과 반영 · 명세 4-8 회신

### 적용 결과 (2026-10-19 · 개발 서버에서 확인)

| 단계 | 상태 | 코드 | 1장 계획과 다른 점 · 확인 |
|---|---|---|---|
| 1 설치 D1~D3 | ✅ | `Save/DeviceAuth` · `LoginScreen` · `DeviceSelectScreen` | 기기 새로 연결하면 `DeviceLink.OnDeviceLinked()` 로 주기 호출 즉시 재개 |
| 2 기기 상주 | ✅ | **`Net/DeviceLink.cs`** — `RuntimeInitializeOnLoadMethod` 로 자동 생성(씬 배치 불필요) · B1 5초 · 명령 5초 · B2 60초 · 발신함 30초 | `camera_connected` 는 `BodyTrackingService.ActiveProvider` 가 `MoveNetPoseProvider` 이고 `IsRunning` 일 때(에디터 Mock 은 false). B2 는 `AudioListener.volume` 에 1→0.4 · 2→0.7 · 3→1.0 으로 바로 적용 · `VoiceEnabled` 는 보관(TTS 도입 시). 주기 호출 실패는 콘솔에 쓰지 않음(`quiet`) · 연결 상태 바뀔 때만 로그. 종료 명령은 ack 후 즉시 종료(에디터는 플레이 중단) |
| 2 401 처리 | ✅ | `ApiClient`(기기 토큰 호출 401 → `DeviceLink.NoteUnauthorized`) · `FlowManager.Go(SCR_001)` | 7장 설계대로 **플레이 중에는 바꾸지 않고 SCR-001 복귀 때** `DeviceAuth.Clear()` → LOGIN. 발신함은 보존 |
| 3 사용자 A1 | ✅ | **`Save/ParticipantStore.cs`**(A1 → `UserProfile` · `SaveData/participants_cache.json`) · `UserSelectScreen` | 캐시로 먼저 그리고 응답 오면 다시 그림 · 실패 시 캐시 유지 · 캐시도 없으면 「지금은 명단을 불러올 수 없어요」. 에디터 「+ 테스트 사용자」 제거. 아바타는 `FlowManager.UserAvatarIndex` 로(목록 재조회 없음) |
| 4 기록 A2 | ✅ | **`Net/Outbox.cs`**(`persistentDataPath/Outbox/<시각>_<SessionId>.json`) · `FlowManager.CommitPlay` → `Outbox.Enqueue` → `DeviceLink.FlushSoon()` | 201/200 삭제 · 400/404/409 → `Outbox/rejected/` + `.reason.txt` 보관 · 401/네트워크/5xx/429 는 그 자리에서 멈춤(순서 보존). **400 + `server_time` + `started_at` issue 면 시계 차이만큼 보정해 1회 재전송**(R6). 플레이 모드 실측: 수확 1판 커밋 → 발신함 1건 → 즉시 A2 201(성공 7 일치) → 0건 |
| 5 내 기록 A3 | ✅ | **`Net/MyRecords.cs`** · `RecordsScreen`(전면 서버 응답) · `LobbyScreen` 최근 참여 | 요약·최근 5건은 전체 `limit=20`, 활동별 5회는 `?activity=&limit=5` 3회(Q4 해소 · `limit=50` 폐기) · 로비 최근 참여는 활동별 `limit=1` · 스토리 묶음은 `story_run_id` 로(인접하지 않아도) · 실패 시 「지금은 기록을 불러올 수 없어요」 · 늦게 온 응답은 화면 이탈 시 버림(seq) |
| 6 정리 | 🔶 | `SaveStore` → `LoadJson/WriteJson` 캐시 도우미만 · `RecordDatabase` 제거 · `SaveModels` 주석 | 93/93-1/91 문서 반영과 명세 4-8 회신(95 로 대체) 은 미완 — 실기기 1차 확인 뒤 |

## 4. 명세 4-8 「조이랩에 요청할 것」 대응

| # | 요청 | 대응 |
|---|---|---|
| 1 | B1 30초 이하(권장 5초) | `ApiConfig.StatusIntervalSec = 5` |
| 2 | 수확 `round_sec` | `HarvestRoundRecord.RoundSec` 추가 · 본문에 포함 |
| 3 | B1 `storage_free_bytes` · `device_time` | `DeviceStatusRequest` 에 포함(단계 2 에서 채움) |
| 4 | 장보기 정확 · 요리 다른 선택 정의 | **기획 확정값으로 서버 수정 요청** — 장보기 1번 지표 재료 단위 · 요리 3번 지표 라운드 단위 (95 R1 · R2) |
| 5 | success/hint 를 같은 규칙으로 | `success_count` 충족 · 장보기 `hint_count` 는 라운드 수준 복귀 유도 힌트 추가 전송 + 서버 합산 요청 (95 R3) |
| 6 | §6 미결 | 6-1 관리 번호 없음 · 6-2 삭제 시 기록 함께 삭제 · 6-5 비회원 미기록 · 6-7 UUID 문자열 — 모두 게임 쪽 현재 구현과 맞음(그대로 수용). 6-3 개인정보 · 6-4 보존 기간은 기획·기관 결정 |
| 7 | 경로 `/api/…` | 기본 주소에 `/api` 포함 |

## 5. 확인·결정이 필요한 것

| # | 항목 | 왜 |
|---|---|---|
| Q1 | 명세가 가리키는 **`신명놀이터_Unity_연동가이드.md` · `unity/Shinmyeong.Api` 스크립트**를 받았는지 | 97 회신으로 v2.2 가 관리자 웹 팀 저장소에 있음을 확인(`ShoppingRound.hint_count` · `ReturnHintShown()` · `FetchMyRecords(activity:)`) — 파일은 미수령 → 98 에서 공유 요청. 게임은 자체 `Net/` 계층 유지(관리자 팀도 「직접 구현했다면 안 써도 됨」) · 받으면 대조만 |
| Q2 | 개발 서버 **테스트 계정·테스트 기기** | ✅ 2026-10-19 사용자 제공 — 기관 「신명복지관」 운영자 계정 `shin1234`(비밀번호는 `api_config.json` 에만) · 기기 「1층」 1대 · 사용자 2명(김명희 · 조복순). 명세 기본값 `manager/manager1234` 는 개발 서버에 없음(401 · 5회 실패 시 429 잠금 확인). 운영 서버에서는 토큰을 새로 발급하지 않는다(명세 ⚠️) |
| Q3 | 기존 로컬 `users.json`·`records/` | 전부 테스트 데이터 → 이관 없이 폐기 가정 |
| Q4 | A3 `limit` | ✅ 2026-10-19 서버에 `?activity=harvest\|shopping\|cooking&limit=5` 반영(97) · dev 에서 세 값 모두 확인(`summary` 는 전체 기준 유지). 5단계에서 활동별 3회 호출로 전환 · 임시 `limit=50` 은 시뮬레이션 전체 목록 확인용으로만 |
| Q5 | `app_version` 체계 | `ProjectSettings bundleVersion` 이 `0.1.0`. 서버 기기 표에 그대로 표시됨 — 빌드마다 올릴 규칙 정하기(명세 예시 `0.9.0`) |
| Q6 | 카드색 8색 | 명세 1-4 색표(`0 #F6DCD2` …)와 `UserSelectScreen.CardColors` 대조 → 다르면 시안 색을 유지할지 서버 표준을 따를지 |
| Q7 | 기획 개정 | 외부 서버(18-1 · 6-1 충돌)·관리 화면 TV 표시는 93 문서 Q1 그대로 대기 중 — 적용 자체는 진행하되 「기획 승인 대기」 표시 유지 |

## 7. 우리 기준(기획 확정 · 게임 코드)과 다른 점 — 2026-10-19 대조

원칙: **A2 본문은 명세와 호환 유지(필드 추가만)** → 관리자 화면이 계속 읽을 수 있다. 계산 규칙은 서버 몫이라 수정 요청(95 문서)으로, 게임 쪽에서 맞출 수 있는 것은 바로 반영.

**2026-10-19 14시 — R1~R6 전부 서버 반영 완료**(관리자 웹 팀 회신 `Docs/97`). 같은 날 15:11 시뮬레이션 재전송으로 R3(장보기 `hint_count` 14건 일치) · R4(`?activity=`) · R6(`clock_suspect`·`server_time`) 확인. 우리 확인 회신은 `Docs/98`. 남은 것: 장보기 재료 총량 21(게임 · 07 5-6 표) vs 20(07 본문 · 관리자 통계 분모) — 기획 Q2 회신 대기.

| # | 항목 | 기획 근거 | 명세 | 처리 |
|---|---|---|---|---|
| R1 | 장보기 ADM 1번 「제대로 산 재료」 | 18-3-1 · 5-14 #8 — 재료 단위(최대 20) | 라운드 단위 | ✅ 반영 — `is_matched` 재료 수 · A3 `accuracy_count` 로도 내려옴. `success_count` 는 라운드 단위 유지(5-14 #9) |
| R2 | 요리 ADM 3번 「재료가 달랐던 음식」 | 18-3-1 · 6-12 — 라운드 단위 | 재료 단위 | ✅ 반영 — 다른 pick 이 있는 라운드 수 (93-1 ERD §4 정의가 맞았음) |
| R3 | 장보기 복귀 유도 힌트 | 18-4 도움 = 자동 힌트 발동 횟수(게임도 포함해 셈) | items 합만 집계 | ✅ 반영 — 서버가 `Σ items[].hint_count + Σ rounds[].hint_count`. 게임: `ShoppingRoundHints` 기록 + `rounds[].hint_count` 전송. 10/19 12시 수신분 4건은 복구 불가 → 15:11 새 id 로 재전송(옛것 삭제 요청) |
| R4 | SCR-023 활동별 최근 5회 · 스토리 묶음 5건 | 8-5 | A3 `limit` 뿐 | ✅ 반영 — `?activity=…&limit=5` · 응답 `activity` · `sessions[].accuracy_count`·`avg_reaction_sec` 추가 |
| R5 | 카드 배경색 8색 | 디자인 시안(`UserSelectScreen.CardColors`) | 명세 색표 8색이 다름 | ✅ 반영 — 관리자 화면이 시안 색(바탕·테두리)으로 교체 · index 체계 동일 |
| R6 | `started_at` 1시간 초과 미래 → 400 | 운영 안정(시설 PC 시계) · 기록 유실 금지 | 400 · 미재시도 → 유실 | ✅ 반영 — 1시간 초과는 저장 + 응답 `clock_suspect:true, server_time` · 7일 초과만 400 · **모든 400 에 `server_time`** → 4단계 발신함에서 시계 오류면 보정 재전송 |
| — | 장보기 재료 총량 | 07 5-6 표 = 21(R1~2 1 · R3~4 2 · R5~7 2 · R8~10 3) vs 본문·요약표 = 20 — **기획 Q2(2026-08-26) 미회신** | 관리자 통계 분모 「/ 20」 고정 | 게임은 표대로 21 유지 · 98 에서 「실제 재료 수(Σ items)를 분모로」 요청 · 기획 회신 오면 통보 |
| — | 수확 `reaction_sec` t=0 | 4-4 「시작 강조 종료 시점」 | 「목표 제시 → 확정」 | 정의 안내만(값은 게임이 보냄) |
| — | 끝내기 3종 | 18-2 — 사용자 화면에 닿는 것은 「프로그램 종료」뿐 | `shutdown` 한 종류 | 일치 |
| — | 사용자 삭제 시 기록 | FN-20 함께 삭제 | 기본 삭제 | 일치 |
| — | 비회원 · 중도 종료 | 6-1 · 6-2 미기록 | A2 완주 시만 | 일치 |
| — | 401(기기 토큰 거부) 처리 시점 | 18-1 어르신 화면에 관리 화면 금지 | 「설치 화면으로」 | **게임 설계**: 플레이 중에는 전환하지 않고 SCR-001 대기 화면 복귀 때 로그인 화면으로 (2단계 DeviceLink 에서 구현) |

## 6. 개발용 사용법

| 수단 | 내용 |
|---|---|
| 실행 인자 `-api http://localhost:3000/api` | 같은 PC 의 관리자 서버로 |
| `persistentDataPath/api_config.json` | `{ "base_url": "http://…/api" }` — 인자가 없을 때 |
| 메뉴 `Shinmyeong/API/서버 연결 확인` | 주소·인증서·JSON 봉투 확인(405 가 정상) |
| 메뉴 `Shinmyeong/API/기초 테스트` | `api_config.json` 의 `test_username` · `test_password` (· `test_device` = D3 까지 시험할 기기 이름)로 D1 → D2 → [D3] → 기기 토큰이 있으면 A1 · B2 · 명령 · B1 실호출 후 결과를 콘솔에 한 번에 출력. 사용 중인 기기는 빼앗지 않는다. **계정 정보는 저장소 밖(persistentDataPath)에만 둔다** |
| 메뉴 `Shinmyeong/API/로컬 기록 → A2 본문 미리보기` | 사용자별 활동 마지막 1건을 A2 JSON 으로 `persistentDataPath/ApiPreview/` 에 저장 + 콘솔 |
| 메뉴 `Shinmyeong/기기 연결 해제` · 인자 `-relogin` | 설치 흐름 다시 보기 |
