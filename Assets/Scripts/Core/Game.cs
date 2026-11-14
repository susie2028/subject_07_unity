// 게임 규칙 전부. 프로토타입 index.html의 "게임 로직" 부분을 함수 단위로 그대로 옮겼다.
// 화면(WorldView), UI(Hud), 소리(Sfx)는 여기서 호출만 한다 — 아트를 바꿔도 이 파일은 건드릴 필요가 없다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Specimen07
{
    public enum Mode { Title, Intro, Play, Lift, Dying, Over, Clear, Pause }

    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        public GameState S;
        public FloorGeo G;
        public Mode mode = Mode.Title;
        public int deaths;
        public bool showMini = true;
        SaveData checkpoint;

        public IGameView view;
        [Tooltip("켜면 2.5D(비스듬한 3D 카메라) 화면으로 그린다. Main_2_5D 씬에서 켜져 있음")]
        public bool use25D;
        public Hud hud;
        public Sfx sfx;

        FloorState Fl { get { return S.Fl; } }

        /* ================= 시작 ================= */
        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            sfx = gameObject.AddComponent<Sfx>();
            if (use25D) view = gameObject.AddComponent<WorldView3D>();
            else view = gameObject.AddComponent<WorldView>();
            hud = gameObject.AddComponent<Hud>();
        }

        void Start()
        {
            AudioListener.volume = 1f;   // 크기는 배경음악·효과음 설정이 따로 맡음
            showMini = Settings.Minimap;
            NewGame();
            EnterFloorVisual();
            // MainMenu 씬에서 넘어왔으면 그 요청대로 시작, 아니면(게임 씬을 바로 Play) 게임 안의 임시 메뉴
            var req = Launch.request; Launch.request = Launch.Request.None;
            if (req == Launch.Request.New) StartGame();
            else if (req == Launch.Request.Continue && HasSave()) Continue();
            else if (hud != null) hud.ShowTitle();
        }

        public void Later(float sec, Action a) { if (laterImmediate) a(); else StartCoroutine(LaterCo(sec, a)); }
        public bool laterImmediate; // 테스트용
        void Toast(string text) { if (hud != null) hud.Toast(text); }
        void Snd(string name) { if (sfx != null) sfx.Play(name); }
        IEnumerator LaterCo(float sec, Action a) { yield return new WaitForSeconds(sec); a(); }

        void NewGame()
        {
            S = new GameState();
            foreach (var id in Data.FloorOrder) S.floors[id] = FloorState.New(FloorGeo.All[id]);
            G = FloorGeo.All["B1"];
            var st = G.def.Arrive("start");
            S.floorId = "B1";
            S.player = new Player { x = st.x, y = st.y };
            S.visited.Add("B1");
            S.ResetTransient();
        }

        string lastVia = "start";
        bool runInSession; // 이번 실행 중에 시작한 판이 있는지 (있으면 이어하기가 메모리의 체크포인트를 씀)

        void SaveCheckpoint()
        {
            checkpoint = ((SaveData)S).Clone();
            SaveFile.Write(S, lastVia, deaths);
        }
        void LoadCheckpoint()
        {
            S.Load(checkpoint);
            S.player.eating = null;
            G = FloorGeo.All[S.floorId];
            EnterFloorVisual();
        }

        /* ================= 흐름 ================= */
        public void StartGame()
        {
            mode = Mode.Intro;
            if (hud != null) hud.ShowIntro();
        }

        public void FinishIntro()
        {
            if (mode != Mode.Intro) return;
            if (hud != null) hud.HideIntro();
            Snd("glass");
            if (hud != null) hud.Flash();
            NewRun();
            var st = G.def.Arrive("start");
            for (int i = 0; i < 26; i++) SpawnParticle(st.x, st.y, i % 3 != 0 ? Data.Hex("#9fd8d6") : Data.Hex("#ff8a98"), U.Rand(2, 6), U.Rand(0.5f, 1.1f), U.Rand(1.5f, 3.5f));
            Toast("쨍그랑.");
        }

        void BeginPlay()
        {
            mode = Mode.Play;
            if (hud != null) hud.OnBeginPlay();
        }

        public void NewRun()
        {
            deaths = 0;
            lastVia = "start";
            runInSession = true; pausedFromPlay = false;
            NewGame();
            EnterFloorVisual();
            SaveCheckpoint();
            BeginPlay();
            Later(0.9f, () => { if (mode == Mode.Play && S.floorId == "B1") if (hud != null) hud.Banner("B1", G.def.sub); });
        }

        public void Retry()
        {
            pausedFromPlay = false;
            if (checkpoint == null) { NewRun(); return; }
            if (mode == Mode.Play) deaths++;
            LoadCheckpoint();
            BeginPlay();
        }

        /* 메인 화면 */
        public bool HasSave() { return (runInSession && checkpoint != null) || SaveFile.Exists(); }

        public void Continue()
        {
            if (!HasSave()) return;
            if (sfx != null) sfx.Init();
            if (pausedFromPlay) { pausedFromPlay = false; BeginPlay(); return; }   // Esc로 나왔으면 그 자리에서 계속
            if (runInSession && checkpoint != null) { LoadCheckpoint(); BeginPlay(); return; }
            var f = SaveFile.Read();
            if (f == null || !FloorGeo.All.ContainsKey(f.floorId)) { SaveFile.Delete(); if (hud != null) hud.ShowTitle(); return; }
            runInSession = true;
            NewGame();
            deaths = f.deaths;
            S.cards = new List<string>(f.cards); S.trapsOff = new List<string>(f.trapsOff);
            S.visited = new List<string>(f.visited); S.seenForm = new List<string>(f.seenForm);
            S.elapsed = f.elapsed; S.alarms = f.alarms;
            if (f.floorId == "B1" && f.via == "start") { lastVia = "start"; EnterFloorVisual(); }
            else GoFloor(f.floorId, f.via);
            if (Data.Forms.ContainsKey(f.form) && !Data.Forms[f.form].Big) { S.player.form = f.form; S.player.timer = f.timer; }
            SaveCheckpoint();
            BeginPlay();
            UpdateCamera(0, true);
        }

        bool pausedFromPlay;

        /* 일시 정지 (MainMenu 씬이 있을 때 Esc) */
        public void Pause() { if (mode != Mode.Play) return; mode = Mode.Pause; if (hud != null) hud.ShowPause(); }
        public void Resume() { if (mode != Mode.Pause) return; if (hud != null) hud.HidePause(); mode = Mode.Play; }

        public void GoToMenu()
        {
            if (mode == Mode.Pause && hud != null) hud.HidePause();
            if (mode == Mode.Pause) mode = Mode.Play;
            if (Launch.MenuSceneInBuild)
            {
                // 진행은 마지막 층 도착 시점에 저장되어 있다. 메뉴에서 다시 시작하면 지금 씬(2D/2.5D)으로 돌아온다
                Launch.GameScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                UnityEngine.SceneManagement.SceneManager.LoadScene(Launch.MenuScene);
                return;
            }
            // 플레이 중에 나오면 이번 실행 동안은 그 자리에서 이어하기. 게임을 끄면 마지막 층 도착 시점부터.
            pausedFromPlay = mode == Mode.Play;
            if (pausedFromPlay && S.player.eating != null)
            {
                if (S.player.eating.target.isNpc) S.player.eating.target.npc.frozen = false;
                S.player.eating = null;
            }
            mode = Mode.Title;
            if (hud != null) hud.ShowTitle();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void EnterFloorVisual()
        {
            // 먹힌 책상·선반: 시간이 다 된 것은 이 층에 다시 들어올 때 제자리에 놓인다
            Fl.tiles.RemoveAll(t => t.respawnT <= 0);
            G.ApplyEaten(Fl.tiles); landCache.Clear();
            if (view != null) view.BuildFloor(G);
            var p = S.player;
            S.cam = new Vector2(p.x - Cfg.VTW / 2, p.y - Cfg.VTH / 2);
            if (hud != null) hud.OnFloorChanged();
        }

        void GoFloor(string id, string via)
        {
            lastVia = via;
            var p = S.player;
            S.floorId = id; G = FloorGeo.All[id];
            var a = G.def.Arrive(via);
            p.x = a.x; p.y = a.y; p.eating = null;
            if (Data.Forms[p.form].Big) { p.form = "body"; p.timer = 0; }   // 큰 가구 모습은 층을 옮기면 풀린다 (도착 자리가 좁을 수 있다)
            S.alarm = null; S.trail.Clear(); S.particles.Clear(); S.spot = 0; S.spotNpc = null; S.arriveT = Cfg.arriveGrace;
            foreach (var n in Fl.npcs) { n.state = NpcState.Patrol; n.path = null; n.lockT = 0; n.aim = 0; n.search = null; n.inspect = null; n.frozen = false; }
            Fl.npcs.RemoveAll(n => n.temp);
            if (hud != null) hud.Fade();
            EnterFloorVisual();
            bool first = !S.visited.Contains(id);
            if (first) S.visited.Add(id);
            if (hud != null) hud.Banner(id, first ? G.def.sub : "체크포인트 저장됨");
            SaveCheckpoint();
        }

        /* 엘리베이터 */
        void OpenLift() { mode = Mode.Lift; if (hud != null) hud.ShowLift(); }
        public void CloseLift() { if (hud != null) hud.HideLift(); mode = Mode.Play; }
        public void ChooseLift(string fid)
        {
            var entry = Data.LiftFloors.FirstOrDefault(e => e[0] == fid);
            if (entry == null || fid == S.floorId) return;
            if (!HasCard(entry[1])) { Snd("deny"); return; }
            CloseLift();
            Snd("lift");
            GoFloor(fid, "lift");
        }

        void Die(string reason, string kind, Npc n)
        {
            if (mode != Mode.Play) return;
            mode = Mode.Dying;
            S.dieT = 1.0f;
            S.deathReason = reason; S.deathKind = kind;
            if (n != null) { n.desFace = Mathf.Atan2(S.player.y - n.y, S.player.x - n.x); if (kind != "shot" && kind != "caught") Say(n, "저, 저게 뭐야!", 3); }
            if (S.player.form != "body") { MeltParticles(); S.player.form = "body"; }
            if (kind == "shot") { Snd("shot"); S.shotFrom = n != null ? new Vector2(n.x, n.y) : (Vector2?)null; }
            else if (kind == "caught") Snd("grab");
            else if (kind == "laser") Snd("zap");
            Snd("sting");
        }

        void ShowOver()
        {
            mode = Mode.Over;
            deaths++;
            if (hud != null) hud.ShowOver();
        }

        void WinGame()
        {
            mode = Mode.Clear;
            SaveFile.Delete();
            runInSession = false; checkpoint = null;
            Snd("win");
            if (hud != null) hud.ShowClear();
        }

        /* ================= 타일 판정 ================= */
        public char TileAt(int tx, int ty) { return G.Tile(tx, ty); }
        int DoorIdx(int tx, int ty) { int i; return G.doorAt.TryGetValue(ty * G.w + tx, out i) ? i : -1; }
        bool DoorPass(int tx, int ty) { int i = DoorIdx(tx, ty); if (i < 0) return true; var s = Fl.doors[i]; return s.open || s.temp > 0; }
        public bool IsOpaque(int tx, int ty) { char c = TileAt(tx, ty); if (c == 'D') return !DoorPass(tx, ty); return FloorGeo.OPAQUE.Contains(c); }
        bool PlayerSolid(int tx, int ty)
        {
            char c = TileAt(tx, ty);
            if (c == 'D') return !DoorPass(tx, ty);
            if (c == 'v') return S.player.form != "body";
            return FloorGeo.SOLID.Contains(c);
        }
        bool NpcSolid(Npc n, int tx, int ty)
        {
            char c = TileAt(tx, ty);
            if (c == 'D') return !DoorPass(tx, ty) && !n.C.Has(G.def.doors[DoorIdx(tx, ty)].req);
            return FloorGeo.SOLID.Contains(c);
        }
        static bool Overlaps(float x, float y, float r, int tx, int ty) { return x + r > tx && x - r < tx + 1 && y + r > ty && y - r < ty + 1; }
        public float PlayerR { get { return Data.Forms[S.player.form].radius; } }
        bool BoxBlocked(float x, float y, float r, float cx, float cy)
        {
            for (int ty = U.Floor(y - r); ty <= U.Floor(y + r); ty++)
                for (int tx = U.Floor(x - r); tx <= U.Floor(x + r); tx++)
                {
                    if (!PlayerSolid(tx, ty)) continue;
                    char c = TileAt(tx, ty);
                    if ((c == 'D' || c == 'v') && Overlaps(cx, cy, r, tx, ty)) continue; // 닫히는 문·환풍구에 낀 경우 빠져나올 수 있게
                    return true;
                }
            return false;
        }
        bool LineClear(float x0, float y0, float x1, float y1)
        {
            float d = U.Dist(x0, y0, x1, y1); int n = Mathf.Max(1, Mathf.CeilToInt(d / 0.08f));
            for (int i = 1; i < n; i++)
            {
                float t = i / (float)n;
                if (IsOpaque(U.Floor(x0 + (x1 - x0) * t), U.Floor(y0 + (y1 - y0) * t))) return false;
            }
            return true;
        }
        bool WallAdjacent(float x, float y)
        {
            const float r = 0.8f;
            Func<float, float, bool> s = (a, b) => FloorGeo.SOLID.Contains(TileAt(U.Floor(a), U.Floor(b)));
            return s(x + r, y) || s(x - r, y) || s(x, y + r) || s(x, y - r);
        }
        public bool IsNatural(Player p)
        {
            var f = Data.Forms[p.form];
            if (f.type != FormType.Object || f.zone == "any") return true;
            return WallAdjacent(p.x, p.y);
        }
        public bool PlayerHidden() { return TileAt(U.Floor(S.player.x), U.Floor(S.player.y)) == 'v'; }
        public TrapDef ActiveTrapAt(float x, float y)
        {
            int tx = U.Floor(x), ty = U.Floor(y);
            if (tx < 0 || ty < 0 || tx >= G.w || ty >= G.h) return null;
            int i = G.trapAt[ty * G.w + tx];
            if (i < 0) return null;
            var tr = G.def.traps[i];
            return S.trapsOff.Contains(tr.id) ? null : tr;
        }
        public bool HasCard(string c) { return c == null || S.cards.Contains(c); }

        /* 시야 판정: 거리 → 각도(모양) → 벽 레이캐스트 */
        public float ViewDist(Npc n) { return n.C.view + Cfg.waryStep * Fl.wary; }
        bool Sees(Npc n, float x, float y)
        {
            var c = n.C;
            float dx = x - n.x, dy = y - n.y, d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > ViewDist(n)) return false;
            if (d > 0.25f)
            {
                float diff = Mathf.Abs(U.AngDiff(Mathf.Atan2(dy, dx), n.face));
                if (c.shape != ViewShape.Circle && diff > Data.ShapeHalf(c.shape)) return false;
                if (c.shape == ViewShape.Circle && diff > Data.Rad(Cfg.rearBlindFrom) && d > Cfg.rearRange) return false;   // 군인 등 뒤 사각지대
            }
            return LineClear(n.x, n.y, x, y);
        }
        bool SeesPlayer(Npc n)
        {
            if (!n.alive || PlayerHidden()) return false;
            var p = S.player;
            if (PlayerInDark() && U.Dist(n.x, n.y, p.x, p.y) > Cfg.darkSeeR) return false;   // 어둠 속: 바로 옆이 아니면 못 알아챈다
            return Sees(n, p.x, p.y);
        }
        // 어두운 칸에 숨어 있는가 (먹는 중엔 소리가 나서 안 됨)
        public bool PlayerInDark()
        {
            var p = S.player;
            if (p.eating != null || G.DarkAt(p.x, p.y) < Cfg.darkHide) return false;
            var z = G.ZoneAt(p.x, p.y);
            return z == null || z.kind != "roof";   // 옥상은 탁 트여 있어서 (군인 손전등) 어둠에 못 숨는다
        }

        /* 격자 BFS 경로 (NPC별 출입 권한 반영) */
        int[] bfsPrev, bfsQ;
        List<Vector2> FindPath(Npc n, float sx, float sy, float gx, float gy)
        {
            int w = G.w, h = G.h;
            int s0 = U.Floor(sx), s1 = U.Floor(sy), g0 = U.Floor(gx), g1 = U.Floor(gy);
            if (g0 < 0 || g1 < 0 || g0 >= w || g1 >= h || NpcSolid(n, g0, g1)) return null;
            if (s0 == g0 && s1 == g1) return new List<Vector2> { new Vector2(gx, gy) };
            if (bfsPrev == null || bfsPrev.Length < w * h) { bfsPrev = new int[w * h]; bfsQ = new int[w * h]; }
            var prev = bfsPrev; var q = bfsQ;
            for (int i = 0; i < w * h; i++) prev[i] = -2;
            int qh = 0, qt = 0;
            int sk = s1 * w + s0, gk = g1 * w + g0;
            if (sk < 0 || sk >= w * h) return null;
            prev[sk] = -1; q[qt++] = sk;
            bool found = false;
            while (qh < qt)
            {
                int k = q[qh++];
                if (k == gk) { found = true; break; }
                int cx = k % w, cy = k / w;
                for (int dir = 0; dir < 4; dir++)
                {
                    int nx = cx + (dir == 0 ? 1 : dir == 1 ? -1 : 0), ny = cy + (dir == 2 ? 1 : dir == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int nk = ny * w + nx;
                    if (prev[nk] != -2 || NpcSolid(n, nx, ny)) continue;
                    prev[nk] = k; q[qt++] = nk;
                }
            }
            if (!found) return null;
            var tiles = new List<int>();
            for (int k = gk; k != -1; k = prev[k]) tiles.Add(k);
            tiles.Reverse();
            var path = new List<Vector2>();
            for (int i = 1; i < tiles.Count; i++) path.Add(new Vector2(tiles[i] % w + 0.5f, tiles[i] / w + 0.5f));
            if (path.Count > 0) path[path.Count - 1] = new Vector2(gx, gy);
            return path;
        }
        Vector2 WanderPoint(Npc n, float cx, float cy, float r)
        {
            for (int i = 0; i < 16; i++)
            {
                float x = U.Floor(cx + U.Rand(-r, r)) + 0.5f, y = U.Floor(cy + U.Rand(-r, r)) + 0.5f;
                if (NpcSolid(n, U.Floor(x), U.Floor(y))) continue;
                if (FindPath(n, n.x, n.y, x, y) != null) return new Vector2(x, y);
            }
            return new Vector2(cx, cy);
        }

        /* ================= 게임 로직 ================= */
        public void Say(Npc n, string text, float dur = 2.6f) { n.bubble = new Bubble { text = text, t = dur }; }

        void SetForm(string id)
        {
            var p = S.player; var f = Data.Forms[id];
            p.form = id; p.timer = f.dur;
            if (!S.seenForm.Contains(id)) S.seenForm.Add(id);
            Toast(Data.Josa(f.name, "이", "가") + " 되었다");
        }

        void GainCards(string charId)
        {
            var got = Data.Chars[charId].access.Where(c => !S.cards.Contains(c)).ToList();
            if (got.Count == 0) return;
            S.cards.AddRange(got);
            S.cards.Sort((a, b) => Array.IndexOf(Data.CardOrder, a) - Array.IndexOf(Data.CardOrder, b));
            string top = got[got.Count - 1];
            Later(0.9f, () => { Toast(Data.Cards[top].name + " 카드 획득"); Snd("card"); });
        }

        public EatTarget NearestEdible()
        {
            var p = S.player;
            EatTarget best = null; float bd = Cfg.eatRange;
            foreach (var o in Fl.objects)
            {
                if (!o.alive) continue;
                float d = U.Dist(p.x, p.y, o.x, o.y);
                if (d < bd) { bd = d; best = new EatTarget { isNpc = false, obj = o, form = o.form }; }
            }
            foreach (var n in Fl.npcs)
            {
                if (!n.alive || n.lockT > 0 || n.frozen) continue;
                float d = U.Dist(p.x, p.y, n.x, n.y);
                if (d < bd) { bd = d; best = new EatTarget { isNpc = true, npc = n, form = n.ch }; }
            }
            // 붙박이 가구 (책상·선반): 칸 가장자리까지의 거리로 잰다
            int px = U.Floor(p.x), py = U.Floor(p.y);
            for (int ty = py - 1; ty <= py + 1; ty++) for (int tx = px - 1; tx <= px + 1; tx++)
            {
                if (!EdibleTile(tx, ty)) continue;
                float cx = Mathf.Clamp(p.x, tx, tx + 1), cy = Mathf.Clamp(p.y, ty, ty + 1);
                float d = U.Dist(p.x, p.y, cx, cy) + 0.45f;   // 사람·물건이 가까이 있으면 그쪽이 먼저
                if (d >= bd) continue;
                var land = LandSpot(tx, ty);
                if (!land.HasValue) continue;
                bd = d; best = new EatTarget { tile = new EatenTile { x = tx, y = ty, ch = TileAt(tx, ty) }, form = Data.TileForms[TileAt(tx, ty)], land = land.Value };
            }
            return best;
        }
        public bool EdibleTile(int tx, int ty)
        {
            char c = TileAt(tx, ty);
            if (!Data.TileForms.ContainsKey(c)) return false;
            return !WorldView3D.IsTankTile(G, tx, ty, G.Salt);   // 수조로 보이는 칸은 못 먹는다
        }
        // 큰 가구로 변신하면 덩치가 커지므로, 그 칸을 비운 뒤 몸이 들어갈 자리를 찾는다 (없으면 못 먹음)
        readonly Dictionary<int, Vector2?> landCache = new Dictionary<int, Vector2?>();
        Vector2? LandSpot(int tx, int ty)
        {
            int key = ty * G.w + tx; Vector2? res;
            if (landCache.TryGetValue(key, out res)) return res;
            float r = Data.Forms[Data.TileForms[TileAt(tx, ty)]].radius;
            char keep = G.grid[ty, tx]; G.grid[ty, tx] = '.';
            float cx0 = tx + 0.5f, cy0 = ty + 0.5f, bestD = 99;
            for (int iy = -12; iy <= 12; iy++) for (int ix = -12; ix <= 12; ix++)
            {
                float x = cx0 + ix * 0.1f, y = cy0 + iy * 0.1f, d = U.Dist(x, y, cx0, cy0);
                if (d > 1.2f || d >= bestD) continue;
                if (BoxSolidAny(x, y, r)) continue;
                bestD = d; res = new Vector2(x, y);
            }
            G.grid[ty, tx] = keep;
            landCache[key] = res;
            return res;
        }
        bool BoxSolidAny(float x, float y, float r)
        {
            for (int ty = U.Floor(y - r); ty <= U.Floor(y + r); ty++)
                for (int tx = U.Floor(x - r); tx <= U.Floor(x + r); tx++)
                {
                    char c = TileAt(tx, ty);
                    if (FloorGeo.SOLID.Contains(c) || c == '>' || c == '<' || ActiveTrapAt(tx + 0.5f, ty + 0.5f) != null) return true;
                }
            return false;
        }
        public int NearDoor()
        {
            var p = S.player;
            for (int i = 0; i < G.def.doors.Length; i++)
            {
                var d = G.def.doors[i];
                if (!Fl.doors[i].open && U.Dist(p.x, p.y, d.x + 0.5f, d.y + 0.5f) < 1.35f) return i;
            }
            return -1;
        }
        public ConsoleDef NearConsole()
        {
            var p = S.player;
            foreach (var c in G.def.consoles) if (U.Dist(p.x, p.y, c.x + 0.5f, c.y + 0.5f) < 1.4f) return c;
            return null;
        }
        public bool NearLift()
        {
            if (G.def.lift == null) return false;
            var p = S.player;
            return G.def.lift.Any(t => U.Dist(p.x, p.y, t.x + 0.5f, t.y + 0.5f) < 1.6f);
        }
        public NoticeDef NearNotice()
        {
            var p = S.player;
            foreach (var nt in G.def.notices) if (U.Dist(p.x, p.y, nt.x + 0.5f, nt.y + 0.5f) < 1.3f) return nt;
            return null;
        }
        public TrapDef NearTrap()
        {
            var p = S.player;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                var tr = ActiveTrapAt(p.x + dx * 0.9f, p.y + dy * 0.9f);
                if (tr != null) return tr;
            }
            return null;
        }
        public bool OthersSee(object target) { return Fl.npcs.Any(n => n.alive && !ReferenceEquals(n, target) && SeesPlayer(n)); }

        public void Action()
        {
            var p = S.player;
            if (p.eating != null) return;
            int di = NearDoor();
            if (di >= 0)
            {
                var dd = G.def.doors[di];
                if (HasCard(dd.req)) { Fl.doors[di].open = true; Snd("door"); Toast(dd.label + " 문이 열렸다"); }
                else { Snd("deny"); Toast(Data.Cards[dd.req].name + " 카드 필요 — " + Data.Cards[dd.req].from); }
                return;
            }
            var cn = NearConsole();
            if (cn != null)
            {
                if (cn.heli)
                {
                    if (S.heli != null) { Toast(S.heli.arrived ? "헬기가 기다리고 있다" : "헬기가 오는 중이다"); return; }
                    if (!HasCard(cn.req)) { Snd("deny"); Toast("소장 인증 실패 — 연구소장 카드 필요"); return; }
                    S.heli = new Heli { t = Cfg.heliTime, arrived = false };
                    Fl.wary = 2;
                    Snd("card"); Snd("heli");
                    Toast("헬기 호출 — 착륙까지 버텨라");
                    return;
                }
                var info = FloorGeo.TrapInfo[cn.trap]; string infoFloor = FloorGeo.TrapFloor[cn.trap];
                if (S.trapsOff.Contains(cn.trap)) { Toast(infoFloor + " " + info.label + " — 이미 꺼져 있다"); return; }
                if (!HasCard(cn.req)) { Snd("deny"); Toast(Data.Cards[cn.req].name + " 카드 필요 — " + Data.Cards[cn.req].from); return; }
                S.trapsOff.Add(cn.trap);
                Snd("door"); Snd("card");
                Toast(infoFloor + " " + info.label + " 해제됨");
                return;
            }
            if (NearLift())
            {
                if (S.alarm != null) { Snd("deny"); Toast("경보 중 — 엘리베이터 정지"); return; }
                OpenLift();
                return;
            }
            var t = NearestEdible();
            if (t == null) return;
            if (p.eatCd > 0) { Snd("deny"); Toast("아직 소화 중… " + Mathf.CeilToInt(p.eatCd) + "초"); return; }
            if (OthersSee(t.Ref)) { Snd("deny"); Toast("누가 보고 있다"); return; }
            if (t.isNpc && p.form == "body" && SeesPlayer(t.npc)) { Snd("deny"); Toast("눈이 마주쳤다 — 뒤로 돌아가거나 변장할 것"); return; }
            p.eating = new Eating { target = t, t = 0 };
            if (t.isNpc) t.npc.frozen = true;
            Snd("gulp");
        }

        void UpdatePlayer(float dt)
        {
            var p = S.player;
            float ox = p.x, oy = p.y;
            if (p.eating != null)
            {
                p.eating.t += dt;
                var tg = p.eating.target;
                p.face = Mathf.Atan2(tg.Y - p.y, tg.X - p.x);
                if (p.eating.t >= Cfg.eatTime)
                {
                    if (tg.isNpc)
                    {
                        var n = tg.npc;
                        n.alive = false; n.frozen = false;
                        GainCards(n.ch);
                        if (!n.temp) Fl.missing.Add(new Missing { t = Cfg.missingDelay, x = n.x, y = n.y, label = n.label });
                    }
                    else if (tg.IsTile)
                    {
                        // 책상·선반: 칸을 비우고 그 자리에 큰 몸으로 선다
                        tg.tile.respawnT = Cfg.respawn;
                        Fl.tiles.Add(tg.tile);
                        G.ApplyEaten(Fl.tiles); landCache.Clear();
                        foreach (var n in Fl.npcs) n.path = null;
                        p.x = tg.land.x; p.y = tg.land.y;
                        if (view != null) view.BuildFloor(G);
                    }
                    else { tg.obj.alive = false; tg.obj.respawnT = Cfg.respawn; }
                    for (int i = 0; i < 16; i++) SpawnParticle(p.x, p.y, i % 2 != 0 ? Data.Hex("#ff8a98") : Data.Hex("#ffc2ca"), U.Rand(1.5f, 4), U.Rand(0.3f, 0.7f), U.Rand(1.5f, 3));
                    p.eating = null; p.eatCd = Cfg.eatCooldown;
                    SetForm(tg.form);
                    Snd("morph");
                }
            }
            else
            {
                var v = Inp.Move(); var f = Data.Forms[p.form];
                float spd = f.speed;
                if (f.seenSpeed > 0 && S.anySee) spd = Mathf.Min(spd, f.seenSpeed * 0.95f);
                if (v.x != 0 || v.y != 0)
                {
                    p.moved = true;
                    p.face = Mathf.Atan2(v.y, v.x);
                    float nx = p.x + v.x * spd * dt;
                    if (!BoxBlocked(nx, p.y, f.radius, p.x, p.y)) p.x = nx;
                    float ny = p.y + v.y * spd * dt;
                    if (!BoxBlocked(p.x, ny, f.radius, p.x, p.y)) p.y = ny;
                }
            }
            p.speedNow = dt > 0 ? U.Dist(ox, oy, p.x, p.y) / dt : 0;
            if (p.eatCd > 0) p.eatCd -= dt;

            // 변신 타이머
            var fm = Data.Forms[p.form];
            if (fm.type != FormType.Body && p.eating == null)
            {
                p.timer -= dt;
                if (p.timer <= 0)
                {
                    MeltParticles();
                    p.form = "body"; p.timer = 0;
                    Snd("melt");
                    Toast("몸이 녹아내렸다");
                }
            }
            // 점액 자국
            if (p.form == "body" && p.speedNow > 0.5f && !PlayerHidden())
            {
                S.trailT -= dt;
                if (S.trailT <= 0) { S.trailT = 0.12f; S.trail.Add(new Trail { x = p.x + U.Rand(-0.12f, 0.12f), y = p.y + U.Rand(-0.12f, 0.12f), t = 3.5f, r = U.Rand(3, 6) }); }
            }
            // 바닥 이벤트: 함정, 계단, 헬리패드
            var tr = ActiveTrapAt(p.x, p.y);
            if (tr != null) { Die(tr.kind == "gas" ? "소장실 전실의 가스를 들이마셨다" : "레이저 그리드에 닿았다", tr.kind, null); return; }
            int tx = U.Floor(p.x), ty = U.Floor(p.y); char ch = TileAt(tx, ty);
            if (ch == '>' || ch == '<')
            {
                var st = G.def.stairs.FirstOrDefault(s => tx >= s.x && tx < s.x + s.w && ty >= s.y && ty < s.y + s.h);
                if (st != null) { GoFloor(st.to, "stairs"); return; }
            }
            if (ch == 'H' && S.heli != null && S.heli.arrived) WinGame();
        }

        void MeltParticles()
        {
            var p = S.player;
            for (int i = 0; i < 20; i++) SpawnParticle(p.x, p.y, i % 2 != 0 ? Data.Hex("#ff7d8c") : Data.Hex("#c2405a"), U.Rand(0.8f, 3), U.Rand(0.5f, 1.1f), U.Rand(2, 4));
        }

        float NpcSpeed(Npc n) { return n.C.speed * (n.state == NpcState.Chase ? Cfg.chaseK : Cfg.patrolK); }
        bool MoveAlong(Npc n, float dt, float spd)
        {
            if (n.path == null || n.path.Count == 0) return true;
            float step = spd * dt;
            while (step > 0 && n.path.Count > 0)
            {
                var t = n.path[0]; float d = U.Dist(n.x, n.y, t.x, t.y);
                if (d <= step) { n.x = t.x; n.y = t.y; n.path.RemoveAt(0); step -= d; }
                else { n.x += (t.x - n.x) / d * step; n.y += (t.y - n.y) / d * step; n.desFace = Mathf.Atan2(t.y - n.y, t.x - n.x); step = 0; }
            }
            return n.path.Count == 0;
        }

        void UpdateNPC(Npc n, float dt)
        {
            if (!n.alive) return;
            if (n.bubble != null) { n.bubble.t -= dt; if (n.bubble.t <= 0) n.bubble = null; }
            n.face = U.ApproachAng(n.face, n.desFace, Cfg.turnRate * dt * (n.state == NpcState.Chase ? 1.6f : 1f));
            if (n.frozen) return;
            if (n.shoutCool > 0) n.shoutCool -= dt;
            if (n.state == NpcState.Chase) { UpdateChase(n, dt); return; }
            if (n.cool > 0) n.cool -= dt;

            if (n.state == NpcState.Inspect)
            {
                var ins = n.inspect;
                if (!ins.staring)
                {
                    bool arrived = MoveAlong(n, dt, NpcSpeed(n));
                    if (arrived || U.Dist(n.x, n.y, ins.x, ins.y) <= 1.4f) { ins.staring = true; ins.t = Cfg.stare; n.path = null; }
                }
                else
                {
                    n.desFace = Mathf.Atan2(ins.y - n.y, ins.x - n.x);
                    ins.t -= dt;
                    if (ins.t <= 0)
                    {
                        Say(n, ins.end ?? U.Pick(Data.InspectEnd));
                        n.state = NpcState.Patrol; n.path = null; n.wait = 0; n.cool = Cfg.inspectCooldown;
                        n.lastInspect = new Vector2(ins.x, ins.y); n.inspect = null;
                    }
                }
                return;
            }

            // 순찰
            if (n.route.Length > 0)
            {
                var wp = n.route[n.wp];
                if (n.wait > 0)
                {
                    n.desFace = Data.Rad(wp.look);
                    n.wait -= dt;
                    if (n.wait <= 0) { n.wp = (n.wp + 1) % n.route.Length; n.path = null; }
                }
                else
                {
                    if (n.path == null) n.path = FindPath(n, n.x, n.y, wp.x, wp.y) ?? new List<Vector2>();
                    if (MoveAlong(n, dt, NpcSpeed(n)))
                    {
                        n.wait = Mathf.Max(0.05f, (wp.wait > 0 ? wp.wait : 0.01f) * n.C.wait * (1 - 0.25f * Fl.wary));
                        n.path = null;
                    }
                }
            }
            // 혼잣말
            n.chatT -= dt;
            if (n.chatT <= 0 && n.bubble == null)
            {
                n.chatT = U.Rand(9, 16);
                Say(n, U.Pick(Data.Chatter[n.ch]));
            }
        }

        void StartInspect(Npc n, float x, float y, string line = null, string end = null)
        {
            var path = FindPath(n, n.x, n.y, x, y);
            if (path == null) return;
            n.state = NpcState.Inspect;
            n.inspect = new Inspect { x = x, y = y, staring = false, t = 0, end = end };
            n.path = path;
            Say(n, line ?? U.Pick(Data.InspectStart), 2.2f);
            Snd("huh");
        }

        /* 경보 / 추격 */
        bool Suspicious()
        {
            var p = S.player;
            if (PlayerHidden()) return false;
            return p.form == "body" || p.eating != null || S.violationNow || (S.alarm != null && S.alarm.mark == p.form);
        }

        void RaiseAlarm(Npc by)
        {
            var p = S.player;
            bool fresh = S.alarm == null;
            if (fresh)
            {
                S.alarm = new Alarm { t = Cfg.alarmTime, lx = p.x, ly = p.y, mark = p.form, spawnT = 1.6f, spawned = false, started = S.time };
                S.alarms++;
                S.sirenT = 0;
                Toast("경보 발령!");
                foreach (var n in Fl.npcs) if (n.alive && n.C.chaser != null && !n.frozen)
                    { n.state = NpcState.Chase; n.path = null; n.inspect = null; n.search = null; n.lockT = 0; n.repath = 0; }
            }
            var A = S.alarm;
            A.t = Cfg.alarmTime; A.lx = p.x; A.ly = p.y;
            if (p.form != "body") A.mark = p.form;
            foreach (var n in Fl.npcs) if (n.state == NpcState.Chase && n.lockT <= 0) { n.search = null; n.repath = 0; }
            if (by != null && by.shoutCool <= 0)
            {
                by.shoutCool = 3;
                string[] lines; if (!Data.AlarmLines.TryGetValue(by.ch, out lines)) lines = Data.AlarmLines["other"];
                Say(by, U.Pick(lines), 2);
                if (by.C.chaser != null) by.lockT = 1.4f;
            }
        }

        void SpawnReinforcements()
        {
            S.alarm.spawned = true;
            int k = 0;
            foreach (var r in G.def.reinforce) for (int i = 0; i < r.n; i++)
            {
                var sp = G.def.spawns[k % G.def.spawns.Length];
                var n = Npc.Make(new NpcDef("tmp" + k, r.ch, r.ch, "출동 " + Data.Chars[r.ch].name, sp.x + U.Rand(-0.3f, 0.3f), sp.y + U.Rand(-0.3f, 0.3f), 0, null), true);
                n.state = NpcState.Chase; n.chatT = 999;
                Fl.npcs.Add(n);
                k++;
            }
            if (k > 0) { if (hud != null) hud.Alert("⚠ " + Data.Chars[G.def.reinforce[0].ch].name + " " + k + "명 출동", "출입구에서 진입 중 — 숨을 곳을 찾아라"); Snd("siren"); }
        }

        void EndAlarm()
        {
            S.alarm = null;
            Fl.npcs.RemoveAll(n => n.temp);
            foreach (var n in Fl.npcs) if (n.state == NpcState.Chase)
            {
                n.state = NpcState.Patrol; n.path = null; n.wait = 0; n.search = null; n.lockT = 0; n.aim = 0;
                Say(n, "…놓쳤나.");
            }
            Snd("calm");
            Toast("경보 해제");
        }

        void UpdateChase(Npc n, float dt)
        {
            var A = S.alarm; var p = S.player; var c = n.C;
            if (A == null) { n.state = NpcState.Patrol; n.path = null; n.lockT = 0; n.aim = 0; return; }
            bool vis = n.seesPlayer && Suspicious();
            if (vis) { n.lockT = 1.4f; A.lx = p.x; A.ly = p.y; A.t = Cfg.alarmTime; }
            else n.lockT = Mathf.Max(0, n.lockT - dt);
            n.repath -= dt;
            float spd = c.speed * Cfg.chaseK;
            if (n.lockT > 0)
            {
                n.search = null;
                float tx = vis ? p.x : A.lx, ty = vis ? p.y : A.ly;
                if (c.chaser == "shoot" && vis)
                {
                    n.desFace = Mathf.Atan2(ty - n.y, tx - n.x);
                    n.path = null;
                    n.aim += dt;
                    if (n.aim >= Cfg.aimTime) Die(n.label + "의 총에 맞았다", "shot", n);
                    return;
                }
                n.aim = Mathf.Max(0, n.aim - dt * 2);
                if (n.repath <= 0 || n.path == null || n.path.Count == 0) { n.repath = 0.35f; n.path = FindPath(n, n.x, n.y, tx, ty) ?? new List<Vector2>(); }
                MoveAlong(n, dt, spd);
                if (c.chaser == "catch" && vis && U.Dist(n.x, n.y, p.x, p.y) < Cfg.catchDist) Die(n.label + "에게 붙잡혔다", "caught", n);
                return;
            }
            n.aim = Mathf.Max(0, n.aim - dt * 2);
            // 수색: 마지막 목격 지점 → 두리번 → 주변 배회
            if (n.search == null) { n.search = new Search { x = A.lx, y = A.ly, looking = false, t = 0 }; n.path = null; }
            var s = n.search;
            if (!s.looking)
            {
                if (n.path == null) { n.path = FindPath(n, n.x, n.y, s.x, s.y) ?? new List<Vector2>(); if (n.path.Count == 0) { s.looking = true; s.t = 2.2f; } }
                if (!s.looking && MoveAlong(n, dt, spd * 0.75f)) { s.looking = true; s.t = 2.2f; n.path = null; }
            }
            else
            {
                s.t -= dt;
                n.desFace = n.face + 1.2f;
                if (s.t <= 0) { var w = WanderPoint(n, A.lx, A.ly, 7); n.search = new Search { x = w.x, y = w.y, looking = false, t = 0 }; n.path = null; }
            }
        }

        void Detect(float dt)
        {
            var p = S.player; var f = Data.Forms[p.form];
            Npc target = p.eating != null && p.eating.target.isNpc ? p.eating.target.npc : null;
            string violation = null, kind = ""; Npc by = null; bool any = false;
            foreach (var n in Fl.npcs.ToArray())
            {
                n.seesPlayer = SeesPlayer(n);
                if (!n.seesPlayer) continue;
                any = true;
                if (S.arriveT > 0 || n.frozen) continue;
                string v = null, k = "";
                if (p.eating != null) { if (n != target) { v = n.label + "에게 먹는 장면을 들켰다"; k = "eating"; } }
                else if (f.type == FormType.Body) { v = n.label + "에게 본모습을 들켰다"; k = "body"; }
                else if (f.type == FormType.Object)
                {
                    float lim = f.seenSpeed;
                    if (p.speedNow > lim + Cfg.moveEps) { v = n.label + "의 눈앞에서 " + Data.Josa(f.name, "이", "가") + " 움직였다"; k = "moved"; }
                    else if (!IsNatural(p) && n.state == NpcState.Patrol && n.cool <= 0 &&
                             !(n.lastInspect.HasValue && U.Dist(n.lastInspect.Value.x, n.lastInspect.Value.y, p.x, p.y) < 0.6f)) StartInspect(n, p.x, p.y);
                }
                else if (f.type == FormType.Human)
                {
                    var z = G.ZoneAt(p.x, p.y);
                    if (z != null && !z.Allows(p.form)) { v = n.label + ": \"" + Data.Josa(f.name, "이", "가") + " 왜 " + z.name + "에 있지?\""; k = "restricted"; }
                }
                // 경보 중: 수배된 모습을 본 사람은 다시 신고한다
                if (v == null && S.alarm != null && n.state != NpcState.Chase && (p.form == S.alarm.mark || f.type == FormType.Body)) RaiseAlarm(n);
                if (v != null && violation == null) { violation = v; kind = k; by = n; }
            }
            S.anySee = any;
            S.violationNow = violation != null;
            if (violation != null)
            {
                S.spot += dt; S.spotNpc = by;
                if (S.spot >= Cfg.grace)
                {
                    if (G.def.lethal) Die(violation, kind, by);
                    else { RaiseAlarm(by); S.spot = Cfg.grace; }
                }
            }
            else
            {
                S.spot = Mathf.Max(0, S.spot - dt * 2);
                if (S.spot == 0) S.spotNpc = null;
            }
        }

        void ReportMissing(Missing m)
        {
            var F = Fl;
            var cands = F.npcs.Where(n => n.alive && !n.temp && n.state == NpcState.Patrol).ToList();
            if (cands.Count == 0) return;
            cands.Sort((a, b) => U.Dist(a.x, a.y, m.x, m.y).CompareTo(U.Dist(b.x, b.y, m.x, m.y)));
            var reporter = cands[0];
            Say(reporter, m.label + ", 어디 갔지?", 3.2f);
            F.wary = Mathf.Min(2, F.wary + 1);
            Toast("실종이 알려졌다 · 경계 강화");
            // 경비·군인이 있으면 마지막 위치를 확인하러 간다
            var checker = cands.FirstOrDefault(n => n.C.chaser != null) ?? reporter;
            StartInspect(checker, m.x, m.y, "확인해 보지.", "아무도 없는데…");
        }

        void UpdateDoors(float dt)
        {
            var F = Fl;
            foreach (var st in F.doors) st.temp = Mathf.Max(0, st.temp - dt);
            foreach (var n in F.npcs)
            {
                if (!n.alive) continue;
                for (int i = 0; i < G.def.doors.Length; i++)
                {
                    var d = G.def.doors[i];
                    if (n.C.Has(d.req) && U.Dist(n.x, n.y, d.x + 0.5f, d.y + 0.5f) < 1.3f) F.doors[i].temp = 0.7f;
                }
            }
        }

        public void SpawnParticle(float x, float y, Color color, float speed, float life, float r)
        {
            float a = UnityEngine.Random.value * Mathf.PI * 2;
            S.particles.Add(new Particle { x = x, y = y, vx = Mathf.Cos(a) * speed, vy = Mathf.Sin(a) * speed, life = life, max = life, r = r, color = color });
        }
        void UpdateFx(float dt)
        {
            float damp = Mathf.Pow(0.9f, dt * 60f);
            foreach (var pt in S.particles) { pt.x += pt.vx * dt; pt.y += pt.vy * dt; pt.vx *= damp; pt.vy *= damp; pt.life -= dt; }
            S.particles.RemoveAll(pt => pt.life <= 0);
            foreach (var tr in S.trail) tr.t -= dt;
            S.trail.RemoveAll(tr => tr.t <= 0);
        }
        void UpdateCamera(float dt, bool snap = false)
        {
            var p = S.player;
            var target = new Vector2(p.x - Cfg.VTW / 2, p.y - Cfg.VTH / 2);
            float k = snap ? 1 : Mathf.Min(1, dt * 6);
            S.cam += (target - S.cam) * k;
        }

        /* ================= 루프 ================= */
        void Update()
        {
            if (S == null) return;
            HandleKeys();
            float dt = Mathf.Min(0.05f, Time.deltaTime);
            Tick(dt);
            if (view != null) view.Render();
            if (hud != null) hud.Refresh(false);
            // 배경음악: 게임 플레이 중에만 (메인 메뉴·인트로 제외), 일시정지·결과 화면에선 작게, 탈출 성공 후엔 끔
            if (sfx != null)
            {
                if (mode == Mode.Title || mode == Mode.Intro || mode == Mode.Clear) sfx.SetMusic(null);
                else sfx.SetMusic("bgm_play", mode == Mode.Pause || mode == Mode.Over ? 0.4f : 1f);
            }
        }

        void HandleKeys()
        {
            if (mode == Mode.Intro) { if (Inp.AnyDown()) FinishIntro(); return; }
            if (mode == Mode.Title)
            {
                if (hud == null) return;
                if (hud.MenuPanelOpen) { if (Inp.Down(KeyCode.Escape)) hud.CloseMenuPanels(); return; }
                if (Inp.Down(KeyCode.Return)) { if (HasSave()) Continue(); else StartGame(); }
                return;
            }
            if (mode == Mode.Lift)
            {
                if (Inp.Down(KeyCode.Escape)) CloseLift();
                for (int i = 1; i <= 5; i++) if (Inp.Down(KeyCode.Alpha0 + i)) ChooseLift(i + "F");
                return;
            }
            if (mode == Mode.Pause) { if (Inp.Down(KeyCode.Escape)) { if (hud != null && hud.HowToOpen) hud.CloseHowTo(); else Resume(); } return; }
            if (mode == Mode.Over) { if (Inp.Down(KeyCode.R) || Inp.Down(KeyCode.Return) || Inp.Down(KeyCode.Space)) Retry(); else if (Inp.Down(KeyCode.Escape)) GoToMenu(); return; }
            if (mode == Mode.Clear) { if (Inp.Down(KeyCode.Return)) NewRun(); else if (Inp.Down(KeyCode.Escape)) GoToMenu(); return; }
            if (mode == Mode.Play)
            {
                if (Inp.Down(KeyCode.R)) { Retry(); return; }
                if (Inp.Down(KeyCode.Escape)) { Pause(); return; }
                if (Inp.Down(KeyCode.M)) { showMini = !showMini; if (hud != null) hud.SetMiniVisible(showMini); return; }
                if (Inp.Down(KeyCode.E) || Inp.Down(KeyCode.Space)) Action();
            }
        }

        void Tick(float dt)
        {
            S.time += dt;
            if (mode == Mode.Title || mode == Mode.Intro) { visValid = false; foreach (var n in Fl.npcs.ToArray()) UpdateNPC(n, dt); UpdateDoors(dt); return; }
            if (mode == Mode.Dying)
            {
                S.dieT -= dt;
                foreach (var n in Fl.npcs) { if (n.bubble != null) n.bubble.t -= dt * 0.2f; n.face = U.ApproachAng(n.face, n.desFace, Cfg.turnRate * 2 * dt); }
                UpdateFx(dt);
                if (S.dieT <= 0) ShowOver();
                return;
            }
            if (mode != Mode.Play) return;
            S.elapsed += dt;
            if (S.arriveT > 0) S.arriveT -= dt;
            UpdatePlayer(dt);
            if (mode != Mode.Play) return;
            var F = Fl;
            foreach (var n in F.npcs.ToArray()) UpdateNPC(n, dt);
            if (mode != Mode.Play) return;
            UpdateDoors(dt);
            Detect(dt);
            if (mode != Mode.Play) return;
            // 경보
            if (S.alarm != null)
            {
                var A = S.alarm;
                if (!A.spawned) { A.spawnT -= dt; if (A.spawnT <= 0) SpawnReinforcements(); }
                bool locked = F.npcs.Any(n => n.alive && n.state == NpcState.Chase && n.lockT > 0);
                if (!locked) A.t -= dt;
                S.sirenT -= dt;
                if (S.sirenT <= 0) { S.sirenT = 1.0f; Snd("siren"); }
                if (A.t <= 0) EndAlarm();
            }
            // 조준음
            var aiming = F.npcs.FirstOrDefault(n => n.alive && n.aim > 0.05f);
            if (aiming != null) { S.aimBeep -= dt; if (S.aimBeep <= 0) { S.aimBeep = Mathf.Max(0.08f, 0.35f - aiming.aim * 0.25f); Snd("beep"); } }
            // 먹힌 물건은 시간이 지나면 다시 놓인다 (보급)
            foreach (var o in F.objects) if (!o.alive && o.respawnT > 0)
            {
                o.respawnT -= dt;
                if (o.respawnT <= 0) { if (U.Dist(o.x, o.y, S.player.x, S.player.y) > 4) o.alive = true; else o.respawnT = 3; }
            }
            foreach (var t in F.tiles) if (t.respawnT > 0) t.respawnT -= dt;
            // 실종 신고
            foreach (var m in F.missing.ToArray()) { m.t -= dt; if (m.t <= 0 && !m.done) { m.done = true; ReportMissing(m); } }
            F.missing.RemoveAll(m => m.done);
            // 헬기
            if (S.heli != null && !S.heli.arrived)
            {
                S.heli.t -= dt;
                if (S.heli.t <= 0) { S.heli.arrived = true; Toast("헬기 착륙 — 헬리패드로!"); Snd("heli"); }
            }
            UpdateFx(dt);
            UpdateCamera(dt);
            ComputeVision();
            Footsteps(dt);
            var p = S.player; var f = Data.Forms[p.form];
            if (f.type != FormType.Body && p.timer < f.dur * 0.25f) if (sfx != null) sfx.SetHum(1.6f, Mathf.Sin(S.time * 9) * 120);
            else if (sfx != null) sfx.SetHum(1, 0);
        }

        /* ================= 주인공 시야 (피드백 1·2) ================= */
        // 주인공 위치에서 사방으로 광선을 쏴서 벽에 막히는 곳까지만 보인다. 본 칸은 미니맵에 기록.
        public const int VIS_RAYS = 240;
        public readonly float[] visDist = new float[VIS_RAYS];
        public float visR = Cfg.visionR, visAvg = 6f;
        public bool visValid;

        public bool[] Explored(string fid)
        {
            bool[] e;
            if (!S.explored.TryGetValue(fid, out e)) { var g = FloorGeo.All[fid]; e = new bool[g.w * g.h]; S.explored[fid] = e; }
            return e;
        }

        void ComputeVision()
        {
            var p = S.player;
            visR = PlayerHidden() ? Cfg.ventVisionR : Cfg.visionR;
            var ex = Explored(S.floorId);
            float sum = 0;
            for (int i = 0; i < VIS_RAYS; i++)
            {
                float a = i * Mathf.PI * 2 / VIS_RAYS, ca = Mathf.Cos(a), sa = Mathf.Sin(a), d = 0;
                while (d < visR)
                {
                    float nd = d + 0.12f;
                    int tx = U.Floor(p.x + ca * nd), ty = U.Floor(p.y + sa * nd);
                    if (tx >= 0 && ty >= 0 && tx < G.w && ty < G.h) ex[ty * G.w + tx] = true;
                    if (IsOpaque(tx, ty)) break;
                    d = nd;
                }
                visDist[i] = d; sum += d;
            }
            visAvg = sum / VIS_RAYS;
            visValid = true;
        }

        public bool PlayerCanSee(float x, float y)
        {
            if (!visValid) return true;
            var p = S.player;
            if (U.Dist(p.x, p.y, x, y) > visR + 0.4f) return false;
            return LineClear(p.x, p.y, x, y);
        }

        // 안 보이는 곳에서 움직이는 NPC의 발소리 (방향·거리에 따라)
        float stepT;
        void Footsteps(float dt)
        {
            stepT -= dt;
            if (stepT > 0 || sfx == null) return;
            var p = S.player; Npc best = null; float bd = 7f;
            foreach (var n in Fl.npcs)
            {
                if (!n.alive || n.frozen) continue;
                bool moving = (n.path != null && n.path.Count > 0) || n.state == NpcState.Chase;
                if (!moving) continue;
                float d = U.Dist(p.x, p.y, n.x, n.y);
                if (d < bd && !PlayerCanSee(n.x, n.y)) { bd = d; best = n; }
            }
            if (best == null) { stepT = 0.15f; return; }
            stepT = best.state == NpcState.Chase ? 0.28f : 0.45f;
            sfx.PlayAt("step", Mathf.Clamp01(1 - bd / 7f) * 0.9f, Mathf.Clamp((best.x - p.x) / 5f, -1, 1));
        }

        /* ================= HUD용 정보 ================= */
        public string Objective()
        {
            Func<string, bool> has = c => S.cards.Contains(c);
            if (S.heli != null) return S.heli.arrived ? "헬기 착륙! <b>헬리패드</b>로" : "헬기 도착까지 <b>" + Mathf.CeilToInt(S.heli.t) + "초</b> — 버텨라";
            if (has("dir")) return S.floorId == "5F" ? "<b>헬기 호출 콘솔</b>에서 소장 인증" : "엘리베이터로 <b>5F 옥상</b>";
            if (!has("clean")) return "<b>늙은 청소부</b>를 먹고 직원 출입문으로";
            if (!has("lab")) return "1F에서 <b>연구원</b>의 출입 권한을 빼앗아라";
            if (!has("sec")) return "2F 보안동 — <b>경비원</b>의 보안 카드";
            if (!has("senior")) return "3F 격리 윙의 <b>고위 연구원</b>" + (S.trapsOff.Contains("L3") ? "" : "\n레이저: B1 기계실 차단 또는 환풍구 우회");
            var need = new List<string>();
            if (!S.trapsOff.Contains("G4")) need.Add("2F 통제실에서 <b>가스 해제</b>");
            if (!has("mil")) need.Add("<b>군인</b> 카드");
            if (need.Count > 0) return "4F 소장실 준비 · " + string.Join(" · ", need.ToArray());
            return "4F <b>소장실</b>에서 연구소장을 먹어라";
        }

        public bool AnyLocked() { return Fl.npcs.Any(n => n.alive && n.state == NpcState.Chase && n.lockT > 0); }

        // 화면 아래 안내 문구. note=true면 게시물(종이) 스타일
        /* ===== 상호작용 테두리: 먹을 수 있는 것·문·콘솔·엘리베이터 =====
           strong = 지금 [E]를 누르면 반응할 대상 (굵게·맥박), 나머지는 가까이 있으면 옅게 */
        public enum HiKind { Obj, Npc, Tile, Door, Console, Lift }
        public struct Highlight { public HiKind kind; public object target; public int tx, ty; public bool strong; public Color color; }
        static readonly Color HI_EAT = new Color(1f, 0.49f, 0.55f), HI_BLOCK = new Color(1f, 0.32f, 0.28f), HI_CONSOLE = new Color(1f, 0.83f, 0.42f), HI_LIFT = new Color(0.5f, 0.88f, 0.69f);
        public void CollectHighlights(List<Highlight> list)
        {
            list.Clear();
            if (S == null || mode != Mode.Play) return;
            var p = S.player;
            int dr = NearDoor(); var cn = dr < 0 ? NearConsole() : null; bool lift = dr < 0 && cn == null && NearLift();
            var tgt = dr < 0 && cn == null && !lift && p.eating == null ? NearestEdible() : null;
            bool blocked = tgt != null && (p.eatCd > 0 || (S.anySee && OthersSee(tgt.Ref)));
            float pulse = 0.75f + 0.25f * Mathf.Sin(S.time * 7);
            Func<Color, float, Color> A = (c, a) => new Color(c.r, c.g, c.b, a);
            Color strongEat = A(blocked ? HI_BLOCK : HI_EAT, pulse), softEat = A(HI_EAT, 0.55f);

            // 먹을 수 있는 물건 (4칸 안, 보이는 것)
            foreach (var o in Fl.objects)
            {
                if (!o.alive || U.Dist(p.x, p.y, o.x, o.y) > 4f || !PlayerCanSee(o.x, o.y)) continue;
                bool st = tgt != null && tgt.obj == o;
                list.Add(new Highlight { kind = HiKind.Obj, target = o, strong = st, color = st ? strongEat : softEat });
            }
            if (tgt != null && tgt.isNpc) list.Add(new Highlight { kind = HiKind.Npc, target = tgt.npc, strong = true, color = strongEat });
            // 책상·선반: 가까울 때만 (방마다 많아서)
            int px = U.Floor(p.x), py = U.Floor(p.y);
            for (int ty = py - 2; ty <= py + 2; ty++) for (int tx = px - 2; tx <= px + 2; tx++)
            {
                if (!EdibleTile(tx, ty)) continue;
                bool st = tgt != null && tgt.IsTile && tgt.tile.x == tx && tgt.tile.y == ty;
                if (!st && (U.Dist(p.x, p.y, tx + 0.5f, ty + 0.5f) > 2.2f || !PlayerCanSee(tx + 0.5f, ty + 0.5f) || !LandSpot(tx, ty).HasValue)) continue;
                list.Add(new Highlight { kind = HiKind.Tile, tx = tx, ty = ty, strong = st, color = st ? strongEat : softEat });
            }
            // 문 (닫힌 것): 카드 색. 열 수 없으면 옅게
            for (int i = 0; i < G.def.doors.Length; i++)
            {
                var d = G.def.doors[i];
                if (Fl.doors[i].open || U.Dist(p.x, p.y, d.x + 0.5f, d.y + 0.5f) > 3f) continue;
                bool st = i == dr; var c = Data.Cards[d.req].color;
                list.Add(new Highlight { kind = HiKind.Door, target = d, tx = d.x, ty = d.y, strong = st, color = A(c, st ? pulse : HasCard(d.req) ? 0.6f : 0.35f) });
            }
            foreach (var c in G.def.consoles)
            {
                if (U.Dist(p.x, p.y, c.x + 0.5f, c.y + 0.5f) > 3f) continue;
                bool st = ReferenceEquals(c, cn);
                list.Add(new Highlight { kind = HiKind.Console, target = c, tx = c.x, ty = c.y, strong = st, color = A(HI_CONSOLE, st ? pulse : 0.55f) });
            }
            if (G.def.lift != null) foreach (var t in G.def.lift)
            {
                if (U.Dist(p.x, p.y, t.x + 0.5f, t.y + 0.5f) > 3f) continue;
                list.Add(new Highlight { kind = HiKind.Lift, tx = t.x, ty = t.y, strong = lift, color = A(HI_LIFT, lift ? pulse : 0.55f) });
            }
        }

        public string Hint(out bool note)
        {
            note = false;
            var p = S.player; var f = Data.Forms[p.form];
            var notice = NearNotice(); int dr = NearDoor(); var cn = NearConsole(); bool lift = NearLift(); var trap = NearTrap();
            var tgt = p.eating != null ? null : NearestEdible();
            if (!p.moved && S.time < 8 && S.floorId == "B1") return "[WASD] 또는 방향키로 이동 · [E] 먹기 · [M] 지도";
            if (p.eating != null) return "꿀꺽…";
            if (dr >= 0)
            {
                var d = G.def.doors[dr];
                return HasCard(d.req) ? "[E] " + d.label + " 열기" : d.label + " · <b>" + Data.Cards[d.req].name + " 카드</b> 필요 (" + Data.Cards[d.req].from + ")";
            }
            if (cn != null)
            {
                if (cn.heli) return S.heli != null ? "헬기 호출됨" : HasCard(cn.req) ? "[E] 소장 인증으로 헬기 호출" : "헬기 호출 콘솔 · <b>소장 카드</b> 필요";
                var info = FloorGeo.TrapInfo[cn.trap]; string fl = FloorGeo.TrapFloor[cn.trap];
                return S.trapsOff.Contains(cn.trap) ? cn.label + " · " + fl + " " + info.label + " 꺼짐" :
                       HasCard(cn.req) ? "[E] " + cn.label + " — " + fl + " " + info.label + " 끄기" : cn.label + " (" + fl + " " + info.label + ") · <b>" + Data.Cards[cn.req].name + " 카드</b> 필요";
            }
            if (lift) return S.alarm != null ? "경보 중 — 엘리베이터 정지" : "[E] 엘리베이터";
            if (trap != null) return "<b>" + trap.label + "</b> 가동 중 — 닿으면 죽는다";
            if (notice != null) { note = true; return notice.text; }
            if (tgt != null) return S.anySee && OthersSee(tgt.Ref) ? "누가 보고 있어서 먹을 수 없다" :
                    p.eatCd > 0 ? "소화 중… " + Mathf.CeilToInt(p.eatCd) + "초 뒤에 먹을 수 있다" :
                    "[E] " + Data.Forms[tgt.form].name + " 먹기" + (tgt.isNpc ? (p.form == "body" ? " · 뒤에서 접근할 것" : " · 변장 중이라 덮칠 수 있다") : Data.Forms[tgt.form].Big ? " · 덩치가 커서 문은 못 지나간다" : "");
            if (PlayerHidden()) return "환풍구 안 — 아무도 볼 수 없다";
            if (PlayerInDark() && !S.anySee) return "어둠 속 — 바로 옆까지 오지 않으면 못 알아챈다";
            if (f.Big) return f.name + " — 방 안에 숨기 좋다 · 문은 너무 좁아서 못 나간다";
            if (f.type == FormType.Body) return G.def.lethal ? "본모습은 누구에게도 보이면 안 된다" : "본모습을 들키면 경보가 울린다";
            if (p.form == "cart") return "카트는 보는 앞에서도 천천히 굴러갈 수 있다";
            if (f.type == FormType.Object) return f.zone == "wall" ? "보는 앞에서는 멈출 것 · 벽에 붙어 있어야 자연스럽다" : "보는 앞에서는 멈출 것";
            return f.name + "의 모습 — 보는 앞에서도 걸을 수 있다";
        }

        /* 개발용: 인스펙터에서 층을 골라 바로 시작 (카드 지급) */
        [ContextMenu("Dev: 3F에서 시작")] void Dev3F() { DevStart("3F"); }
        [ContextMenu("Dev: 5F에서 시작")] void Dev5F() { DevStart("5F"); }
        public void DevStart(string fid)
        {
            if (hud != null) hud.HideAllOverlays();
            int idx = Array.IndexOf(Data.FloorOrder, fid);
            var cards = Data.CardOrder.Take(Mathf.Max(0, idx + (idx >= 4 ? 1 : 0))).ToList();
            if (fid == "5F") cards = Data.CardOrder.ToList();
            deaths = 0;
            NewGame();
            S.cards = cards;
            if (fid != "B1") GoFloor(fid, G.def.lift != null || FloorGeo.All[fid].def.lift != null ? "lift" : "stairs");
            else { EnterFloorVisual(); SaveCheckpoint(); }
            BeginPlay();
            UpdateCamera(0, true);
        }
    }
}
