// 레벨 에디터: 씬 창에 층을 2.5D 그대로 띄워 놓고 칠하고 옮기며 고친다.
// 메뉴: 표본07 → 레벨 에디터 (Ctrl/Cmd+Shift+L)
//
//  - 층 데이터는 Assets/Resources/Levels/<층>.asset (층마다 파일 하나 → 같이 작업해도 덜 부딪힘)
//  - 고치면 바로 저장되고, Play를 누르면 고친 맵으로 게임이 돈다
//  - 씬에 그려진 층은 미리보기라 씬 파일에는 저장되지 않는다
//
// 도구
//  타일   : 클릭·드래그로 칠하기, Shift+드래그 = 사각형 채우기, Ctrl/Cmd+클릭 = 그 칸 글자 집기
//  방     : 방 클릭 = 선택, 모서리·가운데 손잡이로 크기·위치, "새 방" 켜고 드래그 = 새 방
//  사람   : 사람 클릭 = 선택, 끌어서 이동, Shift+클릭 = 순찰 지점 추가, Delete = 지우기
//  물건   : 빈 바닥 클릭 = 놓기, 끌어서 이동, Delete = 지우기
//  장치   : 문·콘솔·게시물·엘리베이터 칸은 칸 클릭으로 놓기 / 계단·함정·헬리패드는 드래그로 사각형
//  지점   : 시작·계단 도착·엘리베이터 도착·수조·지원병 등장·추가 조명
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Specimen07.EditorTools
{
    public class LevelEditor : EditorWindow
    {
        public const string Dir = "Assets/Resources/Levels";
        const string PreviewName = "[레벨 미리보기 — 저장 안 됨]";

        static readonly string[] TOOL_NAMES = { "보기", "타일", "방", "사람", "물건", "장치", "지점" };
        const int T_VIEW = 0, T_TILE = 1, T_ROOM = 2, T_NPC = 3, T_OBJ = 4, T_DEV = 5, T_PT = 6;
        static readonly string[] DEV_NAMES = { "문", "콘솔", "게시물", "엘리베이터", "계단", "함정", "헬리패드" };
        const int D_DOOR = 0, D_CON = 1, D_NOTE = 2, D_LIFT = 3, D_STAIRS = 4, D_TRAP = 5, D_PAD = 6;
        static readonly string[] PT_NAMES = { "시작", "계단 도착", "엘베 도착", "수조", "지원병 등장", "추가 조명" };
        const int P_START = 0, P_STAIRS = 1, P_LIFT = 2, P_TANK = 3, P_SPAWN = 4, P_LAMP = 5;
        static readonly string[] KINDS = { "hall", "office", "lab", "store", "rest", "iso", "sec", "cold", "mech", "wing", "dir", "exit", "lift", "roof" };
        static readonly string[] KIND_NAMES = { "hall 복도·홀", "office 사무실", "lab 실험실", "store 창고", "rest 휴게실", "iso 격리실", "sec 보안실", "cold 냉동실", "mech 기계실", "wing 표본동", "dir 소장실", "exit 계단실", "lift 엘리베이터", "roof 옥상" };
        static readonly string[] PALS = { "cleaner", "researcher", "researcher2", "guard", "senior", "soldier", "director" };

        // 타일 팔레트: 글자, 이름, 색
        static readonly char[] TILE_CH = { '#', '.', 'T', 'S', 'M', 'V', 'W', 'A', 'G', 'v' };
        static readonly string[] TILE_NAME = { "벽", "바닥", "책상", "선반", "설비", "자판기", "탱크", "실외기", "유리", "환풍구" };
        static readonly Color[] TILE_COL =
        {
            new Color(0.25f, 0.27f, 0.55f), new Color(0.55f, 0.58f, 0.7f), new Color(0.6f, 0.43f, 0.3f), new Color(0.3f, 0.36f, 0.42f), new Color(0.25f, 0.55f, 0.4f),
            new Color(0.75f, 0.25f, 0.3f), new Color(0.35f, 0.7f, 0.95f), new Color(0.55f, 0.6f, 0.65f), new Color(0.6f, 0.9f, 1f), new Color(0.6f, 0.5f, 0.9f),
        };

        [SerializeField] string floorId = "B1";
        [SerializeField] LevelAsset lv;
        [SerializeField] int tool = T_VIEW, devKind, ptKind, brushSize = 1, sel = -1, selWp = -1, selRect = -1;
        [SerializeField] char brush = '#';
        [SerializeField] string objForm = "chair";
        [SerializeField] bool showPreview = true, showRoutes = true, showLabels = true, newRoomMode;
        [SerializeField] Vector2 lastClick = new Vector2(-1, -1);
        Vector2 scroll;

        GameObject previewGo; WorldView3D preview;
        bool staticDirty = true, actorsDirty = true; double lastChange;
        string buildError;
        readonly HashSet<int> pendingCells = new HashSet<int>();
        List<Problem> problems;
        class Problem { public string text; public Vector2 at; }

        // 드래그 상태
        bool dragging, rectDrag; Vector2Int dragFrom, dragTo, lastPaint;

        [MenuItem("표본07/레벨 에디터 %#l", false, 0)]
        public static void Open() { var w = GetWindow<LevelEditor>("레벨 에디터"); w.minSize = new Vector2(330, 420); w.Show(); }

        /* ================= 수명 ================= */
        void OnEnable()
        {
            SceneView.duringSceneGui += OnScene;
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (lv == null) Load(floorId, false);
            staticDirty = actorsDirty = true;
        }
        void OnDisable()
        {
            SceneView.duringSceneGui -= OnScene;
            Undo.undoRedoPerformed -= OnUndo;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            ClearPreview();
        }
        void OnUndo() { if (lv != null) lv.FixSize(); staticDirty = actorsDirty = true; lastChange = 0; Repaint(); }
        void OnPlayMode(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.ExitingEditMode) ClearPreview();
            else if (s == PlayModeStateChange.EnteredEditMode) { staticDirty = actorsDirty = true; lastChange = 0; }
        }

        void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || lv == null) return;
            if (!showPreview) { if (previewGo != null) ClearPreview(); return; }
            double now = EditorApplication.timeSinceStartup;
            bool idle = GUIUtility.hotControl == 0 && !dragging;
            if (staticDirty && idle && now - lastChange > 0.15) { RebuildStatic(); return; }
            if (actorsDirty && !staticDirty && now - lastChange > 0.04) RebuildActors();
        }

        /* ================= 층 불러오기 ================= */
        static string PathOf(string id) { return Dir + "/" + id + ".asset"; }
        public static LevelAsset LoadOrCreate(string id)
        {
            var a = AssetDatabase.LoadAssetAtPath<LevelAsset>(PathOf(id));
            if (a != null) return a;
            var def = FloorDefs.All.FirstOrDefault(d => d.id == id);
            if (def == null) return null;
            Directory.CreateDirectory(Dir);
            a = CreateInstance<LevelAsset>();
            a.FromDef(def);
            AssetDatabase.CreateAsset(a, PathOf(id));
            AssetDatabase.SaveAssets();
            Debug.Log("[표본07] 레벨 에셋을 만들었어요: " + PathOf(id) + " (코드에 있던 기본 맵에서 가져옴)");
            return a;
        }
        void Load(string id, bool frame)
        {
            floorId = id;
            lv = LoadOrCreate(id);
            sel = selWp = selRect = -1; problems = null; pendingCells.Clear();
            staticDirty = actorsDirty = true; lastChange = 0;
            if (frame) FrameFloor(false);
            Repaint();
        }

        /* ================= 미리보기 ================= */
        void EnsurePreview()
        {
            if (previewGo != null && preview != null) return;
            // 스크립트를 다시 불러오면 예전 미리보기가 주인 없이 남으므로 이름으로 찾아 지운다
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go != null && go.name == PreviewName && go.transform.parent == null && !EditorUtility.IsPersistent(go)) DestroyImmediate(go);
            previewGo = new GameObject(PreviewName) { hideFlags = HideFlags.DontSave };
            preview = previewGo.AddComponent<WorldView3D>();
            preview.hideFlags = HideFlags.DontSave;
        }
        void ClearPreview()
        {
            if (previewGo != null) DestroyImmediate(previewGo);
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go != null && go.name == PreviewName && go.transform.parent == null && !EditorUtility.IsPersistent(go)) DestroyImmediate(go);
            previewGo = null; preview = null;
        }
        void RebuildStatic()
        {
            staticDirty = false; actorsDirty = false; pendingCells.Clear(); buildError = null;
            try
            {
                EnsurePreview();
                Art.ClearCache();
                var g = new FloorGeo(lv.ToDef());
                preview.BuildPreview(g);
                preview.BuildPreviewActors(g.def);
                SceneVisibilityManager.instance.DisablePicking(previewGo, true);   // 미리보기를 눌러도 선택되지 않게
            }
            catch (System.Exception ex) { buildError = ex.Message; Debug.LogException(ex); }
            SceneView.RepaintAll(); Repaint();
        }
        void RebuildActors()
        {
            actorsDirty = false;
            if (preview == null) { staticDirty = true; return; }
            try { preview.BuildPreviewActors(lv.ToDef()); SceneVisibilityManager.instance.DisablePicking(previewGo, true); }
            catch (System.Exception ex) { buildError = ex.Message; }
            SceneView.RepaintAll();
        }

        // 바꿈 기록: undo 묶음 + 다시 그리기 예약 + 저장
        void Edit(string what) { Undo.RecordObject(lv, what); }
        void Changed(bool statics)
        {
            EditorUtility.SetDirty(lv);
            if (statics) staticDirty = true; else actorsDirty = true;
            lastChange = EditorApplication.timeSinceStartup;
            problems = null;
            Repaint();
        }
        void Save() { if (lv != null) AssetDatabase.SaveAssetIfDirty(lv); }

        /* ================= 좌표 ================= */
        static Vector3 W(float x, float y, float h = 0.04f) { return new Vector3(x, h, -y); }
        static Vector2 G(Vector3 p) { return new Vector2(p.x, -p.z); }
        static bool Mouse(Vector2 guiPos, out Vector2 g)
        {
            var r = HandleUtility.GUIPointToWorldRay(guiPos);
            g = Vector2.zero;
            if (Mathf.Abs(r.direction.y) < 1e-4f) return false;
            float t = -r.origin.y / r.direction.y;
            if (t < 0) return false;
            g = G(r.origin + r.direction * t);
            return true;
        }
        static Vector2 Snap(Vector2 p, bool fine) { float s = fine ? 0.1f : 0.5f; return new Vector2(Mathf.Round(p.x / s) * s, Mathf.Round(p.y / s) * s); }
        static Vector2Int Cell(Vector2 p) { return new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)); }
        static float HS(Vector3 p) { return HandleUtility.GetHandleSize(p); }

        /* ================= 창 ================= */
        void OnGUI()
        {
            if (EditorApplication.isPlaying) { EditorGUILayout.HelpBox("Play 중에는 고칠 수 없어요. Play를 끝내면 다시 보여요.", MessageType.Info); return; }

            // 층 고르기
            EditorGUILayout.Space(4);
            int fi = System.Array.IndexOf(Data.FloorOrder, floorId);
            int nfi = GUILayout.Toolbar(Mathf.Max(0, fi), Data.FloorOrder);
            if (nfi != fi || lv == null) { Save(); Load(Data.FloorOrder[nfi], true); }
            if (lv == null) { EditorGUILayout.HelpBox("층 데이터를 찾을 수 없어요.", MessageType.Warning); return; }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(lv.id + " " + lv.floorName + "  (" + lv.w + "×" + lv.h + ")", EditorStyles.boldLabel);
                if (GUILayout.Button("에셋", GUILayout.Width(44))) { Selection.activeObject = lv; EditorGUIUtility.PingObject(lv); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                bool sp = GUILayout.Toggle(showPreview, "씬에 보기", "Button");
                if (sp != showPreview) { showPreview = sp; staticDirty = true; lastChange = 0; if (!sp) ClearPreview(); }
                showRoutes = GUILayout.Toggle(showRoutes, "경로", "Button");
                showLabels = GUILayout.Toggle(showLabels, "이름", "Button");
                if (GUILayout.Button("층 전체")) FrameFloor(false);
                if (GUILayout.Button("게임 각도")) FrameFloor(true);
            }
            if (!string.IsNullOrEmpty(buildError)) EditorGUILayout.HelpBox("미리보기를 그리다 문제가 생겼어요: " + buildError, MessageType.Error);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 이 층 테스트")) TestPlay(false);
                using (new EditorGUI.DisabledScope(lastClick.x < 0))
                    if (GUILayout.Button(new GUIContent("▶ 클릭한 자리에서", "씬에서 마지막으로 클릭한 칸에서 시작"))) TestPlay(true);
                if (GUILayout.Button("검사")) problems = Check();
            }
            DrawProblems();

            EditorGUILayout.Space(6);
            int nt = GUILayout.Toolbar(tool, TOOL_NAMES);
            if (nt != tool) { tool = nt; sel = selWp = selRect = -1; newRoomMode = false; SceneView.RepaintAll(); }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (tool)
            {
                case T_VIEW: GuiView(); break;
                case T_TILE: GuiTile(); break;
                case T_ROOM: GuiRoom(); break;
                case T_NPC: GuiNpc(); break;
                case T_OBJ: GuiObj(); break;
                case T_DEV: GuiDev(); break;
                case T_PT: GuiPt(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        void GuiView()
        {
            EditorGUILayout.HelpBox("씬 창에서 층을 둘러볼 수 있어요. 위에서 도구를 고르면 씬에서 바로 고칠 수 있어요.\n" +
                "고친 내용은 바로 저장되고 Ctrl/Cmd+Z로 되돌릴 수 있어요.", MessageType.None);
            var so = new SerializedObject(lv);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(so.FindProperty("floorName"), new GUIContent("층 이름"));
            EditorGUILayout.PropertyField(so.FindProperty("sub"), new GUIContent("부제"));
            EditorGUILayout.PropertyField(so.FindProperty("lethal"), new GUIContent("발각 즉시 끝"));
            EditorGUILayout.PropertyField(so.FindProperty("dark"), new GUIContent("어둡기"));
            EditorGUILayout.PropertyField(so.FindProperty("reinforce"), new GUIContent("경보 시 지원병"), true);
            if (EditorGUI.EndChangeCheck()) { so.ApplyModifiedProperties(); Changed(true); Save(); }

            EditorGUILayout.Space(6);
            GUILayout.Label("층 크기", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                nw = EditorGUILayout.IntField("가로", nw <= 0 ? lv.w : nw);
                nh = EditorGUILayout.IntField("세로", nh <= 0 ? lv.h : nh);
            }
            if ((nw != lv.w || nh != lv.h) && GUILayout.Button("크기 바꾸기 (늘린 곳은 벽, 줄인 곳은 잘림)"))
            {
                Edit("층 크기"); lv.w = Mathf.Clamp(nw, 8, 200); lv.h = Mathf.Clamp(nh, 8, 200); lv.FixSize(); Changed(true); Save(); FrameFloor(false);
            }
            EditorGUILayout.Space(6);
            if (GUILayout.Button("모든 층 에셋 만들기 (아직 없는 것만)")) { foreach (var id in Data.FloorOrder) LoadOrCreate(id); }
        }
        int nw, nh;

        /* ---------- 타일 ---------- */
        void GuiTile()
        {
            EditorGUILayout.HelpBox("클릭·드래그 = 칠하기 · Shift+드래그 = 사각형 채우기 · Ctrl/Cmd+클릭 = 글자 집기\n책상·선반은 게임에서 먹고 변신할 수 있어요.", MessageType.None);
            for (int i = 0; i < TILE_CH.Length; i++)
            {
                if (i % 2 == 0) EditorGUILayout.BeginHorizontal();
                var old = GUI.backgroundColor; GUI.backgroundColor = TILE_COL[i];
                bool on = GUILayout.Toggle(brush == TILE_CH[i], TILE_NAME[i] + "  " + TILE_CH[i], "Button", GUILayout.Height(24));
                GUI.backgroundColor = old;
                if (on && brush != TILE_CH[i]) brush = TILE_CH[i];
                if (i % 2 == 1 || i == TILE_CH.Length - 1) EditorGUILayout.EndHorizontal();
            }
            brushSize = EditorGUILayout.IntSlider("붓 크기", brushSize, 1, 5);
        }

        /* ---------- 방 ---------- */
        void GuiRoom()
        {
            EditorGUILayout.HelpBox("방 = 이름·종류(조명·바닥 무늬·소품)·출입 제한의 단위예요. 벽은 타일로 칠해요.\n방을 클릭해 고르고, 모서리·가운데 손잡이로 옮겨요.", MessageType.None);
            newRoomMode = GUILayout.Toggle(newRoomMode, newRoomMode ? "씬에서 드래그해서 새 방 그리기…" : "+ 새 방 (드래그)", "Button", GUILayout.Height(24));
            ListPicker(lv.rooms.Count, i => lv.rooms[i].name + "  [" + lv.rooms[i].kind + "]");
            if (sel < 0 || sel >= lv.rooms.Count) return;
            var r = lv.rooms[sel];
            EditorGUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.TextField("이름", r.name);
            int ki = Mathf.Max(0, System.Array.IndexOf(KINDS, r.kind));
            ki = EditorGUILayout.Popup("종류", ki, KIND_NAMES);
            bool label = EditorGUILayout.Toggle("바닥에 이름 쓰기", r.label);
            var rect = EditorGUILayout.RectIntField("영역 (x, y, 가로, 세로)", r.rect);
            GUILayout.Label("들어가도 이상하지 않은 사람 (모두 끄면 누구나)");
            var allow = (r.allow ?? "").Split(',').Select(s => s.Trim()).Where(s => s != "").ToList();
            var nallow = new List<string>();
            using (new EditorGUILayout.HorizontalScope())
                foreach (var c in Data.CharOrder) if (GUILayout.Toggle(allow.Contains(c), Data.Chars[c].name, "Button")) nallow.Add(c);
            if (EditorGUI.EndChangeCheck())
            {
                Edit("방 고치기");
                r.name = name; r.kind = KINDS[ki]; r.label = label; r.rect = rect; r.allow = string.Join(",", nallow);
                Changed(true); Save();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("둘레 벽 + 안쪽 바닥으로 칠하기")) { Edit("방 칠하기"); PaintRoom(r.rect); Changed(true); Save(); }
                if (GUILayout.Button("지우기", GUILayout.Width(60))) { Edit("방 지우기"); lv.rooms.RemoveAt(sel); sel = -1; Changed(true); Save(); }
            }
        }
        void PaintRoom(RectInt r)
        {
            for (int y = r.y - 1; y <= r.y + r.height; y++)
                for (int x = r.x - 1; x <= r.x + r.width; x++)
                {
                    bool border = x < r.x || y < r.y || x >= r.x + r.width || y >= r.y + r.height;
                    char cur = lv.Tile(x, y);
                    if (border) { if (cur == '.') lv.SetTile(x, y, '#'); }
                    else lv.SetTile(x, y, '.');
                }
        }

        /* ---------- 사람 ---------- */
        void GuiNpc()
        {
            EditorGUILayout.HelpBox("사람 클릭 = 고르기 · 끌기 = 이동 · Shift+클릭 = 순찰 지점 추가 · Delete = 지우기\n순찰은 1 → 2 → … → 다시 1 순서로 돌아요. Ctrl/Cmd 누르고 끌면 0.1칸 단위.", MessageType.None);
            if (GUILayout.Button("+ 새 사람 (화면 가운데)", GUILayout.Height(24)))
            {
                Edit("사람 추가");
                var c = ViewCenter();
                lv.npcs.Add(new LevelAsset.Npc { id = UniqueId("n"), pos = c, route = new List<LevelAsset.Wp> { new LevelAsset.Wp { pos = c, wait = 2 } } });
                sel = lv.npcs.Count - 1; Changed(false); Save();
            }
            ListPicker(lv.npcs.Count, i => lv.npcs[i].label + "  (" + (Data.Chars.ContainsKey(lv.npcs[i].ch) ? Data.Chars[lv.npcs[i].ch].name : lv.npcs[i].ch) + ")");
            if (sel < 0 || sel >= lv.npcs.Count) return;
            var n = lv.npcs[sel];
            EditorGUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            string label = EditorGUILayout.TextField("이름표", n.label);
            string nid = EditorGUILayout.TextField("id (층 안에서 겹치지 않게)", n.id);
            int ci = Mathf.Max(0, System.Array.IndexOf(Data.CharOrder, n.ch));
            ci = EditorGUILayout.Popup("직업", ci, Data.CharOrder.Select(c => Data.Chars[c].name).ToArray());
            int pi = System.Array.IndexOf(PALS, n.pal);
            var palOpts = PALS.Concat(new[] { "직접 입력" }).ToArray();
            pi = EditorGUILayout.Popup("그림", pi < 0 ? PALS.Length : pi, palOpts);
            string pal = pi < PALS.Length ? PALS[pi] : EditorGUILayout.TextField(" ", n.pal);
            float face = EditorGUILayout.Slider("처음 보는 방향 (도)", n.face, -180, 180);
            if (EditorGUI.EndChangeCheck())
            {
                Edit("사람 고치기");
                string ch = Data.CharOrder[ci];
                if (ch != n.ch && pi < PALS.Length && (n.pal == n.ch || n.pal.StartsWith(n.ch))) pal = ch;   // 직업을 바꾸면 그림도 따라가게
                n.label = label; n.id = nid; n.ch = ch; n.pal = pal; n.face = face; Changed(false); Save();
            }
            GUILayout.Label("순찰 지점", EditorStyles.boldLabel);
            for (int i = 0; i < n.route.Count; i++)
            {
                var wp = n.route[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Toggle(selWp == i, (i + 1).ToString(), "Button", GUILayout.Width(28))) { if (selWp != i) { selWp = i; SceneView.RepaintAll(); } }
                    EditorGUI.BeginChangeCheck();
                    EditorGUIUtility.labelWidth = 34;
                    float wait = EditorGUILayout.FloatField("멈춤", wp.wait, GUILayout.Width(80));
                    float look = EditorGUILayout.FloatField("방향", wp.look, GUILayout.Width(80));
                    EditorGUIUtility.labelWidth = 0;
                    if (EditorGUI.EndChangeCheck()) { Edit("순찰 지점"); wp.wait = Mathf.Max(0, wait); wp.look = look; Changed(false); Save(); }
                    if (GUILayout.Button("↑", GUILayout.Width(22)) && i > 0) { Edit("순서"); n.route.RemoveAt(i); n.route.Insert(i - 1, wp); Changed(false); Save(); }
                    if (GUILayout.Button("✕", GUILayout.Width(22))) { Edit("순찰 지점 지우기"); n.route.RemoveAt(i); selWp = -1; Changed(false); Save(); break; }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("복제")) { Edit("사람 복제"); var c = JsonUtility.FromJson<LevelAsset.Npc>(JsonUtility.ToJson(n)); c.id = UniqueId(n.id); c.pos += new Vector2(1, 0); foreach (var wp in c.route) wp.pos += new Vector2(1, 0); lv.npcs.Add(c); sel = lv.npcs.Count - 1; Changed(false); Save(); }
                if (GUILayout.Button("지우기")) { Edit("사람 지우기"); lv.npcs.RemoveAt(sel); sel = selWp = -1; Changed(false); Save(); }
            }
        }
        string UniqueId(string baseId)
        {
            string pre = lv.id.ToLower() + "_";
            for (int i = 1; ; i++) { string id = baseId.StartsWith(pre) ? baseId + i : pre + baseId + i; if (!lv.npcs.Any(n => n.id == id)) return id; }
        }

        /* ---------- 물건 ---------- */
        void GuiObj()
        {
            EditorGUILayout.HelpBox("빈 바닥 클릭 = 고른 물건 놓기 · 물건 클릭 = 고르기 · 끌기 = 이동 · Delete = 지우기\n쓰레기통·카트·화분·정수기는 벽에 붙여야 자연스러워요(벽 옆이 아니면 NPC가 수상하게 봐요).", MessageType.None);
            var forms = Data.Forms.Values.Where(f => f.type == FormType.Object && !f.Big).Select(f => f.id).ToArray();
            int fi = Mathf.Max(0, System.Array.IndexOf(forms, objForm));
            fi = GUILayout.SelectionGrid(fi, forms.Select(f => Data.Forms[f].name).ToArray(), 3);
            objForm = forms[fi];
            GUILayout.Label("이 층 물건: " + lv.objects.Count + "개  " + string.Join(", ", lv.objects.GroupBy(ob => ob.form).Select(g => (Data.Forms.ContainsKey(g.Key) ? Data.Forms[g.Key].name : g.Key) + " " + g.Count())), EditorStyles.wordWrappedMiniLabel);
            if (sel < 0 || sel >= lv.objects.Count) return;
            var o = lv.objects[sel];
            EditorGUI.BeginChangeCheck();
            int oi = Mathf.Max(0, System.Array.IndexOf(forms, o.form));
            oi = EditorGUILayout.Popup("고른 물건", oi, forms.Select(f => Data.Forms[f].name).ToArray());
            float rot = o.form == "chair" ? EditorGUILayout.Slider("방향 (도)", o.rot * Mathf.Rad2Deg, -180, 180) * Mathf.Deg2Rad : o.rot;
            if (EditorGUI.EndChangeCheck()) { Edit("물건 고치기"); o.form = forms[oi]; o.rot = rot; Changed(false); Save(); }
            if (GUILayout.Button("지우기")) { Edit("물건 지우기"); lv.objects.RemoveAt(sel); sel = -1; Changed(false); Save(); }
        }

        /* ---------- 장치 ---------- */
        void GuiDev()
        {
            int nd = GUILayout.SelectionGrid(devKind, DEV_NAMES, 4);
            if (nd != devKind) { devKind = nd; sel = selRect = -1; SceneView.RepaintAll(); }
            string help = devKind == D_DOOR ? "칸 클릭 = 문 놓기 (벽 사이 1칸 통로에). 필요한 카드 색으로 보여요." :
                          devKind == D_CON ? "칸 클릭 = 콘솔 놓기. 함정 id를 적으면 그 함정을 끄는 콘솔이 돼요." :
                          devKind == D_NOTE ? "칸 클릭 = 게시물 놓기 (벽 앞). 읽으면 글이 나와요." :
                          devKind == D_LIFT ? "칸 클릭 = 엘리베이터 칸 놓기/고르기." :
                          devKind == D_STAIRS ? "드래그 = 계단 영역. 이어지는 층과 방향(> 올라감, < 내려감)을 정해요." :
                          devKind == D_TRAP ? "드래그 = 함정 영역 추가 (고른 함정에). 고른 게 없으면 새 함정." :
                          "드래그 = 헬리패드 영역 (옥상).";
            EditorGUILayout.HelpBox(help + "\n고른 것은 Delete로 지워요.", MessageType.None);

            SerializedProperty list = null; string listName = null;
            switch (devKind)
            {
                case D_DOOR: listName = "doors"; ListPicker(lv.doors.Count, i => "문 " + lv.doors[i].pos + "  " + CardName(lv.doors[i].req) + "  " + lv.doors[i].label); break;
                case D_CON: listName = "consoles"; ListPicker(lv.consoles.Count, i => "콘솔 " + lv.consoles[i].pos + "  " + lv.consoles[i].label); break;
                case D_NOTE: listName = "notices"; ListPicker(lv.notices.Count, i => "게시물 " + lv.notices[i].pos + "  " + Short(lv.notices[i].text)); break;
                case D_LIFT: listName = "lift"; ListPicker(lv.lift.Count, i => "엘리베이터 칸 " + lv.lift[i]); break;
                case D_STAIRS: listName = "stairs"; ListPicker(lv.stairs.Count, i => "계단 → " + lv.stairs[i].to + "  " + lv.stairs[i].rect); break;
                case D_TRAP: listName = "traps"; ListPicker(lv.traps.Count, i => lv.traps[i].id + " " + lv.traps[i].kind + "  " + lv.traps[i].label); break;
                case D_PAD:
                    EditorGUI.BeginChangeCheck();
                    bool has = EditorGUILayout.Toggle("헬리패드 있음", lv.hasPad);
                    var pr = EditorGUILayout.RectIntField("영역", lv.pad);
                    if (EditorGUI.EndChangeCheck()) { Edit("헬리패드"); lv.hasPad = has; lv.pad = pr; Changed(true); Save(); }
                    return;
            }
            var so = new SerializedObject(lv);
            list = so.FindProperty(listName);
            if (sel < 0 || list == null || sel >= list.arraySize) return;
            EditorGUILayout.Space(4);
            var el = list.GetArrayElementAtIndex(sel);
            EditorGUI.BeginChangeCheck();
            if (devKind == D_DOOR || devKind == D_CON)
            {
                var req = el.FindPropertyRelative("req");
                int ri = Mathf.Max(0, System.Array.IndexOf(Data.CardOrder, req.stringValue));
                ri = EditorGUILayout.Popup("필요한 카드", ri, Data.CardOrder.Select(CardName).ToArray());
                req.stringValue = Data.CardOrder[ri];
                EditorGUILayout.PropertyField(el.FindPropertyRelative("pos"), new GUIContent("칸"));
                EditorGUILayout.PropertyField(el.FindPropertyRelative("label"), new GUIContent("이름표"));
                if (devKind == D_CON)
                {
                    var ids = AllTrapIds();
                    var trap = el.FindPropertyRelative("trap");
                    var opts = new[] { "(없음)" }.Concat(ids).ToArray();
                    int ti = string.IsNullOrEmpty(trap.stringValue) ? 0 : System.Array.IndexOf(opts, trap.stringValue);
                    ti = EditorGUILayout.Popup("끄는 함정", ti < 0 ? 0 : ti, opts);
                    trap.stringValue = ti == 0 ? "" : opts[ti];
                    EditorGUILayout.PropertyField(el.FindPropertyRelative("heli"), new GUIContent("헬기 호출"));
                }
            }
            else EditorGUILayout.PropertyField(el, new GUIContent(DEV_NAMES[devKind]), true);
            if (EditorGUI.EndChangeCheck()) { so.ApplyModifiedProperties(); Changed(true); Save(); }
            if (GUILayout.Button("지우기")) { Edit("지우기"); DeleteDev(); }
        }
        static string CardName(string id) { return Data.Cards.ContainsKey(id) ? Data.Cards[id].name + " 카드" : id; }
        static string Short(string s) { s = (s ?? "").Replace("\n", " "); return s.Length > 18 ? s.Substring(0, 18) + "…" : s; }
        static string[] AllTrapIds()
        {
            var ids = new List<string>();
            foreach (var id in Data.FloorOrder)
            {
                var a = AssetDatabase.LoadAssetAtPath<LevelAsset>(PathOf(id));
                if (a != null) ids.AddRange(a.traps.Select(t => t.id));
                else { var d = FloorDefs.All.FirstOrDefault(x => x.id == id); if (d != null) ids.AddRange(d.traps.Select(t => t.id)); }
            }
            return ids.Distinct().ToArray();
        }
        void DeleteDev()
        {
            if (sel < 0) return;
            switch (devKind)
            {
                case D_DOOR: if (sel < lv.doors.Count) lv.doors.RemoveAt(sel); break;
                case D_CON: if (sel < lv.consoles.Count) lv.consoles.RemoveAt(sel); break;
                case D_NOTE: if (sel < lv.notices.Count) lv.notices.RemoveAt(sel); break;
                case D_LIFT: if (sel < lv.lift.Count) lv.lift.RemoveAt(sel); break;
                case D_STAIRS: if (sel < lv.stairs.Count) lv.stairs.RemoveAt(sel); break;
                case D_TRAP:
                    if (sel < lv.traps.Count)
                    {
                        var t = lv.traps[sel];
                        if (selRect >= 0 && selRect < t.rects.Count && t.rects.Count > 1) { t.rects.RemoveAt(selRect); selRect = -1; Changed(true); Save(); return; }
                        lv.traps.RemoveAt(sel);
                    }
                    break;
            }
            sel = selRect = -1; Changed(true); Save();
        }

        /* ---------- 지점 ---------- */
        void GuiPt()
        {
            int np = GUILayout.SelectionGrid(ptKind, PT_NAMES, 3);
            if (np != ptKind) { ptKind = np; sel = -1; SceneView.RepaintAll(); }
            EditorGUILayout.HelpBox(ptKind <= P_TANK ? "칸 클릭 = 그 자리로 옮기기 · 손잡이 끌기 = 이동" : "빈 곳 클릭 = 추가 · 클릭 = 고르기 · 끌기 = 이동 · Delete = 지우기", MessageType.None);
            var so = new SerializedObject(lv);
            EditorGUI.BeginChangeCheck();
            switch (ptKind)
            {
                case P_START: EditorGUILayout.PropertyField(so.FindProperty("hasStart"), new GUIContent("게임 시작 지점 있음")); EditorGUILayout.PropertyField(so.FindProperty("start"), new GUIContent("위치")); break;
                case P_STAIRS: EditorGUILayout.PropertyField(so.FindProperty("hasStairsArrive"), new GUIContent("계단 도착 있음")); EditorGUILayout.PropertyField(so.FindProperty("stairsArrive"), new GUIContent("위치")); break;
                case P_LIFT: EditorGUILayout.PropertyField(so.FindProperty("hasLiftArrive"), new GUIContent("엘베 도착 있음")); EditorGUILayout.PropertyField(so.FindProperty("liftArrive"), new GUIContent("위치")); break;
                case P_TANK: EditorGUILayout.PropertyField(so.FindProperty("hasTank"), new GUIContent("주인공 수조 있음")); EditorGUILayout.PropertyField(so.FindProperty("tank"), new GUIContent("위치")); break;
                case P_SPAWN:
                    ListPicker(lv.spawns.Count, i => "등장 " + lv.spawns[i]);
                    break;
                case P_LAMP:
                    ListPicker(lv.lamps.Count, i => "조명 " + lv.lamps[i].pos + "  반경 " + lv.lamps[i].r);
                    if (sel >= 0 && sel < lv.lamps.Count) EditorGUILayout.PropertyField(so.FindProperty("lamps").GetArrayElementAtIndex(sel), new GUIContent("고른 조명"), true);
                    break;
            }
            if (EditorGUI.EndChangeCheck()) { so.ApplyModifiedProperties(); Changed(ptKind == P_LAMP); Save(); }
            if ((ptKind == P_SPAWN || ptKind == P_LAMP) && sel >= 0 && GUILayout.Button("지우기")) DeletePt();
        }
        void DeletePt()
        {
            Edit("지우기");
            if (ptKind == P_SPAWN && sel < lv.spawns.Count) lv.spawns.RemoveAt(sel);
            if (ptKind == P_LAMP && sel < lv.lamps.Count) lv.lamps.RemoveAt(sel);
            sel = -1; Changed(ptKind == P_LAMP); Save();
        }

        void ListPicker(int count, System.Func<int, string> name)
        {
            for (int i = 0; i < count; i++)
            {
                bool on = GUILayout.Toggle(sel == i, name(i), "Button");
                if (on && sel != i) { sel = i; selWp = selRect = -1; SceneView.RepaintAll(); FocusSelected(); }
            }
        }

        /* ================= 검사 ================= */
        void DrawProblems()
        {
            if (problems == null) return;
            if (problems.Count == 0) { EditorGUILayout.HelpBox("문제 없음 ✓", MessageType.Info); return; }
            EditorGUILayout.HelpBox("확인할 것 " + problems.Count + "개", MessageType.Warning);
            foreach (var p in problems.Take(12))
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(p.text, EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button("보기", GUILayout.Width(40))) LookAt(p.at, 8);
                }
        }
        List<Problem> Check()
        {
            var list = new List<Problem>();
            FloorGeo g;
            try { g = new FloorGeo(lv.ToDef()); } catch (System.Exception ex) { list.Add(new Problem { text = "맵을 만들 수 없음: " + ex.Message }); return list; }
            System.Func<float, float, bool> solid = (x, y) => FloorGeo.SOLID.Contains(g.Tile(Mathf.FloorToInt(x), Mathf.FloorToInt(y)));
            foreach (var n in lv.npcs)
            {
                if (solid(n.pos.x, n.pos.y)) list.Add(new Problem { text = n.label + ": 시작 위치가 벽·가구 안", at = n.pos });
                for (int i = 0; i < n.route.Count; i++) if (solid(n.route[i].pos.x, n.route[i].pos.y)) list.Add(new Problem { text = n.label + ": 순찰 지점 " + (i + 1) + "이 벽·가구 안", at = n.route[i].pos });
            }
            foreach (var o in lv.objects) if (solid(o.pos.x, o.pos.y)) list.Add(new Problem { text = (Data.Forms.ContainsKey(o.form) ? Data.Forms[o.form].name : o.form) + ": 벽·가구 안", at = o.pos });
            var ids = new HashSet<string>();
            foreach (var n in lv.npcs) if (!ids.Add(n.id)) list.Add(new Problem { text = "사람 id가 겹침: " + n.id, at = n.pos });
            foreach (var d in lv.doors)
            {
                bool ns = !solid(d.pos.x + 0.5f, d.pos.y - 0.5f) && !solid(d.pos.x + 0.5f, d.pos.y + 1.5f);
                bool ew = !solid(d.pos.x - 0.5f, d.pos.y + 0.5f) && !solid(d.pos.x + 1.5f, d.pos.y + 0.5f);
                if (!ns && !ew) list.Add(new Problem { text = "문 " + d.pos + ": 양쪽이 바닥이 아님 (통로를 막음)", at = d.pos + Vector2.one * 0.5f });
            }
            // 도착 지점에서 갈 수 있는 곳 (문·환풍구는 지나갈 수 있다고 봄)
            var from = new List<Vector2>();
            if (lv.hasStart) from.Add(lv.start); if (lv.hasStairsArrive) from.Add(lv.stairsArrive); if (lv.hasLiftArrive) from.Add(lv.liftArrive);
            foreach (var p in from) if (solid(p.x, p.y)) list.Add(new Problem { text = "도착 지점이 벽·가구 안", at = p });
            if (from.Count == 0) list.Add(new Problem { text = "도착 지점(시작·계단·엘베)이 하나도 없음", at = new Vector2(lv.w / 2f, lv.h / 2f) });
            else
            {
                var reach = new bool[g.w * g.h]; var q = new Queue<Vector2Int>();
                foreach (var p in from) { var c = Cell(p); if (c.x >= 0 && c.y >= 0 && c.x < g.w && c.y < g.h && !reach[c.y * g.w + c.x]) { reach[c.y * g.w + c.x] = true; q.Enqueue(c); } }
                System.Func<char, bool> pass = ch => !FloorGeo.SOLID.Contains(ch) || ch == 'D' || ch == 'v';
                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    foreach (var o in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
                    {
                        int x = c.x + o.x, y = c.y + o.y;
                        if (x < 0 || y < 0 || x >= g.w || y >= g.h || reach[y * g.w + x] || !pass(g.Tile(x, y))) continue;
                        reach[y * g.w + x] = true; q.Enqueue(new Vector2Int(x, y));
                    }
                }
                System.Func<int, int, bool> near = (x, y) =>
                {
                    for (int yy = y - 1; yy <= y + 1; yy++) for (int xx = x - 1; xx <= x + 1; xx++)
                        if (xx >= 0 && yy >= 0 && xx < g.w && yy < g.h && reach[yy * g.w + xx]) return true;
                    return false;
                };
                foreach (var z in g.zones)
                {
                    bool any = false;
                    for (int y = z.y0; y <= z.y1 && !any; y++) for (int x = z.x0; x <= z.x1; x++) if (x >= 0 && y >= 0 && x < g.w && y < g.h && reach[y * g.w + x]) { any = true; break; }
                    if (!any) list.Add(new Problem { text = "갈 수 없는 방: " + z.name, at = new Vector2((z.x0 + z.x1 + 1) / 2f, (z.y0 + z.y1 + 1) / 2f) });
                }
                foreach (var c in lv.consoles) if (!near(c.pos.x, c.pos.y)) list.Add(new Problem { text = "닿을 수 없는 콘솔 " + c.label, at = c.pos + Vector2.one * 0.5f });
                foreach (var s in lv.stairs) if (!near(s.rect.x, s.rect.y)) list.Add(new Problem { text = "닿을 수 없는 계단 → " + s.to, at = new Vector2(s.rect.center.x, s.rect.center.y) });
                foreach (var t in lv.lift) if (!near(t.x, t.y)) list.Add(new Problem { text = "닿을 수 없는 엘리베이터 칸", at = t + Vector2.one * 0.5f });
                foreach (var n in lv.npcs) { var c = Cell(n.pos); if (c.x >= 0 && c.y >= 0 && c.x < g.w && c.y < g.h && !reach[c.y * g.w + c.x] && !solid(n.pos.x, n.pos.y)) list.Add(new Problem { text = n.label + ": 주인공이 갈 수 없는 곳에 있음 (일부러면 무시)", at = n.pos }); }
            }
            var trapIds = AllTrapIds();
            foreach (var c in lv.consoles) if (!string.IsNullOrEmpty(c.trap) && !trapIds.Contains(c.trap)) list.Add(new Problem { text = "콘솔 " + c.label + ": 없는 함정 id " + c.trap, at = c.pos });
            return list;
        }

        /* ================= 테스트 ================= */
        void TestPlay(bool atClick)
        {
            Save();
            if (EditorSceneManager.GetActiveScene().path != SceneSetup.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (!File.Exists(SceneSetup.ScenePath)) { Debug.LogWarning("[표본07] 게임 씬이 없어요. 메뉴 '표본07 → 게임 씬 만들기'"); return; }
                EditorSceneManager.OpenScene(SceneSetup.ScenePath);
            }
            PlayerPrefs.SetString(Game.DEV_FLOOR, floorId);
            PlayerPrefs.SetFloat(Game.DEV_X, atClick ? lastClick.x : -1);
            PlayerPrefs.SetFloat(Game.DEV_Y, atClick ? lastClick.y : -1);
            PlayerPrefs.Save();
            EditorApplication.EnterPlaymode();
        }

        /* ================= 씬 카메라 ================= */
        void FrameFloor(bool gameAngle)
        {
            if (lv == null) return;
            var sv = SceneView.lastActiveSceneView; if (sv == null) return;
            sv.in2DMode = false;
            var c = W(lv.w / 2f, lv.h / 2f, 0);
            if (gameAngle) sv.LookAt(c, Quaternion.Euler(50, 0, 0), 12f);
            else sv.LookAt(c, Quaternion.Euler(70, 0, 0), Mathf.Max(lv.w, lv.h) * 0.62f);
        }
        void LookAt(Vector2 p, float size)
        {
            var sv = SceneView.lastActiveSceneView; if (sv == null) return;
            sv.in2DMode = false;
            sv.LookAt(W(p.x, p.y, 0), sv.rotation, size);
        }
        Vector2 ViewCenter()
        {
            var sv = SceneView.lastActiveSceneView;
            var p = sv != null ? G(sv.pivot) : new Vector2(lv.w / 2f, lv.h / 2f);
            return new Vector2(Mathf.Floor(Mathf.Clamp(p.x, 1, lv.w - 2)) + 0.5f, Mathf.Floor(Mathf.Clamp(p.y, 1, lv.h - 2)) + 0.5f);
        }
        void FocusSelected()
        {
            Vector2? p = null;
            if (tool == T_ROOM && sel < lv.rooms.Count) p = lv.rooms[sel].rect.center;
            if (tool == T_NPC && sel < lv.npcs.Count) p = lv.npcs[sel].pos;
            if (tool == T_OBJ && sel < lv.objects.Count) p = lv.objects[sel].pos;
            if (p.HasValue) { var sv = SceneView.lastActiveSceneView; if (sv != null && sv.size > 14) LookAt(p.Value, 10); else if (sv != null) LookAt(p.Value, sv.size); }
        }

        /* ================= 씬 창 ================= */
        GUIStyle labelStyle, smallStyle;
        void Styles()
        {
            if (labelStyle != null) return;
            labelStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
            labelStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            smallStyle.normal.textColor = new Color(1, 1, 1, 0.9f);
        }

        void OnScene(SceneView sv)
        {
            if (lv == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Styles();
            var e = Event.current;
            int id = GUIUtility.GetControlID("LevelEditor".GetHashCode(), FocusType.Passive);
            if (tool != T_VIEW && e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

            Vector2 m; bool onGround = Mouse(e.mousePosition, out m);
            var cell = Cell(m);
            bool inside = onGround && cell.x >= 0 && cell.y >= 0 && cell.x < lv.w && cell.y < lv.h;
            if (e.type == EventType.MouseDown && e.button == 0 && inside) lastClick = new Vector2(cell.x + 0.5f, cell.y + 0.5f);

            // 층 테두리
            Handles.color = new Color(1, 1, 1, 0.35f);
            RectOutline(new RectInt(0, 0, lv.w, lv.h), new Color(0, 0, 0, 0), new Color(1, 1, 1, 0.4f), 0.02f);

            DrawRooms(tool == T_ROOM);
            DrawDevices(tool == T_DEV);
            DrawPoints(tool == T_PT);
            DrawObjects(tool == T_OBJ);
            DrawNpcs(tool == T_NPC);
            foreach (var c in pendingCells) { int x = c % lv.w, y = c / lv.w; int ti = System.Array.IndexOf(TILE_CH, lv.Tile(x, y)); var col = ti >= 0 ? TILE_COL[ti] : Color.magenta; col.a = 0.85f; RectOutline(new RectInt(x, y, 1, 1), col, col, 0.08f); }

            switch (tool)
            {
                case T_TILE: SceneTile(e, id, m, cell, inside); break;
                case T_ROOM: SceneRoom(e, id, m, cell, inside); break;
                case T_NPC: SceneNpc(e, id, m, inside); break;
                case T_OBJ: SceneObj(e, id, m, cell, inside); break;
                case T_DEV: SceneDev(e, id, m, cell, inside); break;
                case T_PT: ScenePt(e, id, m, cell, inside); break;
            }

            // 마우스 아래 칸 정보
            if (inside && tool != T_VIEW)
            {
                Handles.BeginGUI();
                var z = RoomAt(cell.x, cell.y);
                string info = cell.x + ", " + cell.y + "   '" + lv.Tile(cell.x, cell.y) + "'" + (z != null ? "   " + z.name : "");
                var r = new Rect(8, sv.position.height - 52, 320, 22);
                EditorGUI.DrawRect(r, new Color(0, 0, 0, 0.6f));
                GUI.Label(new Rect(r.x + 6, r.y + 3, r.width, r.height), info, EditorStyles.whiteLabel);
                Handles.EndGUI();
            }
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) sv.Repaint();
        }

        LevelAsset.Room RoomAt(int x, int y)
        {
            LevelAsset.Room best = null;
            foreach (var r in lv.rooms) if (r.rect.Contains(new Vector2Int(x, y)) && (best == null || r.rect.width * r.rect.height < best.rect.width * best.rect.height)) best = r;
            return best;
        }

        static void RectOutline(RectInt r, Color fill, Color line, float h)
        {
            var v = new[] { W(r.xMin, r.yMin, h), W(r.xMax, r.yMin, h), W(r.xMax, r.yMax, h), W(r.xMin, r.yMax, h) };
            Handles.DrawSolidRectangleWithOutline(v, fill, line);
        }
        static void Rect2(Vector2Int a, Vector2Int b, Color fill, Color line, float h)
        {
            var r = new RectInt(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(a.x - b.x) + 1, Mathf.Abs(a.y - b.y) + 1);
            RectOutline(r, fill, line, h);
        }
        static RectInt MakeRect(Vector2Int a, Vector2Int b) { return new RectInt(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(a.x - b.x) + 1, Mathf.Abs(a.y - b.y) + 1); }

        // 클릭으로 바닥을 눌렀는가 (손잡이를 누른 게 아니라)
        static bool GroundDown(Event e, int id) { return e.type == EventType.MouseDown && e.button == 0 && !e.alt && HandleUtility.nearestControl == id; }

        // 땅 위에서 끄는 손잡이 (칸 단위·반 칸 단위 맞춤). 바뀌면 true
        static bool Drag(ref Vector2 p, float h, Color c, float size, bool fine, bool snapCell = false)
        {
            var wp = W(p.x, p.y, h);
            Handles.color = c;
            EditorGUI.BeginChangeCheck();
            var np = Handles.Slider2D(wp, Vector3.up, Vector3.right, Vector3.forward, HS(wp) * size, Handles.CircleHandleCap, Vector2.zero);
            if (!EditorGUI.EndChangeCheck()) return false;
            var g = G(np);
            p = snapCell ? new Vector2(Mathf.Floor(g.x) + 0.5f, Mathf.Floor(g.y) + 0.5f) : Snap(g, fine || Event.current.control || Event.current.command);
            return true;
        }
        static bool Pick(Vector2 p, float h, Color c, float size)
        {
            var wp = W(p.x, p.y, h);
            Handles.color = c;
            return Handles.Button(wp, Quaternion.LookRotation(Vector3.up), HS(wp) * size, HS(wp) * size * 1.3f, Handles.CircleHandleCap);
        }

        /* ---------- 타일 ---------- */
        void SceneTile(Event e, int id, Vector2 m, Vector2Int cell, bool inside)
        {
            if (inside)
            {
                int b = brushSize; var o = new Vector2Int(cell.x - (b - 1) / 2, cell.y - (b - 1) / 2);
                int ti = System.Array.IndexOf(TILE_CH, brush);
                var col = ti >= 0 ? TILE_COL[ti] : Color.white;
                if (rectDrag && dragging) Rect2(dragFrom, dragTo, new Color(col.r, col.g, col.b, 0.35f), Color.white, 0.1f);
                else RectOutline(new RectInt(o.x, o.y, b, b), new Color(col.r, col.g, col.b, 0.25f), Color.white, 0.1f);
            }
            if (GroundDown(e, id) && inside)
            {
                if (e.control || e.command) { brush = lv.Tile(cell.x, cell.y); Repaint(); e.Use(); return; }
                GUIUtility.hotControl = id; dragging = true; rectDrag = e.shift;
                dragFrom = dragTo = lastPaint = cell;
                Undo.IncrementCurrentGroup();
                Edit("타일 칠하기");
                if (!rectDrag) PaintAt(cell);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id && dragging)
            {
                if (inside || rectDrag)
                {
                    var c = new Vector2Int(Mathf.Clamp(cell.x, 0, lv.w - 1), Mathf.Clamp(cell.y, 0, lv.h - 1));
                    if (rectDrag) dragTo = c;
                    else
                    {
                        Edit("타일 칠하기");
                        // 빨리 끌어도 빈칸이 안 생기게 지난 칸에서 선으로 잇는다
                        int n = Mathf.Max(Mathf.Abs(c.x - lastPaint.x), Mathf.Abs(c.y - lastPaint.y));
                        for (int i = 1; i <= n; i++) PaintAt(new Vector2Int(Mathf.RoundToInt(Mathf.Lerp(lastPaint.x, c.x, i / (float)n)), Mathf.RoundToInt(Mathf.Lerp(lastPaint.y, c.y, i / (float)n))));
                        lastPaint = c;
                    }
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id && dragging)
            {
                if (rectDrag)
                {
                    Edit("사각형 채우기");
                    var r = MakeRect(dragFrom, dragTo);
                    for (int y = r.yMin; y < r.yMax; y++) for (int x = r.xMin; x < r.xMax; x++) SetCell(x, y);
                }
                GUIUtility.hotControl = 0; dragging = rectDrag = false;
                Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                Changed(true); Save();
                e.Use();
            }
        }
        void PaintAt(Vector2Int c)
        {
            int b = brushSize; var o = new Vector2Int(c.x - (b - 1) / 2, c.y - (b - 1) / 2);
            for (int y = o.y; y < o.y + b; y++) for (int x = o.x; x < o.x + b; x++) SetCell(x, y);
            lastChange = EditorApplication.timeSinceStartup; EditorUtility.SetDirty(lv);
        }
        void SetCell(int x, int y)
        {
            if (x < 0 || y < 0 || x >= lv.w || y >= lv.h || lv.Tile(x, y) == brush) return;
            lv.SetTile(x, y, brush); pendingCells.Add(y * lv.w + x);
        }

        /* ---------- 방 ---------- */
        void DrawRooms(bool active)
        {
            for (int i = 0; i < lv.rooms.Count; i++)
            {
                var r = lv.rooms[i]; bool s = active && i == sel;
                var line = s ? new Color(1f, 0.85f, 0.3f, 1) : new Color(1, 1, 1, active ? 0.55f : 0.18f);
                RectOutline(r.rect, s ? new Color(1f, 0.85f, 0.3f, 0.08f) : new Color(0, 0, 0, 0), line, 0.03f);
                if (showLabels && (active || tool == T_VIEW)) Handles.Label(W(r.rect.center.x, r.rect.center.y, 0.3f), r.name + "\n" + r.kind, labelStyle);
            }
        }
        void SceneRoom(Event e, int id, Vector2 m, Vector2Int cell, bool inside)
        {
            if (newRoomMode)
            {
                if (dragging) Rect2(dragFrom, dragTo, new Color(1f, 0.85f, 0.3f, 0.2f), Color.yellow, 0.1f);
                if (GroundDown(e, id) && inside) { GUIUtility.hotControl = id; dragging = true; dragFrom = dragTo = cell; e.Use(); }
                else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id && dragging) { dragTo = new Vector2Int(Mathf.Clamp(cell.x, 0, lv.w - 1), Mathf.Clamp(cell.y, 0, lv.h - 1)); e.Use(); }
                else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id && dragging)
                {
                    GUIUtility.hotControl = 0; dragging = false;
                    var r = MakeRect(dragFrom, dragTo);
                    if (r.width >= 2 && r.height >= 2)
                    {
                        Edit("새 방");
                        lv.rooms.Add(new LevelAsset.Room { name = "새 방 " + (lv.rooms.Count + 1), rect = r });
                        sel = lv.rooms.Count - 1; newRoomMode = false; Changed(true); Save();
                    }
                    e.Use();
                }
                return;
            }
            if (sel >= 0 && sel < lv.rooms.Count)
            {
                var r = lv.rooms[sel];
                var rect = r.rect;
                bool ch = false;
                // 모서리 4개
                Vector2[] corners = { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax) };
                for (int k = 0; k < 4; k++)
                {
                    var p = corners[k];
                    if (DragCorner(ref p))
                    {
                        int xa = k == 0 || k == 3 ? Mathf.RoundToInt(p.x) : rect.xMin, xb = k == 1 || k == 2 ? Mathf.RoundToInt(p.x) : rect.xMax;
                        int ya = k == 0 || k == 1 ? Mathf.RoundToInt(p.y) : rect.yMin, yb = k == 2 || k == 3 ? Mathf.RoundToInt(p.y) : rect.yMax;
                        rect = new RectInt(Mathf.Min(xa, xb - 1), Mathf.Min(ya, yb - 1), Mathf.Max(1, Mathf.Abs(xb - xa)), Mathf.Max(1, Mathf.Abs(yb - ya)));
                        ch = true;
                    }
                }
                // 가운데: 통째로 옮기기
                var c = new Vector2(rect.xMin + rect.width / 2f, rect.yMin + rect.height / 2f);
                var c0 = c;
                if (Drag(ref c, 0.1f, new Color(1f, 0.85f, 0.3f), 0.2f, false))
                {
                    var d = new Vector2Int(Mathf.RoundToInt(c.x - c0.x), Mathf.RoundToInt(c.y - c0.y));
                    if (d != Vector2Int.zero) { rect.position += d; ch = true; }
                }
                if (ch) { Edit("방 영역"); r.rect = rect; Changed(true); }
                if (e.type == EventType.MouseUp && e.button == 0) Save();
            }
            if (GroundDown(e, id) && inside)
            {
                int best = -1;
                for (int i = 0; i < lv.rooms.Count; i++)
                    if (lv.rooms[i].rect.Contains(cell) && (best < 0 || lv.rooms[i].rect.width * lv.rooms[i].rect.height < lv.rooms[best].rect.width * lv.rooms[best].rect.height)) best = i;
                sel = best; Repaint(); e.Use();
            }
        }
        static bool DragCorner(ref Vector2 p)
        {
            var wp = W(p.x, p.y, 0.1f);
            Handles.color = new Color(1f, 0.85f, 0.3f);
            EditorGUI.BeginChangeCheck();
            var np = Handles.Slider2D(wp, Vector3.up, Vector3.right, Vector3.forward, HS(wp) * 0.09f, Handles.CubeHandleCap, Vector2.zero);
            if (!EditorGUI.EndChangeCheck()) return false;
            var g = G(np); p = new Vector2(Mathf.Round(g.x), Mathf.Round(g.y));
            return true;
        }

        /* ---------- 사람 ---------- */
        static Color CharColor(string ch)
        {
            switch (ch)
            {
                case "cleaner": return new Color(0.7f, 0.75f, 0.8f);
                case "researcher": return new Color(0.5f, 0.75f, 0.95f);
                case "guard": return new Color(0.95f, 0.7f, 0.3f);
                case "senior": return new Color(0.7f, 0.55f, 0.95f);
                case "soldier": return new Color(0.55f, 0.8f, 0.45f);
                case "director": return new Color(1f, 0.35f, 0.3f);
            }
            return Color.white;
        }
        void DrawNpcs(bool active)
        {
            for (int i = 0; i < lv.npcs.Count; i++)
            {
                var n = lv.npcs[i]; bool s = active && i == sel;
                var col = CharColor(n.ch);
                if (showRoutes || s)
                {
                    if (n.route.Count > 1)
                    {
                        var pts = n.route.Select(w => W(w.pos.x, w.pos.y, 0.06f)).ToList(); pts.Add(pts[0]);
                        Handles.color = new Color(col.r, col.g, col.b, s ? 0.95f : active ? 0.5f : 0.3f);
                        Handles.DrawAAPolyLine(s ? 4f : 2.5f, pts.ToArray());
                    }
                    if (s) for (int k = 0; k < n.route.Count; k++) Handles.Label(W(n.route[k].pos.x, n.route[k].pos.y, 0.4f), (k + 1).ToString(), labelStyle);
                }
                // 처음 보는 방향
                var wp = W(n.pos.x, n.pos.y, 0.06f);
                float f = n.face * Mathf.Deg2Rad;
                Handles.color = new Color(col.r, col.g, col.b, active ? 1 : 0.6f);
                Handles.DrawAAPolyLine(3f, wp, W(n.pos.x + Mathf.Cos(f) * 0.8f, n.pos.y + Mathf.Sin(f) * 0.8f, 0.06f));
                if (!active) Handles.DrawWireDisc(wp, Vector3.up, 0.32f);
                if (showLabels && active) Handles.Label(W(n.pos.x, n.pos.y, 1.9f), n.label, smallStyle);
            }
        }
        void SceneNpc(Event e, int id, Vector2 m, bool inside)
        {
            for (int i = 0; i < lv.npcs.Count; i++)
            {
                var n = lv.npcs[i]; var col = CharColor(n.ch);
                if (i != sel)
                {
                    if (Pick(n.pos, 0.06f, col, 0.14f)) { sel = i; selWp = -1; Repaint(); }
                    continue;
                }
                var p = n.pos;
                if (Drag(ref p, 0.06f, Color.white, 0.18f, false))
                {
                    Edit("사람 이동");
                    // 첫 순찰 지점이 시작 위치와 같았으면 같이 옮긴다
                    if (n.route.Count > 0 && (n.route[0].pos - n.pos).sqrMagnitude < 0.01f) n.route[0].pos = p;
                    n.pos = p; Changed(false);
                }
                for (int k = 0; k < n.route.Count; k++)
                {
                    var wpos = n.route[k].pos;
                    if (Drag(ref wpos, 0.06f, k == selWp ? Color.yellow : new Color(col.r, col.g, col.b), 0.12f, false))
                    { Edit("순찰 지점 이동"); n.route[k].pos = wpos; selWp = k; Changed(false); Repaint(); }
                }
            }
            if (e.type == EventType.MouseUp && e.button == 0) Save();
            if (GroundDown(e, id) && inside)
            {
                if (e.shift && sel >= 0 && sel < lv.npcs.Count)
                {
                    Edit("순찰 지점 추가");
                    var n = lv.npcs[sel];
                    int at = selWp >= 0 && selWp < n.route.Count ? selWp + 1 : n.route.Count;
                    n.route.Insert(at, new LevelAsset.Wp { pos = Snap(m, false), wait = 1.5f, look = 0 });
                    selWp = at; Changed(false); Save(); Repaint();
                }
                else { sel = -1; selWp = -1; Repaint(); }
                e.Use();
            }
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && sel >= 0 && sel < lv.npcs.Count)
            {
                var n = lv.npcs[sel];
                if (selWp >= 0 && selWp < n.route.Count) { Edit("순찰 지점 지우기"); n.route.RemoveAt(selWp); selWp = -1; }
                else { Edit("사람 지우기"); lv.npcs.RemoveAt(sel); sel = -1; }
                Changed(false); Save(); Repaint(); e.Use();
            }
        }

        /* ---------- 물건 ---------- */
        void DrawObjects(bool active)
        {
            if (!active && !showLabels) return;
            for (int i = 0; i < lv.objects.Count; i++)
            {
                var o = lv.objects[i];
                if (active && showLabels) Handles.Label(W(o.pos.x, o.pos.y, 0.9f), Data.Forms.ContainsKey(o.form) ? Data.Forms[o.form].name : o.form, smallStyle);
            }
        }
        void SceneObj(Event e, int id, Vector2 m, Vector2Int cell, bool inside)
        {
            var pink = new Color(1f, 0.49f, 0.55f);
            for (int i = 0; i < lv.objects.Count; i++)
            {
                var o = lv.objects[i];
                if (i != sel) { if (Pick(o.pos, 0.05f, pink, 0.12f)) { sel = i; Repaint(); } continue; }
                var p = o.pos;
                if (Drag(ref p, 0.05f, Color.white, 0.17f, false)) { Edit("물건 이동"); o.pos = p; Changed(false); }
            }
            if (e.type == EventType.MouseUp && e.button == 0) Save();
            if (GroundDown(e, id) && inside)
            {
                Edit("물건 놓기");
                lv.objects.Add(new LevelAsset.Obj { form = objForm, pos = new Vector2(cell.x + 0.5f, cell.y + 0.5f) });
                sel = lv.objects.Count - 1; Changed(false); Save(); Repaint(); e.Use();
            }
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && sel >= 0 && sel < lv.objects.Count)
            { Edit("물건 지우기"); lv.objects.RemoveAt(sel); sel = -1; Changed(false); Save(); Repaint(); e.Use(); }
        }

        /* ---------- 장치 ---------- */
        void DrawDevices(bool active)
        {
            float a = active ? 0.9f : 0.45f;
            foreach (var d in lv.doors) { var c = Data.Cards.ContainsKey(d.req) ? Data.Cards[d.req].color : Color.white; RectOutline(new RectInt(d.pos.x, d.pos.y, 1, 1), new Color(c.r, c.g, c.b, 0.25f * a), new Color(c.r, c.g, c.b, a), 2.0f); }
            if (!active) return;
            for (int i = 0; i < lv.consoles.Count; i++) Cell1(lv.consoles[i].pos, new Color(1f, 0.83f, 0.42f), devKind == D_CON && i == sel, "콘솔");
            for (int i = 0; i < lv.notices.Count; i++) Cell1(lv.notices[i].pos, new Color(0.85f, 0.65f, 0.4f), devKind == D_NOTE && i == sel, "게시물");
            for (int i = 0; i < lv.lift.Count; i++) Cell1(lv.lift[i], new Color(0.5f, 0.88f, 0.69f), devKind == D_LIFT && i == sel, null);
            for (int i = 0; i < lv.doors.Count; i++) if (devKind == D_DOOR && i == sel) Cell1(lv.doors[i].pos, Color.white, true, null);
            for (int i = 0; i < lv.stairs.Count; i++)
            {
                bool s = devKind == D_STAIRS && i == sel;
                RectOutline(lv.stairs[i].rect, new Color(0.6f, 0.9f, 1f, 0.15f), s ? Color.yellow : new Color(0.6f, 0.9f, 1f), 0.1f);
                Handles.Label(W(lv.stairs[i].rect.center.x, lv.stairs[i].rect.center.y, 0.4f), "계단 " + lv.stairs[i].dir + " " + lv.stairs[i].to, smallStyle);
            }
            for (int i = 0; i < lv.traps.Count; i++)
            {
                var t = lv.traps[i]; var c = t.kind == "laser" ? new Color(1f, 0.3f, 0.3f) : new Color(0.6f, 0.9f, 0.35f);
                for (int k = 0; k < t.rects.Count; k++)
                {
                    bool s = devKind == D_TRAP && i == sel;
                    RectOutline(t.rects[k], new Color(c.r, c.g, c.b, 0.18f), s && k == selRect ? Color.yellow : s ? Color.white : c, 0.12f);
                    Handles.Label(W(t.rects[k].center.x, t.rects[k].center.y, 0.4f), t.id + " " + t.kind, smallStyle);
                }
            }
            if (lv.hasPad) RectOutline(lv.pad, new Color(1, 1, 1, 0.08f), Color.white, 0.1f);
        }
        void Cell1(Vector2Int p, Color c, bool s, string label)
        {
            RectOutline(new RectInt(p.x, p.y, 1, 1), new Color(c.r, c.g, c.b, s ? 0.5f : 0.25f), s ? Color.yellow : c, 1.0f);
            if (label != null && showLabels) Handles.Label(W(p.x + 0.5f, p.y + 0.5f, 1.2f), label, smallStyle);
        }
        void SceneDev(Event e, int id, Vector2 m, Vector2Int cell, bool inside)
        {
            bool rectKind = devKind == D_STAIRS || devKind == D_TRAP || devKind == D_PAD;
            if (rectKind)
            {
                // 고른 사각형: 모서리 손잡이
                RectInt? cur = null;
                if (devKind == D_STAIRS && sel >= 0 && sel < lv.stairs.Count) cur = lv.stairs[sel].rect;
                if (devKind == D_TRAP && sel >= 0 && sel < lv.traps.Count && selRect >= 0 && selRect < lv.traps[sel].rects.Count) cur = lv.traps[sel].rects[selRect];
                if (devKind == D_PAD && lv.hasPad) cur = lv.pad;
                if (cur.HasValue)
                {
                    var rect = cur.Value; bool ch = false;
                    Vector2[] corners = { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMax) };
                    for (int k = 0; k < 2; k++)
                    {
                        var p = corners[k];
                        if (DragCorner(ref p))
                        {
                            if (k == 0) rect = new RectInt((int)p.x, (int)p.y, Mathf.Max(1, rect.xMax - (int)p.x), Mathf.Max(1, rect.yMax - (int)p.y));
                            else rect = new RectInt(rect.xMin, rect.yMin, Mathf.Max(1, (int)p.x - rect.xMin), Mathf.Max(1, (int)p.y - rect.yMin));
                            ch = true;
                        }
                    }
                    if (ch)
                    {
                        Edit("영역");
                        if (devKind == D_STAIRS) lv.stairs[sel].rect = rect;
                        else if (devKind == D_TRAP) lv.traps[sel].rects[selRect] = rect;
                        else lv.pad = rect;
                        Changed(true);
                    }
                    if (e.type == EventType.MouseUp && e.button == 0) Save();
                }
                if (dragging) Rect2(dragFrom, dragTo, new Color(1, 1, 1, 0.2f), Color.yellow, 0.12f);
                if (GroundDown(e, id) && inside)
                {
                    // 기존 사각형 클릭 = 고르기, 빈 곳 드래그 = 새로
                    if (devKind == D_STAIRS) { int hit = lv.stairs.FindIndex(s => s.rect.Contains(cell)); if (hit >= 0) { sel = hit; Repaint(); e.Use(); return; } }
                    if (devKind == D_TRAP)
                        for (int i = 0; i < lv.traps.Count; i++) { int k = lv.traps[i].rects.FindIndex(r => r.Contains(cell)); if (k >= 0) { sel = i; selRect = k; Repaint(); e.Use(); return; } }
                    GUIUtility.hotControl = id; dragging = true; dragFrom = dragTo = cell; e.Use();
                }
                else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id && dragging) { dragTo = new Vector2Int(Mathf.Clamp(cell.x, 0, lv.w - 1), Mathf.Clamp(cell.y, 0, lv.h - 1)); e.Use(); }
                else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id && dragging)
                {
                    GUIUtility.hotControl = 0; dragging = false;
                    var r = MakeRect(dragFrom, dragTo);
                    Edit("영역 추가");
                    if (devKind == D_STAIRS) { lv.stairs.Add(new LevelAsset.Stairs { rect = r, to = NextFloor(), dir = ">" }); sel = lv.stairs.Count - 1; }
                    else if (devKind == D_PAD) { lv.hasPad = true; lv.pad = r; }
                    else
                    {
                        if (sel < 0 || sel >= lv.traps.Count) { lv.traps.Add(new LevelAsset.Trap { id = UniqueTrapId(), kind = "laser", label = "새 함정" }); sel = lv.traps.Count - 1; }
                        lv.traps[sel].rects.Add(r); selRect = lv.traps[sel].rects.Count - 1;
                    }
                    Changed(true); Save(); Repaint(); e.Use();
                }
            }
            else if (GroundDown(e, id) && inside)
            {
                Edit("장치 놓기");
                switch (devKind)
                {
                    case D_DOOR: { int hit = lv.doors.FindIndex(d => d.pos == cell); if (hit < 0) { lv.doors.Add(new LevelAsset.Door { pos = cell, req = "lab" }); hit = lv.doors.Count - 1; Changed(true); } sel = hit; break; }
                    case D_CON: { int hit = lv.consoles.FindIndex(d => d.pos == cell); if (hit < 0) { lv.consoles.Add(new LevelAsset.Console { pos = cell, req = "sec", label = "콘솔" }); hit = lv.consoles.Count - 1; Changed(true); } sel = hit; break; }
                    case D_NOTE: { int hit = lv.notices.FindIndex(d => d.pos == cell); if (hit < 0) { lv.notices.Add(new LevelAsset.Notice { pos = cell, text = "" }); hit = lv.notices.Count - 1; Changed(true); } sel = hit; break; }
                    case D_LIFT: { int hit = lv.lift.IndexOf(cell); if (hit < 0) { lv.lift.Add(cell); hit = lv.lift.Count - 1; Changed(true); } sel = hit; break; }
                }
                Save(); Repaint(); e.Use();
            }
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && (sel >= 0 || (devKind == D_PAD && lv.hasPad)))
            {
                Edit("지우기");
                if (devKind == D_PAD) { lv.hasPad = false; Changed(true); Save(); } else DeleteDev();
                Repaint(); e.Use();
            }
        }
        string NextFloor() { int i = System.Array.IndexOf(Data.FloorOrder, lv.id); return i >= 0 && i + 1 < Data.FloorOrder.Length ? Data.FloorOrder[i + 1] : lv.id; }
        string UniqueTrapId() { var ids = AllTrapIds(); for (int i = 1; ; i++) { string id = lv.id + "_T" + i; if (!ids.Contains(id)) return id; } }

        /* ---------- 지점 ---------- */
        void DrawPoints(bool active)
        {
            var mint = new Color(0.5f, 0.95f, 0.75f);
            System.Action<bool, Vector2, string> one = (has, p, name) =>
            {
                if (!has) return;
                var wp = W(p.x, p.y, 0.07f);
                Handles.color = new Color(mint.r, mint.g, mint.b, active ? 1 : 0.55f);
                Handles.DrawWireDisc(wp, Vector3.up, 0.4f); Handles.DrawWireDisc(wp, Vector3.up, 0.25f);
                if (showLabels) Handles.Label(W(p.x, p.y, 0.7f), name, smallStyle);
            };
            one(lv.hasStart, lv.start, "시작");
            one(lv.hasStairsArrive, lv.stairsArrive, "계단 도착");
            one(lv.hasLiftArrive, lv.liftArrive, "엘베 도착");
            one(lv.hasTank, lv.tank, "수조");
            if (!active) return;
            Handles.color = new Color(1f, 0.4f, 0.35f);
            foreach (var s in lv.spawns) { Handles.DrawWireDisc(W(s.x, s.y, 0.07f), Vector3.up, 0.35f); if (showLabels) Handles.Label(W(s.x, s.y, 0.7f), "지원병", smallStyle); }
            foreach (var l in lv.lamps) { Handles.color = new Color(1f, 0.95f, 0.6f, 0.8f); Handles.DrawWireDisc(W(l.pos.x, l.pos.y, 0.07f), Vector3.up, l.r); }
        }
        void ScenePt(Event e, int id, Vector2 m, Vector2Int cell, bool inside)
        {
            var c = new Color(0.5f, 0.95f, 0.75f);
            bool moved = false;
            switch (ptKind)
            {
                case P_START: if (lv.hasStart) { var p = lv.start; if (Drag(ref p, 0.07f, c, 0.2f, false, true)) { Edit("시작 지점"); lv.start = p; moved = true; } } break;
                case P_STAIRS: if (lv.hasStairsArrive) { var p = lv.stairsArrive; if (Drag(ref p, 0.07f, c, 0.2f, false, true)) { Edit("계단 도착"); lv.stairsArrive = p; moved = true; } } break;
                case P_LIFT: if (lv.hasLiftArrive) { var p = lv.liftArrive; if (Drag(ref p, 0.07f, c, 0.2f, false, true)) { Edit("엘베 도착"); lv.liftArrive = p; moved = true; } } break;
                case P_TANK: if (lv.hasTank) { var p = lv.tank; if (Drag(ref p, 0.07f, c, 0.2f, false, true)) { Edit("수조"); lv.tank = p; moved = true; } } break;
                case P_SPAWN:
                    for (int i = 0; i < lv.spawns.Count; i++)
                    {
                        var p = lv.spawns[i];
                        if (i != sel) { if (Pick(p, 0.07f, new Color(1f, 0.4f, 0.35f), 0.14f)) { sel = i; Repaint(); } continue; }
                        if (Drag(ref p, 0.07f, Color.white, 0.18f, false)) { Edit("지원병 등장"); lv.spawns[i] = p; moved = true; }
                    }
                    break;
                case P_LAMP:
                    for (int i = 0; i < lv.lamps.Count; i++)
                    {
                        var p = lv.lamps[i].pos;
                        if (i != sel) { if (Pick(p, 0.07f, new Color(1f, 0.95f, 0.6f), 0.14f)) { sel = i; Repaint(); } continue; }
                        if (Drag(ref p, 0.07f, Color.white, 0.18f, false)) { Edit("조명"); lv.lamps[i].pos = p; Changed(true); }
                    }
                    break;
            }
            if (moved) Changed(false);
            if (e.type == EventType.MouseUp && e.button == 0) Save();
            if (GroundDown(e, id) && inside)
            {
                var center = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                Edit("지점");
                switch (ptKind)
                {
                    case P_START: lv.hasStart = true; lv.start = center; break;
                    case P_STAIRS: lv.hasStairsArrive = true; lv.stairsArrive = center; break;
                    case P_LIFT: lv.hasLiftArrive = true; lv.liftArrive = center; break;
                    case P_TANK: lv.hasTank = true; lv.tank = center; break;
                    case P_SPAWN: lv.spawns.Add(center); sel = lv.spawns.Count - 1; break;
                    case P_LAMP: lv.lamps.Add(new LevelAsset.Lamp { pos = center }); sel = lv.lamps.Count - 1; break;
                }
                Changed(ptKind == P_LAMP); Save(); Repaint(); e.Use();
            }
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && sel >= 0 && (ptKind == P_SPAWN || ptKind == P_LAMP)) { DeletePt(); Repaint(); e.Use(); }
        }
    }
}
