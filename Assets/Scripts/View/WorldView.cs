// 게임 화면(월드) 그리기. 게임 규칙은 건드리지 않고 Game.S(상태)를 읽어서 보여주기만 한다.
// 모든 그림은 Art.Get("이름")에서 오므로, 그림을 바꾸려면 Resources/Art에 PNG를 넣으면 된다.
using System.Collections.Generic;
using UnityEngine;

namespace Specimen07
{
    public class WorldView : MonoBehaviour, IGameView
    {
        // 그리는 순서 (숫자가 클수록 위)
        public const int L_FLOOR = 0, L_DECAL = 1, L_WALL = 2, L_TRIM = 3, L_BIG = 4, L_DOOR = 5, L_TRAP = 8, L_TRAIL = 9, L_OBJ = 10, L_NPC = 11, L_PLAYER = 12,
                         L_HELI = 13, L_DARK = 20, L_TINT = 21, L_GLOW = 22, L_CONE = 23, L_AIM = 24, L_FOG = 25, L_RING = 26, L_FX = 27, L_LABEL = 3;

        Game game; Camera cam;
        Transform floorRoot, dynRoot;
        Material lineMat;

        FloorGeo G;
        // 문
        class DoorView { public SpriteRenderer baseR, stripes, led; }
        readonly List<DoorView> doors = new List<DoorView>();
        // 함정
        class TrapView { public TrapDef def; public List<SpriteRenderer> parts = new List<SpriteRenderer>(); public List<int> rows = new List<int>(); public List<Vector2Int> cells = new List<Vector2Int>(); }
        readonly List<TrapView> traps = new List<TrapView>();
        readonly List<SpriteRenderer> consoleLeds = new List<SpriteRenderer>(), liftLeds = new List<SpriteRenderer>();
        // 풀
        readonly List<SpriteRenderer> objPool = new List<SpriteRenderer>(), npcPool = new List<SpriteRenderer>(), trailPool = new List<SpriteRenderer>(), fxPool = new List<SpriteRenderer>();
        readonly List<MeshFilter> conePool = new List<MeshFilter>();
        readonly List<LineRenderer> aimPool = new List<LineRenderer>();
        readonly List<SpriteRenderer> aimRingPool = new List<SpriteRenderer>();
        SpriteRenderer player, playerGlow, targetRing, heli, heliRotor, alarmTint;
        LineRenderer shotLine;
        // 어둠
        const int DR = 4; // 1칸당 어둠 픽셀
        Texture2D darkTex; SpriteRenderer darkR;
        float[] darkBase, darkCur; Color32[] darkOut; int dW, dH;
        readonly List<LightSrc> flickLights = new List<LightSrc>();
        RectInt prevPlayerBox; float flickTimer;

        void Awake()
        {
            game = GetComponent<Game>();
            cam = Camera.main;
            if (cam == null) { var go = new GameObject("Main Camera"); go.tag = "MainCamera"; cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); }
            cam.orthographic = true;
            cam.orthographicSize = Cfg.VTH / 2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Data.Hex("#07090b");
            cam.nearClipPlane = 0.01f; cam.farClipPlane = 100f;
            lineMat = new Material(Shader.Find("Sprites/Default"));
        }

        /* ===== 층 만들기 ===== */
        public void BuildFloor(FloorGeo g)
        {
            G = g;
            if (floorRoot != null) Destroy(floorRoot.gameObject);
            if (dynRoot != null) Destroy(dynRoot.gameObject);
            floorRoot = new GameObject("Floor_" + g.id).transform;
            dynRoot = new GameObject("Dynamic").transform;
            objPool.Clear(); npcPool.Clear(); trailPool.Clear(); fxPool.Clear(); conePool.Clear(); aimPool.Clear(); aimRingPool.Clear();
            doors.Clear(); traps.Clear(); consoleLeds.Clear(); liftLeds.Clear();

            BuildStatic(g, floorRoot);

            // 문
            foreach (var d in g.def.doors)
            {
                var dv = new DoorView();
                dv.baseR = Spr(floorRoot, "door_closed", L_DOOR); dv.baseR.transform.position = U.W(d.x + 0.5f, d.y + 0.5f); FitTo(dv.baseR, 1, 1);
                dv.stripes = Spr(floorRoot, "door_stripes", L_DOOR + 1); dv.stripes.transform.position = dv.baseR.transform.position; dv.stripes.color = Data.Cards[d.req].color; FitTo(dv.stripes, 1, 1);
                dv.led = Spr(floorRoot, "fx_dot", L_DOOR + 2); dv.led.transform.position = dv.baseR.transform.position; dv.led.transform.localScale = Vector3.one * (5.6f / 32f);
                doors.Add(dv);
            }
            // 함정
            foreach (var tr in g.def.traps)
            {
                var tv = new TrapView { def = tr };
                foreach (var r in tr.rects)
                {
                    if (tr.kind == "laser")
                        for (int yy = r.y; yy < r.y + r.h; yy++)
                            for (int k = -1; k <= 1; k++)
                            {
                                var sr = Spr(floorRoot, "fx_white", L_TRAP);
                                sr.transform.position = U.W(r.x + r.w / 2f, yy + 0.5f + k * 0.25f);
                                sr.transform.localScale = new Vector3(r.w, 2.5f / 32f, 1);
                                tv.parts.Add(sr); tv.rows.Add(yy);
                            }
                    else
                        for (int yy = r.y; yy < r.y + r.h; yy++) for (int xx = r.x; xx < r.x + r.w; xx++)
                        {
                            var sr = Spr(floorRoot, "fx_glow", L_TRAP);
                            sr.transform.localScale = Vector3.one * 1.5f;
                            tv.parts.Add(sr); tv.cells.Add(new Vector2Int(xx, yy));
                        }
                }
                traps.Add(tv);
            }
            foreach (var c in g.def.consoles)
            {
                var led = Spr(floorRoot, "fx_white", L_DOOR); led.transform.position = U.W(c.x + 0.5f, c.y + 0.4f); led.transform.localScale = new Vector3(14f / 32f, 7f / 32f, 1);
                consoleLeds.Add(led);
            }
            if (g.def.lift != null) foreach (var t in g.def.lift)
            {
                var led = Spr(floorRoot, "fx_white", L_DOOR); led.transform.position = U.W(t.x + 0.5f, t.y + 0.5f); led.transform.localScale = Vector3.one * (6f / 32f);
                liftLeds.Add(led);
            }
            // 빛 색조
            foreach (var l in g.lights)
            {
                if (l.tint.a <= 0) continue;
                var sr = Spr(floorRoot, "fx_glow", L_TINT); sr.transform.position = U.W(l.x, l.y); sr.transform.localScale = Vector3.one * l.r * 2; sr.color = l.tint;
            }

            player = Spr(dynRoot, "player_blob", L_PLAYER);
            playerGlow = Spr(dynRoot, "fx_glow", L_GLOW); playerGlow.transform.localScale = Vector3.one * 2.8f;
            targetRing = Spr(dynRoot, "fx_ring", L_RING); targetRing.transform.localScale = Vector3.one * (34f / 32f);
            heli = Spr(dynRoot, "heli", L_HELI); heliRotor = Spr(dynRoot, "heli_rotor", L_HELI + 1);
            alarmTint = Spr(dynRoot, "fx_white", L_DARK + 1); alarmTint.transform.localScale = new Vector3(Cfg.VTW * 2, Cfg.VTH * 2, 1);
            fog = NewCone(); fog.GetComponent<MeshRenderer>().sortingOrder = L_FOG; fog.name = "Fog";
            shotLine = NewLine(); shotLine.startColor = shotLine.endColor = new Color(1, 230 / 255f, 160 / 255f, 0.9f); shotLine.widthMultiplier = 3f / 32f;

            BuildDarkness();
        }

