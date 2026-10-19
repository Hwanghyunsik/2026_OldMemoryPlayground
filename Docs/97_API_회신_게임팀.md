# 신명놀이터 API 수정 요청 회신 (관리자 웹 → 게임 팀)

> 2026-10-19 · 대상: `96_API_수정요청_관리자웹팀.md` · 회신: 관리자 웹 팀
> 반영 서버: 개발 서버 `https://dev.joylab.kr:33780` (2026-10-19 14시 반영) · 최신 명세: `신명놀이터_API.md` · Unity 가이드 v2.2

## 요청별 결과 — 6건 모두 반영

| # | 항목 | 결과 |
|---|---|---|
| 1 | 장보기 지표 1번 「제대로 산 재료」 | ✅ `is_matched: true` 재료 수로 계산 (관리자 화면 「/ 20」) · **`success_count` 는 그대로 라운드 성공 수 0~10** |
| 2 | 요리 지표 3번 「재료가 달랐던 음식」 | ✅ `is_matched: false` pick 이 하나라도 있는 라운드 수 |
| 3 | 장보기 도움 횟수 | ✅ `Σ items[].hint_count + Σ rounds[].hint_count` — 아래 「주의 1」 참고 |
| 4 | A3 활동별 조회 | ✅ `GET /api/participants/{id}/play-sessions?activity=harvest\|shopping\|cooking&limit=5` |
| 5 | 카드 배경색 8색 | ✅ 게임 시안(바탕 · 테두리)으로 관리자 화면 교체 · `card_color_index` 번호는 그대로 |
| 6 | 미래 `started_at` | ✅ 1시간 넘게 미래여도 **저장** + `clock_suspect` 표시 (7일 넘게 미래만 400) |

이미 저장된 기록 14건도 1 · 2 번 새 정의로 다시 계산했습니다.

## 바뀐 API (게임에서 쓸 수 있는 것)

**A2 `POST /api/play-sessions`**
- 장보기 라운드의 `hint_count`(복귀 유도)를 이제 저장합니다. 보내는 모양은 96 문서 그대로입니다
- `started_at` 이 서버보다 1시간 넘게 미래면 저장한 뒤 응답에 `"clock_suspect": true, "server_time": "…"` 를 붙입니다. 관리자 점검 화면은 그 기기를 24시간 「날짜 · 시각 주의」로 표시합니다
- 7일 넘게 미래면 400입니다. **모든 400 응답에 `server_time`**(서버 지금 시각)이 붙으니, 시계 오류면 보정해 다시 보낼 수 있습니다

**A3 `GET /api/participants/{id}/play-sessions`**
- `?activity=` 를 주면 목록만 그 활동으로 거릅니다. 누적 요약(`summary`)은 늘 전체 활동 기준입니다. 잘못된 값은 400
- 응답에 `"activity"`(거른 활동 · 안 거르면 null) 추가
- `sessions[]` 에 추가:
  - `accuracy_count`: 지표 1번 값. 장보기는 제대로 산 재료 수, 그 밖은 `success_count` 와 같습니다
  - `avg_reaction_sec`: 평균 반응 시간(초)

```json
{ "ok": true, "participant": { "id": "…", "name": "김명희" }, "activity": "shopping",
  "summary": { "count": 7, "days": 3, "total_sec": 2210, "last_played_at": "…" },
  "sessions": [ { "id": "…", "activity": "shopping", "mode": "free", "started_at": "…", "duration_sec": 312.4,
                  "success_count": 3, "accuracy_count": 14, "avg_reaction_sec": 4.1, "hint_count": 12, "device_id": "…" } ] }
```

→ 96 「확인 부탁 2」의 A3 `limit=50` 임시 처리는 `?activity=…&limit=5` 로 바꾸셔도 됩니다.

## 주의 · 확인 부탁

**1. 10/19 시뮬레이션의 장보기 기록 4건 — 도움 횟수를 고칠 수 없음**
그 기록이 들어올 때 서버가 장보기 라운드 `hint_count` 를 모르는 필드로 보고 지운 뒤 저장했습니다(이번에 수정). 원본 값이 서버에 없어서 다시 계산할 수 없습니다. 지금 서버 값은 재료 힌트만 더한 값(예: 김명희 10/15 → 6)입니다.
같은 `id` 로 다시 보내면 「이미 있음(200 duplicate)」으로 처리되어 바뀌지 않습니다. 둘 중 하나를 알려 주세요.
- (가) 저희가 dev 의 테스트 기록 14건을 지우고, 게임이 다시 보냄 (백업은 받아 둠)
- (나) 게임이 **새 `id`** 로 다시 보냄 (옛 기록 4건은 저희가 지움)

**2. 장보기 재료 총량 — 20 인가요 21 인가요**
96 에서 총량을 20(5-6 목표 수 배분)이라고 하셨는데, dev 에 들어온 장보기 4건은 모두 재료가 **21개**입니다(제대로 산 재료 + 다르게 산 재료: 14+7 · 12+9 · 18+3 · 17+4). 게임 쪽 배분을 확인해 주세요.
- 관리자 1회 상세 화면은 실제 개수로 표시합니다(「14 / 21」)
- 활동별 통계의 평균은 「/ 20」으로 고정 표시합니다. 21 이 맞으면 알려 주세요. 한 줄만 고치면 됩니다

**3. dev 서버 확인 범위**
저희는 dev 에 연결된 기기의 토큰을 건드리지 않으려고 dev 에서는 기록 전송 테스트를 하지 않았습니다(로컬 서버에서는 같은 코드로 전부 확인). 다음 시뮬레이션 때 아래를 봐 주시면 됩니다.
- 장보기 A2 응답의 `hint_count` 가 게임 화면 도움 횟수와 같은지
- A3 `?activity=shopping` 응답

## 96 「확인 부탁」 답변

1. **Unity 연동 가이드 · 스크립트** — 저장소의 `신명놀이터_Unity_연동가이드.md`(v2.2)와 `unity/Shinmyeong.Api/` 폴더입니다. 이번에 추가된 것:
   - `ShoppingRound.hint_count`
   - 기록기 `ReturnHintShown()`: 복귀 유도 힌트를 셉니다
   - `FetchMyRecords(…, activity: Activity.Shopping)`
   - 게임에서 직접 구현하셨다면 쓰지 않으셔도 됩니다
2. **1~3 반영 시점** — 2026-10-19 반영 완료 (위 표)
