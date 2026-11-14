// 2.5D 비교 씬 (Assets/Scenes/Main_2_5D.unity).
// 2D 게임 씬(Main)과 규칙·맵·UI는 같고, Game의 "Use 25D"만 켜져 있다.
// 프로젝트를 열 때 없으면 자동으로 만들고, 메뉴 "표본07 → 2.5D 씬 만들기 / 열기"로도 쓸 수 있다.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Specimen07.EditorTools
{
    [InitializeOnLoad]
    public static class Scene25DBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main_2_5D.unity";
        // 2.5D 화면이 코드에서 찾아 쓰는 셰이더. 빌드에 빠지지 않게 "항상 포함"에 넣는다.
        static readonly string[] Shaders = { "Legacy Shaders/Diffuse", "Legacy Shaders/Transparent/Cutout/Diffuse", "Legacy Shaders/Transparent/Diffuse", "Unlit/Color", "Sprites/Default", "UI/Default" };

        static Scene25DBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (EditorSceneManager.GetActiveScene().isDirty) return;   // 저장 안 한 작업이 있으면 건드리지 않음 (메뉴로 만들면 됨)
                if (!File.Exists(ScenePath) && File.Exists("Assets/Scenes/Main.unity")) Build(false);
            };
        }

        [MenuItem("표본07/2.5D 씬 만들기 (다시)", false, 20)]
        static void BuildFromMenu() { Build(true); }

        [MenuItem("표본07/2.5D 씬 열기", false, 21)]
        static void Open()
        {
            if (!File.Exists(ScenePath)) { Build(true); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        static void Build(bool fromMenu)
        {
            if (fromMenu && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string prev = EditorSceneManager.GetActiveScene().path;
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 30f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(5 / 255f, 6 / 255f, 12 / 255f);
            cam.nearClipPlane = 0.3f; cam.farClipPlane = 120f;
            cam.renderingPath = RenderingPath.DeferredShading;
            camGo.transform.rotation = Quaternion.Euler(50, 0, 0);
            camGo.transform.position = new Vector3(10, 14, -15);
            camGo.AddComponent<AudioListener>();

            var game = new GameObject("Game (2.5D)").AddComponent<Game>();
            game.use25D = true;

            // 씬 이름표 (Hierarchy에서 바로 보이게)
            new GameObject("— 2.5D 비교 씬 (Game의 Use 25D가 켜져 있음) —");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // 빌드 목록 끝에 추가 (메뉴 → 게임 순서는 그대로)
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();

            AddAlwaysIncludedShaders();
            Debug.Log("[표본07] 2.5D 씬을 만들었습니다: " + ScenePath + " — 열고 Play를 누르면 2.5D로 시작합니다.");

            if (!fromMenu && !string.IsNullOrEmpty(prev) && File.Exists(prev)) EditorSceneManager.OpenScene(prev);
        }

        static void AddAlwaysIncludedShaders()
        {
            var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (gs == null) return;
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            bool changed = false;
            foreach (var name in Shaders)
            {
                var sh = Shader.Find(name);
                if (sh == null) continue;
                bool found = false;
                for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { found = true; break; }
                if (found) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                changed = true;
            }
            if (changed) so.ApplyModifiedProperties();
        }
    }
}
