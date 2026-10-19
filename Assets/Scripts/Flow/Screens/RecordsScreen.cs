using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;
using Shinmyeong.Net;
using Shinmyeong.UI;

namespace Shinmyeong.Flow.Screens
{
    /// SCR-023 개인 누적 성과 이력 (8-5 확정 · 설계서 p38 · 시안 SCR-023).
    /// 누적 요약(함께한 날 · 전체 활동 시간 · 마지막 참여) + 활동별 최근 5회 성공 평균 n/10 +
    /// 같은 5회 구간의 회차별 막대 + 최근 활동 목록 5건 · 「이전으로」(dwell 3초)는 호출 화면 복귀.
    /// 금지: 점수·등급·순위·다른 사용자 비교·감소 구간 강조·상세 수치(ADM 전용) · 기간 필터 없음(최근 5회 고정).
    /// 기록 5회 미만이면 있는 만큼만 평균 내고 빈 막대를 채우지 않는다. 비회원은 진입 버튼 자체가 없다.
    /// 데이터는 서버 A3(2026-10-19): 요약·최근 목록은 전체 호출(limit 20) · 활동별 5회는 `?activity=` 3회 호출. 서버에 닿지 못하면 중립 문구만(평가 표현 금지).
    public class RecordsScreen : ScreenBase
    {
        static readonly (string key, string name, string icon, string ribbon, Color color, Color bar)[] Activities =
        {
            ("Harvest", "수확하기", "Icon-Harvest", "Ribbon-Green", Skin.GreenDeep, Skin.Hex("86a659")),
            ("Shopping", "장보기", "Icon-Shopping", "Ribbon-Orange", Skin.Orange, Skin.Hex("ebb394")),
            ("Cooking", "요리하기", "Icon-Cooking", "Ribbon-Purple", Skin.Purple, Skin.Hex("b69bcf")),
        };

        Text _header;
        Image _avatar;
        Text[] _summaryValues;
        GameObject _dynamicRoot;
        Text _notice;
        int _loadSeq;

        protected override void BuildUi()
        {
            UiKit.Background(transform, "SCR-022-Bg");
            UiKit.Img(transform, "RecordPanel", "SCR-023-Box", 423, 35, 1184, 1008);

            // 헤더 — 아바타 + 「{이름} 님의 기록」
            _avatar = UiKit.ImgFit(transform, "Avatar", "Avatar_01", 511, 68, 119, 119);
            _header = UiKit.Txt(transform, "UserName", 652, 78, 700, 57, "", 40, 6, Skin.Ink, TextAnchor.MiddleLeft);
            UiKit.Txt(transform, "Description", 652, 132, 620, 50, "그동안 하신 활동을 모아 보았습니다", 28, 5, Skin.Brown, TextAnchor.MiddleLeft);

            // 누적 요약 — 쉬운 지표 3종만
            string[] summaryNames = { "함께한 날", "전체 활동 시간", "마지막 참여" };
            _summaryValues = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var card = UiKit.Card(transform, $"Summary{i}", 462 + i * 374.5f, 217, 363, 133, Skin.Face, Skin.Outline);
                UiKit.Txt(card, "Label", 0, 8, 363, 50, summaryNames[i], 25, 7, Skin.BrownDark);
                _summaryValues[i] = UiKit.Txt(card, "Value", 0, 57, 363, 65, "", 25, 7, Skin.Muted);
            }

            // 불러오는 중 · 불러오지 못함 안내 (중립 문구 · 활동 칸 자리)
            _notice = UiKit.Txt(transform, "Notice", 462, 363, 1113, 293, "", 30, 6, Skin.Muted);

            var back = UiKit.NavButton(transform, "BackButton", 92, 478, "이전으로", "Icon-Arrow", () => Flow.GoBack());
            back.gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(좌측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";
        }

        protected override void OnEnter()
        {
            if (_dynamicRoot != null)
                Destroy(_dynamicRoot);
            _dynamicRoot = UiKit.Group(transform, "Dynamic").gameObject;
            transform.Find("BackButton").SetAsLastSibling();

            _header.text = string.IsNullOrEmpty(Flow.UserName) ? "" : $"{Flow.UserName} <size=30>님의 기록</size>";
            ModeSelectScreen.ApplyAvatar(_avatar, Flow);
            foreach (var v in _summaryValues) v.text = "—";
            _notice.text = "기록을 불러오고 있어요";
            Load(++_loadSeq);
        }

        protected override void OnExit() => _loadSeq++; // 늦게 온 응답은 버린다

