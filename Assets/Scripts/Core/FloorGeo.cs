// 층 정의(FloorDef)를 칸 단위 격자로 만든다. 프로토타입 buildGeo()와 같은 규칙.
// 타일 문자: '#' 벽, '.' 바닥, S 선반, V 자판기, T 테이블, M 설비, A 실외기, W 탱크, G 유리,
//            v 환풍구, D 문, C 콘솔, N 게시물, E 엘리베이터, '>' '<' 계단, H 헬리패드
using System.Collections.Generic;
using UnityEngine;

namespace Specimen07
{
    public class Zone
    {
        public string name, kind; public int x0, y0, x1, y1; public string[] allow; public bool label;
        public bool Allows(string form) { return allow == null || System.Array.IndexOf(allow, form) >= 0; }
    }

    public class LightSrc { public float x, y, r, i; public int flick; public Color tint; }

    public class FloorGeo
    {
        public FloorDef def;
        public string id { get { return def.id; } }
        public string name { get { return def.name; } }
        public int w, h;
        public char[,] grid;          // [y, x]
        public int[] zoneIdx, trapAt; // y*w+x
        public List<Zone> zones = new List<Zone>();
        public Dictionary<int, int> doorAt = new Dictionary<int, int>(), consoleAt = new Dictionary<int, int>();
        public List<LightSrc> lights = new List<LightSrc>();

        public static readonly HashSet<char> SOLID = new HashSet<char> { '#', 'S', 'V', 'T', 'N', 'C', 'E', 'G', 'M', 'A', 'W', 'D', 'v' };
        public static readonly HashSet<char> OPAQUE = new HashSet<char> { '#', 'S', 'V', 'N', 'E', 'M', 'A', 'W', 'D', 'v' };
        static readonly Dictionary<string, float> LIGHT_I = new Dictionary<string, float> { { "roof", 0.5f }, { "store", 0.72f }, { "hall", 0.82f }, { "iso", 0.92f }, { "wing", 0.8f }, { "dir", 0.85f } };
        static readonly Dictionary<string, Color> LIGHT_TINT = new Dictionary<string, Color>
        {
            { "lift", new Color(127 / 255f, 224 / 255f, 176 / 255f, 0.08f) }, { "wing", new Color(180 / 255f, 140 / 255f, 240 / 255f, 0.07f) },
            { "dir", new Color(1f, 110 / 255f, 90 / 255f, 0.06f) }, { "cold", new Color(140 / 255f, 200 / 255f, 1f, 0.07f) },
        };

        // 함정 id → (층, 이름, 종류) : 다른 층의 콘솔에서 함정 이름을 보여줄 때 쓴다
        public static readonly Dictionary<string, TrapDef> TrapInfo = new Dictionary<string, TrapDef>();
        public static readonly Dictionary<string, string> TrapFloor = new Dictionary<string, string>();

        public FloorGeo(FloorDef d)
        {
            def = d; w = d.w; h = d.h;
            grid = new char[h, w];
            zoneIdx = new int[w * h]; trapAt = new int[w * h];
            for (int i = 0; i < w * h; i++) { zoneIdx[i] = -1; trapAt[i] = -1; }
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) grid[y, x] = '#';

