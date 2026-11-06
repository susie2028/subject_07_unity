// Assets/Resources/Art 폴더에 PNG를 넣으면 자동으로 픽셀아트용 스프라이트 설정을 해 준다.
// (Sprite, Point 필터, 압축 없음, 1칸 = 32픽셀). 다른 해상도로 그리려면 아래 PPU만 바꾸면 된다.
using UnityEditor;
using UnityEngine;

namespace Specimen07.EditorTools
{
    public class ArtImporter : AssetPostprocessor
    {
        const int PPU = 32;

        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Resources/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            // 이미 손으로 설정을 바꾼 파일(예: 스프라이트 시트 자르기)은 그대로 둔다
            if (ti.userData == "specimen07-manual") return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = PPU;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
        }
    }
}
