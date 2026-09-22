using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;
using Shinmyeong.UI;

namespace Shinmyeong.Flow.Screens
{
    /// 개별 결과 3종(SCR-009·014·019 · 시안) 공통 조각 — 헤더 · 결과 판 · 요약 카드 3종 · 리본 · 재료/음식 나열 · 버튼.
    /// 평가 표현 금지(3-3): 「다른 선택·다르게 산·다른 재료가 들어간」 문구 + 색상 계열 구분만 쓰고,
    /// 시안의 X 아이콘은 부정 기호 금지에 따라 잎 아이콘으로 대체했다(기획 확인 대기).
    public static class ResultKit
    {
        public const float PanelX = 304, PanelY = 273;

        public class Header
        {
            public Image Avatar;
            public GameObject UserFace;
            public Text UserName;
        }

        public static Header BuildHeader(Transform parent, string title, string description)
        {
            var header = UiKit.HeaderPaper(parent);
            var h = new Header();
            h.Avatar = UiKit.ImgFit(header, "Avatar", "Avatar_01", 85, 33, 116, 116);
            UiKit.Txt(header, "Title", 230, 42, 650, 57, title, 40, 6, Skin.Ink, TextAnchor.MiddleLeft);
            UiKit.Txt(header, "Description", 230, 96, 650, 44, description, 28, 5, Skin.Brown, TextAnchor.MiddleLeft);
            h.UserFace = UiKit.Img(header, "UserFace", "Box-Round-20", 938, 57, 191, 68, Skin.Face, sliced: true).gameObject;
            h.UserName = UiKit.Txt(h.UserFace.transform, "UserName", 0, 0, 191, 68, "", 28, 6, Skin.Brown);
            return h;
        }

        public static void ApplyHeader(Header h, FlowManager flow)
        {
            ModeSelectScreen.ApplyAvatar(h.Avatar, flow);
            h.UserFace.SetActive(!flow.IsGuest);
            h.UserName.text = $"{flow.UserName} 님";
        }

        /// 결과 판 1312×663 (x304 y273)
        public static RectTransform BuildPanel(Transform parent)
        {
            return UiKit.Frame(parent, "ResultPanel", PanelX, PanelY, 1312, 663, Skin.Paper, Skin.Outline3);
        }

        public enum CardKind { Proper, Other, Duration }

        /// 요약 카드 403×164 — 판 안 x 37/453/870, y 35
        public static Text SummaryCard(RectTransform panel, string name, int index, CardKind kind, string label)
        {
            float x = 37 + index * 416.5f;
            var card = UiKit.Card(panel, name, x, 35, 403, 164, Skin.Face, Skin.OutlineDark);
            UiKit.Img(card, "Circle", "Circle-125", 30, 35, 93, 93, Skin.Paper);
            switch (kind)
            {
                case CardKind.Proper:
                    UiKit.ImgFit(card, "Icon", "Icon-Check", 41, 40, 80, 68, Skin.Green);
                    break;
                case CardKind.Other:
                    // 시안은 X 아이콘 — 부정 기호 금지(3-3)로 잎 아이콘 + 주황 계열 색만 사용
                    UiKit.ImgFit(card, "Icon", "Icon-Leaf", 54, 54, 45, 50, Skin.Red);
                    break;
                case CardKind.Duration:
                    UiKit.ImgFit(card, "Icon", "Icon-Time", 48, 54, 56, 56, Color.white);
                    break;
            }
            UiKit.Txt(card, "Label", 150, 23, 215, 47, label, 26, 6, Skin.Brown, TextAnchor.MiddleRight);
            var valueColor = kind == CardKind.Proper ? Skin.Green : kind == CardKind.Other ? Skin.Red : Skin.BrownDark;
            return UiKit.Txt(card, "Value", 130, 73, 235, 73, "", 26, 5, valueColor, TextAnchor.MiddleRight);
        }

        public static string CountText(int n, string unit = "개") => $"<size=60><b>{n}</b></size> {unit}";

        public static string DurationText(float sec)
        {
            int s = Mathf.FloorToInt(sec);
            return $"<size=46><b>{s / 60}</b></size> 분 <size=46><b>{s % 60}</b></size> 초";
        }

        /// 리본 제목 (초록/주황) — 판 안 좌표
        public static void Ribbon(RectTransform panel, string name, float x, float y, bool green, string label, int size = 30)
        {
            var ribbon = UiKit.ImgFit(panel, name, green ? "Ribbon-Green" : "Ribbon-Orange", x, y, green ? 339 : 347, green ? 52 : 48);
            UiKit.Txt(ribbon.transform, "Label", 0, -2, green ? 339 : 347, 55, label, size, 7, Color.white);
        }

