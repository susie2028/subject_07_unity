// 게시물 (칸 하나, 벽 앞). 읽으면 글이 나온다
using UnityEngine;

namespace Specimen07
{
    [AddComponentMenu("표본07/레벨/게시물")]
    public class LevelNotice : LevelMarker
    {
        [TextArea(2, 6)] public string text = "";
        public override int SnapMode { get { return 1; } }
        void OnDrawGizmos()
        {
            var cl = Cell; bool s = Selected; var c = new Color(0.85f, 0.65f, 0.4f);
            GBox(cl.x + 0.5f, cl.y + 0.5f, 1.02f, 1.02f, 1.1f, 0, new Color(c.r, c.g, c.b, s ? 0.3f : 0.08f), new Color(c.r, c.g, c.b, s ? 1 : 0.6f));
        }
    }
}
