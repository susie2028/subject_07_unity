// 콘솔 (칸 하나). 함정 id를 적으면 그 함정을 끄는 콘솔, 헬기 호출 콘솔도 이것
using UnityEngine;

namespace Specimen07
{
    [AddComponentMenu("표본07/레벨/콘솔")]
    public class LevelConsole : LevelMarker
    {
        [Tooltip("필요한 카드 id")] public string req = "sec";
        [Tooltip("끄는 함정 id (없으면 비움)")] public string trap = "";
        [Tooltip("헬기 호출 콘솔")] public bool heli;
        public string label = "콘솔";
        public override int SnapMode { get { return 1; } }
        void OnDrawGizmos()
        {
            var cl = Cell; bool s = Selected; var c = new Color(1f, 0.83f, 0.42f);
            GBox(cl.x + 0.5f, cl.y + 0.5f, 1.02f, 1.02f, 0.8f, 0, new Color(c.r, c.g, c.b, s ? 0.3f : 0.08f), new Color(c.r, c.g, c.b, s ? 1 : 0.6f));
            if (s) GLabel(cl.x + 0.5f, cl.y + 0.5f, 1.2f, label, Color.white);
        }
    }
}
