using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shinmyeong.UI
{
    /// 그림 자산 창구 (Docs/92 규격). 코드가 게임 데이터(작물·재료·음식·점포·아바타) 그림을 찾는 유일한 경로.
    /// 찾는 순서: ① `Resources/Art/<분류>/<한글 이름>.png`(발주 규격 · 한글명 그대로)
    ///           ② 디자인 시안 자산 `Assets/UI Image`(UiCatalog) — 번호 파일명이라 아래 별칭표로 잇는다
    /// 둘 다 없으면 호출부가 X-Box 플레이스홀더(색 박스 + 라벨)를 유지한다. 부분 수령도 있는 것만 교체된다.
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

        /// 시안 샘플 자산 별칭 (2026-09 수령분 · 번호 파일명 → 게임 데이터 한글명).
        /// 2026-09-23 기준 작물·재료·음식·점포·아바타 그림 전부 수령 (밥·솔잎은 요리 전용이라 SCR-018 번호)
        static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
        {
            // 작물 8종 (매달린 · 놓인 것은 같은 그림 대체)
            [CropHanging + "/토마토"] = "SCR-008-Vegetable-01",
            [CropHanging + "/가지"] = "SCR-008-Vegetable-02",
            [CropHanging + "/고추"] = "SCR-008-Vegetable-03",
            [CropHanging + "/대추"] = "SCR-008-Vegetable-04",
            [CropHanging + "/오이"] = "SCR-008-Vegetable-05",
            [CropHanging + "/감"] = "SCR-008-Vegetable-06",
            [CropHanging + "/밤"] = "SCR-008-Vegetable-07",
            [CropHanging + "/호박"] = "SCR-008-Vegetable-08",
            [CropBug + "/토마토"] = "SCR-008-Vegetable-01-Bug",
            [CropBug + "/가지"] = "SCR-008-Vegetable-02-Bug",
            [CropBug + "/고추"] = "SCR-008-Vegetable-03-Bug",
            [CropBug + "/대추"] = "SCR-008-Vegetable-04-Bug",
            [CropBug + "/오이"] = "SCR-008-Vegetable-05-Bug",
            [CropBug + "/감"] = "SCR-008-Vegetable-06_Bug",   // 파일명만 '_' (수령 원본 그대로 · 카탈로그 Name과 일치)
            [CropBug + "/밤"] = "SCR-008-Vegetable-07-Bug",
            [CropBug + "/호박"] = "SCR-008-Vegetable-08-Bug",
            // 재료 (장보기 점포 S1~S4 번호 · 점포별 순서는 04 문서 3장 취급 재료 순 + 작물 공용)
            [Ingredient + "/밀가루"] = "SCR-013-S1-Food-01",
            [Ingredient + "/밤"] = "SCR-013-S1-Food-02",
            [Ingredient + "/깨"] = "SCR-013-S1-Food-03",
            [Ingredient + "/팥"] = "SCR-013-S1-Food-04",
            [Ingredient + "/쌀가루"] = "SCR-013-S1-Food-05",
            [Ingredient + "/파"] = "SCR-013-S2-Food-01",
            [Ingredient + "/시금치"] = "SCR-013-S2-Food-02",
            [Ingredient + "/당근"] = "SCR-013-S2-Food-03",
            [Ingredient + "/버섯"] = "SCR-013-S2-Food-04",
            [Ingredient + "/소고기"] = "SCR-013-S3-Food-01",
            [Ingredient + "/갈비"] = "SCR-013-S3-Food-02",
            [Ingredient + "/두부"] = "SCR-013-S3-Food-03",
            [Ingredient + "/달걀"] = "SCR-013-S3-Food-04",
            [Ingredient + "/미역"] = "SCR-013-S4-Food-01",
            [Ingredient + "/김치"] = "SCR-013-S4-Food-02",
            [Ingredient + "/간장"] = "SCR-013-S4-Food-03",
            [Ingredient + "/김"] = "SCR-013-S4-Food-04",
            [Ingredient + "/당면"] = "SCR-013-S4-Food-05",
            [Ingredient + "/곶감"] = "SCR-013-S4-Food-06",
            [Ingredient + "/호박"] = "SCR-008-Vegetable-08",
            [Ingredient + "/밥"] = "SCR-018-Food-Rice",
            [Ingredient + "/솔잎"] = "SCR-018-Food-Leaf",
            // 완성 음식 10종
            [Food + "/잡채"] = "SCR-018-Food-01",
            [Food + "/김치전"] = "SCR-018-Food-02",
            [Food + "/산적"] = "SCR-018-Food-03",
            [Food + "/김밥"] = "SCR-018-Food-04",
            [Food + "/갈비찜"] = "SCR-018-Food-05",
            [Food + "/송편"] = "SCR-018-Food-06",
            [Food + "/수정과"] = "SCR-018-Food-07",
            [Food + "/호박죽"] = "SCR-018-Food-08",
            [Food + "/미역국"] = "SCR-018-Food-09",
            [Food + "/두부조림"] = "SCR-018-Food-10",
            // 점포 4종
            [Store + "/방앗간"] = "SCR-013-Score-01",
            [Store + "/채소 가게"] = "SCR-013-Score-02",
            [Store + "/정육점"] = "SCR-013-Score-03",
            [Store + "/건어물·반찬"] = "SCR-013-Score-04",
            // 아바타 8종
            [Avatar + "/아바타1"] = "Avatar_01",
            [Avatar + "/아바타2"] = "Avatar_02",
            [Avatar + "/아바타3"] = "Avatar_03",
            [Avatar + "/아바타4"] = "Avatar_04",
            [Avatar + "/아바타5"] = "Avatar_05",
            [Avatar + "/아바타6"] = "Avatar_06",
            [Avatar + "/아바타7"] = "Avatar_07",
            [Avatar + "/아바타8"] = "Avatar_08",
        };

        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string category, string name)
        {
            string key = category + "/" + name;
            if (_cache.TryGetValue(key, out var sprite))
                return sprite;
            sprite = Resources.Load<Sprite>("Art/" + key);
            if (sprite == null && Alias.TryGetValue(key, out var alias))
                sprite = Skin.Sprite(alias);
            // 「놓인」 작물은 생략 가능(발주서) — 없으면 매달린 것으로 대체
            if (sprite == null && category == CropLaid)
                sprite = Get(CropHanging, name);
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
            AddIcon(panel.transform, sprite, inset);
            return true;
        }

        public static Image AddIcon(Transform parent, Sprite sprite, float inset = 0.06f)
        {
            var go = new GameObject("ArtIcon");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(inset, inset);
            rect.anchorMax = new Vector2(1f - inset, 1f - inset);
            rect.sizeDelta = Vector2.zero;
            return image;
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
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            CenterPivot(image.rectTransform);
            return true;
        }

        /// preserveAspect는 남는 여백을 피벗 기준으로 정렬한다 — 왼쪽 위 피벗(UiKit.Place)이면 세로로 긴 그림(간장 병)이
        /// 왼쪽, 가로로 긴 그림이 위로 쏠린다. 화면 위치는 그대로 두고 피벗만 중심으로 옮긴다
        static void CenterPivot(RectTransform rect)
        {
            var center = new Vector2(0.5f, 0.5f);
            var delta = center - rect.pivot;
            rect.anchoredPosition += new Vector2(delta.x * rect.sizeDelta.x, delta.y * rect.sizeDelta.y);
            rect.pivot = center;
        }
    }
}
