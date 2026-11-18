// 사람. 자식 LevelWaypoint 들이 순찰 경로 (위에서부터 순서대로, 끝나면 처음으로)
// 사람을 옮기면 순찰 경로도 같이 옮겨진다 (순찰 지점만 따로 옮기려면 그 지점을 고른다)
using System.Collections.Generic;
using UnityEngine;

namespace Specimen07
{
    [AddComponentMenu("표본07/레벨/사람")]
    public class LevelNpc : LevelMarker
    {
        [Tooltip("층 안에서 겹치지 않는 이름 (자동으로 고쳐짐)")] public string npcId = "n1";
        [Tooltip("cleaner researcher guard senior soldier director")] public string ch = "researcher";
        [Tooltip("그림 종류 (researcher2 처럼 같은 직업의 다른 모습). 비우면 직업과 같음")] public string pal = "researcher";
        [Tooltip("이름표")] public string label = "연구원";
        [Tooltip("처음 보는 방향 (도). 0 = 오른쪽, 90 = 아래, -90 = 위")] [Range(-180, 180)] public float face;

        public List<LevelWaypoint> Route
        {
            get
            {
                var l = new List<LevelWaypoint>();
                foreach (Transform c in transform) { var w = c.GetComponent<LevelWaypoint>(); if (w != null && w.IsOn) l.Add(w); }
                return l;
            }
        }
        public static Color CharColor(string ch)
        {
            switch (ch)
            {
                case "cleaner": return new Color(0.75f, 0.8f, 0.85f);
                case "researcher": return new Color(0.5f, 0.75f, 0.95f);
                case "guard": return new Color(0.95f, 0.7f, 0.3f);
                case "senior": return new Color(0.7f, 0.55f, 0.95f);
                case "soldier": return new Color(0.55f, 0.8f, 0.45f);
                case "director": return new Color(1f, 0.35f, 0.3f);
            }
            return Color.white;
        }
        void OnDrawGizmos()
        {
            var p = GamePos; var c = CharColor(ch); bool s = Selected;
            GBox(p.x, p.y, 0.55f, 0.55f, 1.45f, 0, new Color(c.r, c.g, c.b, s ? 0.25f : 0.06f), new Color(c.r, c.g, c.b, s ? 1 : 0.5f));
            float f = face * Mathf.Deg2Rad;
            Gizmos.color = c; Gizmos.DrawLine(W(p.x, p.y, 0.05f), W(p.x + Mathf.Cos(f) * 0.9f, p.y + Mathf.Sin(f) * 0.9f, 0.05f));
            var route = Route;
            var fl = Floor;
            if (route.Count > 1 && (s || fl == null || fl.showRoutes))
            {
                Gizmos.color = new Color(c.r, c.g, c.b, s ? 1f : 0.45f);
                for (int i = 0; i < route.Count; i++) { var a = route[i].GamePos; var b = route[(i + 1) % route.Count].GamePos; Gizmos.DrawLine(W(a.x, a.y, 0.06f), W(b.x, b.y, 0.06f)); }
            }
            if (s) GLabel(p.x, p.y, 1.9f, label, Color.white);
        }
    }
}
