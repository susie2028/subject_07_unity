// Play를 누르지 않아도 Scene 창에서 층 맵을 볼 수 있게 해 주는 미리보기.
// 메뉴: 표본07 → 미리보기 → B1 ~ 5F / 미리보기 끄기
//
// - 미리보기는 씬 파일에 저장되지 않는다 (두 사람이 같이 써도 충돌 없음).
// - Resources/Art에 PNG를 넣거나 바꾸면 미리보기가 자동으로 새로 그려진다.
// - Play를 누르면 잠깐 사라졌다가, Play를 끝내면 다시 나타난다.
using UnityEditor;
using UnityEngine;

namespace Specimen07.EditorTools
{
    [InitializeOnLoad]
    public static class MapPreview
    {
        const string PrefKey = "Specimen07.PreviewFloor";
        const string RootName = "[미리보기 — 저장 안 됨]";
        static GameObject root;

        static MapPreview()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            // 게임 씬(Main)이 열려 있을 때만 미리보기를 보여 준다
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (sc, m) => { if (!EditorApplication.isPlayingOrWillChangePlaymode) Rebuild(false); };
            // 스크립트를 다시 불러온 뒤(코드 수정 후 등) 미리보기 복구
            EditorApplication.delayCall += () =>
            {
                // 이 컴퓨터에서 처음 열면 B1 미리보기를 켠다
                bool first = !EditorPrefs.HasKey(PrefKey);
                if (first) Current = "B1";
                // 프로젝트 기본값을 2D로 (그림을 넣으면 스프라이트로, 새 씬도 2D로)
                if (EditorSettings.defaultBehaviorMode != EditorBehaviorMode.Mode2D) EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
                if (!EditorApplication.isPlayingOrWillChangePlaymode) Rebuild(first);
            };
        }

        static string Current { get { return EditorPrefs.GetString(PrefKey, ""); } set { EditorPrefs.SetString(PrefKey, value); } }

        [MenuItem("표본07/미리보기/B1 격리동", false, 100)] static void B1() { Show("B1"); }
        [MenuItem("표본07/미리보기/1F 연구동", false, 101)] static void F1() { Show("1F"); }
        [MenuItem("표본07/미리보기/2F 보안동", false, 102)] static void F2() { Show("2F"); }
        [MenuItem("표본07/미리보기/3F 실험동", false, 103)] static void F3() { Show("3F"); }
        [MenuItem("표본07/미리보기/4F 소장실층", false, 104)] static void F4() { Show("4F"); }
        [MenuItem("표본07/미리보기/5F 옥상", false, 105)] static void F5() { Show("5F"); }
        [MenuItem("표본07/미리보기/미리보기 끄기", false, 130)] static void Off() { Current = ""; Clear(); }
        [MenuItem("표본07/미리보기/새로 그리기 (그림 바꾼 뒤)", false, 131)] static void Refresh() { Rebuild(false); }

        static void Show(string fid)
        {
            if (EditorApplication.isPlaying) { Debug.Log("[표본07] 미리보기는 Play 중이 아닐 때 쓸 수 있어요."); return; }
            if (!GameSceneOpen) Debug.Log("[표본07] 미리보기는 게임 씬(Main)에서 보여요. 메뉴 '표본07 → 게임 씬 열기'");
            Current = fid;
            Rebuild(true);
        }

