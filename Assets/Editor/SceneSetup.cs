// 프로젝트를 처음 열면 Assets/Scenes/Main.unity를 자동으로 만들고 빌드 목록에 넣는다.
// 메뉴 "표본07"에서 다시 만들 수도 있다.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Specimen07.EditorTools
{
    [InitializeOnLoad]
    public static class SceneSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        static SceneSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) && !EditorApplication.isPlayingOrWillChangePlaymode) CreateMainScene();
            };
        }

        [MenuItem("표본07/메인 씬 만들기 (다시)")]
        public static void CreateMainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Cfg.VTH / 2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(7 / 255f, 9 / 255f, 11 / 255f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();
            new GameObject("Game").AddComponent<Game>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[표본07] 메인 씬을 만들었습니다: " + ScenePath + " — Play를 누르면 시작합니다.");
        }

        [MenuItem("표본07/스프라이트 폴더 열기")]
        static void OpenArt()
        {
            var obj = AssetDatabase.LoadAssetAtPath<Object>("Assets/Resources/Art");
            if (obj != null) { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
        }

        [MenuItem("표본07/개발: 3F에서 바로 시작 (Play 중)")]
        static void Dev3F() { if (Game.I != null) Game.I.DevStart("3F"); }
        [MenuItem("표본07/개발: 4F에서 바로 시작 (Play 중)")]
        static void Dev4F() { if (Game.I != null) Game.I.DevStart("4F"); }
        [MenuItem("표본07/개발: 5F에서 바로 시작 (Play 중)")]
        static void Dev5F() { if (Game.I != null) Game.I.DevStart("5F"); }
    }
}
