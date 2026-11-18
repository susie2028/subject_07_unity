// 층(LevelFloor) 인스펙터: 타일 칠하기, 표시물 추가, 검사, 이 층 테스트
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Specimen07.EditorTools
{
    [CustomEditor(typeof(LevelFloor))]
    public class LevelFloorEditor : Editor
    {
        static readonly char[] TILE_CH = { '#', '.', 'T', 'S', 'M', 'V', 'W', 'A', 'G', 'v' };
        static readonly string[] TILE_NAME = { "벽", "바닥", "책상", "선반", "설비", "자판기", "탱크", "실외기", "유리", "환풍구" };
        static readonly Color[] TILE_COL =
        {
            new Color(0.25f, 0.27f, 0.55f), new Color(0.55f, 0.58f, 0.7f), new Color(0.6f, 0.43f, 0.3f), new Color(0.3f, 0.36f, 0.42f), new Color(0.25f, 0.55f, 0.4f),
            new Color(0.75f, 0.25f, 0.3f), new Color(0.35f, 0.7f, 0.95f), new Color(0.55f, 0.6f, 0.65f), new Color(0.6f, 0.9f, 1f), new Color(0.6f, 0.5f, 0.9f),
        };
        static bool painting; static char brush = '#'; static int brushSize = 1;
        static Vector2 lastClick = new Vector2(-1, -1);
        int nw, nh;
        List<KeyValuePair<string, Object>> problems;

        LevelFloor F { get { return (LevelFloor)target; } }

        void OnDisable() { if (painting) { painting = false; LevelSceneSync.Paused = false; } }

        public override void OnInspectorGUI()
        {
            var f = F;
            EditorGUILayout.HelpBox("자식 오브젝트(방·사람·물건·문…)를 Scene에서 클릭해 고르고 이동 툴(W)로 옮겨요. 늘릴 땐 복제(Ctrl/Cmd+D).\n" +
                "문·콘솔은 칸 가운데로, 방·계단 같은 영역은 칸 모서리로 자동으로 맞춰져요.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 이 층 테스트", GUILayout.Height(26))) TestPlay(f, false);
                using (new EditorGUI.DisabledScope(lastClick.x < 0))
                    if (GUILayout.Button(new GUIContent("▶ 클릭한 자리에서", "타일 칠하기 중 마지막으로 클릭한 칸에서 시작"), GUILayout.Height(26))) TestPlay(f, true);
                if (GUILayout.Button("검사", GUILayout.Height(26), GUILayout.Width(50))) problems = Check(f);
            }
            DrawProblems();

            // 타일 칠하기
            EditorGUILayout.Space(6);
            var old = GUI.backgroundColor;
            GUI.backgroundColor = painting ? new Color(1f, 0.8f, 0.4f) : old;
            bool np = GUILayout.Toggle(painting, painting ? "타일 칠하는 중 — Scene에서 클릭·드래그 (끄려면 다시 누르기)" : "🖌 타일 칠하기 (벽·바닥·책상…)", "Button", GUILayout.Height(28));
            GUI.backgroundColor = old;
            if (np != painting) { painting = np; LevelSceneSync.Paused = false; SceneView.RepaintAll(); if (painting) Tools.current = Tool.None; else Tools.current = Tool.Move; }
            if (painting)
            {
                EditorGUILayout.HelpBox("클릭·드래그 = 칠하기 · Shift+드래그 = 사각형 채우기 · Ctrl/Cmd+클릭 = 그 칸 글자 집기\n책상·선반은 게임에서 먹고 변신할 수 있어요.", MessageType.None);
                for (int i = 0; i < TILE_CH.Length; i++)
                {
                    if (i % 2 == 0) EditorGUILayout.BeginHorizontal();
                    GUI.backgroundColor = TILE_COL[i];
                    bool on = GUILayout.Toggle(brush == TILE_CH[i], TILE_NAME[i] + "  " + TILE_CH[i], "Button", GUILayout.Height(22));
                    GUI.backgroundColor = old;
                    if (on) brush = TILE_CH[i];
                    if (i % 2 == 1 || i == TILE_CH.Length - 1) EditorGUILayout.EndHorizontal();
                }
                brushSize = EditorGUILayout.IntSlider("붓 크기", brushSize, 1, 5);
            }

            // 표시물 추가
            EditorGUILayout.Space(6);
            GUILayout.Label("추가 (Scene 화면 가운데에 생겨요)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("방")) { var m = New<LevelRoom>(f, "방", "새 방"); m.size = new Vector2Int(6, 5); m.SetGamePos(Center(f) - new Vector2(3, 2)); Select(m); }
                if (GUILayout.Button("사람")) { var m = New<LevelNpc>(f, "사람", "새 연구원"); m.SetGamePos(Center(f)); m.npcId = ""; var w = new GameObject("1"); Undo.RegisterCreatedObjectUndo(w, "순찰 지점"); w.transform.SetParent(m.transform, false); w.AddComponent<LevelWaypoint>().SetGamePos(Center(f)); Select(m); }
                if (GUILayout.Button("물건")) { var m = New<LevelObject>(f, "물건", "의자"); m.SetGamePos(Center(f)); Select(m); }
                if (GUILayout.Button("문")) { var m = New<LevelDoor>(f, "문", "문"); m.SetGamePos(Center(f)); Select(m); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("콘솔")) { var m = New<LevelConsole>(f, "콘솔", "콘솔"); m.SetGamePos(Center(f)); Select(m); }
                if (GUILayout.Button("게시물")) { var m = New<LevelNotice>(f, "게시물", "게시물"); m.SetGamePos(Center(f)); Select(m); }
                if (GUILayout.Button("계단")) { var m = New<LevelStairs>(f, "계단", "계단"); m.size = new Vector2Int(2, 2); m.SetGamePos(Center(f)); Select(m); }
                if (GUILayout.Button("엘베 칸")) { var m = New<LevelLift>(f, "엘리베이터", "엘리베이터 칸"); m.SetGamePos(Center(f)); Select(m); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("함정")) { var m = New<LevelTrap>(f, "함정", "새 함정"); m.trapId = f.id + "_T" + Random.Range(10, 99); m.SetGamePos(Center(f)); var a = new GameObject("영역"); Undo.RegisterCreatedObjectUndo(a, "영역"); a.transform.SetParent(m.transform, false); var ar = a.AddComponent<LevelTrapArea>(); ar.size = new Vector2Int(3, 1); ar.SetGamePos(Center(f)); Select(ar); }
                if (GUILayout.Button("조명")) { var m = New<LevelLamp>(f, "조명", "조명"); m.SetGamePos(Center(f)); Select(m); }
                if (GUILayout.Button("지점")) { var m = New<LevelPoint>(f, "지점", "지점"); m.SetGamePos(Center(f)); Select(m); }
                if (GUILayout.Button("헬리패드")) { var m = New<LevelPad>(f, "지점", "헬리패드"); m.size = new Vector2Int(4, 4); m.SetGamePos(Center(f)); Select(m); }
            }

            EditorGUILayout.Space(8);
            DrawDefaultInspector();

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                nw = EditorGUILayout.IntField("가로", nw <= 0 ? f.w : nw);
                nh = EditorGUILayout.IntField("세로", nh <= 0 ? f.h : nh);
            }
            if ((nw != f.w || nh != f.h) && GUILayout.Button("층 크기 바꾸기 (늘린 곳은 벽, 줄인 곳은 잘림)"))
            { Undo.RecordObject(f, "층 크기"); f.w = Mathf.Clamp(nw, 8, 200); f.h = Mathf.Clamp(nh, 8, 200); f.FixSize(); EditorUtility.SetDirty(f); }
        }

        static T New<T>(LevelFloor f, string group, string name) where T : LevelMarker
        {
            var m = LevelBuild.Add<T>(f, group, name);
            Undo.RegisterCreatedObjectUndo(m.gameObject, "추가: " + name);
            return m;
        }
        static void Select(Component c) { Selection.activeGameObject = c.gameObject; Tools.current = Tool.Move; painting = false; }
        static Vector2 Center(LevelFloor f)
        {
            var sv = SceneView.lastActiveSceneView;
            Vector3 l = sv != null ? f.transform.InverseTransformPoint(sv.pivot) : new Vector3(f.w / 2f, 0, -f.h / 2f);
            return new Vector2(Mathf.Floor(Mathf.Clamp(l.x, 1, f.w - 2)) + 0.5f, Mathf.Floor(Mathf.Clamp(-l.z, 1, f.h - 2)) + 0.5f);
        }

        /* ===== Scene: 타일 칠하기 ===== */
        bool dragging, rectDrag; Vector2Int from, to, last;
        readonly HashSet<int> pending = new HashSet<int>();

        void OnSceneGUI()
        {
            if (!painting) return;
            var f = F; var e = Event.current;
            int id = GUIUtility.GetControlID("LevelPaint".GetHashCode(), FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

            Vector2 m; bool hit = Mouse(f, e.mousePosition, out m);
            var cell = new Vector2Int(Mathf.FloorToInt(m.x), Mathf.FloorToInt(m.y));
            bool inside = hit && cell.x >= 0 && cell.y >= 0 && cell.x < f.w && cell.y < f.h;
            int ti = System.Array.IndexOf(TILE_CH, brush); var col = ti >= 0 ? TILE_COL[ti] : Color.white;

            foreach (var c in pending) { int x = c % f.w, y = c / f.w; int k = System.Array.IndexOf(TILE_CH, f.Tile(x, y)); var cc = k >= 0 ? TILE_COL[k] : Color.magenta; cc.a = 0.85f; Quad(f, new RectInt(x, y, 1, 1), cc, cc, 0.08f); }
            if (inside)
            {
                if (dragging && rectDrag) Quad(f, Rect(from, to), new Color(col.r, col.g, col.b, 0.35f), Color.white, 0.1f);
                else { int b = brushSize; Quad(f, new RectInt(cell.x - (b - 1) / 2, cell.y - (b - 1) / 2, b, b), new Color(col.r, col.g, col.b, 0.3f), Color.white, 0.1f); }
                Handles.BeginGUI();
                var r = new UnityEngine.Rect(8, SceneView.currentDrawingSceneView.position.height - 52, 260, 22);
                EditorGUI.DrawRect(r, new Color(0, 0, 0, 0.6f));
                GUI.Label(new UnityEngine.Rect(r.x + 6, r.y + 3, r.width, r.height), cell.x + ", " + cell.y + "   '" + f.Tile(cell.x, cell.y) + "'   붓: " + brush, EditorStyles.whiteLabel);
                Handles.EndGUI();
            }

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && HandleUtility.nearestControl == id && inside)
            {
                lastClick = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                if (e.control || e.command) { brush = f.Tile(cell.x, cell.y); Repaint(); e.Use(); return; }
                GUIUtility.hotControl = id; dragging = true; rectDrag = e.shift; from = to = last = cell;
                LevelSceneSync.Paused = true;
                Undo.IncrementCurrentGroup(); Undo.RecordObject(f, "타일 칠하기");
                if (!rectDrag) Paint(f, cell);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id && dragging)
            {
                var c = new Vector2Int(Mathf.Clamp(cell.x, 0, f.w - 1), Mathf.Clamp(cell.y, 0, f.h - 1));
                if (rectDrag) to = c;
                else if (inside)
                {
                    Undo.RecordObject(f, "타일 칠하기");
                    int n = Mathf.Max(Mathf.Abs(c.x - last.x), Mathf.Abs(c.y - last.y));
                    for (int i = 1; i <= n; i++) Paint(f, new Vector2Int(Mathf.RoundToInt(Mathf.Lerp(last.x, c.x, i / (float)n)), Mathf.RoundToInt(Mathf.Lerp(last.y, c.y, i / (float)n))));
                    last = c;
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id && dragging)
            {
                if (rectDrag) { Undo.RecordObject(f, "사각형 채우기"); var r = Rect(from, to); for (int y = r.yMin; y < r.yMax; y++) for (int x = r.xMin; x < r.xMax; x++) Set(f, x, y); }
                GUIUtility.hotControl = 0; dragging = rectDrag = false;
                Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                EditorUtility.SetDirty(f); EditorSceneManager.MarkSceneDirty(f.gameObject.scene);
                LevelSceneSync.Paused = false; pending.Clear();
                e.Use();
            }
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) SceneView.RepaintAll();
        }
        void Paint(LevelFloor f, Vector2Int c)
        {
            int b = brushSize;
            for (int y = c.y - (b - 1) / 2; y < c.y - (b - 1) / 2 + b; y++) for (int x = c.x - (b - 1) / 2; x < c.x - (b - 1) / 2 + b; x++) Set(f, x, y);
        }
        void Set(LevelFloor f, int x, int y)
        {
            if (x < 0 || y < 0 || x >= f.w || y >= f.h || f.Tile(x, y) == brush) return;
            f.SetTile(x, y, brush); pending.Add(y * f.w + x);
        }
        static RectInt Rect(Vector2Int a, Vector2Int b) { return new RectInt(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(a.x - b.x) + 1, Mathf.Abs(a.y - b.y) + 1); }
        static bool Mouse(LevelFloor f, Vector2 gui, out Vector2 g)
        {
            g = Vector2.zero;
            var ray = HandleUtility.GUIPointToWorldRay(gui);
            var plane = new Plane(f.transform.up, f.transform.position);
            float t; if (!plane.Raycast(ray, out t)) return false;
            var l = f.transform.InverseTransformPoint(ray.GetPoint(t));
            g = new Vector2(l.x, -l.z); return true;
        }
        static void Quad(LevelFloor f, RectInt r, Color fill, Color line, float h)
        {
            var t = f.transform;
            var v = new[] { t.TransformPoint(new Vector3(r.xMin, h, -r.yMin)), t.TransformPoint(new Vector3(r.xMax, h, -r.yMin)), t.TransformPoint(new Vector3(r.xMax, h, -r.yMax)), t.TransformPoint(new Vector3(r.xMin, h, -r.yMax)) };
            Handles.DrawSolidRectangleWithOutline(v, fill, line);
        }

        /* ===== 테스트 ===== */
        static void TestPlay(LevelFloor f, bool atClick)
        {
            if (EditorSceneManager.GetActiveScene() != f.gameObject.scene) EditorSceneManager.SetActiveScene(f.gameObject.scene);
            PlayerPrefs.SetString(Game.DEV_FLOOR, f.id);
            PlayerPrefs.SetFloat(Game.DEV_X, atClick ? lastClick.x : -1);
            PlayerPrefs.SetFloat(Game.DEV_Y, atClick ? lastClick.y : -1);
            PlayerPrefs.Save();
            EditorApplication.EnterPlaymode();
        }

        /* ===== 검사 ===== */
        void DrawProblems()
        {
            if (problems == null) return;
            if (problems.Count == 0) { EditorGUILayout.HelpBox("문제 없음 ✓", MessageType.Info); return; }
            EditorGUILayout.HelpBox("확인할 것 " + problems.Count + "개 (고르기 → 그 오브젝트로)", MessageType.Warning);
            foreach (var p in problems.Take(15))
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(p.Key, EditorStyles.wordWrappedMiniLabel);
                    if (p.Value != null && GUILayout.Button("고르기", GUILayout.Width(50))) { Selection.activeObject = p.Value; SceneView.FrameLastActiveSceneView(); }
                }
        }
        static List<KeyValuePair<string, Object>> Check(LevelFloor f)
        {
            var list = new List<KeyValuePair<string, Object>>();
            System.Action<string, Object> add = (s, o) => list.Add(new KeyValuePair<string, Object>(s, o));
            FloorGeo g;
            try { g = new FloorGeo(f.ToDef()); } catch (System.Exception ex) { add("맵을 만들 수 없음: " + ex.Message, f); return list; }
            System.Func<Vector2, bool> solid = p => FloorGeo.SOLID.Contains(g.Tile(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)));
            foreach (var n in f.All<LevelNpc>())
            {
                if (solid(n.GamePos)) add(n.label + ": 시작 위치가 벽·가구 안", n);
                var r = n.Route; for (int i = 0; i < r.Count; i++) if (solid(r[i].GamePos)) add(n.label + ": 순찰 지점 " + (i + 1) + "이 벽·가구 안", r[i]);
            }
            foreach (var o in f.All<LevelObject>()) if (solid(o.GamePos)) add((Data.Forms.ContainsKey(o.form) ? Data.Forms[o.form].name : o.form) + ": 벽·가구 안", o);
            foreach (var o in f.All<LevelObject>()) if (!Data.Forms.ContainsKey(o.form)) add("모르는 물건 종류: " + o.form, o);
            foreach (var n in f.All<LevelNpc>()) if (!Data.Chars.ContainsKey(n.ch)) add("모르는 직업: " + n.ch, n);
            foreach (var d in f.All<LevelDoor>())
            {
                var c = d.Cell;
                bool ns = !solid(new Vector2(c.x + 0.5f, c.y - 0.5f)) && !solid(new Vector2(c.x + 0.5f, c.y + 1.5f));
                bool ew = !solid(new Vector2(c.x - 0.5f, c.y + 0.5f)) && !solid(new Vector2(c.x + 1.5f, c.y + 0.5f));
                if (!ns && !ew) add("문 " + c + ": 양쪽이 바닥이 아님 (통로를 막음)", d);
                if (!Data.Cards.ContainsKey(d.req)) add("문 " + c + ": 모르는 카드 " + d.req, d);
            }
            var pts = f.All<LevelPoint>().Where(p => p.kind == LevelPoint.Kind.시작 || p.kind == LevelPoint.Kind.계단도착 || p.kind == LevelPoint.Kind.엘베도착).ToList();
            foreach (var p in pts) if (solid(p.GamePos)) add("도착 지점이 벽·가구 안", p);
            if (pts.Count == 0) { add("도착 지점(시작·계단도착·엘베도착)이 하나도 없음", f); return list; }
            var reach = new bool[g.w * g.h]; var q = new Queue<Vector2Int>();
            foreach (var p in pts) { var c = p.Cell; if (c.x >= 0 && c.y >= 0 && c.x < g.w && c.y < g.h && !reach[c.y * g.w + c.x]) { reach[c.y * g.w + c.x] = true; q.Enqueue(c); } }
            System.Func<char, bool> pass = ch => !FloorGeo.SOLID.Contains(ch) || ch == 'D' || ch == 'v';
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int x = c.x + (k == 0 ? 1 : k == 1 ? -1 : 0), y = c.y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (x < 0 || y < 0 || x >= g.w || y >= g.h || reach[y * g.w + x] || !pass(g.Tile(x, y))) continue;
                    reach[y * g.w + x] = true; q.Enqueue(new Vector2Int(x, y));
                }
            }
            System.Func<Vector2Int, bool> near = c =>
            {
                for (int yy = c.y - 1; yy <= c.y + 1; yy++) for (int xx = c.x - 1; xx <= c.x + 1; xx++)
                    if (xx >= 0 && yy >= 0 && xx < g.w && yy < g.h && reach[yy * g.w + xx]) return true;
                return false;
            };
            foreach (var z in f.All<LevelRoom>())
            {
                var r = z.Rect; bool any = false;
                for (int y = r.yMin; y < r.yMax && !any; y++) for (int x = r.xMin; x < r.xMax; x++) if (x >= 0 && y >= 0 && x < g.w && y < g.h && reach[y * g.w + x]) { any = true; break; }
                if (!any) add("갈 수 없는 방: " + z.roomName, z);
            }
            foreach (var c in f.All<LevelConsole>()) if (!near(c.Cell)) add("닿을 수 없는 콘솔 " + c.label, c);
            foreach (var s in f.All<LevelStairs>()) if (!near(new Vector2Int(s.Rect.x, s.Rect.y))) add("닿을 수 없는 계단 → " + s.to, s);
            foreach (var t in f.All<LevelLift>()) if (!near(t.Cell)) add("닿을 수 없는 엘리베이터 칸", t);
            var trapIds = new HashSet<string>(Object.FindObjectsByType<LevelTrap>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(t => t.trapId));
            foreach (var c in f.All<LevelConsole>()) if (!string.IsNullOrEmpty(c.trap) && !trapIds.Contains(c.trap)) add("콘솔 " + c.label + ": 없는 함정 id " + c.trap, c);
            return list;
        }
    }
}
