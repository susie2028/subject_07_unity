// 엘리베이터 칸
using UnityEngine;

namespace Specimen07
{
    [AddComponentMenu("표본07/레벨/엘리베이터 칸")]
    public class LevelLift : LevelMarker
    {
        public override int SnapMode { get { return 1; } }
        void OnDrawGizmos()
        {
            var cl = Cell; bool s = Selected; var c = new Color(0.5f, 0.88f, 0.69f);
            GBox(cl.x + 0.5f, cl.y + 0.5f, 1.02f, 1.02f, 1.35f, 0, new Color(c.r, c.g, c.b, s ? 0.3f : 0.06f), new Color(c.r, c.g, c.b, s ? 1 : 0.5f));
        }
    }
}
