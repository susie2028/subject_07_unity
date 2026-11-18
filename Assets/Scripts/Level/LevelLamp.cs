// 추가 조명 (방 조명은 방마다 자동으로 생긴다)
using UnityEngine;

namespace Specimen07
{
    [AddComponentMenu("표본07/레벨/추가 조명")]
    public class LevelLamp : LevelMarker
    {
        public float radius = 4, intensity = 0.8f;
        [Tooltip("0 안 깜빡임, 1 가끔, 2 자주")] public int flick;
        [Tooltip("알파 0이면 기본 색")] public Color tint = new Color(0, 0, 0, 0);
        void OnDrawGizmos()
        {
            var p = GamePos; bool s = Selected;
            GBox(p.x, p.y, 0.35f, 0.35f, 0.35f, 1.2f, new Color(1f, 0.95f, 0.6f, s ? 0.8f : 0.4f), new Color(1f, 0.95f, 0.6f));
            if (s) { Gizmos.color = new Color(1f, 0.95f, 0.6f, 0.5f); Gizmos.DrawWireSphere(W(p.x, p.y, 0.1f), radius); }
        }
    }
}
