// 2.5D 화면이 쓰는 공용 도우미: 타일 변형 고르기, 소품 조명, 방 꾸밈, 사람 그림(서기·걷기) 고르기.
// (예전 2D 화면 WorldView에서 그림과 상관없는 부분만 남긴 것)
using System.Collections.Generic;
using UnityEngine;

namespace Specimen07
{
    public static class ViewKit
    {
        /* ===== 소품 조명 (천장등, 수조, 모니터, 엘리베이터) ===== */
        public struct Glow { public float x, y, r; public Color c; }
        // 벽면 그림: 소장실은 액자·문장, 나머지는 가끔 포스터·메모
        internal static string WallFace(string kind, int x, int y, int salt)
        {
            int r = Hash(x, y, salt + 5);
            if (kind == "dir") return r < 18 ? "wall_face_art" : r < 32 ? "wall_face_art2" : r < 42 ? "wall_face_emblem" : "wall_face";
            return r < 6 ? "wall_face_poster" : r < 12 ? "wall_face_note" : "wall_face";
        }
        // 방 종류별 바닥 장식: (그림, 칸 사각형). 가구 밑에 깔린다
        public struct Decal { public string key; public float x, y, w, h; }
        internal static List<Decal> RoomDecals(FloorGeo g)
        {
            var list = new List<Decal>();
            foreach (var z in g.zones)
            {
                int zw = z.x1 - z.x0 + 1, zh = z.y1 - z.y0 + 1;
                float cx = z.x0 + zw / 2f, cy = z.y0 + zh / 2f;
                if (zw < 7 || zh < 6) continue;
                switch (z.kind)
                {
                    case "dir":   // 큰 책상 앞에 넓게
                        list.Add(new Decal { key = "decal_rug_dir", x = z.x0 + 2.5f, y = z.y0 + 5.6f, w = zw - 5f, h = Mathf.Min(zh - 7.5f, (zw - 5f) * 0.62f) }); break;
                    case "rest":
                        { float d = Mathf.Min(4.5f, Mathf.Min(zw, zh) - 4); list.Add(new Decal { key = "decal_rug_rest", x = cx - d / 2, y = cy - d / 2, w = d, h = d }); }
                        break;
                    case "office":
                        list.Add(new Decal { key = "decal_runner", x = z.x0 + 1.5f, y = z.y1 - 1.3f, w = zw - 3f, h = 1f }); break;
                    case "lab": case "cold":
                        list.Add(new Decal { key = "decal_hazard", x = cx - 1.1f, y = z.y1 - 2.4f, w = 2.2f, h = 2.2f }); break;
                }
            }
            return list;
        }

        public static bool IsFixture(FloorGeo g, int x, int y, out bool red)
        {
            red = false;
            char ch = g.Tile(x, y);
            if (FACE_OK.IndexOf(ch) < 0 || g.Tile(x, y - 1) != '#' || x % 4 != 2) return false;
            var z = g.ZoneAt(x + 0.5f, y + 0.5f); string kind = z != null ? z.kind : "hall";
            if (kind == "roof") return false;
            red = kind == "sec" || kind == "dir";
            return true;
        }
        public static void CollectProps(FloorGeo g, List<LightSrc> lights, List<Glow> glows)
        {
            Color cyan = new Color(0.35f, 0.86f, 1f), red = new Color(1f, 0.35f, 0.43f), mint = new Color(0.43f, 0.94f, 0.78f), green = new Color(0.35f, 0.94f, 0.67f);
            for (int y = 0; y < g.h; y++) for (int x = 0; x < g.w; x++)
            {
                char ch = g.grid[y, x]; bool isRed;
                if (IsFixture(g, x, y, out isRed))
                {
                    lights.Add(new LightSrc { x = x + 0.5f, y = y + 0.4f, r = 3.6f, i = 0.8f });
                    glows.Add(new Glow { x = x + 0.5f, y = y + 0.25f, r = 2.2f, c = WithA(isRed ? red : cyan, 0.30f) });
                }
                if (ch == 'C' || ch == 'V' || ch == 'N')
                {
                    lights.Add(new LightSrc { x = x + 0.5f, y = y + 0.5f, r = 1.8f, i = 0.6f });
                    glows.Add(new Glow { x = x + 0.5f, y = y + 0.4f, r = 1.3f, c = WithA(cyan, 0.28f) });
                }
                if (ch == 'M' && (x + y) % 2 == 0) glows.Add(new Glow { x = x + 0.5f, y = y + 0.5f, r = 1.0f, c = WithA(green, 0.18f) });
                if (ch == 'E')
                {
                    lights.Add(new LightSrc { x = x + 0.5f, y = y + 0.5f, r = 2.2f, i = 0.55f });
                    glows.Add(new Glow { x = x + 0.5f, y = y + 0.2f, r = 1.4f, c = WithA(mint, 0.22f) });
                }
            }
            foreach (var f in g.def.fill)
                if (f.ch == 'W')
                {
                    float cx = f.x + f.w / 2f, cy = f.y + f.h / 2f;
                    lights.Add(new LightSrc { x = cx, y = cy, r = 3.8f, i = 0.85f });
                    glows.Add(new Glow { x = cx, y = cy, r = 3.4f, c = WithA(cyan, 0.38f) });
                }
        }
        static Color WithA(Color c, float a) { c.a = a; return c; }

