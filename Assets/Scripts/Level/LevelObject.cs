// 먹고 변신할 수 있는 물건 (의자·쓰레기통·카트·화분·정수기·상자). 의자 방향 = Y 회전
using UnityEngine;

namespace Specimen07
{
    [AddComponentMenu("표본07/레벨/물건")]
    public class LevelObject : LevelMarker
    {
        [Tooltip("chair trash cart plant water box")] public string form = "chair";
        void OnDrawGizmos()
        {
            var p = GamePos; bool s = Selected; var c = new Color(0.66f, 0.48f, 0.95f);
            GBox(p.x, p.y, 0.62f, 0.62f, 0.75f, 0, new Color(c.r, c.g, c.b, s ? 0.25f : 0.04f), new Color(c.r, c.g, c.b, s ? 1 : 0.4f));
        }
    }
}