        // 움직이지 않는 부분 (바닥, 벽, 가구, 큰 설비, 방 이름). 에디터 미리보기에서도 같이 쓴다.
        public static void BuildStatic(FloorGeo g, Transform root)
        {
            var tileRoot = new GameObject("Tiles").transform; tileRoot.SetParent(root, false);
            int salt = 0; foreach (char c in g.id) salt += c;
            System.Func<int, int, char> at = (x, y) => g.Tile(x, y);                    // 맵 밖은 '#'
            System.Func<int, int, bool> wall = (x, y) => { char c = at(x, y); return c == '#' || c == 'v'; };
            System.Func<int, int, bool> inside = (x, y) => x >= 0 && y >= 0 && x < g.w && y < g.h;

            for (int y = 0; y < g.h; y++) for (int x = 0; x < g.w; x++)
            {
                char ch = g.grid[y, x];
                var z = g.ZoneAt(x + 0.5f, y + 0.5f);
                string kind = z != null ? z.kind : "hall";
                bool wallish = ch == '#' || ch == 'v';

                // 1) 바닥 (가끔 금 간 타일·얼룩·종이)
                if (!wallish) Tile(tileRoot, Variant("tile_floor_" + kind, x, y, salt, FLOOR_V, FLOOR_W), x, y, L_FLOOR);

                // 2) 벽면: 바로 위가 벽인 칸의 윗부분에 벽 패널 (옥상 제외). 4칸마다 천장 조명.
                if (FACE_OK.IndexOf(ch) >= 0 && at(x, y - 1) == '#' && kind != "roof")
                {
                    string fk; bool redFix;
                    if (IsFixture(g, x, y, out redFix)) fk = redFix ? "wall_face_alarm" : "wall_face_light";
                    else { int r = Hash(x, y, salt + 5); fk = r < 6 ? "wall_face_poster" : r < 12 ? "wall_face_note" : "wall_face"; }
                    Tile(tileRoot, Pick(fk, "wall_face"), x, y, L_DECAL);
                }

                // 3) 벽·가구
                string key = null;
                switch (ch)
                {
                    case '#': key = "tile_wall"; break;
                    case 'v': key = "tile_vent"; break;
                    case 'G': key = "tile_glass"; break;
                    case 'S': key = Variant("tile_shelf", x, y, salt + 1, SHELF_V, SHELF_W); break;
                    case 'V': key = "tile_vending"; break;
                    case 'T':
                        key = at(x, y + 1) == 'T' && Art.Has("tile_table_inner")
                            ? Variant("tile_table_inner", x, y, salt + 2, INNER_V, INNER_W)   // 아래로 책상이 이어지면 앞면 없는 윗면
                            : Variant("tile_table", x, y, salt + 2, TABLE_V, TABLE_W);
                        break;
                    case 'N': key = "tile_notice"; break;
                    case 'M': key = Variant("tile_machine", x, y, salt + 3, MACH_V, MACH_W); break;
                    case 'C': key = "tile_console"; break;
                    case 'E': key = "tile_lift"; break;
                    case '>': case '<': key = "tile_stairs"; break;
                }
                if (key != null) Tile(tileRoot, key, x, y, L_WALL);

                // 4) 벽 테두리: 바닥과 맞닿은 면에 라벤더 선, 안쪽 모서리 조각
                if (wallish)
                {
                    if (inside(x, y - 1) && !wall(x, y - 1)) Trim(tileRoot, "wall_trim_n", x, y);
                    if (inside(x, y + 1) && !wall(x, y + 1)) Trim(tileRoot, "wall_trim_s", x, y);
                    if (inside(x + 1, y) && !wall(x + 1, y)) Trim(tileRoot, "wall_trim_e", x, y);
                    if (inside(x - 1, y) && !wall(x - 1, y)) Trim(tileRoot, "wall_trim_w", x, y);
                    if (inside(x + 1, y + 1) && wall(x + 1, y) && wall(x, y + 1) && !wall(x + 1, y + 1)) Trim(tileRoot, "wall_corner_se", x, y);
                    if (inside(x - 1, y + 1) && wall(x - 1, y) && wall(x, y + 1) && !wall(x - 1, y + 1)) Trim(tileRoot, "wall_corner_sw", x, y);
                    if (inside(x + 1, y - 1) && wall(x + 1, y) && wall(x, y - 1) && !wall(x + 1, y - 1)) Trim(tileRoot, "wall_corner_ne", x, y);
                    if (inside(x - 1, y - 1) && wall(x - 1, y) && wall(x, y - 1) && !wall(x - 1, y - 1)) Trim(tileRoot, "wall_corner_nw", x, y);
                }
            }
            // 큰 설비 (실외기, 탱크): 영역 크기에 맞춰 늘린다
            foreach (var f in g.def.fill)
            {
                if (f.ch == 'A') Stretch(root, "big_aircon", f.x, f.y, f.w, f.h, L_BIG);
                else if (f.ch == 'W') Stretch(root, "big_tank", f.x, f.y, f.w, f.h, L_BIG);
            }
            if (g.def.pad.HasValue) { var p = g.def.pad.Value; Stretch(root, "helipad", p.x, p.y, p.w, p.h, L_DECAL); }
            if (g.def.tank.HasValue) { var sr = Spr(root, "tank_broken", L_DECAL); sr.transform.position = U.W(g.def.tank.Value.x, g.def.tank.Value.y); FitTo(sr, 4, 4); }
            // 바닥 글씨 (방 이름, 계단)
            foreach (var z in g.zones)
            {
                if (!z.label || z.x1 - z.x0 < 4) continue;
                Label(root, z.name, (z.x0 + z.x1 + 1) / 2f, z.y1 + 0.35f, 0.6f, new Color(220 / 255f, 235 / 255f, 230 / 255f, 0.1f));
            }
            foreach (var s in g.def.stairs) Label(root, (s.dir == '>' ? "▲ " : "▼ ") + s.to, s.x + s.w / 2f, s.y + s.h / 2f, 0.4f, Data.Hex("#7fe0b0"));

        }