        async void Load(int seq)
        {
            if (string.IsNullOrEmpty(Flow.UserId))
            {
                _notice.text = "";
                return;
            }
            var all = await MyRecords.FetchAll(Flow.UserId, 20);
            if (seq != _loadSeq) return;
            if (!all.Ok)
            {
                _notice.text = "지금은 기록을 불러올 수 없어요\n잠시 후 다시 봐 주세요";
                return;
            }
            var byActivity = new List<PlaySessionSummaryDto>[Activities.Length];
            for (int i = 0; i < Activities.Length; i++)
            {
                var res = await MyRecords.FetchByActivity(Flow.UserId, Activities[i].key, 5);
                if (seq != _loadSeq) return;
                byActivity[i] = res.Ok ? res.Data.sessions : null;
            }

            _notice.text = "";
            BuildSummary(all.Data);
            BuildActivityColumns(byActivity);
            BuildRecentList(all.Data.sessions);
        }

        static string Big(string number, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><size=50>{number}</size></color>";

        void BuildSummary(MyRecordsResponse data)
        {
            var s = data.summary;
            bool any = s != null && s.count > 0;
            _summaryValues[0].text = any ? $"{Big(s.days.ToString(), Skin.Green)} 일" : "—";
            int sec = any ? Mathf.FloorToInt(s.total_sec) : 0; // 서버 합계 = duration 합 (일시정지 시간은 게임 계측에서 이미 제외)
            _summaryValues[1].text = !any ? "—"
                : sec >= 3600 ? $"{Big((sec / 3600).ToString(), Skin.Green)} 시간  {Big((sec % 3600 / 60).ToString(), Skin.Green)} 분"
                : $"{Big((sec / 60).ToString(), Skin.Green)} 분  {Big((sec % 60).ToString(), Skin.Green)} 초";
            var (month, day) = MonthDay(any ? MyRecords.ToLocal(s.last_played_at) : "");
            _summaryValues[2].text = any && month > 0 ? $"{Big(month.ToString(), Skin.Green)} 월  {Big(day.ToString(), Skin.Green)} 일" : "—";
        }

        /// 활동별 최근 5회 평균(반올림 정수) + 같은 5회 구간의 회차별 막대 (왼쪽이 오래된 회차 · 최근 회차만 진한 색)
        void BuildActivityColumns(List<PlaySessionSummaryDto>[] byActivity)
        {
            for (int i = 0; i < Activities.Length; i++)
            {
                var (key, name, icon, ribbon, color, barColor) = Activities[i];
                var recent = new List<PlaySessionSummaryDto>();
                if (byActivity[i] != null)
                {
                    recent.AddRange(byActivity[i]); // 서버는 최신순
                    recent.Reverse();               // 막대는 오래된 것이 왼쪽
                }

                var card = UiKit.Card(_dynamicRoot.transform, $"Activity_{key}", 463 + i * 374.5f, 363, 363, 293, Skin.Face, color);
                UiKit.ImgFit(card, "Icon", icon, 22, 24, 134, 132);
                UiKit.Txt(card, "Title", 167, 10, 183, 58, name, 35, 7, Skin.BrownDark);
                var rb = UiKit.ImgFit(card, "Ribbon", ribbon, 173, 69, 175, 35);
                UiKit.Txt(rb.transform, "Label", 0, 0, 175, 35, "최근 5회 평균", 21, 7, Color.white);
                UiKit.Txt(card, "ChartLabel", 259, 236, 84, 44, "최근 5회", 18, 5, Skin.Muted);

                if (recent.Count == 0)
                {
                    UiKit.Txt(card, "Value", 176, 111, 140, 65, "—", 40, 7, Skin.Muted);
                    continue;
                }

                int sum = 0;
                foreach (var r in recent)
                    sum += r.success_count;
                int avg = Mathf.FloorToInt((float)sum / recent.Count + 0.5f); // 반올림 · 소수점 미표시 (확정)
                UiKit.Txt(card, "Value", 150, 111, 100, 65, avg.ToString(), 50, 7, color, TextAnchor.MiddleRight);
                UiKit.Txt(card, "Total", 254, 121, 70, 55, "/10", 30, 5, Skin.Muted, TextAnchor.MiddleLeft);

                // 회차별 막대 — 있는 만큼만 그린다 (빈 막대 미표시 · 확정) · 높낮이만 사실대로
                var chart = UiKit.Node(card, "RecentFiveChart", 31, 190, 210, 82);
                for (int b = 0; b < recent.Count; b++)
                {
                    float h = 8f + 74f * (recent[b].success_count / 10f);
                    bool latest = b == recent.Count - 1;
                    UiKit.Img(chart, $"Bar{b + 1}", "Box-Round-5", b * 43, 82 - h, 38, h, latest ? color : barColor, sliced: true);
                }
            }
        }

        /// 최근 활동 목록 5건 — 날짜 · 활동 · 결과(성공 n개) · 소요 시간 · 최신이 위. 스토리 한 판(같은 story_run_id)은 한 줄로 묶는다
        void BuildRecentList(List<PlaySessionSummaryDto> sessions)
        {
            var box = UiKit.Card(_dynamicRoot.transform, "RecentActivities", 462, 671, 1113, 336, Skin.FaceWarm, Skin.Outline, "Box-Round-20");
            UiKit.Txt(box, "Title", 32, 15, 200, 45, "최근 활동", 28, 7, Skin.BrownDark, TextAnchor.MiddleLeft);

            // 같은 story_run_id 는 목록에서 떨어져 있어도 한 줄로 (최신순 목록이라 첫 등장 위치가 줄 순서)
            var rows = new List<List<PlaySessionSummaryDto>>();
            var used = new HashSet<string>();
            for (int i = 0; i < sessions.Count && rows.Count < 5; i++)
            {
                if (used.Contains(sessions[i].id)) continue;
                var group = new List<PlaySessionSummaryDto> { sessions[i] };
                used.Add(sessions[i].id);
                string run = sessions[i].story_run_id;
                if (!string.IsNullOrEmpty(run))
                    for (int j = i + 1; j < sessions.Count && group.Count < 3; j++)
                        if (!used.Contains(sessions[j].id) && sessions[j].story_run_id == run)
                        {
                            group.Add(sessions[j]);
                            used.Add(sessions[j].id);
                        }
                group.Sort((a, b) => Order(a.activity).CompareTo(Order(b.activity))); // 수확 → 장보기 → 요리
                rows.Add(group);
            }

            if (rows.Count == 0)
                UiKit.Txt(box, "Empty", 0, 60, 1113, 240, "아직 함께한 활동이 없어요", 28, 6, Skin.Muted);

            for (int r = 0; r < rows.Count; r++)
            {
                var group = rows[r];
                var row = UiKit.Node(box, $"Row{r + 1}", 23, 69 + r * 48.5f, 1067, 50);
                UiKit.Img(row, "Face", "Box-Round-10", 0, 0, 1067, 50, Color.white, sliced: true);
                foreach (float dx in new[] { 156f, 417f, 917f })
                {
                    var div = UiKit.Img(row, "Divider", null, dx, 0, 2, 50, Skin.Hex("c8c0af"));
                    div.sprite = null;
                }
                bool story = group.Count > 1 || group[0].mode == "story";
                float totalSec = 0f;
                var parts = new List<string>();
                foreach (var rec in group)
                {
                    totalSec += rec.duration_sec;
                    var act = Find(MyRecords.ActivityKey(rec.activity));
                    parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(act.color)}>{act.name} {rec.success_count}개</color>");
                }
                var first = Find(MyRecords.ActivityKey(group[0].activity));
                UiKit.Txt(row, "Date", 25, 0, 130, 50, FormatDate(MyRecords.ToLocal(group[0].started_at)), 22, 6, Skin.BrownDark, TextAnchor.MiddleLeft);
                UiKit.Txt(row, "Activity", 187, 0, 202, 50, story ? "스토리 게임" : first.name, 22, 6, story ? Skin.BrownDark : first.color, TextAnchor.MiddleLeft);
                UiKit.Txt(row, "Result", 439, 0, 470, 50, string.Join(" · ", parts), 22, 6, Skin.BrownDark, TextAnchor.MiddleLeft);
                UiKit.Txt(row, "Duration", 931, 0, 120, 50, FormatMinutes(totalSec), 22, 6, Skin.Muted);
            }
        }

