// 씬의 층(LevelFloor)을 Play 없이 2.5D로 그려 주고, 표시물을 칸에 맞춘다.
//  - 게임 씬(Main)에 층이 하나도 없으면 코드에 적힌 기본 맵으로 자동으로 만든다
//  - 표시물을 옮기거나 타일을 칠하면 잠깐 뒤 그 층만 다시 그린다
//  - 그려진 벽·가구·사람은 숨겨진 미리보기 오브젝트라 씬 파일에 저장되지 않고, 눌러도 선택되지 않는다
//    (선택은 각 표시물의 기즈모(반투명 상자)로)
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Specimen07.EditorTools
{
    [InitializeOnLoad]
    public static class LevelSceneSync
    {
        public const string RootName = "레벨 (층별)";
        const string PreviewName = "미리보기 (저장 안 됨)";

        class Pv { public GameObject go; public WorldView3D view; public int sHash, aHash; public bool built; }
        static readonly Dictionary<LevelFloor, Pv> pvs = new Dictionary<LevelFloor, Pv>();
        static double next;
        public static bool Paused;   // 타일 칠하는 동안 등

        static LevelSceneSync()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.ExitingEditMode) ClearAll();
            };
            EditorSceneManager.sceneOpened += (sc, mode) => { EditorApplication.delayCall += EnsureLevels; };
            EditorApplication.delayCall += EnsureLevels;
            AssemblyReloadEvents.beforeAssemblyReload += ClearAll;
        }

        /* ===== 게임 씬에 층이 없으면 만들기 ===== */
        static void EnsureLevels()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var sc = EditorSceneManager.GetActiveScene();
            if (!sc.IsValid() || sc.path != SceneSetup.ScenePath) return;
            if (Object.FindObjectsByType<LevelFloor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0) return;
            bool wasDirty = sc.isDirty;
            CreateFromDefaults();
            if (!wasDirty) EditorSceneManager.SaveScene(sc);
            Debug.Log("[표본07] 게임 씬에 층 6개를 만들었어요 (Hierarchy의 '" + RootName + "'). 저장된 씬을 Commit·Push 해 주세요.");
        }

        [MenuItem("표본07/씬에 층 다시 만들기 (기본 맵에서)", false, 21)]
        static void RecreateMenu()
        {
            if (!EditorUtility.DisplayDialog("층 다시 만들기", "씬의 '" + RootName + "'를 지우고 코드에 적힌 기본 맵으로 다시 만들까요?\n씬에서 고친 내용은 사라져요.", "다시 만들기", "취소")) return;
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects()) if (go.name == RootName) Undo.DestroyObjectImmediate(go);
            CreateFromDefaults();
        }
        [MenuItem("표본07/층 미리보기 다시 그리기", false, 22)]
        static void RedrawMenu() { Art.ClearCache(); ClearAll(); }

        public static void CreateFromDefaults()
        {
            ClearAll();
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "층 만들기");
            float ox = 0;
            foreach (var d in FloorDefs.All)
            {
                var f = LevelBuild.FromDef(d, root.transform);
                f.transform.position = new Vector3(ox, 0, 0);
                ox += d.w + 10;
            }
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        /* ===== 미리보기 ===== */
        static void ClearAll()
        {
            foreach (var kv in pvs) if (kv.Value.go != null) Object.DestroyImmediate(kv.Value.go);
            pvs.Clear();
            // 스크립트를 다시 불러온 뒤 주인 없이 남은 미리보기
            foreach (var f in Object.FindObjectsByType<LevelFloor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                for (int i = f.transform.childCount - 1; i >= 0; i--) { var c = f.transform.GetChild(i); if (c.name == PreviewName) Object.DestroyImmediate(c.gameObject); }
        }

        static void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 0.2;
            bool dragging = GUIUtility.hotControl != 0;

            var floors = Object.FindObjectsByType<LevelFloor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            // 없어진 층의 미리보기 정리
            foreach (var dead in pvs.Keys.Where(k => k == null || !floors.Contains(k)).ToList()) { if (pvs[dead].go != null) Object.DestroyImmediate(pvs[dead].go); pvs.Remove(dead); }

            bool rebuiltOne = false;
            foreach (var f in floors)
            {
                if (!dragging) { Snap(f); FixIds(f); }
                if (!f.gameObject.activeInHierarchy) continue;
                Pv pv;
                if (!pvs.TryGetValue(f, out pv) || pv.go == null) { pv = new Pv(); pvs[f] = pv; }
                FloorDef d;
                try { d = f.ToDef(); } catch (System.Exception e) { Debug.LogWarning("[표본07] " + f.id + " 미리보기 실패: " + e.Message); continue; }
                int sh = StaticHash(d, f), ah = ActorHash(d);
                if (!pv.built || sh != pv.sHash)
                {
                    if (dragging || Paused || rebuiltOne) continue;   // 끌거나 칠하는 동안엔 기다렸다가, 한 번에 한 층씩
                    Build(f, pv, d); pv.sHash = sh; pv.aHash = ah; rebuiltOne = true;
                }
                else if (ah != pv.aHash)
                {
                    try { pv.view.BuildPreviewActors(d); Hide(pv.go); } catch (System.Exception e) { Debug.LogWarning(e.Message); }
                    pv.aHash = ah;
                }
            }
            if (rebuiltOne) SceneView.RepaintAll();
        }

        static void Build(LevelFloor f, Pv pv, FloorDef d)
        {
            if (pv.go == null)
            {
                for (int i = f.transform.childCount - 1; i >= 0; i--) { var c = f.transform.GetChild(i); if (c.name == PreviewName) Object.DestroyImmediate(c.gameObject); }
                pv.go = new GameObject(PreviewName) { hideFlags = HideFlags.HideAndDontSave };
                pv.go.transform.SetParent(f.transform, false);
                pv.view = pv.go.AddComponent<WorldView3D>();
            }
            try
            {
                var g = new FloorGeo(d);
                pv.view.BuildPreview(g);
                pv.view.BuildPreviewActors(d);
                Hide(pv.go);
                pv.built = true;
            }
            catch (System.Exception e) { Debug.LogException(e); pv.built = true; }
        }
        static void Hide(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
            SceneVisibilityManager.instance.DisablePicking(go, true);
        }

        static int StaticHash(FloorDef d, LevelFloor f)
        {
            var sb = new StringBuilder();
            foreach (var r in d.tiles) sb.Append(r);
            sb.Append('|').Append(d.id).Append(d.dark).Append(d.lethal);
            foreach (var r in d.rooms) sb.Append(r.name).Append(r.kind).Append(r.x).Append(',').Append(r.y).Append(',').Append(r.w).Append(',').Append(r.h).Append(r.label);
            foreach (var x in d.doors) sb.Append('D').Append(x.x).Append(',').Append(x.y).Append(x.req);
            foreach (var x in d.consoles) sb.Append('C').Append(x.x).Append(',').Append(x.y);
            foreach (var x in d.notices) sb.Append('N').Append(x.x).Append(',').Append(x.y);
            foreach (var x in d.stairs) sb.Append('S').Append(x.x).Append(',').Append(x.y).Append(x.w).Append(x.h).Append(x.dir);
            if (d.lift != null) foreach (var x in d.lift) sb.Append('E').Append(x.x).Append(',').Append(x.y);
            if (d.pad.HasValue) sb.Append('P').Append(d.pad.Value.x).Append(d.pad.Value.y).Append(d.pad.Value.w).Append(d.pad.Value.h);
            foreach (var t in d.traps) { sb.Append('T').Append(t.kind); foreach (var r in t.rects) sb.Append(r.x).Append(',').Append(r.y).Append(r.w).Append(r.h); }
            foreach (var l in d.extraLights) sb.Append('L').Append(l.x).Append(',').Append(l.y).Append(l.r).Append(l.i);
            return sb.ToString().GetHashCode();
        }
        static int ActorHash(FloorDef d)
        {
            var sb = new StringBuilder();
            foreach (var n in d.npcs) { sb.Append(n.id).Append(n.pal).Append(n.x).Append(',').Append(n.y).Append(n.face); }
            foreach (var o in d.objects) sb.Append(o.form).Append(o.x).Append(',').Append(o.y).Append(o.rot);
            return sb.ToString().GetHashCode();
        }

        /* ===== 칸 맞춤 · 이름 겹침 고치기 ===== */
        static void Snap(LevelFloor f)
        {
            foreach (var m in f.GetComponentsInChildren<LevelMarker>(true))
            {
                var t = m.transform;
                var l = f.transform.InverseTransformPoint(t.position);
                Vector3 n = l; n.y = 0;
                if (m.SnapMode == 1) { n.x = Mathf.Floor(l.x) + 0.5f; n.z = -(Mathf.Floor(-l.z) + 0.5f); }
                else if (m.SnapMode == 2) { n.x = Mathf.Round(l.x); n.z = Mathf.Round(l.z); }
                if ((n - l).sqrMagnitude > 1e-6f)
                {
                    t.position = f.transform.TransformPoint(n);
                    EditorSceneManager.MarkSceneDirty(f.gameObject.scene);
                }
            }
        }
        static void FixIds(LevelFloor f)
        {
            var seen = new HashSet<string>();
            foreach (var n in f.GetComponentsInChildren<LevelNpc>(true))
            {
                if (!string.IsNullOrEmpty(n.npcId) && seen.Add(n.npcId)) continue;
                for (int k = 1; ; k++) { string id = f.id.ToLower() + "_n" + k; if (!seen.Contains(id) && !f.GetComponentsInChildren<LevelNpc>(true).Any(o => o != n && o.npcId == id)) { n.npcId = id; seen.Add(id); break; } }
                EditorUtility.SetDirty(n);
            }
        }
    }

    // 기본 맵(FloorDef) → 씬 오브젝트
    public static class LevelBuild
    {
        public static readonly string[] Groups = { "방", "사람", "물건", "문", "콘솔", "게시물", "계단", "엘리베이터", "함정", "조명", "지점" };

        public static Transform Group(LevelFloor f, string name)
        {
            var t = f.transform.Find(name);
            if (t != null) return t;
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "그룹");
            go.transform.SetParent(f.transform, false);
            return go.transform;
        }
        public static T Add<T>(LevelFloor f, string group, string name) where T : LevelMarker
        {
            var go = new GameObject(name);
            go.transform.SetParent(Group(f, group), false);
            return go.AddComponent<T>();
        }

        public static LevelFloor FromDef(FloorDef d, Transform root)
        {
            var go = new GameObject(d.id + " " + d.name);
            go.transform.SetParent(root, false);
            var f = go.AddComponent<LevelFloor>();
            f.id = d.id; f.floorName = d.name; f.sub = d.sub; f.lethal = d.lethal; f.dark = d.dark; f.w = d.w; f.h = d.h;
            var g = FloorGeo.BaseGrid(d);
            f.tiles = new List<string>();
            for (int y = 0; y < d.h; y++) { var row = new char[d.w]; for (int x = 0; x < d.w; x++) row[x] = g[y, x]; f.tiles.Add(new string(row)); }
            f.reinforce = new List<LevelFloor.Reinforce>();
            if (d.reinforce != null) foreach (var r in d.reinforce) f.reinforce.Add(new LevelFloor.Reinforce { ch = r.ch, n = r.n });
            foreach (var n in Groups) Group(f, n);

            foreach (var r in d.rooms) { var m = Add<LevelRoom>(f, "방", r.name); m.roomName = r.name; m.kind = r.kind; m.allow = r.allow != null ? string.Join(",", r.allow) : ""; m.label = r.label; m.Rect = new RectInt(r.x, r.y, r.w, r.h); }
            foreach (var n in d.npcs)
            {
                var m = Add<LevelNpc>(f, "사람", n.label);
                m.npcId = n.id; m.ch = n.ch; m.pal = n.pal; m.label = n.label; m.face = n.face;
                m.SetGamePos(new Vector2(n.x, n.y));
                for (int i = 0; i < n.route.Length; i++)
                {
                    var wgo = new GameObject((i + 1).ToString()); wgo.transform.SetParent(m.transform, false);
                    var wp = wgo.AddComponent<LevelWaypoint>(); wp.wait = n.route[i].wait;
                    wp.SetGamePos(new Vector2(n.route[i].x, n.route[i].y));
                    wgo.transform.rotation = f.transform.rotation * Quaternion.Euler(0, n.route[i].look, 0);
                }
            }
            foreach (var o in d.objects)
            {
                var m = Add<LevelObject>(f, "물건", Data.Forms.ContainsKey(o.form) ? Data.Forms[o.form].name : o.form);
                m.form = o.form; m.SetGamePos(new Vector2(o.x, o.y));
                m.transform.rotation = f.transform.rotation * Quaternion.Euler(0, o.rot * Mathf.Rad2Deg - 90, 0);
            }
            foreach (var x in d.doors) { var m = Add<LevelDoor>(f, "문", "문 " + (Data.Cards.ContainsKey(x.req) ? Data.Cards[x.req].name : x.req) + (string.IsNullOrEmpty(x.label) ? "" : " " + x.label)); m.req = x.req; m.label = x.label; m.SetGamePos(new Vector2(x.x + 0.5f, x.y + 0.5f)); }
            foreach (var x in d.consoles) { var m = Add<LevelConsole>(f, "콘솔", "콘솔 " + x.label); m.req = x.req; m.trap = x.trap ?? ""; m.heli = x.heli; m.label = x.label; m.SetGamePos(new Vector2(x.x + 0.5f, x.y + 0.5f)); }
            foreach (var x in d.notices) { var m = Add<LevelNotice>(f, "게시물", "게시물"); m.text = x.text; m.SetGamePos(new Vector2(x.x + 0.5f, x.y + 0.5f)); }
            foreach (var x in d.stairs) { var m = Add<LevelStairs>(f, "계단", "계단 " + x.dir + " " + x.to); m.to = x.to; m.dir = x.dir.ToString(); m.Rect = new RectInt(x.x, x.y, x.w, x.h); }
            if (d.lift != null) foreach (var x in d.lift) { var m = Add<LevelLift>(f, "엘리베이터", "엘리베이터 칸"); m.SetGamePos(new Vector2(x.x + 0.5f, x.y + 0.5f)); }
            if (d.pad.HasValue) { var p = d.pad.Value; var m = Add<LevelPad>(f, "지점", "헬리패드"); m.Rect = new RectInt(p.x, p.y, p.w, p.h); }
            foreach (var t in d.traps)
            {
                var m = Add<LevelTrap>(f, "함정", t.id + " " + t.label); m.trapId = t.id; m.kind = t.kind; m.label = t.label;
                m.SetGamePos(t.rects.Length > 0 ? new Vector2(t.rects[0].x, t.rects[0].y) : Vector2.zero);
                foreach (var r in t.rects) { var a = new GameObject("영역").AddComponent<LevelTrapArea>(); a.transform.SetParent(m.transform, false); a.Rect = new RectInt(r.x, r.y, r.w, r.h); }
            }
            if (d.extraLights != null) foreach (var l in d.extraLights) { var m = Add<LevelLamp>(f, "조명", "조명"); m.radius = l.r; m.intensity = l.i; m.flick = l.flick; m.tint = l.tint; m.SetGamePos(new Vector2(l.x, l.y)); }
            System.Action<Vector2?, LevelPoint.Kind> pt = (v, k) => { if (!v.HasValue) return; var m = Add<LevelPoint>(f, "지점", k.ToString()); m.kind = k; m.SetGamePos(v.Value); };
            pt(d.arriveStart, LevelPoint.Kind.시작); pt(d.arriveStairs, LevelPoint.Kind.계단도착); pt(d.arriveLift, LevelPoint.Kind.엘베도착); pt(d.tank, LevelPoint.Kind.수조);
            if (d.spawns != null) foreach (var s in d.spawns) pt(s, LevelPoint.Kind.지원병등장);
            return f;
        }
    }
}
