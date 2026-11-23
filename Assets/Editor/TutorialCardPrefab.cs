// 튜토리얼 카드를 프리팹(Assets/Resources/UI/TutorialCard.prefab)으로 만들어 두고, 메뉴로 열어서 씬·하이어라키에서 디자인할 수 있게 한다.
// 프리팹이 없으면 에디터를 열 때 자동으로 기본 모양을 만든다. 게임은 이 프리팹을 그대로 띄운다.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Specimen07.EditorTools
{
    [InitializeOnLoad]
    public static class TutorialCardPrefab
    {
        public const string PATH = "Assets/Resources/UI/TutorialCard.prefab";

        static TutorialCardPrefab() { EditorApplication.delayCall += () => { if (!File.Exists(PATH)) Create(false); }; }

        [MenuItem("표본07/튜토리얼 카드 편집", false, 30)]
        static void Open()
        {
            if (!File.Exists(PATH)) Create(false);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PATH);
            if (go != null) AssetDatabase.OpenAsset(go);   // 프리팹 편집 화면: 씬 뷰 + 하이어라키에 카드가 뜬다
        }

        [MenuItem("표본07/튜토리얼 카드 기본 모양으로 되돌리기", false, 31)]
        static void Reset()
        {
            if (File.Exists(PATH) && !EditorUtility.DisplayDialog("튜토리얼 카드", "지금 디자인을 지우고 기본 모양으로 되돌릴까요?", "되돌리기", "취소")) return;
            Create(true);
        }

        static void Create(bool overwrite)
        {
            if (File.Exists(PATH) && !overwrite) return;
            Directory.CreateDirectory(Path.GetDirectoryName(PATH));
            var root = Hud.BuildTutorialObject();
            PrefabUtility.SaveAsPrefabAsset(root, PATH);
            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            Debug.Log("[표본07] 튜토리얼 카드 프리팹을 만들었어요: " + PATH + " (메뉴 표본07 > 튜토리얼 카드 편집)");
        }
    }
}
