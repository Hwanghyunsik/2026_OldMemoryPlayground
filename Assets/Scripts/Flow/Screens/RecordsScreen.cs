using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Save;

namespace Shinmyeong.Flow.Screens
{
    /// SCR-023 개인 누적 성과 이력 (8-5 확정 · 설계서 p38).
    /// 누적 요약(함께한 날 · 전체 활동 시간 · 마지막 참여) + 활동별 최근 5회 성공 평균 n/10 +
    /// 같은 5회 구간의 회차별 막대 + 최근 활동 목록 5건 · 「이전으로」(dwell 3초)는 호출 화면 복귀.
    /// 금지: 점수·등급·순위·다른 사용자 비교·감소 구간 강조·상세 수치(ADM 전용) · 기간 필터 없음(최근 5회 고정).
    /// 기록 5회 미만이면 있는 만큼만 평균 내고 빈 막대를 채우지 않는다. 비회원은 진입 버튼 자체가 없다.
    public class RecordsScreen : ScreenBase
    {
        static readonly (string key, string name)[] Activities =
        {
            ("Harvest", "수확하기"),
            ("Shopping", "장보기"),
            ("Cooking", "요리하기"),
        };

        // 막대는 전부 같은 색 — 높낮이만 사실대로 보여주고 잘된 회차·줄어든 회차를 색으로 구분하지 않는다
        static readonly Color BarColor = new Color(0.95f, 0.85f, 0.45f);

        Text _header;
        Image _avatar;
        Text[] _summaryValues;
        GameObject _dynamicRoot;

        protected override void BuildUi()
        {
            UiKit.Panel(transform, "BG", new Color(0.11f, 0.12f, 0.15f));

            // 헤더 — 사용자 이름 + 아바타(선택 화면과 동일 카드 색 · 그림 8종은 자산 도입 시)
            _avatar = UiKit.Panel(transform, "Avatar", new Color(0.35f, 0.35f, 0.4f));
            _avatar.rectTransform.SetSizeWithAnchors(new Vector2(0.395f, 0.92f), new Vector2(64, 64));
            _header = UiKit.Label(transform, "Header", new Vector2(0.53f, 0.92f), new Vector2(700, 70), 46, "");

            // 누적 요약 — 쉬운 지표 3종만
            string[] summaryNames = { "함께한 날", "전체 활동 시간", "마지막 참여" };
            _summaryValues = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var card = UiKit.Panel(transform, $"Summary_{i}", new Color(1f, 1f, 1f, 0.08f));
                card.rectTransform.SetSizeWithAnchors(new Vector2(0.34f + 0.16f * i, 0.80f), new Vector2(280, 105));
                UiKit.Label(card.transform, "Name", new Vector2(0.5f, 0.74f), new Vector2(260, 34), 26, summaryNames[i]);
                _summaryValues[i] = UiKit.Label(card.transform, "Value", new Vector2(0.5f, 0.32f), new Vector2(260, 46), 34, "");
            }

            UiKit.Button(transform, "Back", new Vector2(0.5f, 0.145f), new Vector2(260, 95), "이전으로",
                () => Flow.GoBack());
        }

        protected override void OnEnter()
        {
            if (_dynamicRoot != null)
                Destroy(_dynamicRoot);
            _dynamicRoot = new GameObject("Dynamic");
            _dynamicRoot.transform.SetParent(transform, false);
            UiKit.Stretch(_dynamicRoot);

            _header.text = string.IsNullOrEmpty(Flow.UserName) ? "" : $"{Flow.UserName} 님의 기록";
            var profile = SaveStore.LoadUsers().Users.Find(u => u.Id == Flow.UserId);
            _avatar.color = profile != null
                ? UserSelectScreen.CardColors[Mathf.Abs(profile.CardColorIndex) % UserSelectScreen.CardColors.Length]
                : new Color(0.35f, 0.35f, 0.4f);
            if (profile != null)
                UI.ArtCatalog.TryApply(_avatar, UI.ArtCatalog.Avatar, $"아바타{profile.AvatarIndex + 1}");

            var records = string.IsNullOrEmpty(Flow.UserId)
                ? new List<PlayRecord>()
                : SaveStore.LoadRecords(Flow.UserId).Records;

            BuildSummary(records);
            BuildActivityColumns(records);
            BuildRecentList(records);
        }

        void BuildSummary(List<PlayRecord> records)
        {
            var days = new HashSet<string>();
            float totalSec = 0f;
            foreach (var r in records)
            {
                days.Add(DatePart(r.StartedAt));
                totalSec += r.DurationSec; // 게임이 timeScale=0 정지로 계측해 일시정지 시간이 이미 제외돼 있다
            }
            _summaryValues[0].text = records.Count > 0 ? $"{days.Count}일" : "—";
            _summaryValues[1].text = records.Count > 0 ? FormatDuration(totalSec) : "—";
            _summaryValues[2].text = records.Count > 0 ? FormatDate(records[records.Count - 1].StartedAt) : "—";
        }

