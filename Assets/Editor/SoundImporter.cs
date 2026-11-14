// Resources/Sounds 의 배경음악(bgm_*)은 길어서 메모리에 다 풀지 않고 재생하면서 읽게(Streaming) 설정한다.
using UnityEditor;
using UnityEngine;

namespace Specimen07.EditorTools
{
    public class SoundImporter : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Resources/Sounds/")) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            if (System.IO.Path.GetFileName(p).StartsWith("bgm_"))
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
            }
            else s.loadType = AudioClipLoadType.DecompressOnLoad;
            ai.defaultSampleSettings = s;
        }
    }
}
