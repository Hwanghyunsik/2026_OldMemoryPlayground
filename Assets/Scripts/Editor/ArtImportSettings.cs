using UnityEditor;

namespace Shinmyeong.EditorTools
{
    /// Resources/Art 아래 그림은 자동으로 Sprite로 임포트한다 (Docs/92).
    /// 프로젝트가 3D 템플릿 기반이라 PNG 기본 임포트가 Default(Texture)인데,
    /// 그러면 ArtCatalog의 Resources.Load&lt;Sprite&gt;가 null을 받아 교체가 되지 않는다.
    /// 파일을 넣기만 하면 되게 임포트 설정을 여기서 강제한다.
    /// `Assets/UI Image`(디자인 시안)는 Sprite Multiple + 슬라이스 0개로 들어오면 Sprite 서브에셋이 없어
    /// UiCatalog에서 빠진다(2026-09-23 밥·솔잎) — 그 경우만 Single로 바꾼다. 디자이너가 슬라이스한 파일은 건드리지 않는다.
    public class ArtImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (path.StartsWith("Assets/UI Image/"))
            {
                var ui = (TextureImporter)assetImporter;
#pragma warning disable 618 // spritesheet — 슬라이스 유무 확인용
                if (ui.textureType == TextureImporterType.Sprite
                    && ui.spriteImportMode == SpriteImportMode.Multiple
                    && ui.spritesheet.Length == 0)
                    ui.spriteImportMode = SpriteImportMode.Single;
#pragma warning restore 618
                return;
            }

            if (!path.Contains("/Resources/Art/"))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
        }
    }
}
