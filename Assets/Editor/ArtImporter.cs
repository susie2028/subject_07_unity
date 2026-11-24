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
            // UI 틀(9-slice): 늘려도 테두리가 안 깨지게 테두리 두께를 넣어 둔다 (Tools/make_ui_frames.py)
            // 튜토리얼 카드 움짤(녹화 영상을 줄인 그림)은 부드럽게
            if (System.IO.Path.GetFileName(p).StartsWith("tut_") || System.IO.Path.GetFileName(p) == "vignette.png") ti.filterMode = FilterMode.Bilinear;
            Vector4 b;
            if (UiBorders.TryGetValue(System.IO.Path.GetFileNameWithoutExtension(p), out b))
            {
                ti.spriteBorder = b;
                ti.wrapMode = TextureWrapMode.Clamp;
            }
        }

        // 왼쪽, 아래, 오른쪽, 위 (픽셀)
        static readonly System.Collections.Generic.Dictionary<string, Vector4> UiBorders = new System.Collections.Generic.Dictionary<string, Vector4>
        {
            { "frame_panel", new Vector4(8, 8, 8, 8) },   // 큰 판넬 — Image Type: Tiled 권장 (가운데 스캔라인이 안 뭉개짐)
            { "frame_tab", new Vector4(8, 3, 4, 3) },
            { "frame_button", new Vector4(8, 5, 8, 5) },
            { "slider_track", new Vector4(3, 3, 3, 3) },  // 음량 바 (빈 칸)
            { "slider_fill", new Vector4(3, 3, 3, 3) },   // 음량 바 (채운 칸)  // 모든 버튼 — 양 끝은 고정, 가운데만 늘어남 (Sliced)     // 이름표 — 왼쪽 노랑 줄무늬는 고정, 가운데만 늘어남 (Sliced)
        };
    }
}