        static int Order(string activityCode) => activityCode == "harvest" ? 0 : activityCode == "shopping" ? 1 : 2;

        static (string key, string name, string icon, string ribbon, Color color, Color bar) Find(string key)
        {
            foreach (var a in Activities)
                if (a.key == key)
                    return a;
            return (key, key, "", "", Skin.BrownDark, Skin.Tan);
        }

        static string DatePart(string startedAt) =>
            string.IsNullOrEmpty(startedAt) || startedAt.Length < 10 ? "" : startedAt.Substring(0, 10);

        static (int month, int day) MonthDay(string startedAt)
        {
            var parts = DatePart(startedAt).Split('-');
            if (parts.Length < 3 || !int.TryParse(parts[1], out int month) || !int.TryParse(parts[2], out int day))
                return (0, 0);
            return (month, day);
        }

        /// "yyyy-MM-dd HH:mm:ss" → "n월 n일"
        internal static string FormatDate(string startedAt)
        {
            var (month, day) = MonthDay(startedAt);
            return month == 0 ? DatePart(startedAt) : $"{month}월 {day}일";
        }

        static string FormatMinutes(float sec)
        {
            int m = Mathf.Max(1, Mathf.RoundToInt(sec / 60f));
            return $"{m}분";
        }
    }
}
