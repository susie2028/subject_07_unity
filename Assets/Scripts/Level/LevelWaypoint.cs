// 순찰 지점. 사람(LevelNpc)의 자식. 멈췄을 때 보는 방향 = Y 회전
using UnityEngine;

namespace Specimen07
{
    [AddComponentMenu("표본07/레벨/순찰 지점")]
    public class LevelWaypoint : LevelMarker
    {
        [Tooltip("멈춰 있는 시간 (초)")] public float wait = 1.5f;
        void OnDrawGizmos()
        {
            var n = transform.parent != null ? transform.parent.GetComponent<LevelNpc>() : null;
            var c = n != null ? LevelNpc.CharColor(n.ch) : Color.white;
            bool s = Selected; var p = GamePos;
            var fl = Floor;
            if (!s && fl != null && !fl.showRoutes) return;
            GBox(p.x, p.y, 0.3f, 0.3f, 0.12f, 0, new Color(c.r, c.g, c.b, s ? 0.8f : 0.35f), c);
            if (s) { float f = Yaw * Mathf.Deg2Rad; Gizmos.color = c; Gizmos.DrawLine(W(p.x, p.y, 0.1f), W(p.x + Mathf.Cos(f) * 0.6f, p.y + Mathf.Sin(f) * 0.6f, 0.1f)); }
            if (s || (n != null && IsParentSelected(n))) GLabel(p.x, p.y, 0.45f, (transform.GetSiblingIndex() + 1).ToString(), Color.white);
        }
        bool IsParentSelected(LevelNpc n)
        {
#if UNITY_EDITOR
            foreach (var o in UnityEditor.Selection.transforms) if (o == n.transform) return true;
#endif
            return false;
        }
    }
}
