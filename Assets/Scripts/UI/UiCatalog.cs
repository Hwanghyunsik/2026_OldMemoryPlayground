using System.Collections.Generic;
using UnityEngine;

namespace Shinmyeong.UI
{
    /// 디자인 자산 창구 — `Assets/UI Image/*.png`(UI 스프라이트)와 `Assets/Font/*.OTF`(S-Core Dream)를
    /// 에디터가 자동으로 모아 `Resources/UiCatalog.asset`에 담는다(Editor/UiCatalogBuilder).
    /// 런타임 코드는 파일명(확장자 없음)으로 스프라이트를 찾는다 — 디자이너가 파일을 갱신하면 재생성만 하면 된다.
    public class UiCatalog : ScriptableObject
    {
        [System.Serializable]
        public class SpriteEntry
        {
            public string Name;
            public Sprite Sprite;
        }

        public List<SpriteEntry> Sprites = new List<SpriteEntry>();
        public List<Font> Fonts = new List<Font>();

        static UiCatalog _instance;
        static bool _loaded;
        Dictionary<string, Sprite> _map;
        Dictionary<string, Font> _fontMap;

        public static UiCatalog Instance
        {
            get
            {
                if (!_loaded)
                {
                    _instance = Resources.Load<UiCatalog>("UiCatalog");
                    _loaded = true;
                    if (_instance == null)
                        Debug.LogWarning("[UiCatalog] Resources/UiCatalog.asset 없음 — 메뉴 「신명/UI 카탈로그 재생성」 실행");
                }
                return _instance;
            }
        }

        public Sprite FindSprite(string name)
        {
            if (_map == null)
            {
                _map = new Dictionary<string, Sprite>();
                foreach (var e in Sprites)
                    if (e != null && !string.IsNullOrEmpty(e.Name) && e.Sprite != null)
                        _map[e.Name] = e.Sprite;
            }
            return _map.TryGetValue(name, out var s) ? s : null;
        }

        public Font FindFont(string name)
        {
            if (_fontMap == null)
            {
                _fontMap = new Dictionary<string, Font>();
                foreach (var f in Fonts)
                    if (f != null)
                        _fontMap[f.name.ToUpperInvariant()] = f;
            }
            return _fontMap.TryGetValue(name.ToUpperInvariant(), out var font) ? font : null;
        }
    }

    /// 디자인 토큰 — 시안(Assets/Planning/신명UI미리보기 · SinMyung.unity)에서 뽑은 색·폰트.
    /// 화면 코드는 색값을 직접 쓰지 않고 여기 이름을 쓴다.
    public static class Skin
    {
        // ---- 글자 색 ----
        public static readonly Color Ink = Hex("1d1513");        // 큰 제목
        public static readonly Color Brown = Hex("362f2d");      // 본문
        public static readonly Color BrownDark = Hex("3b220a");  // 강조 본문 · 숫자
        public static readonly Color Muted = Hex("6c6253");      // 보조 설명
        public static readonly Color ButtonLabel = Hex("493a24");
        public static readonly Color IconTint = Hex("432908");
        public static readonly Color Cream = Hex("faf3e6");

        // ---- 계열 색 ----
        public static readonly Color Green = Hex("3d6a4d");
        public static readonly Color GreenDeep = Hex("357f12");
        public static readonly Color GreenLine = Hex("426e04");
        public static readonly Color Orange = Hex("d66121");
        public static readonly Color Red = Hex("df4117");
        public static readonly Color Purple = Hex("662f9c");
        public static readonly Color Blue = Hex("4b87ff");       // 선택 진행 표시
        public static readonly Color Gold = Hex("d7a45a");
        public static readonly Color Amber = Hex("b18860");      // 튜토리얼 대기 단계 번호
        public static readonly Color Teal = Hex("44979c");
        public static readonly Color Navy = Hex("1d5d83");

        // ---- 면 · 테두리 ----
        public static readonly Color Paper = Hex("f6ebd1");
        public static readonly Color Face = Hex("fcf8e9");
        public static readonly Color FaceWarm = Hex("f7ead2");
        public static readonly Color FaceLight = Hex("fff8e8");
        public static readonly Color FaceGreen = Hex("edf0d9");
        public static readonly Color Outline = Hex("d5c5a4");
        public static readonly Color Outline2 = Hex("d2c4ab");
        public static readonly Color Outline3 = Hex("c2b286");
        public static readonly Color OutlineDark = Hex("b5a67d");
        public static readonly Color Tan = Hex("ddd3bb");
        public static readonly Color Track = Hex("c9bca1");
        public static readonly Color Track2 = Hex("e8ddc8");
        public static readonly Color Track3 = Hex("d7cdb7");
        public static readonly Color Dim = Hex("1d1513", 0.88f);
        public static readonly Color DimBrown = Hex("362f2d", 0.88f);
        public static readonly Color Shadow = Hex("4d3d2b", 0.30f);

        // ---- 결과 나열 색 (제대로 = 초록 계열 / 다르게 = 주황 계열 · 6-9-3 색상 계열 구분) ----
        public static readonly Color ProperFace = Hex("e8eddf");
        public static readonly Color ProperCard = Hex("f3f6e0");
        public static readonly Color ProperOutline = Hex("91af83");
        public static readonly Color OtherFace = Hex("f8d9cb");
        public static readonly Color OtherCard = Hex("f3e4de");
        public static readonly Color OtherOutline = Hex("d27d74");
        public static readonly Color OtherLine = Hex("e6591e");

        public static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            c.a = alpha;
            return c;
        }

        public static Sprite Sprite(string name)
        {
            var cat = UiCatalog.Instance;
            return cat != null ? cat.FindSprite(name) : null;
        }

        /// S-Core Dream 굵기 4(가는)~8(굵은) — 없으면 내장 폰트
        public static Font Font(int weight)
        {
            var cat = UiCatalog.Instance;
            var font = cat != null ? cat.FindFont($"SCDREAM{Mathf.Clamp(weight, 1, 9)}") : null;
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