        static void Clear()
        {
            // 저장되지 않는 오브젝트는 씬 검색에 안 잡힐 수 있어서 전체에서 이름으로 찾는다
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go != null && go.name == RootName && go.transform.parent == null && !EditorUtility.IsPersistent(go))
                    Object.DestroyImmediate(go);
            root = null;
        }

        static bool GameSceneOpen { get { return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name == "Main"; } }

        public static void Rebuild(bool frame)
        {
            Clear();
            if (!GameSceneOpen) return;
            string fid = Current;
            if (string.IsNullOrEmpty(fid) || !FloorGeo.All.ContainsKey(fid)) return;
            Art.ClearCache();
            var g = FloorGeo.All[fid];

            root = EditorUtility.CreateGameObjectWithHideFlags(RootName, HideFlags.DontSave);
            var t = root.transform;
            WorldView.BuildStatic(g, t);

            // 문, 함정, 콘솔·엘리베이터 표시등
            foreach (var d in g.def.doors)
            {
                var b = WorldView.Spr(t, "door_closed", WorldView.L_DOOR); b.transform.position = U.W(d.x + 0.5f, d.y + 0.5f);
                var s = WorldView.Spr(t, "door_stripes", WorldView.L_DOOR + 1); s.transform.position = b.transform.position; s.color = Data.Cards[d.req].color;
            }
            foreach (var tr in g.def.traps)
                foreach (var r in tr.rects)
                {
                    if (tr.kind == "laser")
                        for (int yy = r.y; yy < r.y + r.h; yy++)
                            for (int k = -1; k <= 1; k++)
                            {
                                var sr = WorldView.Spr(t, "fx_white", WorldView.L_TRAP);
                                sr.transform.position = U.W(r.x + r.w / 2f, yy + 0.5f + k * 0.25f);
                                sr.transform.localScale = new Vector3(r.w, 2.5f / 32f, 1);
                                sr.color = new Color(1, 60 / 255f, 60 / 255f, 0.8f);
                            }
                    else
                        for (int yy = r.y; yy < r.y + r.h; yy++) for (int xx = r.x; xx < r.x + r.w; xx++)
                        {
                            var sr = WorldView.Spr(t, "fx_glow", WorldView.L_TRAP);
                            sr.transform.position = U.W(xx + 0.5f, yy + 0.5f); sr.transform.localScale = Vector3.one * 1.5f;
                            sr.color = new Color(150 / 255f, 230 / 255f, 90 / 255f, 0.2f);
                        }
                }
            // 물건 (시작 위치)
            foreach (var o in g.def.objects)
            {
                var sr = WorldView.Spr(t, "obj_" + o.form, WorldView.L_OBJ);
                sr.transform.position = U.W(o.x, o.y);
                sr.transform.rotation = Quaternion.Euler(0, 0, U.Deg(o.form == "chair" ? o.rot : 0));
            }
            // 사람 (시작 위치와 바라보는 방향) + 순찰 경로 선
            var lineMat = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            foreach (var n in g.def.npcs)
            {
                float face = Data.Rad(n.face);
                var sr = WorldView.Spr(t, "char_" + n.pal, WorldView.L_NPC);
                sr.name = n.label;
                var dir = Art.Dir("char_" + n.pal);
                if (dir != null) sr.sprite = dir[Art.DirIndex(face)];
                else sr.transform.rotation = Quaternion.Euler(0, 0, U.Deg(face));
                sr.transform.position = U.W(n.x, n.y);
                if (n.route != null && n.route.Length > 1)
                {
                    var go = new GameObject("경로_" + n.label); go.transform.SetParent(t, false);
                    var lr = go.AddComponent<LineRenderer>();
                    lr.sharedMaterial = lineMat; lr.useWorldSpace = true; lr.loop = true; lr.widthMultiplier = 0.06f; lr.sortingOrder = WorldView.L_CONE;
                    lr.positionCount = n.route.Length;
                    for (int i = 0; i < n.route.Length; i++) lr.SetPosition(i, U.W(n.route[i].x, n.route[i].y));
                    lr.startColor = lr.endColor = new Color(243 / 255f, 176 / 255f, 74 / 255f, 0.35f);
                }
            }
            // 주인공 시작 위치 (도착 지점)
            foreach (var via in new[] { "start", "stairs", "lift" })
            {
                bool has = via == "start" ? g.def.arriveStart.HasValue : via == "stairs" ? g.def.arriveStairs.HasValue : g.def.arriveLift.HasValue;
                if (!has) continue;
                var a = g.def.Arrive(via);
                var sr = WorldView.Spr(t, "player_blob", WorldView.L_PLAYER);
                sr.transform.position = U.W(a.x, a.y); sr.color = new Color(1, 1, 1, 0.6f); sr.name = "도착 지점 (" + via + ")";
            }

            // 씬에 저장되지 않게 (자식까지 전부)
            foreach (var tf in root.GetComponentsInChildren<Transform>(true)) tf.gameObject.hideFlags = HideFlags.DontSave;

            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                sv.in2DMode = true;
                if (frame) sv.Frame(new Bounds(new Vector3(g.w / 2f, -g.h / 2f, 0), new Vector3(g.w, g.h, 1)), false);
                sv.Repaint();
            }
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.ExitingEditMode) Clear();
            else if (s == PlayModeStateChange.EnteredEditMode) Rebuild(false);
        }

        // Resources/Art에 그림을 넣거나 바꾸면 자동으로 다시 그림
        class ArtWatcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                bool art = false;
                foreach (var p in imported) if (p.Contains("/Resources/Art/")) art = true;
                foreach (var p in deleted) if (p.Contains("/Resources/Art/")) art = true;
                if (!art) return;
                Art.ClearCache();
                if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.delayCall += () => Rebuild(false);
            }
        }
    }
}
