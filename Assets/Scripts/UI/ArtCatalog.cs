using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shinmyeong.UI
{
    /// 그림 자산 창구 (Docs/92 규격). 코드가 그림을 찾는 유일한 경로 —
    /// `Resources/Art/<분류>/<한글 이름>.png`에 스프라이트가 있으면 쓰고,
    /// 없으면 호출부가 X-Box 플레이스홀더(색 박스 + 라벨)를 유지한다.
    /// 자산이 일부만 도착해도 있는 것만 교체된다 (코드 수정 없는 교체가 목적).
    public static class ArtCatalog
    {
        // 분류 폴더명 — Docs/92_자산규격_플레이스홀더.md 2장과 1:1
        public const string CropHanging = "작물_매달린";
        public const string CropBug = "작물_벌레";
        public const string CropLaid = "작물_놓인";
        public const string Food = "음식";
        public const string Ingredient = "재료";
        public const string Store = "점포";
        public const string Avatar = "아바타";
        public const string Stage = "무대";

        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string category, string name)
        {
            string key = category + "/" + name;
            if (_cache.TryGetValue(key, out var sprite))
                return sprite;
            sprite = Resources.Load<Sprite>("Art/" + key);
            // 「놓인」 작물은 생략 가능(발주서) — 없으면 매달린 것으로 대체
            if (sprite == null && category == CropLaid)
                sprite = Resources.Load<Sprite>("Art/" + CropHanging + "/" + name);
            _cache[key] = sprite; // null도 캐시해 매번 디스크를 뒤지지 않는다
            return sprite;
        }

        /// 패널 안쪽에 그림 아이콘 자식을 얹는다 — 그림이 있으면 true.
        /// 패널 색은 테두리·배경으로 남아 판정 색(요리 일괄 판정 등)이 그대로 보인다
        public static bool TryAddIcon(Image panel, string category, string name, float inset = 0.06f)
        {
            var sprite = Get(category, name);
            if (sprite == null)
                return false;
            var go = new GameObject("ArtIcon");
            go.transform.SetParent(panel.transform, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(inset, inset);
            rect.anchorMax = new Vector2(1f - inset, 1f - inset);
            rect.sizeDelta = Vector2.zero;
            return true;
        }

        /// 스프라이트가 있으면 Image에 적용하고 true — 없으면 건드리지 않고 false(플레이스홀더 유지).
        /// 적용 시 색을 흰색으로 되돌려 플레이스홀더 색이 그림에 물들지 않게 한다
        public static bool TryApply(Image image, string category, string name)
        {
            var sprite = Get(category, name);
            if (sprite == null)
                return false;
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            return true;
        }
    }
}
