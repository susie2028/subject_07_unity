// 층 하나. 씬의 "레벨" 아래에 층마다 하나씩 있고, 자식으로 방·사람·물건·문… 표시물이 들어 있다.
//  - 바닥·벽·가구(타일)는 이 컴포넌트의 tiles에 들어 있다 → 인스펙터의 "타일 칠하기"로 씬에서 칠한다
//  - 나머지는 자식 오브젝트를 고르고 이동 툴(W)로 옮긴다 (의자·순찰 지점의 방향은 회전 툴 E). 복제(Ctrl/Cmd+D)로 늘린다
//  - 게임은 시작할 때 씬의 층들을 읽어 맵으로 쓴다 (없으면 코드에 적힌 기본 맵)
//  - 씬에 보이는 벽·가구·사람 그림은 미리보기라 씬 파일에 저장되지 않는다
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Specimen07
{
    [AddComponentMenu("표본07/레벨/층")]
    [DisallowMultipleComponent]
    public class LevelFloor : MonoBehaviour
    {
        [Serializable] public class Reinforce { public string ch = "soldier"; public int n = 3; }

        [Header("층")]
        public string id = "B1";
        public string floorName = "격리동";
        [TextArea(1, 3)] public string sub = "";
        [Tooltip("들키면 바로 끝나는 층")] public bool lethal;
        [Range(0, 1)] public float dark = 0.6f;
        [Tooltip("경보가 울리면 나타나는 지원병")] public List<Reinforce> reinforce = new List<Reinforce>();

        [Header("타일 (한 줄 = 한 행, 위에서 아래로). 인스펙터의 '타일 칠하기'로 고친다")]
        public int w = 40, h = 30;
        [HideInInspector] public List<string> tiles = new List<string>();

        [Header("씬에서 보기")]
        public bool showRoutes = true;

        /* ===== 타일 ===== */
        public char Tile(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || y >= tiles.Count) return '#';
            var row = tiles[y]; return row != null && x < row.Length ? row[x] : '#';
        }
        public void SetTile(int x, int y, char c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            FixSize();
            var row = tiles[y].ToCharArray(); if (row[x] == c) return;
            row[x] = c; tiles[y] = new string(row);
        }
        public void FixSize()
        {
            while (tiles.Count < h) tiles.Add(new string('#', w));
            if (tiles.Count > h) tiles.RemoveRange(h, tiles.Count - h);
            for (int y = 0; y < h; y++)
            {
                var r = tiles[y] ?? "";
                if (r.Length < w) r = r + new string('#', w - r.Length); else if (r.Length > w) r = r.Substring(0, w);
                tiles[y] = r;
            }
        }

        // 켜져 있는 표시물만 (층 자체가 꺼져 있어도 상관없음)
        public List<T> All<T>() where T : LevelMarker
        {
            var l = new List<T>();
            foreach (var c in GetComponentsInChildren<T>(true)) if (c.IsOn && c.Floor == this) l.Add(c);
            return l;
        }

        /* ===== 게임용 층 정의로 ===== */
        static float R(float v) { return Mathf.Round(v * 100f) / 100f; }
        static Vector2 R(Vector2 v) { return new Vector2(R(v.x), R(v.y)); }
        public FloorDef ToDef()
        {
            FixSize();
            var d = new FloorDef
            {
                id = id, name = floorName, sub = sub, w = w, h = h, lethal = lethal, dark = dark,
                tiles = tiles.ToArray(),
                gaps = new Vector2Int[0], glass = new RectI[0], vents = new RectI[0],
            };
            var rooms = new List<RoomDef>();
            foreach (var r in All<LevelRoom>())
            {
                string[] allowArr = null;
                if (!string.IsNullOrEmpty(r.allow) && r.allow.Trim() != "")
                {
                    var parts = new List<string>();
                    foreach (var p in r.allow.Split(',')) if (p.Trim() != "") parts.Add(p.Trim());
                    allowArr = parts.ToArray();
                }
                var rc = r.Rect;
                rooms.Add(new RoomDef(r.roomName, rc.x, rc.y, rc.width, rc.height, r.kind, allowArr, r.label));
            }
            d.rooms = rooms.ToArray();
            d.fill = BigRects();
            d.doors = All<LevelDoor>().ConvertAll(x => new DoorDef(x.Cell.x, x.Cell.y, x.req, x.label)).ToArray();
            d.consoles = All<LevelConsole>().ConvertAll(x => new ConsoleDef(x.Cell.x, x.Cell.y, x.req, string.IsNullOrEmpty(x.trap) ? null : x.trap, x.heli, x.label)).ToArray();
            d.notices = All<LevelNotice>().ConvertAll(x => new NoticeDef(x.Cell.x, x.Cell.y, x.text)).ToArray();
            d.stairs = All<LevelStairs>().ConvertAll(x => { var r = x.Rect; return new StairsDef(r.x, r.y, r.width, r.height, x.to, string.IsNullOrEmpty(x.dir) ? '>' : x.dir[0]); }).ToArray();
            var lift = All<LevelLift>().ConvertAll(x => x.Cell);
            d.lift = lift.Count > 0 ? lift.ToArray() : null;
            var pads = All<LevelPad>();
            if (pads.Count > 0) { var r = pads[0].Rect; d.pad = new RectI(r.x, r.y, r.width, r.height); }
            var traps = new List<TrapDef>();
            foreach (var t in All<LevelTrap>())
            {
                var rs = new List<RectI>();
                foreach (var a in t.GetComponentsInChildren<LevelTrapArea>(true)) if (a.IsOn) { var r = a.Rect; rs.Add(new RectI(r.x, r.y, r.width, r.height)); }
                traps.Add(new TrapDef(t.trapId, t.kind, t.label, rs.ToArray()));
            }
            d.traps = traps.ToArray();
            var spawns = new List<Vector2>();
            foreach (var p in All<LevelPoint>())
            {
                var v = R(p.GamePos);
                switch (p.kind)
                {
                    case LevelPoint.Kind.시작: d.arriveStart = v; break;
                    case LevelPoint.Kind.계단도착: d.arriveStairs = v; break;
                    case LevelPoint.Kind.엘베도착: d.arriveLift = v; break;
                    case LevelPoint.Kind.수조: d.tank = v; break;
                    case LevelPoint.Kind.지원병등장: spawns.Add(v); break;
                }
            }
            d.spawns = spawns.ToArray();
            d.reinforce = reinforce.ConvertAll(r => new ReinforceDef(r.ch, r.n)).ToArray();
            d.extraLights = All<LevelLamp>().ConvertAll(l => { var p = R(l.GamePos); return new LightDef(p.x, p.y, l.radius, l.intensity, l.flick, l.tint); }).ToArray();
            d.objects = All<LevelObject>().ConvertAll(o => { var p = R(o.GamePos); return new ObjDef(o.form, p.x, p.y, (o.Yaw + 90) * Mathf.Deg2Rad); }).ToArray();
            d.npcs = All<LevelNpc>().ConvertAll(n =>
            {
                var p = R(n.GamePos);
                var route = n.Route.ConvertAll(wp => { var q = R(wp.GamePos); return new RouteWp(q.x, q.y, wp.wait, Mathf.Round(wp.Yaw)); }).ToArray();
                if (route.Length == 0) route = new[] { new RouteWp(p.x, p.y, 99, Mathf.Round(n.face)) };   // 경로가 없으면 그 자리에 서 있기
                return new NpcDef(n.npcId, n.ch, string.IsNullOrEmpty(n.pal) ? n.ch : n.pal, n.label, p.x, p.y, n.face, route);
            }).ToArray();
            return d;
        }

        // 실외기·탱크는 덩어리째 그리므로 칠한 칸에서 덩어리를 찾는다
        FillDef[] BigRects()
        {
            var list = new List<FillDef>();
            var seen = new bool[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                char c = Tile(x, y);
                if ((c != 'A' && c != 'W') || seen[y * w + x]) continue;
                int x0 = x, x1 = x, y0 = y, y1 = y;
                var q = new Queue<Vector2Int>(); q.Enqueue(new Vector2Int(x, y)); seen[y * w + x] = true;
                while (q.Count > 0)
                {
                    var p = q.Dequeue();
                    x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y);
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = p.x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = p.y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen[ny * w + nx] || Tile(nx, ny) != c) continue;
                        seen[ny * w + nx] = true; q.Enqueue(new Vector2Int(nx, ny));
                    }
                }
                list.Add(new FillDef(c, x0, y0, x1 - x0 + 1, y1 - y0 + 1));
            }
            return list.ToArray();
        }

        /* ===== 게임 시작 시: 씬의 층들을 맵으로 쓰고 표시물은 숨긴다 ===== */
        public static void UseSceneLevels()
        {
            var floors = FindObjectsByType<LevelFloor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (floors.Length == 0) return;
            var defs = new List<FloorDef>();
            foreach (var f in floors) { try { defs.Add(f.ToDef()); } catch (Exception e) { Debug.LogError("[표본07] 층 " + f.id + "을 읽지 못했어요: " + e.Message); } }
            FloorGeo.UseLevels(defs);
            foreach (var f in floors) { var root = f.transform.parent != null ? f.transform.parent.gameObject : f.gameObject; root.SetActive(false); }
        }

        void OnDrawGizmos()
        {
            // 층 테두리 + 이름 (층을 누르기 쉽게 바깥 테두리 띠)
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1, 1, 1, 0.5f);
            Gizmos.DrawWireCube(new Vector3(w / 2f, 0, -h / 2f), new Vector3(w, 0.02f, h));
            Gizmos.matrix = Matrix4x4.identity;
#if UNITY_EDITOR
            var st = new GUIStyle(UnityEditor.EditorStyles.boldLabel) { fontSize = 20 };
            st.normal.textColor = Color.white;
            UnityEditor.Handles.Label(transform.TransformPoint(new Vector3(0, 0.1f, 1.6f)), id + " " + floorName, st);
#endif
        }
    }
}