        /* ===== 타일 변형 고르기 ===== */
        // 가구·바닥은 같은 이름 뒤에 _2, _3 … 을 붙인 그림이 있으면 칸마다 섞어서 쓴다 (없으면 기본 그림만).
        internal const string FACE_OK = ".STVMCN";
        internal static readonly string[] FLOOR_V = { "", "_2", "_3", "_4", "_5" }; internal static readonly int[] FLOOR_W = { 82, 8, 5, 3, 2 };
        internal static readonly string[] SHELF_V = { "", "_2", "_3" }; internal static readonly int[] SHELF_W = { 50, 25, 25 };
        internal static readonly string[] TABLE_V = { "", "_2", "_3", "_4" }; internal static readonly int[] TABLE_W = { 55, 20, 15, 10 };
        internal static readonly string[] INNER_V = { "", "_2", "_3" }; internal static readonly int[] INNER_W = { 60, 20, 20 };
        internal static readonly string[] MACH_V = { "", "_2" }; internal static readonly int[] MACH_W = { 50, 50 };

        // 칸 좌표로 정해지는 0~99 (매번 같은 결과 → 맵이 실행할 때마다 똑같이 보임)
        public static int Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(s * 83492791);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (int)((h ^ (h >> 16)) % 100);
            }
        }
        internal static string Variant(string baseKey, int x, int y, int salt, string[] suffix, int[] weight)
        {
            int r = Hash(x, y, salt), acc = 0;
            for (int i = 0; i < suffix.Length; i++)
            {
                acc += weight[i];
                if (r < acc) return suffix[i] == "" ? baseKey : Pick(baseKey + suffix[i], baseKey);
            }
            return baseKey;
        }
        internal static string Pick(string key, string fallback) { return Art.Has(key) ? key : fallback; }
        internal static float LightLevel(LightSrc l, float t)
        {
            if (l.flick == 0) return l.i;
            float n = Mathf.Sin(t * 13 + l.x) * Mathf.Sin(t * 7.3f + l.y * 2);
            float v = l.i * (0.94f + 0.06f * Mathf.Sin(t * 50 + l.x));
            if (l.flick == 2 && n > 0.7f) v *= 0.3f;
            if (l.flick == 1 && n > 0.9f) v *= 0.55f;
            return v;
        }
        // 카메라 줌 (좁은 통로 = 줌 인, 넓은 곳 = 줌 아웃). 1 = 기본
        public static float Zoom = 1f;

        // 서 있는 사람 그림 (stand_<종류>_front/back/side). 있으면 true, sc = 크기 배율
        public const float CHAR_PX = 26f;     // 사람 그림 몇 픽셀이 1칸인지
        //   걸을 때 walk_<종류>_<방향>_N 그림을 씀 (walkT = 걸음 진행 시간, -1 = 서 있음)
        //   - 6장 이상(팔다리 움직이는 그림): 순서대로 재생 + 약한 출렁임
        //   - 4장 이하: 발 번갈아 드는 그림 + 코드로 통통 눌렸다 늘어나는 출렁임·좌우 기우뚱 (LastPose)
        public struct Pose { public float sx, sy, lean, dx; }
        public static readonly Pose Still = new Pose { sx = 1, sy = 1 };
        public static Pose LastPose = Still;
        public const float STRIDE_HZ = 1.1f;  // 1초에 몇 걸음 쌍(왼발+오른발)
        internal static bool ApplyStand(SpriteRenderer sr, string kind, float face, out float sc, float walkT = -1)
        {
            sc = 1; LastPose = Still;
            if (!Art.Has("stand_" + kind + "_front")) { sr.flipX = false; return false; }
            int dir = Art.DirIndex(face);
            string view = dir == 1 ? "front" : dir == 3 ? "back" : "side";
            bool flip = dir == 2;
            // 8방향: 대각선 그림(stand_<종류>_frontside / backside, 오른쪽을 보는 그림)이 있으면 대각선일 때 쓰고 왼쪽은 뒤집는다
            float deg = Mathf.Repeat(face * Mathf.Rad2Deg + 180f, 360f) - 180f, ad = Mathf.Abs(deg);
            if (ad > 22.5f && ad < 157.5f && Mathf.Abs(ad - 90f) > 22.5f)
            {
                string dv = deg > 0 ? "frontside" : "backside";
                if (Art.Has("stand_" + kind + "_" + dv)) { view = dv; flip = ad > 90f; dir = flip ? 2 : 0; }
            }
            var walk = walkT >= 0 ? Art.Frames("walk_" + kind + "_" + view) : null;
            Sprite spr = Art.Get("stand_" + kind + "_" + view);
            if (walk != null && walk.Count > 0)
            {
                float cyc = walkT * STRIDE_HZ;
                if (walk.Count >= 6)
                {
                    // 팔다리가 움직이는 프레임 그림 + 약한 출렁임
                    spr = walk[Mathf.FloorToInt(Mathf.Repeat(cyc, 1f) * walk.Count) % walk.Count];
                    float sn = Mathf.Sin(cyc * Mathf.PI * 2), b = Mathf.Abs(sn);
                    LastPose = new Pose { sx = 1.02f - 0.03f * b, sy = 0.97f + 0.05f * b, lean = sn * 2f * (dir == 2 ? -1 : 1), dx = 0 };
                }
                else
                {
                    float ph = cyc * Mathf.PI * 2, sn = Mathf.Sin(ph), b = Mathf.Abs(sn);
                    int idx = sn > 0.35f ? 1 : sn < -0.35f ? 3 : 0;
                    spr = walk[Mathf.Min(idx, walk.Count - 1)];
                    // 디딜 때 꾹 눌리고, 발을 들 때 쭉 늘어나며, 걸음마다 좌우로 기우뚱
                    LastPose = new Pose { sx = 1.04f - 0.06f * b, sy = 0.93f + 0.1f * b, lean = sn * 4f * (dir == 2 ? -1 : 1), dx = sn * 0.03f };
                }
            }
            sr.sprite = spr;
            sr.flipX = flip; sr.transform.rotation = Quaternion.identity;
            sc = sr.sprite.pixelsPerUnit / CHAR_PX * HeightMul(kind, view);
            return true;
        }

        // 방향마다 그림 키가 달라서(정면·뒤 36, 옆·대각선 40 …) 돌아설 때 커졌다 작아졌다 하는 것을 막는다.
        // 표(SpriteHeights, Tools/gen_sprite_heights.py)로 그 종류의 옆·대각선 평균 키에 맞춰 크기를 곱한다.
        static readonly Dictionary<string, float> targetH = new Dictionary<string, float>();
        internal static float HeightMul(string kind, string view)
        {
            int h;
            if (!SpriteHeights.H.TryGetValue("stand_" + kind + "_" + view, out h) || h <= 0) return 1;
            float tgt;
            if (!targetH.TryGetValue(kind, out tgt))
            {
                float sum = 0; int n = 0; int v;
                foreach (var w in new[] { "side", "frontside", "backside" })
                    if (SpriteHeights.H.TryGetValue("stand_" + kind + "_" + w, out v)) { sum += v; n++; }
                tgt = n > 0 ? sum / n : h;
                targetH[kind] = tgt;
            }
            return tgt / h * Tweak(kind, view);
        }

        // 키는 같아도 눈으로 보기에 작아 보이는 그림 손보기 (종류 앞부분이 같으면 변형까지 적용)
        static readonly Dictionary<string, float> tweaks = new Dictionary<string, float>
        {
            { "researcher|backside", 1.1f },   // 연구원 대각선 위: 몸이 홀쭉하게 나와 작아 보임
            { "researcher|side", 1.08f },      // 연구원 옆모습도 작아 보임
        };
        static float Tweak(string kind, string view)
        {
            foreach (var kv in tweaks)
            {
                int bar = kv.Key.IndexOf('|');
                if (view == kv.Key.Substring(bar + 1) && kind.StartsWith(kv.Key.Substring(0, bar))) return kv.Value;
            }
            return 1;
        }

        internal static bool HasWalk(string kind) { return Art.Has("walk_" + kind + "_front_0"); }
        // 같은 종류라도 사람마다 다른 모습: stand_<종류>_v2, _v3 … 그림이 있으면 사람 id로 골라 씀
        internal static string Variant(string pal, string id)
        {
            int n = 1; while (Art.Has("stand_" + pal + "_v" + (n + 1) + "_front")) n++;
            if (n == 1) return pal;
            int h = 0; foreach (char c in id) h = h * 31 + c;
            int k = (h & 0x7fffffff) % n;
            return k == 0 ? pal : pal + "_v" + (k + 1);
        }

        internal static List<Sprite> PlayerFrames(string anim, string dir)
        {
            var l = Art.Frames("player_" + anim + "_" + dir);
            if (l.Count == 0) l = Art.Frames("player_" + anim + "_side");
            if (l.Count == 0) l = Art.Frames("player_" + anim);
            return l;
        }
        internal const float TILE_PX = 16f; // 타일 그림 1칸의 픽셀 수 (캐릭터 크기 기준)
    }
}
