// 문 (칸 하나). 필요한 카드 색으로 보인다
using UnityEngine;

namespace Specimen07
{
    [AddComponentMenu("표본07/레벨/문")]
    public class LevelDoor : LevelMarker
    {
        [Tooltip("lab sec bio exec 등 (카드 id)")] public string req = "lab";
        public string label = "";
        public override int SnapMode { get { return 1; } }
        void OnDrawGizmos()
        {
            var c = Data.Cards.ContainsKey(req) ? Data.Cards[req].color : Color.white; var cl = Cell; bool s = Selected;
            GBox(cl.x + 0.5f, cl.y + 0.5f, 1.02f, 1.02f, 1.95f, 0, new Color(c.r, c.g, c.b, s ? 0.3f : 0.08f), new Color(c.r, c.g, c.b, s ? 1 : 0.6f));
        }
    }
}