        /// 나열 상자 1238×186 (판 안 x38) — 제대로(초록 점선) / 다르게(주황 점선)
        public static RectTransform MaterialBox(RectTransform panel, string name, float y, bool proper)
        {
            var box = UiKit.Node(panel, name, 38, y, 1238, 186);
            UiKit.Img(box, "Face", proper ? "Box-Round-20" : "Box-Round-23", 3, 2, 1232, 180, proper ? Skin.ProperFace : Skin.OtherFace, sliced: true);
            UiKit.Img(box, "Border", "Dot-Line-Wide", 3, 2, 1232, 180, proper ? Skin.GreenLine : Skin.OtherLine);
            return box;
        }

        /// 나열 항목 110×110 + 이름표 + 개수 배지 (상자 안 x = 30 + i·pitch, y 44)
        public static GameObject MaterialItem(RectTransform box, int index, float pitch, bool proper, string label, Sprite icon, int count)
        {
            float x = 30 + index * pitch;
            var item = UiKit.Node(box, $"Item{index + 1}", x, 44, 110, 110);
            UiKit.Img(item, "Face", "Box-Round-23", 1, 2, 108, 108, proper ? Skin.ProperCard : Skin.OtherCard, sliced: true);
            UiKit.Img(item, "Outline", "Box-Round-23-Outline", 1, 2, 108, 108, proper ? Skin.ProperOutline : Skin.OtherOutline, sliced: true);
            if (icon != null)
                UiKit.ImgFit(item, "Food", null, 14, 12, 82, 72).sprite = icon;
            else
                UiKit.Txt(item, "Placeholder", 10, 10, 90, 76, label, 20, 6, Skin.Brown);
            UiKit.Img(item, "LabelFace", "Box-Round-15", 13, 94, 84, 32, proper ? Skin.Green : Skin.Red, sliced: true);
            UiKit.Txt(item, "Label", 13, 94, 84, 32, label, 18, 5, Color.white);
            if (!proper)
            {
                var rt = (RectTransform)item;
                UiKit.PlaceCentered(rt, x, 44, 110, 110);
                rt.localRotation = Quaternion.Euler(0, 0, index % 2 == 0 ? 8f : -8f);
            }
            // 개수 배지는 항상 맨 위 층(Badges)에 두어 다음 카드에 가려지지 않게 한다 (카드가 촘촘할 때)
            if (count > 1)
                UiKit.Badge(BadgeLayer(box), $"Badge{index + 1}", x + 75, 44 - 16, 44, count, proper ? Skin.Green : Skin.Red);
            return item.gameObject;
        }

        /// 상자 안 맨 위 층 — 배지 전용. 호출 때마다 마지막 자식으로 올린다
        public static RectTransform BadgeLayer(RectTransform box)
        {
            var layer = box.Find("Badges") as RectTransform;
            if (layer == null)
                layer = UiKit.Group(box, "Badges");
            layer.SetAsLastSibling();
            return layer;
        }

        /// 왼쪽 2단(다시 하기 · 내 기록 보기) + 우측 「다른 활동 고르기」. 기록 버튼은 비회원에게 표시하지 않는다
        public static GameObject BuildButtons(Transform parent, System.Action retry, System.Action lobby, System.Action records)
        {
            var stack = UiKit.ButtonStack(parent, "IndividualLeftButtons", 59, 342);
            UiKit.StackButton(stack, "RetryButton", 20, "다시 하기", "Icon-ReStart", retry);
            var rec = UiKit.StackButton(stack, "RecordsButton", 272, "내 기록 보기", "Icon-Record", records, labelSize: 22);
            stack.gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(좌측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";

            var other = UiKit.Node(parent, "OtherActivitiesButton", 1689, 461, 160, 158);
            UiKit.Img(other, "Black", "Box-Round-34", -15, -15, 190, 188, Skin.Hex("1c1412", 0.8f), sliced: true);
            UiKit.Img(other, "Background", "Bt-Round-Green-02", 0, 0, 160, 160, Color.white, sliced: true);
            UiKit.ImgFit(other, "Icon", "Icon-Menu", 60, 27, 40, 40, Color.white);
            UiKit.Txt(other, "Label", 5, 68, 150, 72, "다른 활동\n고르기", 23, 7, Color.white);
            UiKit.Dwell(other, 160, 158, 3f, lobby)
                .gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(우측 끝) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";
            return rec.gameObject;
        }

        /// 종류 단위 묶음 (라벨·제대로 여부) → 개수
        public static List<(string label, int count, bool proper)> Group(IEnumerable<(string label, bool proper)> items)
        {
            var list = new List<(string label, int count, bool proper)>();
            foreach (var (label, proper) in items)
            {
                int idx = list.FindIndex(g => g.label == label && g.proper == proper);
                if (idx >= 0)
                    list[idx] = (label, list[idx].count + 1, proper);
                else
                    list.Add((label, 1, proper));
            }
            return list;
        }

        public static float Pitch(int count, float available, float itemWidth, float preferred)
        {
            if (count <= 1)
                return preferred;
            return Mathf.Min(preferred, (available - itemWidth) / (count - 1));
        }
    }
}
