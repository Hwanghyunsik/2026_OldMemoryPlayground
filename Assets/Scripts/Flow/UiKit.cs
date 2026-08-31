using System;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;

namespace Shinmyeong.Flow
{
    /// 플레이스홀더 화면을 코드로 조립하는 최소 UI 헬퍼 (X-Box 단계 전용 — 실제 아트 도입 시 프리팹으로 대체)
    public static class UiKit
    {
        public static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static RectTransform Stretch(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null)
                rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

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

        public static Text Label(Transform parent, string name, Vector2 anchor, Vector2 size, int fontSize, string text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = DefaultFont;
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            t.text = text;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            return t;
        }

        /// dwell 선택 버튼 (기본 3초 — UI 버튼 규칙 · 07 문서 2-1)
        public static DwellTarget Button(Transform parent, string name, Vector2 anchor, Vector2 size,
            string label, Action onSelected, float dwellSeconds = 3f, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color ?? new Color(0.3f, 0.45f, 0.7f);
            img.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;

            Label(go.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(size.x - 20, size.y - 20), 32, label);

            var target = go.AddComponent<DwellTarget>();
            target.SetDwellSeconds(dwellSeconds);
            if (onSelected != null)
                target.Selected += _ => onSelected();
            return target;
        }
    }
}
