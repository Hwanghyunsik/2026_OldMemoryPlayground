using System;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;
using Shinmyeong.UI;

namespace Shinmyeong.Flow
{
    /// 화면 조립 헬퍼. 디자인 시안(SinMyung.unity · 1920×1080)의 좌표를 그대로 옮길 수 있게
    /// 「왼쪽 위 기준 픽셀 좌표(x, y, w, h)」로 배치한다 — 캔버스는 1920×1080 고정 스케일.
    /// 스프라이트·폰트·색은 Skin(UiCatalog)에서 가져온다. 시안 요소 이름을 코드 이름으로 쓴다.
    public static class UiKit
    {
        public static Font DefaultFont => Skin.Font(5);

        // ---------------------------------------------------------------- 배치

        public static RectTransform Stretch(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null)
                rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        /// 왼쪽 위 기준 픽셀 배치 — 부모의 왼쪽 위 모서리에서 (x, y)만큼, 크기 (w, h)
        public static RectTransform Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x, -y);
            return rect;
        }

        /// 화면 위치는 그대로 두고 피벗만 바꾼다 (anchoredPosition은 피벗 변화량 × sizeDelta 만큼 보정).
        /// preserveAspect 그림은 남는 여백을 피벗 기준으로 정렬하므로, 비율 유지 그림은 반드시 중심 피벗이어야
        /// 세로로 긴 그림이 왼쪽, 가로로 긴 그림이 위로 쏠리지 않는다
        public static RectTransform SetPivot(RectTransform rect, Vector2 pivot)
        {
            var delta = pivot - rect.pivot;
            rect.anchoredPosition += new Vector2(delta.x * rect.sizeDelta.x, delta.y * rect.sizeDelta.y);
            rect.pivot = pivot;
            return rect;
        }

        /// 중심 피벗 배치 (회전·확대 연출용) — 좌표는 여전히 왼쪽 위 기준 x, y, w, h
        public static RectTransform PlaceCentered(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x + w * 0.5f, -(y + h * 0.5f));
            return rect;
        }

        public static RectTransform Node(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return Place(go.AddComponent<RectTransform>(), x, y, w, h);
        }

        public static RectTransform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return Stretch(go);
        }

        // ---------------------------------------------------------------- 그림

        public static Image Img(Transform parent, string name, string sprite, float x, float y, float w, float h,
            Color? color = null, bool sliced = false, float ppuMultiplier = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = color ?? Color.white;
            Apply(img, sprite, sliced, ppuMultiplier);
            Place(img.rectTransform, x, y, w, h);
            return img;
        }

        /// 비율 유지 그림 (일러스트·아이콘) — 중심 피벗이라 좌우 뒤집기(localScale -1)도 제자리에서 된다
        public static Image ImgFit(Transform parent, string name, string sprite, float x, float y, float w, float h, Color? color = null)
        {
            var img = Img(parent, name, sprite, x, y, w, h, color);
            img.preserveAspect = true;
            SetPivot(img.rectTransform, new Vector2(0.5f, 0.5f));
            return img;
        }

        public static void Apply(Image img, string sprite, bool sliced = false, float ppuMultiplier = 1f)
        {
            if (string.IsNullOrEmpty(sprite))
            {
                img.sprite = null; // 나중에 채우는 자리 (아바타·목표 그림 등)
                return;
            }
            var s = Skin.Sprite(sprite);
            if (s == null)
            {
                Debug.LogWarning($"[UiKit] 스프라이트 없음: {sprite}");
                img.sprite = null;
                return;
            }
            img.sprite = s;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.pixelsPerUnitMultiplier = ppuMultiplier;
        }

        /// 1920×1080 배경 그림
        public static Image Background(Transform parent, string sprite)
        {
            var img = Img(parent, "Bg", sprite, 0, 0, 1920, 1080);
            Stretch(img.gameObject);
            return img;
        }

        /// 단색 면 (전체 덮기) — 딤·차폐용
        public static Image Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            Stretch(go);
            return img;
        }

        /// 둥근 알약 면 (Box-Round-28 · 9분할)
        public static Image Pill(Transform parent, string name, float x, float y, float w, float h, Color color, string sprite = "Box-Round-28")
        {
            return Img(parent, name, sprite, x, y, w, h, color, sliced: true);
        }

        /// 시안 공통 프레임: 흰 테(Box-Round-28) + 5px 안쪽 면(Box-Round-23) + 윤곽선(Box-Round-23-Outline).
        /// 반환값은 프레임 루트(자식을 이 안에 절대 좌표로 배치하려면 x, y를 빼서 넣는다)
        public static RectTransform Frame(Transform parent, string name, float x, float y, float w, float h,
            Color face, Color outline, bool shadow = false)
        {
            var root = Node(parent, name, x, y, w, h);
            if (shadow)
                Img(root, "Shadow", "Box-Round-28", 2, 5, w, h, Skin.Shadow, sliced: true);
            Img(root, "Frame", "Box-Round-28", 0, 0, w, h, Color.white, sliced: true);
            Img(root, "Face", "Box-Round-23", 5, 5, w - 10, h - 10, face, sliced: true);
            Img(root, "Outline", "Box-Round-23-Outline", 5, 5, w - 10, h - 10, outline, sliced: true);
            return root;
        }

        /// 테 없는 카드: 면(Box-Round-23) + 윤곽선
        public static RectTransform Card(Transform parent, string name, float x, float y, float w, float h,
            Color face, Color outline, string sprite = "Box-Round-23")
        {
            var root = Node(parent, name, x, y, w, h);
            Img(root, "Face", sprite, 0, 0, w, h, face, sliced: true);
            Img(root, "Outline", sprite + "-Outline", 0, 0, w, h, outline, sliced: true);
            return root;
        }

        // ---------------------------------------------------------------- 글자

        /// S-Core Dream 글자. weight 4~8 · 기본 가운데 정렬 · 넘침 허용(잘리지 않게)
        public static Text Txt(Transform parent, string name, float x, float y, float w, float h, string text,
            int size, int weight = 5, Color? color = null, TextAnchor align = TextAnchor.MiddleCenter, bool wrap = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Skin.Font(weight);
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? Skin.Brown;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;
            Place(t.rectTransform, x, y, w, h);
            return t;
        }

        // ---------------------------------------------------------------- 공통 조각

        /// 상단 종이 헤더 (Title-bg 1184×182) — 메뉴 화면 x368 · 플레이/튜토리얼 x329
        public static RectTransform HeaderPaper(Transform parent, float x = 368, float y = 50)
        {
            var root = Node(parent, "Header", x, y, 1184, 182);
            Img(root, "Paper", "Title-bg", 0, 0, 1184, 182);
            return root;
        }

        /// 하단 안내 알약 (어두운 면 + 흰 글자 26px)
        public static Text Footer(Transform parent, string text, float x = 696, float y = 943, float w = 498, float h = 56, int size = 26)
        {
            var root = Node(parent, "Footer", x, y, w, h);
            Pill(root, "Background", 0, 0, w, h, Skin.Dim);
            return Txt(root, "Notice", 0, 0, w, h, text, size, 6, Color.white);
        }

        /// 진행 막대 — 둥근 트랙 위에 둥근 채움. SetBar로 채움 비율을 바꾼다
        public static Image Bar(Transform parent, string name, float x, float y, float w, float h, Color track, Color fill,
            string trackSprite = "TimeBar-Bg", string fillSprite = "Circle-25")
        {
            var root = Node(parent, name, x, y, w, h);
            Img(root, "Track", trackSprite, 0, 0, w, h, track, sliced: true);
            var f = Img(root, "Fill", fillSprite, 0, 0, w, h, fill, sliced: true);
            var holder = f.gameObject.AddComponent<BarFill>();
            holder.Width = w;
            holder.Height = h;
            return f;
        }

        public static void SetBar(Image fill, float fraction01)
        {
            var holder = fill.GetComponent<BarFill>();
            float w = holder != null ? holder.Width : fill.rectTransform.sizeDelta.x;
            float h = holder != null ? holder.Height : fill.rectTransform.sizeDelta.y;
            float min = Mathf.Min(h, w);
            float width = Mathf.Lerp(min, w, Mathf.Clamp01(fraction01));
            bool empty = fraction01 <= 0.001f;
            if (fill.enabled == empty)
                fill.enabled = !empty;
            fill.rectTransform.sizeDelta = new Vector2(width, h);
        }

        /// 원형 개수 배지 (Circle-52 + 숫자)
        public static Text Badge(Transform parent, string name, float x, float y, float size, int count, Color color, int fontSize = 30)
        {
            var root = Node(parent, name, x, y, size, size);
            Img(root, "Circle", "Circle-52", 0, 0, size, size, color);
            return Txt(root, "Count", 0, 0, size, size, count.ToString(), fontSize, 7, Color.white);
        }

        // ---------------------------------------------------------------- 버튼

        /// 시안 표준 버튼 160×123: 어두운 받침(Box-Round-34) + 상아/초록 버튼면 + 아이콘 + 라벨 · dwell 3초(2-1)
        public static DwellTarget NavButton(Transform parent, string name, float x, float y, string label, string icon,
            Action onSelected, bool green = false, float w = 160, float h = 123, float dwellSeconds = 3f, int labelSize = 23)
        {
            var root = Node(parent, name, x, y, w, h);
            Img(root, "Black", "Box-Round-34", -14, -13, w + 28, h + 28, Skin.Dim, sliced: true);
            Img(root, "Background", green ? "Bt-Round-Green" : "Bt-Round-Ivory", 0, 0, w, h);
            bool twoLine = label.Contains("\n");
            if (!string.IsNullOrEmpty(icon))
            {
                float iconSize = 42f;
                ImgFit(root, "Icon", icon, (w - iconSize) * 0.5f, twoLine ? h * 0.15f : h * 0.22f, iconSize, iconSize,
                    green ? Color.white : Skin.IconTint);
            }
            Txt(root, "Label", 5, twoLine ? h * 0.42f : h * 0.58f, w - 10, twoLine ? 70 : 32, label, labelSize, 7,
                green ? Color.white : Skin.ButtonLabel);
            return Dwell(root, w, h, dwellSeconds, onSelected);
        }

        /// 임의 노드를 dwell 선택 대상으로 만들고 선택 진행 표시를 얹는다
        public static DwellTarget Dwell(RectTransform node, float w, float h, float dwellSeconds, Action onSelected, bool withText = true)
        {
            var target = node.gameObject.AddComponent<DwellTarget>();
            target.SetDwellSeconds(dwellSeconds);
            if (onSelected != null)
                target.Selected += _ => onSelected();
            DwellSelectOverlay.Attach(target, w, h, withText);
            return target;
        }

        /// 세로 2단 버튼 묶음 받침 (시안 IndividualLeftButtons) — 190×396 어두운 면 + 가운데 점선
        public static RectTransform ButtonStack(Transform parent, string name, float x, float y)
        {
            var root = Node(parent, name, x, y, 190, 396);
            Img(root, "Black", "Box-Round-34", 0, 0, 187, 394, Skin.Dim, sliced: true);
            Img(root, "Divider", "Dot-Line-2", 20, 196, 149, 2, new Color(1f, 1f, 1f, 0.6f), sliced: true);
            return root;
        }

        /// ButtonStack 안의 버튼 (받침이 이미 있으므로 어두운 받침 없음)
        public static DwellTarget StackButton(RectTransform stack, string name, float yInStack, string label, string icon, Action onSelected, int labelSize = 23)
        {
            var target = NavButton(stack, name, 15, yInStack, label, icon, onSelected, labelSize: labelSize);
            var black = target.transform.Find("Black");
            if (black != null)
                UnityEngine.Object.Destroy(black.gameObject);
            return target;
        }

        // ---------------------------------------------------------------- 레거시 (앵커 기준 · 남은 호출부용)

        public static Text Label(Transform parent, string name, Vector2 anchor, Vector2 size, int fontSize, string text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Skin.Font(6);
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            return t;
        }

        public static DwellTarget Button(Transform parent, string name, Vector2 anchor, Vector2 size,
            string label, Action onSelected, float dwellSeconds = 3f, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color ?? new Color(0.3f, 0.45f, 0.7f);
            img.raycastTarget = false;
            Apply(img, "Bt-Round-Ivory");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            var t = Label(go.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(size.x - 20, size.y - 20), 28, label);
            t.color = Skin.ButtonLabel;
            return Dwell(rect, size.x, size.y, dwellSeconds, onSelected);
        }
    }

    /// Bar 채움 크기 기억용
    public class BarFill : MonoBehaviour
    {
        public float Width;
        public float Height;
    }

    static class RectTransformExt
    {
        public static void SetSizeWithAnchors(this RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
        }
    }
}
