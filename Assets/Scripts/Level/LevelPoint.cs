// 지점: 게임 시작·계단 도착·엘리베이터 도착·주인공 수조·지원병 등장
using UnityEngine;

namespace Specimen07
{
    [AddComponentMenu("표본07/레벨/지점")]
    public class LevelPoint : LevelMarker
    {
        public enum Kind { 시작, 계단도착, 엘베도착, 수조, 지원병등장 }
        public Kind kind;
        void OnDrawGizmos()
        {
            var p = GamePos; bool s = Selected;
            var c = kind == Kind.지원병등장 ? new Color(1f, 0.4f, 0.35f) : new Color(0.5f, 0.95f, 0.75f);
            GBox(p.x, p.y, 0.7f, 0.7f, 0.1f, 0, new Color(c.r, c.g, c.b, s ? 0.6f : 0.25f), c);
            GLabel(p.x, p.y, 0.6f, kind.ToString(), new Color(c.r, c.g, c.b, 1));
        }
    }
}