        /* ===== 소품 조명 (천장등, 수조, 모니터, 엘리베이터) ===== */
        public struct Glow { public float x, y, r; public Color c; }
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
        static void Trim(Transform parent, string key, int x, int y) { if (Art.Has(key)) Tile(parent, key, x, y, L_TRIM); }

        public static SpriteRenderer Spr(Transform parent, string key, int order)
        {
            var go = new GameObject(key);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Get(key); sr.sortingOrder = order;
            return sr;
        }
        static void Tile(Transform parent, string key, int x, int y, int order)
        {
            var sr = Spr(parent, key, order);
            sr.transform.position = U.W(x + 0.5f, y + 0.5f);
            FitTo(sr, 1, 1);
        }
        static void Stretch(Transform parent, string key, int x, int y, int w, int h, int order)
        {
            var sr = Spr(parent, key, order);
            sr.transform.position = U.W(x + w / 2f, y + h / 2f);
            FitTo(sr, w, h);
        }
        public static void FitTo(SpriteRenderer sr, float w, float h)
        {
            var b = sr.sprite.bounds.size;
            if (b.x <= 0 || b.y <= 0) return;
            sr.transform.localScale = new Vector3(w / b.x, h / b.y, 1);
        }
        static void Label(Transform parent, string text, float x, float y, float size, Color col)
        {
            var go = new GameObject("Label_" + text);
            go.transform.SetParent(parent, false);
            go.transform.position = U.W(x, y, 0);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Fonts.Main; tm.text = text; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.fontSize = 64; tm.characterSize = size / 6.4f; tm.color = col;
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = Fonts.Main.material; mr.sortingOrder = L_LABEL;
        }
        LineRenderer NewLine()
        {
            var go = new GameObject("Line"); go.transform.SetParent(dynRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = lineMat; lr.positionCount = 2; lr.useWorldSpace = true; lr.sortingOrder = L_AIM; lr.numCapVertices = 2;
            lr.enabled = false;
            return lr;
        }

        /* ===== 어둠 ===== */
        void BuildDarkness()
        {
            dW = G.w * DR; dH = G.h * DR;
            darkBase = new float[dW * dH]; darkCur = new float[dW * dH]; darkOut = new Color32[dW * dH];
            darkTex = new Texture2D(dW, dH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            flickLights.Clear();
            // 바탕 어둠은 진하게, 방 조명은 약하게 → 소품 조명 주변만 밝게 고인다
            float baseDark = Mathf.Min(Cfg.maxDark, G.def.dark * Cfg.darkMul);
            for (int i = 0; i < darkBase.Length; i++) darkBase[i] = baseDark;
            var all = new List<LightSrc>();
            foreach (var l in G.lights) all.Add(new LightSrc { x = l.x, y = l.y, r = l.r * Cfg.roomLightR, i = l.i * Cfg.roomLightI, flick = l.flick, tint = l.tint });
            var glows = new List<Glow>();
            CollectProps(G, all, glows);
            foreach (var gl in glows)
            {
                var sr = Spr(floorRoot, "fx_glow", L_TINT);
                sr.transform.position = U.W(gl.x, gl.y); sr.transform.localScale = Vector3.one * gl.r * 2; sr.color = gl.c;
            }
            foreach (var l in all)
            {
                if (l.flick != 0) { flickLights.Add(l); continue; }
                ApplyLight(darkBase, l, l.i, 0, 0, dW, dH);
            }
            System.Array.Copy(darkBase, darkCur, darkBase.Length);
            foreach (var l in flickLights) ApplyLight(darkCur, l, l.i, 0, 0, dW, dH);
            for (int i = 0; i < darkCur.Length; i++) darkOut[i] = DarkPx(darkCur[i]);
            darkTex.SetPixels32(darkOut); darkTex.Apply();
            var go = new GameObject("Darkness"); go.transform.SetParent(floorRoot, false);
            darkR = go.AddComponent<SpriteRenderer>();
            darkR.sprite = Sprite.Create(darkTex, new Rect(0, 0, dW, dH), new Vector2(0, 1), DR);
            darkR.sortingOrder = L_DARK;
            go.transform.position = Vector3.zero; // 맵 왼쪽 위 = (0,0)
            prevPlayerBox = new RectInt(0, 0, 0, 0);
        }
        // 어둠: 깊은 남색. 세기는 Cfg.darkMul / maxDark / roomLightI 로 조절
        static Color32 DarkPx(float a) { return new Color32(6, 7, 34, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255), 0, 255)); }
        // 텍스처 행 인덱스: y=0이 아래쪽이므로 게임 y(아래쪽)를 뒤집는다
        int Idx(int px, int py) { return (dH - 1 - py) * dW + px; }
        static float LightA(float d, float r, float lv)
        {
            float t = d / r;
            if (t >= 1) return 0;
            return t < 0.55f ? Mathf.Lerp(lv, lv * 0.7f, t / 0.55f) : Mathf.Lerp(lv * 0.7f, 0, (t - 0.55f) / 0.45f);
        }
        void ApplyLight(float[] buf, LightSrc l, float lv, int bx0, int by0, int bx1, int by1)
        {
            int x0 = Mathf.Max(bx0, Mathf.FloorToInt((l.x - l.r) * DR)), x1 = Mathf.Min(bx1, Mathf.CeilToInt((l.x + l.r) * DR));
            int y0 = Mathf.Max(by0, Mathf.FloorToInt((l.y - l.r) * DR)), y1 = Mathf.Min(by1, Mathf.CeilToInt((l.y + l.r) * DR));
            for (int py = y0; py < y1; py++) for (int px = x0; px < x1; px++)
            {
                float wx = (px + 0.5f) / DR, wy = (py + 0.5f) / DR;
                float a = LightA(U.Dist(wx, wy, l.x, l.y), l.r, lv);
                if (a > 0) buf[Idx(px, py)] *= 1 - a;
            }
        }
        RectInt LightBox(LightSrc l)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((l.x - l.r) * DR)), x1 = Mathf.Min(dW, Mathf.CeilToInt((l.x + l.r) * DR));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((l.y - l.r) * DR)), y1 = Mathf.Min(dH, Mathf.CeilToInt((l.y + l.r) * DR));
            return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }
        void CopyBox(RectInt b, float[] src, float[] dst) { for (int py = b.yMin; py < b.yMax; py++) for (int px = b.xMin; px < b.xMax; px++) { int i = Idx(px, py); dst[i] = src[i]; } }
        void OutBox(RectInt b) { for (int py = b.yMin; py < b.yMax; py++) for (int px = b.xMin; px < b.xMax; px++) { int i = Idx(px, py); darkOut[i] = DarkPx(darkCur[i]); } }
        internal static float LightLevel(LightSrc l, float t)
        {
            if (l.flick == 0) return l.i;
            float n = Mathf.Sin(t * 13 + l.x) * Mathf.Sin(t * 7.3f + l.y * 2);
            float v = l.i * (0.94f + 0.06f * Mathf.Sin(t * 50 + l.x));
            if (l.flick == 2 && n > 0.7f) v *= 0.3f;
            if (l.flick == 1 && n > 0.9f) v *= 0.55f;
            return v;
        }
        void UpdateDarkness(float t, bool playing)
        {
            var S = game.S;
            flickTimer -= Time.deltaTime;
            if (flickTimer <= 0 && flickLights.Count > 0)
            {
                flickTimer = 0.05f;
                foreach (var l in flickLights) CopyBox(LightBox(l), darkBase, darkCur);
                foreach (var l in flickLights) { var b = LightBox(l); ApplyLight(darkCur, l, LightLevel(l, t), b.xMin, b.yMin, b.xMax, b.yMax); }
                foreach (var l in flickLights) OutBox(LightBox(l));
            }
            OutBox(prevPlayerBox);
            if (playing)
            {
                var pl = new LightSrc { x = S.player.x, y = S.player.y, r = 2.2f };
                var b = LightBox(pl);
                for (int py = b.yMin; py < b.yMax; py++) for (int px = b.xMin; px < b.xMax; px++)
                {
                    float d = U.Dist((px + 0.5f) / DR, (py + 0.5f) / DR, pl.x, pl.y) / pl.r;
                    if (d >= 1) continue;
                    int i = Idx(px, py);
                    darkOut[i] = DarkPx(darkCur[i] * (1 - 0.75f * (1 - d)));
                }
                prevPlayerBox = b;
            }
            darkTex.SetPixels32(darkOut); darkTex.Apply();
        }

        /* ===== 매 프레임 ===== */
        // 카메라 줌 (좁은 통로 = 줌 인, 넓은 곳 = 줌 아웃). 1 = 기본
        public static float Zoom = 1f;
        MeshFilter fog;

        public static bool InViewOf(Vector2 cam, float x, float y, float m)
        {
            float cx = cam.x + Cfg.VTW / 2, cy = cam.y + Cfg.VTH / 2;
            return Mathf.Abs(x - cx) < Cfg.VTW / 2 * Zoom + m && Mathf.Abs(y - cy) < Cfg.VTH / 2 * Zoom + m;
        }
        public bool InView(float x, float y, float m) { return InViewOf(game.S.cam, x, y, m); }
        public Vector3 HeadScreen(float x, float y, float extra) { return cam.WorldToScreenPoint(U.W(x, y - extra)); }

        void UpdateZoom(bool playing)
        {
            var S = game.S;
            float target = 1f;
            if (playing && game.visValid)
            {
                target = Mathf.Lerp(0.8f, 1.12f, Mathf.InverseLerp(2.5f, 7.5f, game.visAvg));
                if (game.PlayerHidden()) target = 0.72f;
                if (S.spot > 0 || game.AnyLocked()) target *= 0.94f;                      // 들킬 때 살짝 조여 옴
                if (S.player.eating != null) target *= 0.9f;                             // 먹을 때 확 다가감
            }
            Zoom += (target - Zoom) * Mathf.Min(1, Time.deltaTime * 2.2f);
            cam.orthographicSize = Cfg.VTH / 2f * Zoom;
        }

        // 시야 밖 어둠: 광선이 닿은 경계에서 바깥으로 짙어지는 메시
        readonly List<Vector3> fv = new List<Vector3>(); readonly List<Color> fc = new List<Color>(); readonly List<int> ft = new List<int>();
        void UpdateFog(bool playing)
        {
            bool on = playing && game.visValid;
            fog.gameObject.SetActive(on);
            if (!on) return;
            var p = game.S.player; int N = Game.VIS_RAYS;
            const float FOG_A = 0.84f;
            Color inner = new Color(5 / 255f, 6 / 255f, 22 / 255f, 0f), outer = new Color(5 / 255f, 6 / 255f, 22 / 255f, FOG_A);
            fv.Clear(); fc.Clear(); ft.Clear();
            for (int i = 0; i < N; i++)
            {
                float a = i * Mathf.PI * 2 / N, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float d = game.visDist[i];
                float din = d < game.visR - 0.05f ? d + 0.35f : d;     // 벽이면 벽면이 보이게 조금 더
                fv.Add(U.W(p.x + ca * din, p.y + sa * din)); fc.Add(inner);
                fv.Add(U.W(p.x + ca * (din + 1.1f), p.y + sa * (din + 1.1f))); fc.Add(outer);
                fv.Add(U.W(p.x + ca * 60, p.y + sa * 60)); fc.Add(outer);
            }
            for (int i = 0; i < N; i++)
            {
                int j = (i + 1) % N, a0 = i * 3, b0 = j * 3;
                ft.Add(a0); ft.Add(b0); ft.Add(b0 + 1); ft.Add(a0); ft.Add(b0 + 1); ft.Add(a0 + 1);
                ft.Add(a0 + 1); ft.Add(b0 + 1); ft.Add(b0 + 2); ft.Add(a0 + 1); ft.Add(b0 + 2); ft.Add(a0 + 2);
            }
            var m = fog.sharedMesh; m.Clear(); m.SetVertices(fv); m.SetColors(fc); m.SetTriangles(ft, 0); m.RecalculateBounds();
        }

        T Pooled<T>(List<T> pool, int i, System.Func<T> make) where T : Component
        {
            while (pool.Count <= i) pool.Add(make());
            var c = pool[i]; c.gameObject.SetActive(true); return c;
        }
        static void HideFrom<T>(List<T> pool, int n) where T : Component { for (int i = n; i < pool.Count; i++) if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false); }

        // 서 있는 사람 그림 (stand_<종류>_front/back/side). 있으면 true, sc = 크기 배율
        public const float CHAR_PX = 26f;     // 사람 그림 몇 픽셀이 1칸인지
        const float STAND_FOOT = 0.3f;        // 발 위치: 사람 좌표보다 이만큼 아래(화면상)
        //   걸을 때 walk_<종류>_<방향>_0..3 그림이 있으면 그걸 순서대로 (walkT = 걸음 진행 시간)
        internal static bool ApplyStand(SpriteRenderer sr, string kind, float face, out float sc, float walkT = -1)
        {
            sc = 1;
            if (!Art.Has("stand_" + kind + "_front")) { sr.flipX = false; return false; }
            int dir = Art.DirIndex(face);
            string view = dir == 1 ? "front" : dir == 3 ? "back" : "side";
            var walk = walkT >= 0 ? Art.Frames("walk_" + kind + "_" + view) : null;
            sr.sprite = walk != null && walk.Count > 0 ? walk[Mathf.FloorToInt(walkT * 8f) % walk.Count] : Art.Get("stand_" + kind + "_" + view);
            sr.flipX = dir == 2; sr.transform.rotation = Quaternion.identity;
            sc = sr.sprite.pixelsPerUnit / CHAR_PX;
            return true;
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

        // 캐릭터 그림 적용: 4방향 그림이 있으면 방향 선택, 없으면 회전
        static void ApplyFacing(SpriteRenderer sr, string key, float face)
        {
            var dir = Art.Dir(key);
            if (dir != null) { sr.sprite = dir[Art.DirIndex(face)]; sr.transform.rotation = Quaternion.identity; }
            else { sr.sprite = Art.Get(key); sr.transform.rotation = Quaternion.Euler(0, 0, U.Deg(face)); }
        }

        public void Render()
        {
            if (game == null || game.S == null || G == null || player == null) return;
            var S = game.S; var F = S.Fl; var p = S.player; float t = S.time;
            bool playing = game.mode != Mode.Title && game.mode != Mode.Intro;

            // 카메라
            Vector2 shake = game.mode == Mode.Dying ? new Vector2(U.Rand(-3, 3), U.Rand(-3, 3)) / 32f : Vector2.zero;
            if (S.alarm != null && S.time - S.alarm.started < 0.4f) shake += new Vector2(U.Rand(-2, 2), U.Rand(-2, 2)) / 32f;   // 경보 순간 흔들림
            cam.transform.position = new Vector3(S.cam.x + Cfg.VTW / 2 + shake.x, -(S.cam.y + Cfg.VTH / 2) + shake.y, -10);
            UpdateZoom(playing);
            UpdateFog(playing);

            // 문
            for (int i = 0; i < doors.Count; i++)
            {
                var d = G.def.doors[i]; var st = F.doors[i]; var v = doors[i];
                bool open = st.open || st.temp > 0;
                var want = Art.Get(open ? "door_open" : "door_closed");
                if (v.baseR.sprite != want) { v.baseR.sprite = want; FitTo(v.baseR, 1, 1); }
                v.stripes.enabled = !open;
                if (open) { v.led.color = st.open ? Data.Hex("#7fe0b0") : Data.Cards[d.req].color; v.led.transform.position = U.W(d.x + 0.17f, d.y + 0.17f); }
                else { v.led.color = game.HasCard(d.req) ? Data.Hex("#7fe0b0") : Data.Hex("#ff5148"); v.led.transform.position = U.W(d.x + 0.5f, d.y + 0.5f); }
            }
            // 함정
            foreach (var tv in traps)
            {
                bool off = S.trapsOff.Contains(tv.def.id);
                for (int i = 0; i < tv.parts.Count; i++)
                {
                    var sr = tv.parts[i];
                    if (tv.def.kind == "laser")
                    {
                        sr.enabled = !off;
                        float a = 0.55f + 0.35f * Mathf.Sin(t * 20 + tv.rows[i] * 2.3f);
                        sr.color = new Color(1, 60 / 255f, 60 / 255f, a);
                    }
                    else
                    {
                        var c = tv.cells[i]; float ph = t * 1.3f + c.x * 1.7f + c.y * 2.9f;
                        sr.transform.position = U.W(c.x + 0.5f + 0.25f * Mathf.Sin(ph), c.y + 0.5f + 0.25f * Mathf.Cos(ph * 0.8f));
                        sr.color = off ? new Color(160 / 255f, 200 / 255f, 140 / 255f, 0.05f) : new Color(150 / 255f, 230 / 255f, 90 / 255f, 0.16f + 0.08f * Mathf.Sin(ph));
                    }
                }
            }
            for (int i = 0; i < consoleLeds.Count; i++)
            {
                var c = G.def.consoles[i];
                bool done = c.heli ? S.heli != null : S.trapsOff.Contains(c.trap);
                consoleLeds[i].color = done ? Data.Hex("#7fe0b0") : new Color(1, 81 / 255f, 72 / 255f, 0.6f + 0.4f * Mathf.Sin(t * 5));
            }
            foreach (var led in liftLeds) led.color = S.alarm != null ? new Color(1, 81 / 255f, 72 / 255f, 0.5f + 0.5f * Mathf.Sin(t * 8)) : Data.Hex("#7fe0b0");

            // 점액 자국
            int ti = 0;
            foreach (var tr in S.trail)
            {
                var sr = Pooled(trailPool, ti++, () => Spr(dynRoot, "fx_dot", L_TRAIL));
                sr.transform.position = U.W(tr.x, tr.y); sr.transform.localScale = Vector3.one * (tr.r * 2 / 32f);
                sr.color = new Color(1, 125 / 255f, 140 / 255f, 0.22f * tr.t / 3.5f);
            }
            HideFrom(trailPool, ti);

            // 물건
            int oi = 0;
            foreach (var o in F.objects)
            {
                if (!o.alive || !InView(o.x, o.y, 1)) continue;
                var sr = Pooled(objPool, oi++, () => Spr(dynRoot, "obj_chair", L_OBJ));
                sr.sprite = Art.Get("obj_" + o.form);
                float s = 1;
                if (p.eating != null && !p.eating.target.isNpc && p.eating.target.obj == o) s = Mathf.Max(0.05f, 1 - p.eating.t / Cfg.eatTime);
                sr.transform.position = U.W(o.x, o.y); sr.transform.rotation = Quaternion.Euler(0, 0, U.Deg(o.form == "chair" ? o.rot : 0));
                sr.transform.localScale = Vector3.one * s; sr.color = Color.white;
            }
            HideFrom(objPool, oi);

            // 사람
            int ni = 0;
            foreach (var n in F.npcs)
            {
                if (!n.alive || !InView(n.x, n.y, 1)) continue;
                if (playing && !game.PlayerCanSee(n.x, n.y)) continue;       // 주인공이 못 보는 사람은 안 그림
                var sr = Pooled(npcPool, ni++, () => Spr(dynRoot, "char_researcher", L_NPC));
                bool moving = !n.frozen && ((n.path != null && n.path.Count > 0) || n.state == NpcState.Chase);
                string kind = Variant(n.pal, n.id);
                float wt = moving ? t * (n.state == NpcState.Chase ? 1.5f : 1f) + (n.id.Length % 4) * 0.13f : -1;
                float sc; bool standing = ApplyStand(sr, kind, n.face, out sc, wt);
                if (!standing) ApplyFacing(sr, "char_" + n.pal, n.face);
                float s = 1;
                if (p.eating != null && p.eating.target.isNpc && p.eating.target.npc == n) s = Mathf.Max(0.05f, 1 - p.eating.t / Cfg.eatTime);
                // 걸을 때 통통 흔들림 (걷기 그림이 있으면 그림이 대신함)
                float ph = t * (n.state == NpcState.Chase ? 18 : 12) + n.id.GetHashCode() % 7;
                float bob = moving && !(standing && HasWalk(kind)) ? Mathf.Abs(Mathf.Sin(ph)) * 0.06f : 0;
                if (standing)
                {
                    // 서 있는 그림: 발이 사람 위치보다 살짝 아래, 걸을 때 위아래로 통통
                    sr.transform.localScale = Vector3.one * sc * s;
                    sr.transform.position = U.W(n.x, n.y + STAND_FOOT + sr.sprite.bounds.min.y * sc * s - bob);
                }
                else
                {
                    sr.transform.position = U.W(n.x, n.y - bob);
                    if (moving && Art.Dir("char_" + n.pal) == null) sr.transform.rotation = Quaternion.Euler(0, 0, U.Deg(n.face) + Mathf.Sin(ph) * 5f);
                    sr.transform.localScale = Vector3.one * s * (moving ? 1 + Mathf.Sin(ph * 2) * 0.03f : 1);
                }
            }
            HideFrom(npcPool, ni);

            // 주인공
            player.gameObject.SetActive(playing);
            playerGlow.gameObject.SetActive(false);
            if (playing) DrawPlayer(t);

            // 헬기
            DrawHeli(t);

            // 어둠 + 경보 색
            UpdateDarkness(t, playing);
            alarmTint.gameObject.SetActive(S.alarm != null);
            if (S.alarm != null)
            {
                alarmTint.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0);
                alarmTint.color = new Color(120 / 255f, 0, 0, 0.10f + 0.08f * Mathf.Sin(t * 6));
            }

            // 시야
            int ci = 0;
            foreach (var n in F.npcs)
            {
                if (!n.alive || !InView(n.x, n.y, game.ViewDist(n) + 1)) continue;
                var mf = Pooled(conePool, ci++, NewCone);
                BuildCone(mf.sharedMesh, n);
            }
            HideFrom(conePool, ci);

            // 조준선
            int ai = 0;
            foreach (var n in F.npcs)
            {
                if (!(n.alive && n.aim > 0.02f && n.seesPlayer)) continue;
                float k2 = Mathf.Min(1, n.aim / Cfg.aimTime);
                var lr = Pooled(aimPool, ai, NewLine); lr.enabled = true;
                lr.SetPosition(0, U.W(n.x, n.y)); lr.SetPosition(1, U.W(p.x, p.y));
                lr.widthMultiplier = (1 + 2 * k2) / 32f;
                lr.startColor = lr.endColor = new Color(1, 40 / 255f, 40 / 255f, 0.25f + 0.65f * k2);
                var ring = Pooled(aimRingPool, ai, () => Spr(dynRoot, "fx_ring", L_RING));
                ring.transform.position = U.W(p.x, p.y); ring.transform.localScale = Vector3.one * ((22 - 10 * k2) * 2 / 32f);
                ring.color = new Color(1, 40 / 255f, 40 / 255f, 0.5f + 0.5f * k2);
                ai++;
            }
            HideFrom(aimPool, ai); HideFrom(aimRingPool, ai);
            bool shot = game.mode == Mode.Dying && S.deathKind == "shot" && S.shotFrom.HasValue;
            shotLine.enabled = shot;
            if (shot) { shotLine.SetPosition(0, U.W(S.shotFrom.Value.x, S.shotFrom.Value.y)); shotLine.SetPosition(1, U.W(p.x, p.y)); }

            // 먹을 대상 표시
            var tgt = game.mode == Mode.Play && p.eating == null ? game.NearestEdible() : null;
            targetRing.gameObject.SetActive(tgt != null);
            if (tgt != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 6);
                bool blocked = S.anySee && game.OthersSee(tgt.Ref);
                targetRing.transform.position = U.W(tgt.X, tgt.Y);
                targetRing.color = blocked ? new Color(1, 81 / 255f, 72 / 255f, 0.5f + pulse * 0.4f) : new Color(1, 125 / 255f, 140 / 255f, 0.5f + pulse * 0.4f);
            }

            // 파티클
            int fi = 0;
            foreach (var pt in S.particles)
            {
                var sr = Pooled(fxPool, fi++, () => Spr(dynRoot, "fx_dot", L_FX));
                sr.transform.position = U.W(pt.x, pt.y); sr.transform.localScale = Vector3.one * (pt.r * 2 / 32f);
                var c = pt.color; c.a = Mathf.Max(0, pt.life / pt.max); sr.color = c;
            }
            HideFrom(fxPool, fi);
        }

        bool faceLeft;
        // player_<동작>_<방향>_N → 없으면 옆모습(side) → 없으면 player_<동작>_N
        internal static List<Sprite> PlayerFrames(string anim, string dir)
        {
            var l = Art.Frames("player_" + anim + "_" + dir);
            if (l.Count == 0) l = Art.Frames("player_" + anim + "_side");
            if (l.Count == 0) l = Art.Frames("player_" + anim);
            return l;
        }
        internal const float TILE_PX = 16f; // 타일 그림 1칸의 픽셀 수 (캐릭터 크기 기준)

        void DrawPlayer(float t)
        {
            var S = game.S; var p = S.player; var f = Data.Forms[p.form];
            bool hidden = game.PlayerHidden();
            var tr = player.transform;
            player.sortingOrder = L_PLAYER;
            Color col = new Color(1, 1, 1, hidden ? 0.45f : 1f);
            Vector2 j = Vector2.zero;
            player.flipX = false;
            // 방향: 0 오른쪽, 1 아래(앞모습), 2 왼쪽(옆모습 반전), 3 위(뒷모습)
            int dir = Art.DirIndex(p.face);
            string dn = dir == 1 ? "down" : dir == 3 ? "up" : "side";
            if (dir == 0) faceLeft = false; else if (dir == 2) faceLeft = true;
            var idleF = PlayerFrames("idle", dn);
            if ((p.eating != null || f.type == FormType.Body) && idleF.Count > 0)
            {
                var crawlF = PlayerFrames("crawl", dn); var eatF = PlayerFrames("eat", dn);
                Sprite spr;
                if (p.eating != null && eatF.Count > 0) spr = eatF[Mathf.Min(eatF.Count - 1, Mathf.FloorToInt(p.eating.t / Cfg.eatTime * eatF.Count))];
                else if (p.speedNow > 0.3f && crawlF.Count > 0) spr = crawlF[Mathf.FloorToInt(t * 10) % crawlF.Count];
                else spr = idleF[Mathf.FloorToInt(t * 2.5f) % idleF.Count];
                player.sprite = spr; player.flipX = dn == "side" && faceLeft; tr.rotation = Quaternion.identity;
                tr.localScale = Vector3.one * (spr.pixelsPerUnit / TILE_PX) * (hidden ? 0.8f : 1f);   // 16픽셀 = 1칸
                if (!hidden && p.eating == null)
                {
                    playerGlow.gameObject.SetActive(true);
                    playerGlow.transform.position = U.W(p.x, p.y);
                    playerGlow.color = new Color(0.62f, 0.42f, 1f, 0.16f);
                }
                tr.position = U.W(p.x, p.y - 0.15f);
                player.color = col;
                return;
            }
            if (p.eating != null)
            {
                float k = p.eating.t / Cfg.eatTime;
                ApplyFacing(player, "player_blob", p.face);
                float s = 1 + 0.25f * Mathf.Sin(k * Mathf.PI), sq = 0.15f * Mathf.Sin(k * Mathf.PI * 3);
                tr.localScale = new Vector3(s * (1 + sq), s * (1 - sq * 0.6f), 1);
            }
            else if (f.type == FormType.Body)
            {
                ApplyFacing(player, "player_blob", p.face);
                float sq = p.speedNow > 0.3f ? 0.06f * Mathf.Sin(t * 14) : 0;
                float s = hidden ? 0.8f : 1f;
                tr.localScale = new Vector3(s * (1 + sq), s * (1 - sq * 0.6f), 1);
                if (!hidden)
                {
                    playerGlow.gameObject.SetActive(true);
                    playerGlow.transform.position = U.W(p.x, p.y);
                    playerGlow.color = new Color(1, 125 / 255f, 140 / 255f, 0.18f);
                }
            }
            else
            {
                bool warn = p.timer < f.dur * 0.25f;
                if (warn) { j = new Vector2(Mathf.Sin(t * 43) * 1.2f, Mathf.Cos(t * 37) * 1.0f) / 32f; col = Color.Lerp(col, new Color(1, 0.6f, 0.65f, col.a), 0.5f + 0.5f * Mathf.Sin(t * 8)); }
                if (f.type == FormType.Object && p.speedNow > 0.3f) j.y += Mathf.Sin(t * 22) * 0.8f / 32f;
                tr.localScale = Vector3.one;
                float sc;
                if (f.type == FormType.Human && ApplyStand(player, p.form, p.face, out sc, p.speedNow > 0.3f ? t : -1))
                {
                    tr.localScale = Vector3.one * sc;
                    j.y += STAND_FOOT + player.sprite.bounds.min.y * sc;
                }
                else if (f.type == FormType.Human) ApplyFacing(player, "char_" + p.form, p.face);
                else { player.sprite = Art.Get("obj_" + p.form); tr.rotation = Quaternion.identity; }
            }
            tr.position = U.W(p.x + j.x, p.y + j.y);
            player.color = col;
        }

        void DrawHeli(float t)
        {
            var S = game.S;
            bool show = G.def.pad.HasValue && S.heli != null;
            float k = 0;
            if (show) { k = S.heli.arrived ? 1 : Mathf.Max(0, 1 - S.heli.t / 5); if (k <= 0) show = false; }
            heli.gameObject.SetActive(show); heliRotor.gameObject.SetActive(show);
            if (!show) return;
            var pad = G.def.pad.Value;
            float cx = pad.x + pad.w / 2f, cy = pad.y + pad.h / 2f - (1 - k) * 120 / 32f;
            float s = 0.5f + 0.5f * k, a = Mathf.Min(1, k * 1.5f);
            heli.transform.position = U.W(cx, cy); heli.transform.localScale = Vector3.one * s; heli.color = new Color(1, 1, 1, a);
            heliRotor.transform.position = U.W(cx, cy); heliRotor.transform.localScale = Vector3.one * s; heliRotor.color = new Color(1, 1, 1, a);
            heliRotor.transform.rotation = Quaternion.Euler(0, 0, -t * 18 * Mathf.Rad2Deg);
        }

        /* ===== 시야 부채꼴 ===== */
        MeshFilter NewCone()
        {
            var go = new GameObject("Cone"); go.transform.SetParent(dynRoot, false);
            var mf = go.AddComponent<MeshFilter>(); var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = lineMat; mr.sortingOrder = L_CONE;
            var m = new Mesh(); m.MarkDynamic(); mf.sharedMesh = m;
            return mf;
        }
        readonly List<Vector3> cv = new List<Vector3>(); readonly List<Color> cc = new List<Color>(); readonly List<int> ct = new List<int>();
        void BuildCone(Mesh m, Npc n)
        {
            var S = game.S; var c = n.C;
            float half = Data.ShapeHalf(c.shape), vd = game.ViewDist(n);
            int N = c.shape == ViewShape.Circle ? 72 : c.shape == ViewShape.Half ? 48 : 34;
            bool danger = (S.spotNpc == n && S.spot > 0) || n.lockT > 0;
            bool hunting = n.state == NpcState.Chase;
            Color col = danger ? new Color(1, 81 / 255f, 72 / 255f) : hunting ? new Color(1, 140 / 255f, 90 / 255f) : new Color(243 / 255f, 176 / 255f, 74 / 255f);
            float a0 = danger ? 0.34f : n.seesPlayer ? 0.26f : (n.state == NpcState.Inspect || hunting) ? 0.22f : 0.14f;
            cv.Clear(); cc.Clear(); ct.Clear();
            cv.Add(U.W(n.x, n.y)); cc.Add(new Color(col.r, col.g, col.b, a0));
            for (int i = 0; i <= N; i++)
            {
                float a = n.face - half + (2 * half) * i / N, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float d = 0, lim = vd;
                if (c.shape == ViewShape.Circle && Mathf.Abs(U.AngDiff(a, n.face)) > Data.Rad(Cfg.rearBlindFrom)) lim = Cfg.rearRange;   // 등 뒤 사각지대
                while (d < lim)
                {
                    float nd = d + 0.08f;
                    if (game.IsOpaque(U.Floor(n.x + ca * nd), U.Floor(n.y + sa * nd))) break;
                    d = nd;
                }
                cv.Add(U.W(n.x + ca * d, n.y + sa * d));
                cc.Add(new Color(col.r, col.g, col.b, Mathf.Lerp(a0, 0.03f, d / vd)));
                if (i > 0) { ct.Add(0); ct.Add(i + 1); ct.Add(i); }
            }
            m.Clear();
            m.SetVertices(cv); m.SetColors(cc); m.SetTriangles(ct, 0);
            m.RecalculateBounds();
        }
    }
}
