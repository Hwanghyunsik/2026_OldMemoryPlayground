using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using Shinmyeong.UI;

namespace Shinmyeong.EditorTools
{
    /// `Assets/UI Image`(디자인 스프라이트)·`Assets/Font`(S-Core Dream)·`Assets/Video`(영상)·`Assets/Prefabs/P_*`(이펙트)를 `Resources/UiCatalog.asset`으로 모은다.
    /// 디자이너가 파일을 넣거나 바꾸면 자동 재생성 — 코드 수정 없이 반영된다.
    public static class UiCatalogBuilder
    {
        const string SpriteFolder = "Assets/UI Image";
        const string FontFolder = "Assets/Font";
        const string VideoFolder = "Assets/Video";
        const string EffectFolder = "Assets/Prefabs";
        const string EffectPrefix = "P_"; // 디자이너 파티클 이펙트 프리팹 이름 규칙 (P_Get_Game1 · P_TargetPoint_Game1 …)
        const string ScreenPrefix = "SCR-"; // 디자이너 화면 레이아웃 프리팹 (SCR-000-Login …)
        const string AudioFolder = "Assets/Audio";
        // 08 사운드 시나리오 파일명 규칙 — `01_BGM_main` · `08_SE_harvest` · `01_VO_손을_올리세요` → 코드 `01_BGM` · `08_SE` · `01_VO`
        static readonly System.Text.RegularExpressions.Regex SoundName = new System.Text.RegularExpressions.Regex(@"^(\d\d_(?:BGM|SE|VO))_");
        const string AssetPath = "Assets/Resources/UiCatalog.asset";

        [InitializeOnLoadMethod]
        static void EnsureExists()
        {
            EditorApplication.delayCall += () =>
            {
                var catalog = AssetDatabase.LoadAssetAtPath<UiCatalog>(AssetPath);
                // 소리 목록이 빈 예전 카탈로그도 한 번 다시 만든다 (오디오 폴더에 규칙에 맞는 파일이 있을 때만)
                if (catalog == null || (catalog.Sounds.Count == 0 && HasSoundFiles()))
                    Rebuild();
            };
        }

        static bool HasSoundFiles()
        {
            if (!AssetDatabase.IsValidFolder(AudioFolder))
                return false;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
                if (SoundName.IsMatch(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid))))
                    return true;
            return false;
        }

        [MenuItem("신명/UI 카탈로그 재생성")]
        public static void Rebuild()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UiCatalog>(AssetPath);
            bool created = false;
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                catalog = ScriptableObject.CreateInstance<UiCatalog>();
                created = true;
            }

            catalog.Sprites.Clear();
            var seen = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                Sprite sprite = null;
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (obj is Sprite s) { sprite = s; break; }
                if (sprite == null)
                {
                    Debug.LogWarning($"[UiCatalog] 스프라이트 아님(임포트 설정 확인): {path}");
                    continue;
                }
                if (!seen.Add(name))
                    Debug.LogWarning($"[UiCatalog] 같은 이름 중복: {name}");
                catalog.Sprites.Add(new UiCatalog.SpriteEntry { Name = name, Sprite = sprite });
            }
            catalog.Sprites.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            catalog.Fonts.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:Font", new[] { FontFolder }))
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
                if (font != null)
                    catalog.Fonts.Add(font);
            }

            catalog.Videos.Clear();
            if (AssetDatabase.IsValidFolder(VideoFolder))
                foreach (var guid in AssetDatabase.FindAssets("t:VideoClip", new[] { VideoFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
                    if (clip != null)
                        catalog.Videos.Add(new UiCatalog.VideoEntry { Name = Path.GetFileNameWithoutExtension(path), Clip = clip });
                }
            catalog.Videos.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            catalog.Effects.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { EffectFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileName(path).StartsWith(EffectPrefix))
                    continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<ParticleSystem>() != null)
                    catalog.Effects.Add(prefab);
            }
            catalog.Effects.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            catalog.Screens.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { EffectFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileName(path).StartsWith(ScreenPrefix))
                    continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    catalog.Screens.Add(prefab);
            }
            catalog.Screens.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            catalog.Sounds.Clear();
            if (AssetDatabase.IsValidFolder(AudioFolder))
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var m = SoundName.Match(Path.GetFileNameWithoutExtension(path));
                    if (!m.Success)
                        continue; // 규칙 밖 파일(예전 영상 BGM 등)은 담지 않는다
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip == null)
                        continue;
                    string code = m.Groups[1].Value;
                    if (catalog.Sounds.Exists(e => e.Code == code))
                        Debug.LogWarning($"[UiCatalog] 같은 소리 코드 중복: {code} ({path})");
                    catalog.Sounds.Add(new UiCatalog.SoundEntry { Code = code, Clip = clip });
                }
            catalog.Sounds.Sort((a, b) => string.CompareOrdinal(a.Code, b.Code));

            if (created)
                AssetDatabase.CreateAsset(catalog, AssetPath);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[UiCatalog] 재생성 — 스프라이트 {catalog.Sprites.Count} · 폰트 {catalog.Fonts.Count} · 영상 {catalog.Videos.Count} · 이펙트 {catalog.Effects.Count} · 화면 {catalog.Screens.Count} · 소리 {catalog.Sounds.Count}");
        }

        class Watcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                bool hit = false;
                foreach (var list in new[] { imported, deleted, moved, movedFrom })
                    foreach (var p in list)
                        if (p.StartsWith(SpriteFolder + "/") || p.StartsWith(FontFolder + "/") || p.StartsWith(VideoFolder + "/") || p.StartsWith(AudioFolder + "/")
                            || (p.StartsWith(EffectFolder + "/") && (Path.GetFileName(p).StartsWith(EffectPrefix) || Path.GetFileName(p).StartsWith(ScreenPrefix))))
                            hit = true;
                if (hit)
                    EditorApplication.delayCall += Rebuild;
            }
        }
    }
}
