using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;
using Shinmyeong.Save;
using Shinmyeong.UI;

namespace Shinmyeong.Flow.Screens
{
    /// SCR-023 개인 누적 성과 이력 (8-5 확정 · 설계서 p38 · 시안 SCR-023).
    /// 누적 요약(함께한 날 · 전체 활동 시간 · 마지막 참여) + 활동별 최근 5회 성공 평균 n/10 +
    /// 같은 5회 구간의 회차별 막대 + 최근 활동 목록 5건 · 「이전으로」(dwell 3초)는 호출 화면 복귀.
    /// 금지: 점수·등급·순위·다른 사용자 비교·감소 구간 강조·상세 수치(ADM 전용) · 기간 필터 없음(최근 5회 고정).
    /// 기록 5회 미만이면 있는 만큼만 평균 내고 빈 막대를 채우지 않는다. 비회원은 진입 버튼 자체가 없다.
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

            var records = string.IsNullOrEmpty(Flow.UserId)
                ? new List<PlayRecord>()
                : SaveStore.LoadRecords(Flow.UserId).Records;

            BuildSummary(records);
            BuildActivityColumns(records);
            BuildRecentList(records);
        }

        static string Big(string number, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><size=50>{number}</size></color>";

        void BuildSummary(List<PlayRecord> records)
        {
            var days = new HashSet<string>();
            float totalSec = 0f;
            foreach (var r in records)
            {
                days.Add(DatePart(r.StartedAt));
                totalSec += r.DurationSec; // 게임이 timeScale=0 정지로 계측해 일시정지 시간이 이미 제외돼 있다
            }
            bool any = records.Count > 0;
            _summaryValues[0].text = any ? $"{Big(days.Count.ToString(), Skin.Green)} 일" : "—";
            int s = Mathf.FloorToInt(totalSec);
            _summaryValues[1].text = !any ? "—"
                : s >= 3600 ? $"{Big((s / 3600).ToString(), Skin.Green)} 시간  {Big((s % 3600 / 60).ToString(), Skin.Green)} 분"
                : $"{Big((s / 60).ToString(), Skin.Green)} 분  {Big((s % 60).ToString(), Skin.Green)} 초";
            var (month, day) = MonthDay(any ? records[records.Count - 1].StartedAt : "");
            _summaryValues[2].text = any ? $"{Big(month.ToString(), Skin.Green)} 월  {Big(day.ToString(), Skin.Green)} 일" : "—";
        }

        /// 활동별 최근 5회 평균(반올림 정수) + 같은 5회 구간의 회차별 막대 (왼쪽이 오래된 회차 · 최근 회차만 진한 색)
        void BuildActivityColumns(List<PlayRecord> records)
        {
            for (int i = 0; i < Activities.Length; i++)
            {
                var (key, name, icon, ribbon, color, barColor) = Activities[i];
                var recent = new List<PlayRecord>();
                foreach (var r in records)
                    if (r.Activity == key)
                        recent.Add(r);
                if (recent.Count > 5)
                    recent.RemoveRange(0, recent.Count - 5);

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
                    sum += r.SuccessCount;
                int avg = Mathf.FloorToInt((float)sum / recent.Count + 0.5f); // 반올림 · 소수점 미표시 (확정)
                UiKit.Txt(card, "Value", 150, 111, 100, 65, avg.ToString(), 50, 7, color, TextAnchor.MiddleRight);
                UiKit.Txt(card, "Total", 254, 121, 70, 55, "/10", 30, 5, Skin.Muted, TextAnchor.MiddleLeft);

                // 회차별 막대 — 있는 만큼만 그린다 (빈 막대 미표시 · 확정) · 높낮이만 사실대로
                var chart = UiKit.Node(card, "RecentFiveChart", 31, 190, 210, 82);
                for (int b = 0; b < recent.Count; b++)
                {
                    float h = 8f + 74f * (recent[b].SuccessCount / 10f);
                    bool latest = b == recent.Count - 1;
                    UiKit.Img(chart, $"Bar{b + 1}", "Box-Round-5", b * 43, 82 - h, 38, h, latest ? color : barColor, sliced: true);
                }
            }
        }

        /// 최근 활동 목록 5건 — 날짜 · 활동 · 결과(성공 n개) · 소요 시간 · 최신이 위. 스토리 한 판은 한 줄로 묶는다
        void BuildRecentList(List<PlayRecord> records)
        {
            var box = UiKit.Card(_dynamicRoot.transform, "RecentActivities", 462, 671, 1113, 336, Skin.FaceWarm, Skin.Outline, "Box-Round-20");
            UiKit.Txt(box, "Title", 32, 15, 200, 45, "최근 활동", 28, 7, Skin.BrownDark, TextAnchor.MiddleLeft);

            var rows = new List<List<PlayRecord>>();
            for (int i = records.Count - 1; i >= 0 && rows.Count < 5; i--)
            {
                var group = new List<PlayRecord> { records[i] };
                if (records[i].Mode == "Story")
                {
                    // 같은 스토리 판의 앞선 게임(요리 ← 장보기 ← 수확)을 한 줄로
                    while (i - 1 >= 0 && records[i - 1].Mode == "Story" && group.Count < 3
                        && Order(records[i - 1].Activity) < Order(group[0].Activity))
                    {
                        i--;
                        group.Insert(0, records[i]);
                    }
                }
                rows.Add(group);
            }

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
                bool story = group.Count > 1 || group[0].Mode == "Story";
                float totalSec = 0f;
                var parts = new List<string>();
                foreach (var rec in group)
                {
                    totalSec += rec.DurationSec;
                    var act = Find(rec.Activity);
                    parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(act.color)}>{act.name} {rec.SuccessCount}개</color>");
                }
                var first = Find(group[0].Activity);
                UiKit.Txt(row, "Date", 25, 0, 130, 50, FormatDate(group[0].StartedAt), 22, 6, Skin.BrownDark, TextAnchor.MiddleLeft);
                UiKit.Txt(row, "Activity", 187, 0, 202, 50, story ? "스토리 게임" : first.name, 22, 6, story ? Skin.BrownDark : first.color, TextAnchor.MiddleLeft);
                UiKit.Txt(row, "Result", 439, 0, 470, 50, string.Join(" · ", parts), 22, 6, Skin.BrownDark, TextAnchor.MiddleLeft);
                UiKit.Txt(row, "Duration", 931, 0, 120, 50, FormatMinutes(totalSec), 22, 6, Skin.Muted);
            }
        }

        static int Order(string activity) => activity == "Harvest" ? 0 : activity == "Shopping" ? 1 : 2;

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
