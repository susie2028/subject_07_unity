// 2.5D 화면 (비교용). 게임 규칙·맵·UI는 2D와 똑같고, 그리는 방식만 다르다.
//  - 바닥: 2D와 같은 타일 그림을 바닥 판에 붙임
//  - 벽·가구: 높이 있는 상자 (윗면에 타일 그림). 벽 앞면에는 wall_face 그림
//  - 조명: 진짜 점광원 (천장등, 수조, 모니터, 방 조명). 주인공 빛만 그림자를 만든다
//  - 사람·주인공: 픽셀 그림을 세워서(빌보드) 표시. NPC 그림은 stand_<종류>_<front|back|side>
// 3D 좌표: (게임 x, 높이, -게임 y). 1칸 = 1.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Specimen07
{
    public partial class WorldView3D : MonoBehaviour, IGameView
    {
        /* ===== 조절값 (보면서 바꾸기) ===== */
        const float PITCH = 50f;          // 카메라 내려다보는 각도 (90 = 바로 위)
        const float FOV = 30f;            // 화각
        const float DIST = 19f;           // 카메라 거리 (줌 1 기준)
        const float WALL_H = 1.9f;        // 벽 높이 (높을수록 벽 뒤가 더 가려짐)
        const float FOG_H = WALL_H + 0.06f; // 시야 밖 어둠 판 높이 (벽 위)
        const float ROOM_LIGHT_I = 2.0f, ROOM_LIGHT_R = 1.7f, PROP_LIGHT_I = 1.8f, PROP_LIGHT_R = 1.8f;
        const float PLAYER_LIGHT_I = 1.1f, PLAYER_LIGHT_R = 7.5f;   // 주인공 주변(시야 안)을 밝혀 주는 빛. 이 빛만 그림자를 만든다
        static readonly Color AMBIENT = new Color(0.17f, 0.18f, 0.3f);    // 전체 바탕 밝기 (시야 밖은 어둠 판이 덮음)
        static readonly float BB_Y = 1f / Mathf.Cos(PITCH * Mathf.Deg2Rad);   // 세운 그림이 비스듬히 보여 납작해지는 만큼 세로로 늘림
        static float HeadH { get { return 1.45f * BB_Y; } }

        Game game; Camera cam; FloorGeo G;
        Transform floorRoot, dynRoot;
        Shader litSh, cutSh, transSh, unlitColSh, spriteSh;
        Material spriteMat, fogMat, xrayMat;
        readonly Dictionary<Texture, Material> texMats = new Dictionary<Texture, Material>();
        readonly Dictionary<Color, Material> colMats = new Dictionary<Color, Material>();
        Mesh unitBox;

        /* ===== 시작 ===== */
        void Awake()
        {
            game = GetComponent<Game>();
            cam = Camera.main;
            if (cam == null) { var go = new GameObject("Main Camera"); go.tag = "MainCamera"; cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); }
            cam.orthographic = false;
            cam.fieldOfView = FOV;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Data.Hex("#05060c");
            cam.nearClipPlane = 0.3f; cam.farClipPlane = 120f;
            cam.renderingPath = RenderingPath.DeferredShading;   // 점광원을 많이 써도 되도록

            litSh = Find("Legacy Shaders/Diffuse", "Standard");
            cutSh = Find("Legacy Shaders/Transparent/Cutout/Diffuse", "Legacy Shaders/Diffuse");
            transSh = Find("Legacy Shaders/Transparent/Diffuse", "Sprites/Default");
            unlitColSh = Find("Unlit/Color", "Sprites/Default");
            spriteSh = Shader.Find("Sprites/Default");
            spriteMat = new Material(spriteSh);
            fogMat = new Material(spriteSh) { renderQueue = 3100 };
            // 벽 뒤에 가려진 주인공 실루엣: UI 셰이더의 "항상 위에 그리기" 옵션 사용
            var ui = Shader.Find("UI/Default");
            if (ui != null) { xrayMat = new Material(ui) { renderQueue = 3200 }; xrayMat.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always); }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AMBIENT;
            RenderSettings.fog = false;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 40f;
            unitBox = MakeUnitBox();
        }
        static Shader Find(string a, string b) { var s = Shader.Find(a); return s != null ? s : Shader.Find(b); }

        static Vector3 P(float x, float h, float y) { return new Vector3(x, h, -y); }

        /* ===== 재질 ===== */
        Material TexMat(Sprite spr, bool cutout)
        {
            var tex = spr.texture; Material m;
            if (texMats.TryGetValue(tex, out m) && (m.shader == (cutout ? cutSh : litSh))) return m;
            m = new Material(cutout ? cutSh : litSh) { mainTexture = tex };
            if (cutout) m.SetFloat("_Cutoff", 0.4f);
            texMats[tex] = m;
            return m;
        }
        Material ColMat(Color c, bool unlit = false)
        {
            var key = unlit ? new Color(c.r, c.g, c.b, -1) : c; Material m;
            if (colMats.TryGetValue(key, out m)) return m;
            m = new Material(unlit ? unlitColSh : (c.a < 1 ? transSh : litSh)) { color = c };
            colMats[key] = m;
            return m;
        }
        static Rect UV(Sprite s)
        {
            var r = s.textureRect; float tw = s.texture.width, th = s.texture.height;
            return new Rect(r.x / tw, r.y / th, r.width / tw, r.height / th);
        }

        /* ===== 메시 모으기 (같은 재질끼리 하나로) ===== */
        class Buf { public List<Vector3> v = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>(); public List<int> t = new List<int>(); }
        readonly Dictionary<Material, Buf> bufs = new Dictionary<Material, Buf>();
        Buf B(Material m) { Buf b; if (!bufs.TryGetValue(m, out b)) { b = new Buf(); bufs[m] = b; } return b; }
        // 시계 방향 a(왼위) b(오른위) c(오른아래) d(왼아래) — 보는 쪽에서
        static void Quad(Buf b, Vector3 a, Vector3 bb, Vector3 c, Vector3 d, Rect uv)
        {
            int i = b.v.Count;
            b.v.Add(a); b.v.Add(bb); b.v.Add(c); b.v.Add(d);
            b.uv.Add(new Vector2(uv.xMin, uv.yMax)); b.uv.Add(new Vector2(uv.xMax, uv.yMax)); b.uv.Add(new Vector2(uv.xMax, uv.yMin)); b.uv.Add(new Vector2(uv.xMin, uv.yMin));
            b.t.Add(i); b.t.Add(i + 1); b.t.Add(i + 2); b.t.Add(i); b.t.Add(i + 2); b.t.Add(i + 3);
        }
        static readonly Rect FULL = new Rect(0, 0, 1, 1);
        // 바닥/윗면 판: 게임 좌표 사각형 (x0,y0)-(x1,y1), 높이 h
        void Top(Material m, float x0, float y0, float x1, float y1, float h, Rect uv) { Quad(B(m), P(x0, h, y0), P(x1, h, y0), P(x1, h, y1), P(x0, h, y1), uv); }
        // 상자 옆면. 앞(남쪽) 면만 따로 재질을 줄 수 있다. 이웃이 막혀 있으면 그 면은 만들지 않는다.
        void Sides(Material front, Material side, float x0, float y0, float x1, float y1, float h0, float h1, bool s, bool e, bool w, Rect frontUV)
        {
            if (s) Quad(B(front), P(x0, h1, y1), P(x1, h1, y1), P(x1, h0, y1), P(x0, h0, y1), frontUV);
            if (w) Quad(B(side), P(x0, h1, y0), P(x0, h1, y1), P(x0, h0, y1), P(x0, h0, y0), FULL);
            if (e) Quad(B(side), P(x1, h1, y1), P(x1, h1, y0), P(x1, h0, y0), P(x1, h0, y1), FULL);
        }
        void Flush(Transform parent)
        {
            foreach (var kv in bufs)
            {
                if (kv.Value.v.Count == 0) continue;
                var go = new GameObject("Mesh_" + kv.Key.name); go.transform.SetParent(parent, false);
                var m = new Mesh();
                if (kv.Value.v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(kv.Value.v); m.SetUVs(0, kv.Value.uv); m.SetTriangles(kv.Value.t, 0);
                m.RecalculateNormals(); m.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = kv.Key;
                mr.shadowCastingMode = kv.Key.shader == transSh || kv.Key.shader == unlitColSh ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
            bufs.Clear();
        }

        // 가로세로 1, 높이 1, 바닥 가운데가 원점인 상자 (문, LED, 레이저에 재사용)
        static Mesh MakeUnitBox()
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>(); var col = new List<Color>();
            System.Action<Vector3, Vector3, Vector3, Vector3> q = (a, b, c, d) =>
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, 0));
                for (int k = 0; k < 4; k++) col.Add(Color.white);
                t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
            };
            float a0 = -0.5f, a1 = 0.5f;
            q(new Vector3(a0, 1, a1), new Vector3(a1, 1, a1), new Vector3(a1, 1, a0), new Vector3(a0, 1, a0));   // 위
            q(new Vector3(a0, 1, a0), new Vector3(a1, 1, a0), new Vector3(a1, 0, a0), new Vector3(a0, 0, a0));   // 앞
            q(new Vector3(a1, 1, a1), new Vector3(a0, 1, a1), new Vector3(a0, 0, a1), new Vector3(a1, 0, a1));   // 뒤
            q(new Vector3(a0, 1, a1), new Vector3(a0, 1, a0), new Vector3(a0, 0, a0), new Vector3(a0, 0, a1));   // 왼
            q(new Vector3(a1, 1, a0), new Vector3(a1, 1, a1), new Vector3(a1, 0, a1), new Vector3(a1, 0, a0));   // 오른
            var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
        MeshRenderer Box(Transform parent, Material mat, float x, float y, float w, float d, float h, float h0 = 0)
        {
            var go = new GameObject("Box"); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = unitBox;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            go.transform.position = P(x, h0, y); go.transform.localScale = new Vector3(w, h, d);
            return mr;
        }

        /* ===== 층 만들기 ===== */
        class DoorV { public Transform box; public MeshRenderer led; public float h = WALL_H; }
        class TrapV { public TrapDef def; public Material mat; public List<SpriteRenderer> gas = new List<SpriteRenderer>(); public List<Vector2Int> cells = new List<Vector2Int>(); public List<int> rows = new List<int>(); public List<MeshRenderer> bars = new List<MeshRenderer>(); }
        class LampV { public Light l; public LightSrc src; public float baseI; }
        readonly List<DoorV> doors = new List<DoorV>();
        readonly List<TrapV> traps = new List<TrapV>();
        readonly List<MeshRenderer> consoleLeds = new List<MeshRenderer>(), liftLeds = new List<MeshRenderer>();
        readonly List<LampV> lamps = new List<LampV>();
        Light playerLight;
        SpriteRenderer player, playerShadow, playerXray, targetRing, heli, heliRotor;
        MeshFilter fog;
        LineRenderer shotLine;
        readonly List<SpriteRenderer> objPool = new List<SpriteRenderer>(), npcPool = new List<SpriteRenderer>(), shadowPool = new List<SpriteRenderer>(),
                                      trailPool = new List<SpriteRenderer>(), fxPool = new List<SpriteRenderer>(), aimRingPool = new List<SpriteRenderer>();
        readonly List<MeshFilter> conePool = new List<MeshFilter>();
        readonly List<LineRenderer> aimPool = new List<LineRenderer>();

        static bool Wallish(char c) { return c == '#' || c == 'v'; }
        static bool Tall(char c) { return Wallish(c) || c == 'D' || "SVEMN".IndexOf(c) >= 0; }

        public void BuildFloor(FloorGeo g)
        {
            G = g;
            if (floorRoot != null) Destroy(floorRoot.gameObject);
            if (dynRoot != null) Destroy(dynRoot.gameObject);
            floorRoot = new GameObject("Floor3D_" + g.id).transform;
            dynRoot = new GameObject("Dynamic3D").transform;
            objPool.Clear(); npcPool.Clear(); shadowPool.Clear(); trailPool.Clear(); fxPool.Clear(); aimRingPool.Clear(); conePool.Clear(); aimPool.Clear();
            doors.Clear(); traps.Clear(); consoleLeds.Clear(); liftLeds.Clear(); lamps.Clear(); bubbles.Clear();
            models.Clear(); modelUsed.Clear(); playerModel = null;

            BuildStatic();
            BuildLights();

            // 문: 닫히면 벽 높이, 열리면 바닥으로 내려간다
            foreach (var d in g.def.doors)
            {
                var mat = new Material(litSh) { mainTexture = Art.Get("door_closed").texture, color = Color.Lerp(Color.white, Data.Cards[d.req].color, 0.45f) };
                var dv = new DoorV();
                dv.box = Box(floorRoot, mat, d.x + 0.5f, d.y + 0.5f, 0.98f, 0.98f, WALL_H).transform;
                dv.led = Box(floorRoot, new Material(spriteMat), d.x + 0.5f, d.y + 0.5f, 0.16f, 0.16f, 0.06f, WALL_H + 0.01f);
                doors.Add(dv);
            }
            // 함정
            foreach (var tr in g.def.traps)
            {
                var tv = new TrapV { def = tr, mat = new Material(spriteMat) };
                foreach (var r in tr.rects)
                {
                    if (tr.kind == "laser")
                        for (int yy = r.y; yy < r.y + r.h; yy++)
                            for (int k = 0; k < 3; k++)
                            {
                                var bar = Box(floorRoot, tv.mat, r.x + r.w / 2f, yy + 0.5f, r.w, 0.05f, 0.04f, 0.2f + 0.24f * k);
                                bar.shadowCastingMode = ShadowCastingMode.Off;
                                tv.bars.Add(bar); tv.rows.Add(yy);
                            }
                    else
                        for (int yy = r.y; yy < r.y + r.h; yy++) for (int xx = r.x; xx < r.x + r.w; xx++)
                        {
                            var sr = Flat(floorRoot, "fx_glow", 1.6f); tv.gas.Add(sr); tv.cells.Add(new Vector2Int(xx, yy));
                        }
                }
                traps.Add(tv);
            }
            foreach (var c in g.def.consoles) consoleLeds.Add(Box(floorRoot, new Material(spriteMat), c.x + 0.5f, c.y + 0.45f, 0.45f, 0.2f, 0.04f, 0.76f));
            if (g.def.lift != null) foreach (var t in g.def.lift) liftLeds.Add(Box(floorRoot, new Material(spriteMat), t.x + 0.5f, t.y + 0.5f, 0.2f, 0.2f, 0.05f, 1.31f));

            // 움직이는 것들
            player = Stand(dynRoot, "player_blob");
            playerShadow = Flat(dynRoot, "fx_dot", 0.9f); playerShadow.color = new Color(0, 0, 0, 0.45f);
            playerXray = Stand(dynRoot, "player_blob");
            if (xrayMat != null) playerXray.sharedMaterial = xrayMat;
            targetRing = Flat(dynRoot, "fx_ring", 34f / 32f);
            heli = Flat(dynRoot, "heli", 1); heliRotor = Flat(dynRoot, "heli_rotor", 1);
            var lgo = new GameObject("PlayerLight"); lgo.transform.SetParent(dynRoot, false);
            playerLight = lgo.AddComponent<Light>(); playerLight.type = LightType.Point; playerLight.range = PLAYER_LIGHT_R; playerLight.intensity = PLAYER_LIGHT_I;
            playerLight.color = new Color(0.78f, 0.62f, 1f); playerLight.shadows = LightShadows.None;   // 길게 늘어지는 그림자 없음 (캐릭터 발밑 그림자만)
            fog = NewMesh("Fog", fogMat);
            shotLine = NewLine(); shotLine.startColor = shotLine.endColor = new Color(1, 230 / 255f, 160 / 255f, 0.9f); shotLine.widthMultiplier = 0.06f;
        }

        void BuildStatic()
        {
            var g = G;
            int salt = 0; foreach (char c in g.id) salt += c;
            System.Func<int, int, char> at = (x, y) => g.Tile(x, y);
            System.Func<int, int, bool> wall = (x, y) => Wallish(at(x, y));
            System.Func<int, int, bool> inside = (x, y) => x >= 0 && y >= 0 && x < g.w && y < g.h;
            var wallSide = ColMat(Data.Hex("#23264f"));
            var trim = ColMat(Data.Hex("#9d8cff"), true);
            var wallTop = TexMat(Art.Get("tile_wall"), false);
            var ventTop = TexMat(Art.Get(WorldView.Pick("tile_vent", "tile_wall")), false);
            var furnSide = new Dictionary<char, Material>
            {
                { 'T', ColMat(Data.Hex("#4a3a30")) }, { 'S', ColMat(Data.Hex("#2a3238")) }, { 'V', ColMat(Data.Hex("#5a2028")) }, { 'N', ColMat(Data.Hex("#3a2e22")) },
                { 'M', ColMat(Data.Hex("#2c3340")) }, { 'C', ColMat(Data.Hex("#252b33")) }, { 'E', ColMat(Data.Hex("#1f3a33")) },
            };
            var furnH = new Dictionary<char, float> { { 'T', 0.5f }, { 'S', 1.15f }, { 'V', 1.25f }, { 'N', 1.05f }, { 'M', 0.95f }, { 'C', 0.72f }, { 'E', 1.3f } };
            var glass = ColMat(new Color(150 / 255f, 215 / 255f, 235 / 255f, 0.22f));

            for (int y = 0; y < g.h; y++) for (int x = 0; x < g.w; x++)
            {
                char ch = g.grid[y, x];
                var z = g.ZoneAt(x + 0.5f, y + 0.5f);
                string kind = z != null ? z.kind : "hall";

                if (Wallish(ch))
                {
                    // 벽 상자: 윗면 + 바닥 쪽으로 드러난 옆면. 남쪽(카메라 쪽) 면에는 벽 패널 그림
                    Top(ch == 'v' ? ventTop : wallTop, x, y, x + 1, y + 1, WALL_H, UV(Art.Get(ch == 'v' ? WorldView.Pick("tile_vent", "tile_wall") : "tile_wall")));
                    bool s = inside(x, y + 1) && !wall(x, y + 1), e = inside(x + 1, y) && !wall(x + 1, y), w = inside(x - 1, y) && !wall(x - 1, y);
                    Material front = wallSide; Rect fuv = FULL;
                    if (s)
                    {
                        var zs = g.ZoneAt(x + 0.5f, y + 1.5f);
                        if ((zs == null || zs.kind != "roof") && WorldView.FACE_OK.IndexOf(at(x, y + 1)) >= 0)
                        {
                            string fk; bool redFix;
                            if (WorldView.IsFixture(g, x, y + 1, out redFix)) fk = redFix ? "wall_face_alarm" : "wall_face_light";
                            else { int r = WorldView.Hash(x, y + 1, salt + 5); fk = r < 6 ? "wall_face_poster" : r < 12 ? "wall_face_note" : "wall_face"; }
                            var spr = Art.Get(WorldView.Pick(fk, "wall_face"));
                            front = TexMat(spr, false); fuv = UV(spr);
                        }
                    }
                    Sides(front, wallSide, x, y, x + 1, y + 1, 0, WALL_H, s, e, w, fuv);
                    // 라벤더 테두리 (바닥과 맞닿은 윗면 가장자리)
                    const float tw = 0.07f, th = WALL_H + 0.012f;
                    if (s) Top(trim, x, y + 1 - tw, x + 1, y + 1, th, FULL);
                    if (inside(x, y - 1) && !wall(x, y - 1)) Top(trim, x, y, x + 1, y + tw, th, FULL);
                    if (e) Top(trim, x + 1 - tw, y, x + 1, y + 1, th, FULL);
                    if (w) Top(trim, x, y, x + tw, y + 1, th, FULL);
                    continue;
                }

                // 바닥
                var fspr = Art.Get(WorldView.Variant("tile_floor_" + kind, x, y, salt, WorldView.FLOOR_V, WorldView.FLOOR_W));
                Top(TexMat(fspr, false), x, y, x + 1, y + 1, 0, UV(fspr));

                // 가구
                string key = null;
                switch (ch)
                {
                    case 'S': key = WorldView.Variant("tile_shelf", x, y, salt + 1, WorldView.SHELF_V, WorldView.SHELF_W); break;
                    case 'V': key = "tile_vending"; break;
                    case 'T':
                        key = at(x, y + 1) == 'T' && Art.Has("tile_table_inner")
                            ? WorldView.Variant("tile_table_inner", x, y, salt + 2, WorldView.INNER_V, WorldView.INNER_W)
                            : WorldView.Variant("tile_table", x, y, salt + 2, WorldView.TABLE_V, WorldView.TABLE_W);
                        break;
                    case 'N': key = "tile_notice"; break;
                    case 'M': key = WorldView.Variant("tile_machine", x, y, salt + 3, WorldView.MACH_V, WorldView.MACH_W); break;
                    case 'C': key = "tile_console"; break;
                    case 'E': key = "tile_lift"; break;
                    case '>': case '<':
                        { var sp = Art.Get("tile_stairs"); Top(TexMat(sp, true), x, y, x + 1, y + 1, 0.01f, UV(sp)); }
                        break;
                    case 'G':
                        Top(glass, x + 0.35f, y, x + 0.65f, y + 1, 1.0f, FULL);
                        Sides(glass, glass, x + 0.35f, y, x + 0.65f, y + 1, 0, 1.0f, true, true, true, FULL);
                        break;
                }
                if (key != null && IsTankTile(g, x, y, salt)) { BuildTank(x, y, salt); key = null; }   // 실험용 수조
                if (key != null)
                {
                    float h = furnH[ch]; bool joinS = at(x, y + 1) == ch, joinE = at(x + 1, y) == ch, joinW = at(x - 1, y) == ch;
                    float ins = ch == 'T' ? 0 : 0.04f;   // 책상은 이어 붙이고 나머지는 살짝 띄운다
                    float x0 = x + (joinW ? 0 : ins), x1 = x + 1 - (joinE ? 0 : ins), y0 = y + ins, y1 = y + 1 - (joinS ? 0 : ins);
                    var spr = Art.Get(key);
                    Top(TexMat(spr, false), x0, y0, x1, y1, h, UV(spr));
                    Sides(furnSide[ch], furnSide[ch], x0, y0, x1, y1, 0, h, !joinS || ins > 0, !joinE || ins > 0, !joinW || ins > 0, FULL);
                }
            }
            // 큰 설비
            foreach (var f in g.def.fill)
            {
                if (f.ch == 'A')
                {
                    var spr = Art.Get("big_aircon"); var side = ColMat(Data.Hex("#3a4048"));
                    Top(TexMat(spr, false), f.x, f.y, f.x + f.w, f.y + f.h, 1.0f, UV(spr));
                    Sides(side, side, f.x, f.y, f.x + f.w, f.y + f.h, 0, 1.0f, true, true, true, FULL);
                }
                else if (f.ch == 'W')
                {
                    var spr = Art.Get("big_tank");
                    Top(TexMat(spr, true), f.x, f.y, f.x + f.w, f.y + f.h, 0.02f, UV(spr));
                    var tank = ColMat(new Color(0.35f, 0.86f, 1f, 0.28f));
                    Top(tank, f.x + 0.15f, f.y + 0.15f, f.x + f.w - 0.15f, f.y + f.h - 0.15f, 1.6f, FULL);
                    Sides(tank, tank, f.x + 0.15f, f.y + 0.15f, f.x + f.w - 0.15f, f.y + f.h - 0.15f, 0, 1.6f, true, true, true, FULL);
                }
            }
            if (g.def.pad.HasValue) { var p = g.def.pad.Value; var spr = Art.Get("helipad"); Top(TexMat(spr, true), p.x, p.y, p.x + p.w, p.y + p.h, 0.012f, UV(spr)); }
            if (g.def.tank.HasValue) { var t = g.def.tank.Value; var spr = Art.Get("tank_broken"); Top(TexMat(spr, true), t.x - 2, t.y - 2, t.x + 2, t.y + 2, 0.014f, UV(spr)); }
            Flush(floorRoot);

            // 바닥 글씨
            foreach (var z in g.zones)
            {
                if (!z.label || z.x1 - z.x0 < 4) continue;
                Label(z.name, (z.x0 + z.x1 + 1) / 2f, z.y1 + 0.35f, 0.6f, new Color(220 / 255f, 235 / 255f, 230 / 255f, 0.12f));
            }
            foreach (var s in g.def.stairs) Label((s.dir == '>' ? "▲ " : "▼ ") + s.to, s.x + s.w / 2f, s.y + s.h / 2f, 0.4f, Data.Hex("#7fe0b0"));
        }

        void Label(string text, float x, float y, float size, Color col)
        {
            var go = new GameObject("Label_" + text); go.transform.SetParent(floorRoot, false);
            go.transform.position = P(x, 0.02f, y); go.transform.rotation = Quaternion.Euler(90, 0, 0);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Fonts.Main; tm.text = text; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.fontSize = 64; tm.characterSize = size / 6.4f; tm.color = col;
            go.GetComponent<MeshRenderer>().sharedMaterial = Fonts.Main.material;
        }

        /* ===== 조명 ===== */
        void BuildLights()
        {
            var g = G;
            // 방 조명 (천장 높이)
            foreach (var l in g.lights)
            {
                var col = l.tint.a > 0 ? Color.Lerp(new Color(0.8f, 0.86f, 1f), new Color(l.tint.r, l.tint.g, l.tint.b), 0.65f) : new Color(0.8f, 0.86f, 1f);
                float r = l.r * Cfg.roomLightR * ROOM_LIGHT_R + 1f, inten = l.i * Cfg.roomLightI * ROOM_LIGHT_I;
                AddLamp(l, P(l.x, WALL_H + 1.1f, l.y), r, inten, col);
            }
            // 소품 조명 (벽 천장등, 모니터, 자판기, 수조, 엘리베이터)
            var props = new List<LightSrc>(); var glows = new List<WorldView.Glow>();
            WorldView.CollectProps(g, props, glows);
            foreach (var gl in glows)
            {
                var c = new Color(gl.c.r, gl.c.g, gl.c.b);
                AddLamp(null, P(gl.x, 1.1f, gl.y + 0.1f), gl.r * PROP_LIGHT_R, PROP_LIGHT_I * Mathf.Clamp01(gl.c.a * 3f), c);
            }
            // 천장등 패널 (벽 앞면에 붙은 발광 띠)
            for (int y = 0; y < g.h; y++) for (int x = 0; x < g.w; x++)
            {
                bool red;
                if (!WorldView.IsFixture(g, x, y, out red)) continue;
                var mr = Box(floorRoot, ColMat(red ? new Color(1f, 0.45f, 0.5f) : new Color(0.6f, 0.92f, 1f), true), x + 0.5f, y + 0.02f, 0.5f, 0.05f, 0.08f, WALL_H - 0.4f);
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        void AddLamp(LightSrc src, Vector3 pos, float range, float inten, Color col)
        {
            var go = new GameObject("Lamp"); go.transform.SetParent(floorRoot, false); go.transform.position = pos;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.range = range; l.intensity = inten; l.color = col; l.shadows = LightShadows.None;
            lamps.Add(new LampV { l = l, src = src != null && src.flick != 0 ? src : null, baseI = inten });
        }
        // 세운 그림(사람·주인공)의 색: 그 자리에 실제로 닿는 빛(바탕 + 켜진 조명들 + 주인공 빛)을 계산해서 곱한다.
        // 그래서 붉은 경보, 깜빡이는 등, 수조의 푸른 빛이 사람에게도 똑같이 묻는다.
        Color Tint(float x, float y, float a = 1)
        {
            var pos = P(x, 0.9f, y);
            Color c = RenderSettings.ambientLight * 1.15f;
            foreach (var lp in lamps) if (lp.l.enabled) c += LightOn(lp.l, pos);
            if (playerLight != null && playerLight.enabled) c += LightOn(playerLight, pos);
            // 너무 밝으면 하얗게 날아가므로 부드럽게 눌러 줌
            c = new Color(Soft(c.r), Soft(c.g), Soft(c.b), a);
            return c;
        }
        static float Soft(float v) { return v < 0.8f ? v : 0.8f + 0.2f * (1 - Mathf.Exp(-(v - 0.8f) * 3f)); }
        // 유니티 기본 점광원 감쇠와 비슷하게
        static Color LightOn(Light l, Vector3 pos)
        {
            var d = l.transform.position - pos; float t = d.magnitude / Mathf.Max(0.01f, l.range);
            if (t >= 1) return Color.clear;
            float att = 1f / (1f + 25f * t * t) * Mathf.Clamp01((1 - t) * 4f);
            return l.color * (l.intensity * att * 0.9f);
        }

        /* ===== 그림 조각 ===== */
        SpriteRenderer Spr(Transform parent, string key)
        {
            var go = new GameObject(key); go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = Art.Get(key); sr.sharedMaterial = spriteMat;
            sr.shadowCastingMode = ShadowCastingMode.Off; sr.receiveShadows = false;
            return sr;
        }
        // 바닥에 눕힌 그림
        SpriteRenderer Flat(Transform parent, string key, float size)
        {
            var sr = Spr(parent, key);
            sr.transform.rotation = Quaternion.Euler(90, 0, 0);
            sr.transform.localScale = Vector3.one * size;
            return sr;
        }
        // 세운 그림 (카메라 쪽을 봄)
        SpriteRenderer Stand(Transform parent, string key) { return Spr(parent, key); }
        // 발이 (x,y)에 닿게 세운다. pxPerTile: 그림 몇 픽셀이 1칸인지
        void PlaceStand(SpriteRenderer sr, float x, float y, float scale, float lift = 0) { PlaceStand(sr, x, y, scale, lift, WorldView.Still); }
        void PlaceStand(SpriteRenderer sr, float x, float y, float scale, float lift, WorldView.Pose pz)
        {
            var spr = sr.sprite; if (spr == null) return;
            float s = scale;
            sr.transform.rotation = Quaternion.Euler(0, 0, pz.lean);
            sr.transform.localScale = new Vector3(s * pz.sx, s * BB_Y * pz.sy, s);
            float bottom = spr.bounds.min.y * s * BB_Y * pz.sy;   // 피벗 아래로 내려간 만큼 올린다
            sr.transform.position = P(x + pz.dx, lift - bottom, y);
        }

        MeshFilter NewMesh(string name, Material mat)
        {
            var go = new GameObject(name); go.transform.SetParent(dynRoot, false);
            var mf = go.AddComponent<MeshFilter>(); var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            var m = new Mesh(); m.MarkDynamic(); mf.sharedMesh = m;
            return mf;
        }
        LineRenderer NewLine()
        {
            var go = new GameObject("Line"); go.transform.SetParent(dynRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = spriteMat; lr.positionCount = 2; lr.useWorldSpace = true; lr.numCapVertices = 2;
            lr.shadowCastingMode = ShadowCastingMode.Off; lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }
        T Pooled<T>(List<T> pool, int i, System.Func<T> make) where T : Component
        {
            while (pool.Count <= i) pool.Add(make());
            var c = pool[i]; c.gameObject.SetActive(true); return c;
        }
        static void HideFrom<T>(List<T> pool, int n) where T : Component { for (int i = n; i < pool.Count; i++) if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false); }

        /* ===== 변신할 수 있는 물건의 3D 모형 ===== */
        // Resources/Models/obj_<이름> 프리팹이 있으면 그걸 쓰고, 없으면 상자로 만든 임시 모형.
        // 모형 기준: 바닥 가운데가 원점, 1칸 크기, 앞(+z)이 북쪽.
        readonly Dictionary<string, List<Transform>> models = new Dictionary<string, List<Transform>>();
        readonly Dictionary<string, int> modelUsed = new Dictionary<string, int>();
        Transform playerModel; string playerModelForm;
        Mesh prism;
        void BeginModels() { var keys = new List<string>(modelUsed.Keys); foreach (var k in keys) modelUsed[k] = 0; }
        Transform Model(string form)
        {
            List<Transform> list; if (!models.TryGetValue(form, out list)) { list = new List<Transform>(); models[form] = list; }
            int used; modelUsed.TryGetValue(form, out used); modelUsed[form] = used + 1;
            while (list.Count <= used) list.Add(BuildModel(form));
            var t = list[used]; t.gameObject.SetActive(true); return t;
        }
        void EndModels()
        {
            foreach (var kv in models)
            {
                int used; modelUsed.TryGetValue(kv.Key, out used);
                for (int i = used; i < kv.Value.Count; i++) if (kv.Value[i].gameObject.activeSelf) kv.Value[i].gameObject.SetActive(false);
            }
        }
        Transform BuildModel(string form)
        {
            var prefab = Resources.Load<GameObject>("Models/obj_" + form);
            if (prefab != null) { var inst = Instantiate(prefab, dynRoot); inst.name = "obj_" + form; return inst.transform; }
            var root = new GameObject("obj_" + form).transform; root.SetParent(dynRoot, false);
            if (form == "chair")
            {
                Material seat = ColMat(Data.Hex("#4f5f78")), metal = ColMat(Data.Hex("#262b33")), pad = ColMat(Data.Hex("#3e4a5e"));
                Part(root, metal, 0, 0, 0, 0.08f, 0.38f, 0.08f);                     // 기둥
                Part(root, metal, 0, 0, 0, 0.5f, 0.05f, 0.08f); Part(root, metal, 0, 0, 0, 0.08f, 0.05f, 0.5f);   // 다리 (십자)
                Part(root, seat, 0, 0.38f, 0, 0.5f, 0.08f, 0.48f);                  // 앉는 곳
                Part(root, metal, 0, 0.46f, 0.21f, 0.06f, 0.14f, 0.05f);             // 등받이 받침
                Part(root, pad, 0, 0.58f, 0.22f, 0.48f, 0.42f, 0.07f);              // 등받이
            }
            else if (form == "trash")
            {
                Material body = ColMat(Data.Hex("#5e6d7a")), rim = ColMat(Data.Hex("#363f49")), inside = ColMat(Data.Hex("#14171b"));
                Cyl(root, body, 0, 0, 0, 0.21f, 0.56f);
                Cyl(root, rim, 0, 0.54f, 0, 0.235f, 0.06f);
                Cyl(root, inside, 0, 0.6f, 0, 0.19f, 0.004f);
                Part(root, rim, 0, 0.2f, -0.21f, 0.12f, 0.1f, 0.03f);                 // 앞 라벨
            }
            else // cart
            {
                Material frame = ColMat(Data.Hex("#8f9ba8")), tray = ColMat(Data.Hex("#35627f")), wheel = ColMat(Data.Hex("#16191d")), box = ColMat(Data.Hex("#c9d3da"));
                foreach (var sx in new[] { -0.33f, 0.33f }) foreach (var sz in new[] { -0.19f, 0.19f })
                {
                    Part(root, wheel, sx, 0, sz, 0.09f, 0.09f, 0.05f);
                    Part(root, frame, sx, 0.08f, sz, 0.04f, 0.5f, 0.04f);
                }
                Part(root, tray, 0, 0.17f, 0, 0.74f, 0.05f, 0.44f);                 // 아래 선반
                Part(root, tray, 0, 0.55f, 0, 0.8f, 0.07f, 0.48f);                  // 위 선반
                Part(root, frame, -0.42f, 0.55f, -0.17f, 0.04f, 0.36f, 0.04f); Part(root, frame, -0.42f, 0.55f, 0.17f, 0.04f, 0.36f, 0.04f);
                Part(root, frame, -0.42f, 0.89f, 0, 0.05f, 0.05f, 0.42f);           // 손잡이
                Part(root, box, 0.12f, 0.62f, 0.05f, 0.24f, 0.13f, 0.18f);          // 위에 올린 상자
            }
            return root;
        }
        // 모형 조각: 로컬 (x, 바닥 높이, z)에 너비 w, 높이 h, 깊이 d
        void Part(Transform root, Material m, float x, float h0, float z, float w, float h, float d)
        {
            var go = new GameObject("part"); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = unitBox;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            go.transform.localPosition = new Vector3(x, h0, z); go.transform.localScale = new Vector3(w, h, d);
        }
        void Cyl(Transform root, Material m, float x, float h0, float z, float r, float h)
        {
            if (prism == null) prism = MakePrism(12);
            var go = new GameObject("part"); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = prism;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            go.transform.localPosition = new Vector3(x, h0, z); go.transform.localScale = new Vector3(r * 2, h, r * 2);
        }
        // 지름 1, 높이 1, 바닥 가운데 원점인 n각 기둥 (옆면 + 윗면)
        static Mesh MakePrism(int n)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            System.Func<float, float, Vector3> pt = (a, h) => new Vector3(Mathf.Sin(a) * 0.5f, h, Mathf.Cos(a) * 0.5f);
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2 / n, a1 = (i + 1) * Mathf.PI * 2 / n;
                int k = v.Count;   // 바깥에서 볼 때 시계 방향: 왼위(a1) 오른위(a0) 오른아래(a0) 왼아래(a1)
                v.Add(pt(a1, 1)); v.Add(pt(a0, 1)); v.Add(pt(a0, 0)); v.Add(pt(a1, 0));
                t.Add(k); t.Add(k + 1); t.Add(k + 2); t.Add(k); t.Add(k + 2); t.Add(k + 3);
                k = v.Count;       // 윗면 (위에서 볼 때 시계 방향)
                v.Add(new Vector3(0, 1, 0)); v.Add(pt(a0, 1)); v.Add(pt(a1, 1));
                t.Add(k); t.Add(k + 1); t.Add(k + 2);
            }
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /* ===== 화면 질문 (Hud가 씀) ===== */
        public bool InView(float x, float y, float m)
        {
            if (cam == null) return false;
            var v = cam.WorldToViewportPoint(P(x, 0.5f, y));
            float k = 0.04f + m * 0.05f;
            return v.z > 0 && v.x > -k && v.x < 1 + k && v.y > -k && v.y < 1 + k;
        }
        public Vector3 HeadScreen(float x, float y, float extra) { return cam.WorldToScreenPoint(P(x, HeadH + extra * 0.8f, y)); }

        /* ===== 매 프레임 ===== */
        float zoom = 1f;
        void UpdateCamera(bool playing)
        {
            var S = game.S;
            float target = 1f;
            if (playing && game.visValid)
            {
                target = Mathf.Lerp(0.82f, 1.1f, Mathf.InverseLerp(2.5f, 7.5f, game.visAvg));
                if (game.PlayerHidden()) target = 0.75f;
                if (S.spot > 0 || game.AnyLocked()) target *= 0.94f;
                if (S.player.eating != null) target *= 0.88f;
            }
            zoom += (target - zoom) * Mathf.Min(1, Time.deltaTime * 2.2f);
            WorldView.Zoom = zoom;
            Vector3 shake = Vector3.zero;
            if (game.mode == Mode.Dying) shake = new Vector3(U.Rand(-3, 3), 0, U.Rand(-3, 3)) / 32f;
            if (S.alarm != null && S.time - S.alarm.started < 0.4f) shake += new Vector3(U.Rand(-2, 2), 0, U.Rand(-2, 2)) / 32f;
            var look = P(S.cam.x + Cfg.VTW / 2, 0, S.cam.y + Cfg.VTH / 2) + shake;
            cam.transform.rotation = Quaternion.Euler(PITCH, 0, 0);
            cam.transform.position = look - cam.transform.forward * (DIST * zoom);
        }

        public void Render()
        {
            if (game == null || game.S == null || G == null || player == null) return;
            var S = game.S; var F = S.Fl; var p = S.player; float t = S.time;
            bool playing = game.mode != Mode.Title && game.mode != Mode.Intro;

            UpdateCamera(playing);
            UpdateLights(t, playing);
            UpdateFog(playing);

            // 문
            for (int i = 0; i < doors.Count; i++)
            {
                var d = G.def.doors[i]; var st = F.doors[i]; var v = doors[i];
                bool open = st.open || st.temp > 0;
                float want = open ? 0.04f : WALL_H;
                v.h += (want - v.h) * Mathf.Min(1, Time.deltaTime * 10);
                v.box.localScale = new Vector3(0.98f, Mathf.Max(0.02f, v.h), 0.98f);
                v.led.transform.position = P(d.x + 0.5f, v.h + 0.01f, d.y + 0.5f);
                v.led.sharedMaterial.color = open ? (st.open ? Data.Hex("#7fe0b0") : Data.Cards[d.req].color) : (game.HasCard(d.req) ? Data.Hex("#7fe0b0") : Data.Hex("#ff5148"));
            }
            // 함정
            foreach (var tv in traps)
            {
                bool off = S.trapsOff.Contains(tv.def.id);
                if (tv.def.kind == "laser")
                {
                    for (int i = 0; i < tv.bars.Count; i++) tv.bars[i].enabled = !off;
                    tv.mat.color = new Color(1, 60 / 255f, 60 / 255f, 0.6f + 0.35f * Mathf.Sin(t * 20));
                }
                else
                    for (int i = 0; i < tv.gas.Count; i++)
                    {
                        var c = tv.cells[i]; float ph = t * 1.3f + c.x * 1.7f + c.y * 2.9f;
                        tv.gas[i].transform.position = P(c.x + 0.5f + 0.25f * Mathf.Sin(ph), 0.25f + 0.1f * Mathf.Sin(ph * 0.7f), c.y + 0.5f + 0.25f * Mathf.Cos(ph * 0.8f));
                        tv.gas[i].color = off ? new Color(160 / 255f, 200 / 255f, 140 / 255f, 0.05f) : new Color(150 / 255f, 230 / 255f, 90 / 255f, 0.2f + 0.08f * Mathf.Sin(ph));
                    }
            }
            for (int i = 0; i < consoleLeds.Count; i++)
            {
                var c = G.def.consoles[i];
                bool done = c.heli ? S.heli != null : S.trapsOff.Contains(c.trap);
                consoleLeds[i].sharedMaterial.color = done ? Data.Hex("#7fe0b0") : new Color(1, 81 / 255f, 72 / 255f, 0.6f + 0.4f * Mathf.Sin(t * 5));
            }
            foreach (var led in liftLeds) led.sharedMaterial.color = S.alarm != null ? new Color(1, 81 / 255f, 72 / 255f, 0.5f + 0.5f * Mathf.Sin(t * 8)) : Data.Hex("#7fe0b0");

            UpdateBubbles(t);

            // 점액 자국
            int ti = 0;
            foreach (var tr in S.trail)
            {
                var sr = Pooled(trailPool, ti++, () => Flat(dynRoot, "fx_dot", 1));
                sr.transform.position = P(tr.x, 0.015f, tr.y); sr.transform.localScale = Vector3.one * (tr.r * 2 / 32f);
                sr.color = new Color(1, 125 / 255f, 140 / 255f, 0.22f * tr.t / 3.5f);
            }
            HideFrom(trailPool, ti);

            // 물건 (3D 모형: 의자, 쓰레기통, 카트)
            BeginModels();
            foreach (var o in F.objects)
            {
                if (!o.alive || !InView(o.x, o.y, 1)) continue;
                var m = Model(o.form);
                float s = 1;
                if (p.eating != null && !p.eating.target.isNpc && p.eating.target.obj == o) s = Mathf.Max(0.05f, 1 - p.eating.t / Cfg.eatTime);
                m.position = P(o.x, 0, o.y);
                m.rotation = Quaternion.Euler(0, o.form == "chair" ? o.rot * Mathf.Rad2Deg - 90 : 0, 0);
                m.localScale = Vector3.one * s;
            }
            EndModels();

            // 사람 (세운 그림 + 발밑 그림자)
            int ni = 0;
            foreach (var n in F.npcs)
            {
                if (!n.alive || !InView(n.x, n.y, 1)) continue;
                if (playing && !game.PlayerCanSee(n.x, n.y)) continue;
                var sr = Pooled(npcPool, ni, () => Stand(dynRoot, "stand_researcher_front"));
                var sh = Pooled(shadowPool, ni, () => Flat(dynRoot, "fx_dot", 0.8f)); ni++;
                bool moving = !n.frozen && ((n.path != null && n.path.Count > 0) || n.state == NpcState.Chase);
                string kind = WorldView.Variant(n.pal, n.id);
                float sc0;
                WorldView.ApplyStand(sr, kind, n.face, out sc0, moving ? t * (n.state == NpcState.Chase ? 1.5f : 1f) + (n.id.Length % 4) * 0.13f : -1);
                float s = 1;
                if (p.eating != null && p.eating.target.isNpc && p.eating.target.npc == n) s = Mathf.Max(0.05f, 1 - p.eating.t / Cfg.eatTime);
                float ph = t * (n.state == NpcState.Chase ? 18 : 12) + n.id.GetHashCode() % 7;
                float bob = moving && !WorldView.HasWalk(kind) ? Mathf.Abs(Mathf.Sin(ph)) * 0.07f : 0;
                PlaceStand(sr, n.x, n.y, s * sr.sprite.pixelsPerUnit / WorldView.CHAR_PX, bob, WorldView.LastPose);
                sr.color = Tint(n.x, n.y);
                sh.transform.position = P(n.x, 0.02f, n.y); sh.transform.localScale = new Vector3(0.75f, 0.45f, 1) * s; sh.color = new Color(0, 0, 0, 0.45f);
            }
            HideFrom(npcPool, ni); HideFrom(shadowPool, ni);

            // 주인공
            player.gameObject.SetActive(playing); playerShadow.gameObject.SetActive(playing); playerXray.gameObject.SetActive(false);
            if (playing) DrawPlayer(t);

            DrawHeli(t);

            // 경보: 전체 빛이 붉게 맥동
            RenderSettings.ambientLight = S.alarm != null ? Color.Lerp(AMBIENT, new Color(0.5f, 0.04f, 0.06f), 0.45f + 0.35f * Mathf.Sin(t * 6)) : AMBIENT;

            // 시야 부채꼴 (바닥에 깔림)
            int ci = 0;
            foreach (var n in F.npcs)
            {
                if (!n.alive || !InView(n.x, n.y, game.ViewDist(n) + 1)) continue;
                var mf = Pooled(conePool, ci++, () => NewMesh("Cone", spriteMat));
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
                lr.SetPosition(0, P(n.x, 1.0f * BB_Y, n.y)); lr.SetPosition(1, P(p.x, 0.4f, p.y));
                lr.widthMultiplier = (1 + 2 * k2) / 32f;
                lr.startColor = lr.endColor = new Color(1, 40 / 255f, 40 / 255f, 0.25f + 0.65f * k2);
                var ring = Pooled(aimRingPool, ai, () => Flat(dynRoot, "fx_ring", 1));
                ring.transform.position = P(p.x, 0.03f, p.y); ring.transform.localScale = Vector3.one * ((22 - 10 * k2) * 2 / 32f);
                ring.color = new Color(1, 40 / 255f, 40 / 255f, 0.5f + 0.5f * k2);
                ai++;
            }
            HideFrom(aimPool, ai); HideFrom(aimRingPool, ai);
            bool shot = game.mode == Mode.Dying && S.deathKind == "shot" && S.shotFrom.HasValue;
            shotLine.enabled = shot;
            if (shot) { shotLine.SetPosition(0, P(S.shotFrom.Value.x, 1.0f * BB_Y, S.shotFrom.Value.y)); shotLine.SetPosition(1, P(p.x, 0.4f, p.y)); }

            // 먹을 대상 표시
            var tgt = game.mode == Mode.Play && p.eating == null ? game.NearestEdible() : null;
            targetRing.gameObject.SetActive(tgt != null);
            if (tgt != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 6);
                bool blocked = S.anySee && game.OthersSee(tgt.Ref);
                targetRing.transform.position = P(tgt.X, 0.035f, tgt.Y);
                targetRing.color = blocked ? new Color(1, 81 / 255f, 72 / 255f, 0.5f + pulse * 0.4f) : new Color(1, 125 / 255f, 140 / 255f, 0.5f + pulse * 0.4f);
            }

            // 파티클 (카메라를 보게)
            int fi = 0;
            foreach (var pt in S.particles)
            {
                var sr = Pooled(fxPool, fi++, () => Spr(dynRoot, "fx_dot"));
                sr.transform.position = P(pt.x, 0.35f, pt.y); sr.transform.rotation = cam.transform.rotation;
                sr.transform.localScale = Vector3.one * (pt.r * 2 / 32f);
                var c = pt.color; c.a = Mathf.Max(0, pt.life / pt.max); sr.color = c;
            }
            HideFrom(fxPool, fi);
        }

        void UpdateLights(float t, bool playing)
        {
            var S = game.S;
            Vector3 c = cam.transform.position + cam.transform.forward * (DIST * zoom);   // 화면 가운데 바닥
            foreach (var lp in lamps)
            {
                var pos = lp.l.transform.position;
                bool near = Mathf.Abs(pos.x - c.x) < 18 + lp.l.range && Mathf.Abs(pos.z - c.z) < 13 + lp.l.range;
                if (lp.l.enabled != near) lp.l.enabled = near;
                if (near && lp.src != null) lp.l.intensity = lp.baseI * WorldView.LightLevel(lp.src, t) / Mathf.Max(0.01f, lp.src.i);
            }
            playerLight.enabled = playing;
            if (playing) playerLight.transform.position = P(S.player.x, 1.1f, S.player.y);
        }

        bool faceLeft; WorldView.Pose humanPose = WorldView.Still;
        void DrawPlayer(float t)
        {
            var S = game.S; var p = S.player; var f = Data.Forms[p.form];
            bool hidden = game.PlayerHidden();
            player.flipX = false; player.color = Color.white;
            float a = hidden ? 0.5f : 1f;
            int dir = Art.DirIndex(p.face);
            string dn = dir == 1 ? "down" : dir == 3 ? "up" : "side";
            if (dir == 0) faceLeft = false; else if (dir == 2) faceLeft = true;
            playerShadow.transform.position = P(p.x, 0.02f, p.y);
            playerShadow.transform.localScale = new Vector3(0.85f, 0.5f, 1);

            bool asObj = f.type == FormType.Object;
            if (playerModel != null && (!asObj || playerModelForm != p.form)) { Destroy(playerModel.gameObject); playerModel = null; }
            if (asObj)
            {
                // 물건으로 변신: 3D 모형 (변신이 거의 풀리면 덜덜 떨림, 움직이면 덜컹)
                if (playerModel == null) { playerModel = BuildModel(p.form); playerModelForm = p.form; }
                Vector2 j = Vector2.zero;
                bool warn = p.timer < f.dur * 0.25f;
                if (warn) j = new Vector2(Mathf.Sin(t * 43) * 1.2f, Mathf.Cos(t * 37)) / 32f;
                float bump = p.speedNow > 0.3f ? Mathf.Abs(Mathf.Sin(t * 22)) * 0.03f : 0;
                playerModel.position = P(p.x + j.x, bump, p.y + j.y);
                float yaw = p.form == "trash" ? 0 : p.face * Mathf.Rad2Deg + (p.form == "chair" ? -90 : 0);   // 카트는 손잡이가, 의자는 등받이가 뒤로
                playerModel.rotation = Quaternion.Euler(0, yaw, warn ? Mathf.Sin(t * 40) * 3f : 0);
                player.gameObject.SetActive(false); playerShadow.gameObject.SetActive(false);
                return;
            }
            float scale = 1;
            if (f.type == FormType.Human)
            {
                float sc0; WorldView.ApplyStand(player, p.form, p.face, out sc0, p.speedNow > 0.3f ? t : -1);
                scale = player.sprite.pixelsPerUnit / WorldView.CHAR_PX; humanPose = WorldView.LastPose;
                bool warn = p.timer < f.dur * 0.25f;
                player.color = warn ? Color.Lerp(Tint(p.x, p.y), new Color(1, 0.6f, 0.65f), 0.5f + 0.5f * Mathf.Sin(t * 8)) : Tint(p.x, p.y);
            }
            else
            {
                var idleF = WorldView.PlayerFrames("idle", dn);
                var crawlF = WorldView.PlayerFrames("crawl", dn); var eatF = WorldView.PlayerFrames("eat", dn);
                Sprite spr = null;
                if (p.eating != null && eatF.Count > 0) spr = eatF[Mathf.Min(eatF.Count - 1, Mathf.FloorToInt(p.eating.t / Cfg.eatTime * eatF.Count))];
                else if (p.speedNow > 0.3f && crawlF.Count > 0) spr = crawlF[Mathf.FloorToInt(t * 10) % crawlF.Count];
                else if (idleF.Count > 0) spr = idleF[Mathf.FloorToInt(t * 2.5f) % idleF.Count];
                if (spr != null) { player.sprite = spr; player.flipX = dn == "side" && faceLeft; scale = spr.pixelsPerUnit / WorldView.TILE_PX; }
                else player.sprite = Art.Get("player_blob");
                if (hidden) scale *= 0.8f;
                var lit = Color.Lerp(Tint(p.x, p.y), Color.white, 0.3f); lit.a = a;   // 주인공은 조금 더 잘 보이게
                player.color = lit;
            }
            PlaceStand(player, p.x, p.y, scale, 0, f.type == FormType.Human ? humanPose : WorldView.Still);
            if (hidden) playerShadow.gameObject.SetActive(false);
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
            float cx = pad.x + pad.w / 2f, cy = pad.y + pad.h / 2f, h = 0.6f + (1 - k) * 9f;
            float a = Mathf.Min(1, k * 1.5f);
            heli.transform.position = P(cx, h, cy); heli.color = new Color(1, 1, 1, a);
            heliRotor.transform.position = P(cx, h + 0.05f, cy); heliRotor.color = new Color(1, 1, 1, a);
            heliRotor.transform.rotation = Quaternion.Euler(90, t * 18 * Mathf.Rad2Deg, 0);
        }

        /* ===== 시야 밖 어둠 ===== */
        // 광선이 닿은 경계에서 바깥으로 짙어지는 판을 벽 위 높이에 깐다.
        // 카메라에서 바닥 점을 바라본 선이 그 높이를 지나는 곳에 꼭짓점을 둬서, 화면에서는 바닥 경계와 딱 맞는다.
        readonly List<Vector3> fv = new List<Vector3>(); readonly List<Color> fc = new List<Color>(); readonly List<int> ft = new List<int>();
        Vector3 FogPt(Vector3 camPos, float x, float y)
        {
            var g = P(x, 0, y); float k = (camPos.y - FOG_H) / camPos.y;
            return camPos + (g - camPos) * k;
        }
        void UpdateFog(bool playing)
        {
            bool on = playing && game.visValid;
            fog.gameObject.SetActive(on);
            if (!on) return;
            var p = game.S.player; int N = Game.VIS_RAYS; var cp = cam.transform.position;
            const float FOG_A = 0.86f;
            Color inner = new Color(5 / 255f, 6 / 255f, 22 / 255f, 0f), outer = new Color(5 / 255f, 6 / 255f, 22 / 255f, FOG_A);
            fv.Clear(); fc.Clear(); ft.Clear();
            for (int i = 0; i < N; i++)
            {
                float a = i * Mathf.PI * 2 / N, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float d = game.visDist[i];
                float din = d < game.visR - 0.05f ? d + 1.4f : d;     // 벽에 막혔으면 벽 앞면까지 보이게
                fv.Add(FogPt(cp, p.x + ca * din, p.y + sa * din)); fc.Add(inner);
                fv.Add(FogPt(cp, p.x + ca * (din + 1.1f), p.y + sa * (din + 1.1f))); fc.Add(outer);
                fv.Add(FogPt(cp, p.x + ca * 60, p.y + sa * 60)); fc.Add(outer);
            }
            for (int i = 0; i < N; i++)
            {
                int j = (i + 1) % N, a0 = i * 3, b0 = j * 3;
                // 위에서 볼 때 시계 방향이 되도록 (게임 y가 뒤집혀 있어 2D와 순서가 반대)
                ft.Add(a0); ft.Add(b0 + 1); ft.Add(b0); ft.Add(a0); ft.Add(a0 + 1); ft.Add(b0 + 1);
                ft.Add(a0 + 1); ft.Add(b0 + 2); ft.Add(b0 + 1); ft.Add(a0 + 1); ft.Add(a0 + 2); ft.Add(b0 + 2);
            }
            var m = fog.sharedMesh; m.Clear(); m.SetVertices(fv); m.SetColors(fc); m.SetTriangles(ft, 0); m.RecalculateBounds();
        }

        /* ===== 시야 부채꼴 ===== */
        readonly List<Vector3> cv = new List<Vector3>(); readonly List<Color> cc = new List<Color>(); readonly List<int> ct = new List<int>();
        void BuildCone(Mesh m, Npc n)
        {
            var S = game.S; var c = n.C;
            float half = Data.ShapeHalf(c.shape), vd = game.ViewDist(n);
            int N = c.shape == ViewShape.Circle ? 72 : c.shape == ViewShape.Half ? 48 : 34;
            bool danger = (S.spotNpc == n && S.spot > 0) || n.lockT > 0;
            bool hunting = n.state == NpcState.Chase;
            Color col = danger ? new Color(1, 81 / 255f, 72 / 255f) : hunting ? new Color(1, 140 / 255f, 90 / 255f) : new Color(243 / 255f, 176 / 255f, 74 / 255f);
            float a0 = danger ? 0.36f : n.seesPlayer ? 0.28f : (n.state == NpcState.Inspect || hunting) ? 0.24f : 0.16f;
            const float H = 0.03f;
            cv.Clear(); cc.Clear(); ct.Clear();
            cv.Add(P(n.x, H, n.y)); cc.Add(new Color(col.r, col.g, col.b, a0));
            for (int i = 0; i <= N; i++)
            {
                float a = n.face - half + (2 * half) * i / N, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float d = 0, lim = vd;
                if (c.shape == ViewShape.Circle && Mathf.Abs(U.AngDiff(a, n.face)) > Data.Rad(Cfg.rearBlindFrom)) lim = Cfg.rearRange;
                while (d < lim)
                {
                    float nd = d + 0.08f;
                    if (game.IsOpaque(U.Floor(n.x + ca * nd), U.Floor(n.y + sa * nd))) break;
                    d = nd;
                }
                cv.Add(P(n.x + ca * d, H, n.y + sa * d));
                cc.Add(new Color(col.r, col.g, col.b, Mathf.Lerp(a0, 0.03f, d / vd)));
                if (i > 0) { ct.Add(0); ct.Add(i); ct.Add(i + 1); }   // 위에서 볼 때 시계 방향
            }
            m.Clear(); m.SetVertices(cv); m.SetColors(cc); m.SetTriangles(ct, 0); m.RecalculateBounds();
        }
    }
}
