using UnityEditor;

namespace Shinmyeong.EditorTools
{
    /// Resources/Art 아래 그림은 자동으로 Sprite로 임포트한다 (Docs/92).
    /// 프로젝트가 3D 템플릿 기반이라 PNG 기본 임포트가 Default(Texture)인데,
    /// 그러면 ArtCatalog의 Resources.Load&lt;Sprite&gt;가 null을 받아 교체가 되지 않는다.
    /// 파일을 넣기만 하면 되게 임포트 설정을 여기서 강제한다.
    public class ArtImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Art/"))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
        }
    }
}