        /// 활동별 최근 5회 평균(반올림 정수) + 같은 5회 구간의 회차별 막대 (왼쪽이 오래된 회차)
        void BuildActivityColumns(List<PlayRecord> records)
        {
            for (int i = 0; i < Activities.Length; i++)
            {
                var (key, name) = Activities[i];
                var recent = new List<PlayRecord>();
                foreach (var r in records)
                    if (r.Activity == key)
                        recent.Add(r);
                if (recent.Count > 5)
                    recent.RemoveRange(0, recent.Count - 5);

                var card = UiKit.Panel(_dynamicRoot.transform, $"Activity_{key}", new Color(1f, 1f, 1f, 0.06f));
                card.rectTransform.SetSizeWithAnchors(new Vector2(0.34f + 0.16f * i, 0.585f), new Vector2(280, 275));
                UiKit.Label(card.transform, "Name", new Vector2(0.5f, 0.9f), new Vector2(260, 38), 28, name);

                if (recent.Count == 0)
                {
                    UiKit.Label(card.transform, "Value", new Vector2(0.5f, 0.68f), new Vector2(260, 56), 40, "—");
                    continue;
                }

                int sum = 0;
                foreach (var r in recent)
                    sum += r.SuccessCount;
                int avg = Mathf.FloorToInt((float)sum / recent.Count + 0.5f); // 반올림 · 소수점 미표시 (확정)
                UiKit.Label(card.transform, "Value", new Vector2(0.5f, 0.68f), new Vector2(260, 56), 44, $"{avg} / 10");

                // 막대 기준선 + 회차별 막대 — 있는 만큼만 그린다 (빈 막대 미표시 · 확정)
                UiKit.Panel(card.transform, "Baseline", new Color(1f, 1f, 1f, 0.18f))
                    .rectTransform.SetSizeWithAnchors(new Vector2(0.5f, 0.12f), new Vector2(230, 4));
                for (int b = 0; b < recent.Count; b++)
                {
                    float x = 0.5f + (b - (recent.Count - 1) * 0.5f) * 0.155f;
                    var bar = UiKit.Panel(card.transform, $"Bar_{b}", BarColor);
                    var rect = bar.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(x, 0.13f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.sizeDelta = new Vector2(28, 8f + 92f * (recent[b].SuccessCount / 10f));
                }
            }
        }

        /// 최근 활동 목록 5건 — 날짜 · 활동 · 결과(성공 n/10) · 소요 시간 · 최신이 위
        void BuildRecentList(List<PlayRecord> records)
        {
            int count = Mathf.Min(records.Count, 5);
            for (int i = 0; i < count; i++)
            {
                var r = records[records.Count - 1 - i];
                var row = UiKit.Panel(_dynamicRoot.transform, $"Recent_{i}", new Color(1f, 1f, 1f, i % 2 == 0 ? 0.07f : 0.04f));
                row.rectTransform.SetSizeWithAnchors(new Vector2(0.5f, 0.395f - 0.043f * i), new Vector2(900, 42));
                UiKit.Label(row.transform, "Date", new Vector2(0.14f, 0.5f), new Vector2(220, 36), 26, FormatDate(r.StartedAt));
                UiKit.Label(row.transform, "Activity", new Vector2(0.4f, 0.5f), new Vector2(220, 36), 26, ActivityName(r.Activity));
                UiKit.Label(row.transform, "Result", new Vector2(0.65f, 0.5f), new Vector2(200, 36), 26, $"{r.SuccessCount} / 10");
                UiKit.Label(row.transform, "Time", new Vector2(0.87f, 0.5f), new Vector2(200, 36), 26, FormatDuration(r.DurationSec));
            }
        }

        static string ActivityName(string key)
        {
            foreach (var (k, name) in Activities)
                if (k == key)
                    return name;
            return key;
        }

        static string DatePart(string startedAt) =>
            string.IsNullOrEmpty(startedAt) || startedAt.Length < 10 ? "" : startedAt.Substring(0, 10);

        /// "yyyy-MM-dd HH:mm:ss" → "n월 n일"
        static string FormatDate(string startedAt)
        {
            var date = DatePart(startedAt);
            var parts = date.Split('-');
            if (parts.Length < 3 || !int.TryParse(parts[1], out int month) || !int.TryParse(parts[2], out int day))
                return date;
            return $"{month}월 {day}일";
        }

        static string FormatDuration(float sec)
        {
            int s = Mathf.FloorToInt(sec);
            return s >= 3600 ? $"{s / 3600}시간 {s % 3600 / 60}분" : $"{s / 60}분 {s % 60}초";
        }
    }
}
