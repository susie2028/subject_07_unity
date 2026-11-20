// 게임 씬(Assets/Scenes/Main.unity)이 없으면 자동으로 만든다. 화면은 2.5D 하나뿐이다.
// 메뉴 "표본07 → 게임 씬 만들기 (다시)"로 새로 만들 수도 있다.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Specimen07.EditorTools
{
    [InitializeOnLoad]
    public static class SceneSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        // 화면 코드가 이름으로 찾아 쓰는 셰이더. 빌드에 빠지지 않게 "항상 포함"에 넣는다.
        static readonly string[] Shaders = { "Legacy Shaders/Diffuse", "Legacy Shaders/Transparent/Cutout/Diffuse", "Legacy Shaders/Transparent/Diffuse", "Unlit/Color", "Sprites/Default", "UI/Default" };

        static SceneSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(ScenePath)) { AddAlwaysIncludedShaders(); return; }
                if (EditorSceneManager.GetActiveScene().isDirty) return;   // 저장 안 한 작업이 있으면 건드리지 않음 (메뉴로 만들면 됨)
                CreateMainScene(false);
            };
        }

        [MenuItem("표본07/게임 씬 만들기 (다시)", false, 20)]
        static void FromMenu()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("게임 씬", "Main 씬을 새로 만들까요? (씬에 직접 넣은 것이 있으면 사라져요. 레벨 데이터는 그대로예요)", "새로 만들기", "취소")) return;
            CreateMainScene(true);
        }

        public static void CreateMainScene(bool interactive)
        {
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
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
            new GameObject("Game").AddComponent<Game>();
            LevelSceneSync.CreateFromDefaults();   // 층 6개 (레벨 디자인용)

            EditorSceneManager.SaveScene(scene, ScenePath);
            // 빌드 목록: 메인 메뉴 → 게임 (예전 2.5D 비교 씬 항목은 지운다)
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath && !s.path.EndsWith("Main_2_5D.unity")).ToList();
            int menu = list.FindIndex(s => s.path.EndsWith("MainMenu.unity"));
            list.Insert(menu >= 0 ? menu + 1 : 0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AddAlwaysIncludedShaders();
            Debug.Log("[표본07] 게임 씬을 만들었습니다: " + ScenePath);
            if (!interactive && !string.IsNullOrEmpty(prev) && File.Exists(prev)) EditorSceneManager.OpenScene(prev);
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

        [MenuItem("표본07/스프라이트 폴더 열기", false, 40)]
        static void OpenArt()
        {
            var obj = AssetDatabase.LoadAssetAtPath<Object>("Assets/Resources/Art");
            if (obj != null) { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
        }

        [MenuItem("표본07/개발: 3F에서 바로 시작 (Play 중)", false, 60)]
        static void Dev3F() { if (Game.I != null) Game.I.DevStart("3F"); }
        [MenuItem("표본07/개발: 4F에서 바로 시작 (Play 중)", false, 61)]
        static void Dev4F() { if (Game.I != null) Game.I.DevStart("4F"); }
        [MenuItem("표본07/개발: 5F에서 바로 시작 (Play 중)", false, 62)]
        static void Dev5F() { if (Game.I != null) Game.I.DevStart("5F"); }
    }
}