            for (int i = 0; i < d.rooms.Length; i++)
            {
                var r = d.rooms[i];
                var z = new Zone { name = r.name, kind = r.kind, x0 = r.x, y0 = r.y, x1 = r.x + r.w - 1, y1 = r.y + r.h - 1, allow = r.allow, label = r.label };
                zones.Add(z);
                for (int yy = z.y0; yy <= z.y1; yy++) for (int xx = z.x0; xx <= z.x1; xx++) { grid[yy, xx] = '.'; zoneIdx[yy * w + xx] = i; }
            }
            foreach (var g in d.gaps) Put('.', g.x, g.y, 1, 1);
            foreach (var f in d.fill) Put(f.ch, f.x, f.y, f.w, f.h);
            foreach (var g in d.glass) Put('G', g.x, g.y, g.w, g.h);
            foreach (var v in d.vents) Put('v', v.x, v.y, v.w, v.h);
            for (int i = 0; i < d.doors.Length; i++) { Put('D', d.doors[i].x, d.doors[i].y, 1, 1); doorAt[d.doors[i].y * w + d.doors[i].x] = i; }
            for (int i = 0; i < d.consoles.Length; i++) { Put('C', d.consoles[i].x, d.consoles[i].y, 1, 1); consoleAt[d.consoles[i].y * w + d.consoles[i].x] = i; }
            foreach (var n in d.notices) Put('N', n.x, n.y, 1, 1);
            for (int i = 0; i < d.traps.Length; i++)
            {
                var tr = d.traps[i];
                TrapInfo[tr.id] = tr; TrapFloor[tr.id] = d.id;
                foreach (var r in tr.rects)
                    for (int yy = r.y; yy < r.y + r.h; yy++) for (int xx = r.x; xx < r.x + r.w; xx++) trapAt[yy * w + xx] = i;
            }
            if (d.lift != null) foreach (var t in d.lift) Put('E', t.x, t.y, 1, 1);
            foreach (var s in d.stairs) Put(s.dir, s.x, s.y, s.w, s.h);
            if (d.pad.HasValue) { var p = d.pad.Value; Put('H', p.x, p.y, p.w, p.h); }

            // 조명: 방마다 일정 간격으로 자동 배치 (프로토타입과 같은 시드)
            long seed = d.w * 131 + d.h * 7;
            System.Func<float> rnd = () => { seed = (seed * 16807) % 2147483647; return seed / 2147483647f; };
            foreach (var z in zones)
            {
                int zw = z.x1 - z.x0 + 1, zh = z.y1 - z.y0 + 1; float sp = z.kind == "roof" ? 12 : 7;
                int nx = Mathf.Max(1, Mathf.RoundToInt(zw / sp)), ny = Mathf.Max(1, Mathf.RoundToInt(zh / sp));
                for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++)
                {
                    float r = Mathf.Max(3.6f, Mathf.Min(z.kind == "roof" ? 7.5f : 6f, Mathf.Max(zw / (float)nx, zh / (float)ny) * 0.75f));
                    float f = rnd();
                    float li; if (!LIGHT_I.TryGetValue(z.kind, out li)) li = 0.88f;
                    Color tint; if (!LIGHT_TINT.TryGetValue(z.kind, out tint)) tint = Color.clear;
                    lights.Add(new LightSrc { x = z.x0 + zw * (i + 0.5f) / nx, y = z.y0 + zh * (j + 0.5f) / ny, r = r, i = li, flick = f < 0.12f ? 2 : f < 0.32f ? 1 : 0, tint = tint });
                }
            }
            foreach (var l in d.extraLights) lights.Add(new LightSrc { x = l.x, y = l.y, r = l.r, i = l.i, flick = l.flick, tint = l.tint });
        }

        void Put(char ch, int x, int y, int fw, int fh)
        {
            for (int yy = y; yy < y + fh; yy++) for (int xx = x; xx < x + fw; xx++)
                if (xx >= 0 && yy >= 0 && xx < w && yy < h) grid[yy, xx] = ch;
        }

        public char Tile(int tx, int ty) { if (tx < 0 || ty < 0 || tx >= w || ty >= h) return '#'; return grid[ty, tx]; }

        public Zone ZoneAt(float x, float y)
        {
            int tx = Mathf.FloorToInt(x), ty = Mathf.FloorToInt(y);
            if (tx < 0 || ty < 0 || tx >= w || ty >= h) return null;
            int i = zoneIdx[ty * w + tx];
            return i >= 0 ? zones[i] : null;
        }

        /* ===== 전체 층 ===== */
        static Dictionary<string, FloorGeo> all;
        public static Dictionary<string, FloorGeo> All
        {
            get
            {
                if (all == null)
                {
                    all = new Dictionary<string, FloorGeo>();
                    foreach (var d in FloorDefs.All) all[d.id] = new FloorGeo(d);
                }
                return all;
            }
        }
    }
}
