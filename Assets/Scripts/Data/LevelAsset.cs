// 층 하나의 레벨 데이터 (Assets/Resources/Levels/<층>.asset).
// 유니티 메뉴 "표본07 → 레벨 에디터"에서 씬에 층을 띄워 놓고 칠하고 옮기며 고친다. 게임은 시작할 때 이걸 읽는다.
// 에셋이 없으면 코드에 적힌 기본 맵(FloorDefs.cs)을 쓴다.
//
// 좌표: x 오른쪽, y 아래쪽, 1칸 = 1. 사람·물건 위치는 칸 가운데가 .5
// 타일 글자: '#' 벽, '.' 바닥, T 책상, S 선반, M 설비, V 자판기, W 탱크, A 실외기, G 유리, v 환풍구
//           (문·콘솔·게시물·엘리베이터·계단·헬리패드는 아래 목록에서 따로 놓는다)
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Specimen07
{
    [CreateAssetMenu(menuName = "표본07/레벨 (층 하나)", fileName = "Level")]
    public class LevelAsset : ScriptableObject
    {
        [Serializable] public class Room { public string name = "새 방"; public string kind = "office"; public RectInt rect = new RectInt(1, 1, 6, 5); [Tooltip("들어가도 이상하지 않은 사람 종류 (쉼표로). 비우면 누구나")] public string allow = ""; public bool label = true; }
        [Serializable] public class Door { public Vector2Int pos; public string req = "lab"; public string label = ""; }
        [Serializable] public class Console { public Vector2Int pos; public string req = "sec"; [Tooltip("끄는 함정 id (없으면 비움)")] public string trap = ""; [Tooltip("헬기 호출 콘솔")] public bool heli; public string label = ""; }
        [Serializable] public class Notice { public Vector2Int pos; [TextArea(2, 5)] public string text = ""; }
        [Serializable] public class Stairs { public RectInt rect = new RectInt(1, 1, 2, 1); [Tooltip("이어지는 층 id")] public string to = "1F"; [Tooltip("> 올라가기 / < 내려가기")] public string dir = ">"; }
        [Serializable] public class Trap { public string id = "T1"; [Tooltip("laser | gas")] public string kind = "laser"; public string label = ""; public List<RectInt> rects = new List<RectInt>(); }
        [Serializable] public class Obj { public string form = "chair"; public Vector2 pos; [Tooltip("라디안 (의자만 씀)")] public float rot; }
        [Serializable] public class Wp { public Vector2 pos; [Tooltip("멈춰 있는 시간 (초)")] public float wait = 1; [Tooltip("멈췄을 때 볼 방향 (도). 0 = 오른쪽, 90 = 아래")] public float look; }
        [Serializable] public class Npc { public string id = "n1"; public string ch = "researcher"; [Tooltip("그림 종류 (researcher2 처럼 같은 직업의 다른 모습)")] public string pal = "researcher"; public string label = "연구원"; public Vector2 pos; [Tooltip("처음 보는 방향 (도)")] public float face; public List<Wp> route = new List<Wp>(); }
        [Serializable] public class Lamp { public Vector2 pos; public float r = 4, i = 0.8f; [Tooltip("0 안 깜빡임, 1 가끔, 2 자주")] public int flick; public Color tint = new Color(0, 0, 0, 0); }
        [Serializable] public class Reinforce { public string ch = "soldier"; public int n = 3; }

        [Header("층")]
        public string id = "B1";
        public string floorName = "격리동";
        [TextArea(1, 3)] public string sub = "";
        public bool lethal;
        [Range(0, 1)] public float dark = 0.6f;

        [Header("타일 (한 줄 = 한 행, 위에서 아래로)")]
        public int w = 40, h = 30;
        public List<string> tiles = new List<string>();

        [Header("방 (이름·종류·조명·바닥 무늬·출입 제한의 단위)")]
        public List<Room> rooms = new List<Room>();

        [Header("문·장치")]
        public List<Door> doors = new List<Door>();
        public List<Console> consoles = new List<Console>();
        public List<Notice> notices = new List<Notice>();
        public List<Stairs> stairs = new List<Stairs>();
        public List<Vector2Int> lift = new List<Vector2Int>();
        public bool hasPad; public RectInt pad;
        public List<Trap> traps = new List<Trap>();

        [Header("도착 지점 (계단·엘리베이터로 왔을 때, 게임 시작)")]
        public bool hasStart; public Vector2 start;
        public bool hasStairsArrive; public Vector2 stairsArrive;
        public bool hasLiftArrive; public Vector2 liftArrive;
        public bool hasTank; public Vector2 tank;

        [Header("사람·물건")]
        public List<Npc> npcs = new List<Npc>();
        public List<Obj> objects = new List<Obj>();
        [Tooltip("지원 병력이 나타나는 곳")] public List<Vector2> spawns = new List<Vector2>();
        public List<Reinforce> reinforce = new List<Reinforce>();

        [Header("추가 조명 (방 조명은 방마다 자동)")]
        public List<Lamp> lamps = new List<Lamp>();

        /* ===== 타일 읽고 쓰기 ===== */
        public char Tile(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || y >= tiles.Count) return '#';
            var row = tiles[y]; return x < row.Length ? row[x] : '#';
        }
        public void SetTile(int x, int y, char c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            FixSize();
            var row = tiles[y].ToCharArray(); if (row[x] == c) return;
            row[x] = c; tiles[y] = new string(row);
        }
        // 줄 수·길이를 w×h에 맞춘다 (모자라면 벽으로 채움)
        public void FixSize()
        {
            while (tiles.Count < h) tiles.Add(new string('#', w));
            if (tiles.Count > h) tiles.RemoveRange(h, tiles.Count - h);
            for (int y = 0; y < h; y++)
            {
                var r = tiles[y] ?? "";
                if (r.Length < w) r = r + new string('#', w - r.Length);
                else if (r.Length > w) r = r.Substring(0, w);
                tiles[y] = r;
            }
        }

        /* ===== 게임용 층 정의로 ===== */
        public FloorDef ToDef()
        {
            FixSize();
            var d = new FloorDef
            {
                id = id, name = floorName, sub = sub, w = w, h = h, lethal = lethal, dark = dark,
                tiles = tiles.ToArray(),
                gaps = new Vector2Int[0], glass = new RectI[0], vents = new RectI[0],
            };
            var rs = new List<RoomDef>();
            foreach (var r in rooms)
            {
                string[] allowArr = null;
                if (!string.IsNullOrEmpty(r.allow?.Trim()))
                {
                    var parts = new List<string>();
                    foreach (var p in r.allow.Split(',')) if (p.Trim() != "") parts.Add(p.Trim());
                    allowArr = parts.ToArray();
                }
                rs.Add(new RoomDef(r.name, r.rect.x, r.rect.y, r.rect.width, r.rect.height, r.kind, allowArr, r.label));
            }
            d.rooms = rs.ToArray();
            d.fill = BigRects();   // 실외기·탱크는 덩어리째 그리므로 칠한 칸에서 덩어리를 찾는다
            d.doors = doors.ConvertAll(x => new DoorDef(x.pos.x, x.pos.y, x.req, x.label)).ToArray();
            d.consoles = consoles.ConvertAll(x => new ConsoleDef(x.pos.x, x.pos.y, x.req, string.IsNullOrEmpty(x.trap) ? null : x.trap, x.heli, x.label)).ToArray();
            d.notices = notices.ConvertAll(x => new NoticeDef(x.pos.x, x.pos.y, x.text)).ToArray();
            d.stairs = stairs.ConvertAll(x => new StairsDef(x.rect.x, x.rect.y, x.rect.width, x.rect.height, x.to, string.IsNullOrEmpty(x.dir) ? '>' : x.dir[0])).ToArray();
            d.lift = lift.Count > 0 ? lift.ToArray() : null;
            d.pad = hasPad ? (RectI?)new RectI(pad.x, pad.y, pad.width, pad.height) : null;
            d.traps = traps.ConvertAll(t => new TrapDef(t.id, t.kind, t.label, t.rects.ConvertAll(r => new RectI(r.x, r.y, r.width, r.height)).ToArray())).ToArray();
            if (hasStart) d.arriveStart = start;
            if (hasStairsArrive) d.arriveStairs = stairsArrive;
            if (hasLiftArrive) d.arriveLift = liftArrive;
            if (hasTank) d.tank = tank;
            d.spawns = spawns.ToArray();
            d.reinforce = reinforce.ConvertAll(r => new ReinforceDef(r.ch, r.n)).ToArray();
            d.extraLights = lamps.ConvertAll(l => new LightDef(l.pos.x, l.pos.y, l.r, l.i, l.flick, l.tint)).ToArray();
            d.objects = objects.ConvertAll(o => new ObjDef(o.form, o.pos.x, o.pos.y, o.rot)).ToArray();
            d.npcs = npcs.ConvertAll(n =>
            {
                var route = n.route.ConvertAll(wp => new RouteWp(wp.pos.x, wp.pos.y, wp.wait, wp.look)).ToArray();
                if (route.Length == 0) route = new[] { new RouteWp(n.pos.x, n.pos.y, 99, n.face) };   // 경로가 없으면 그 자리에 서 있기
                return new NpcDef(n.id, n.ch, string.IsNullOrEmpty(n.pal) ? n.ch : n.pal, n.label, n.pos.x, n.pos.y, n.face, route);
            }).ToArray();
            return d;
        }
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
                    foreach (var o in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
                    {
                        int nx = p.x + o.x, ny = p.y + o.y;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen[ny * w + nx] || Tile(nx, ny) != c) continue;
                        seen[ny * w + nx] = true; q.Enqueue(new Vector2Int(nx, ny));
                    }
                }
                list.Add(new FillDef(c, x0, y0, x1 - x0 + 1, y1 - y0 + 1));
            }
            return list.ToArray();
        }

        /* ===== 코드에 적힌 기본 맵에서 가져오기 (처음 한 번) ===== */
        public void FromDef(FloorDef d)
        {
            id = d.id; floorName = d.name; sub = d.sub; lethal = d.lethal; dark = d.dark; w = d.w; h = d.h;
            var g = FloorGeo.BaseGrid(d);
            tiles = new List<string>();
            for (int y = 0; y < h; y++) { var row = new char[w]; for (int x = 0; x < w; x++) row[x] = g[y, x]; tiles.Add(new string(row)); }
            rooms = new List<Room>();
            foreach (var r in d.rooms) rooms.Add(new Room { name = r.name, kind = r.kind, rect = new RectInt(r.x, r.y, r.w, r.h), allow = r.allow != null ? string.Join(",", r.allow) : "", label = r.label });
            doors = new List<Door>(); foreach (var x in d.doors) doors.Add(new Door { pos = new Vector2Int(x.x, x.y), req = x.req, label = x.label });
            consoles = new List<Console>(); foreach (var x in d.consoles) consoles.Add(new Console { pos = new Vector2Int(x.x, x.y), req = x.req, trap = x.trap ?? "", heli = x.heli, label = x.label });
            notices = new List<Notice>(); foreach (var x in d.notices) notices.Add(new Notice { pos = new Vector2Int(x.x, x.y), text = x.text });
            stairs = new List<Stairs>(); foreach (var x in d.stairs) stairs.Add(new Stairs { rect = new RectInt(x.x, x.y, x.w, x.h), to = x.to, dir = x.dir.ToString() });
            lift = d.lift != null ? new List<Vector2Int>(d.lift) : new List<Vector2Int>();
            hasPad = d.pad.HasValue; if (hasPad) pad = new RectInt(d.pad.Value.x, d.pad.Value.y, d.pad.Value.w, d.pad.Value.h);
            traps = new List<Trap>();
            foreach (var t in d.traps) { var tr = new Trap { id = t.id, kind = t.kind, label = t.label }; foreach (var r in t.rects) tr.rects.Add(new RectInt(r.x, r.y, r.w, r.h)); traps.Add(tr); }
            hasStart = d.arriveStart.HasValue; if (hasStart) start = d.arriveStart.Value;
            hasStairsArrive = d.arriveStairs.HasValue; if (hasStairsArrive) stairsArrive = d.arriveStairs.Value;
            hasLiftArrive = d.arriveLift.HasValue; if (hasLiftArrive) liftArrive = d.arriveLift.Value;
            hasTank = d.tank.HasValue; if (hasTank) tank = d.tank.Value;
            spawns = d.spawns != null ? new List<Vector2>(d.spawns) : new List<Vector2>();
            reinforce = new List<Reinforce>(); if (d.reinforce != null) foreach (var r in d.reinforce) reinforce.Add(new Reinforce { ch = r.ch, n = r.n });
            lamps = new List<Lamp>(); if (d.extraLights != null) foreach (var l in d.extraLights) lamps.Add(new Lamp { pos = new Vector2(l.x, l.y), r = l.r, i = l.i, flick = l.flick, tint = l.tint });
            objects = new List<Obj>(); foreach (var o in d.objects) objects.Add(new Obj { form = o.form, pos = new Vector2(o.x, o.y), rot = o.rot });
            npcs = new List<Npc>();
            foreach (var n in d.npcs)
            {
                var nn = new Npc { id = n.id, ch = n.ch, pal = n.pal, label = n.label, pos = new Vector2(n.x, n.y), face = n.face };
                foreach (var wp in n.route) nn.route.Add(new Wp { pos = new Vector2(wp.x, wp.y), wait = wp.wait, look = wp.look });
                npcs.Add(nn);
            }
        }
    }
}
