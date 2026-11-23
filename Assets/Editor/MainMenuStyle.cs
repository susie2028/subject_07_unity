// MainMenu 씬을 열면 메인 버튼 4개에 심플한 띠 모양(SimpleMenuButton)을 한 번 입혀서 씬에 저장한다.
// 그 뒤로는 씬 화면에서도 그 모양 그대로 보이고, 위치·색은 씬/인스펙터에서 직접 고치면 된다.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Specimen07.EditorTools
{
    [InitializeOnLoad]
    public static class MainMenuStyle
    {
        static MainMenuStyle()
        {
            EditorSceneManager.sceneOpened += (s, m) => Apply(false);
            EditorApplication.delayCall += () => Apply(false);
        }

        [MenuItem("표본07/메인 메뉴 버튼 모양 다시 입히기", false, 32)]
        static void Reapply() { Apply(true); }

        static void Apply(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.name != Launch.MenuScene) return;
            var ctl = Object.FindFirstObjectByType<MainMenuController>();
            if (ctl == null) return;
            bool changed = false;
            foreach (var b in new UnityEngine.UI.Button[] { ctl.newGameButton, ctl.continueButton, ctl.settingsButton, ctl.quitButton })
            {
                if (b == null) continue;
                var s = b.GetComponent<SimpleMenuButton>();
                if (s != null && !force && s.styleVersion >= SimpleMenuButton.STYLE) continue;
                if (s == null) s = Undo.AddComponent<SimpleMenuButton>(b.gameObject);
                Undo.RegisterFullObjectHierarchyUndo(b.gameObject, "메인 메뉴 버튼 모양");
                s.ApplyStyle();
                EditorUtility.SetDirty(b.gameObject);
                changed = true;
            }
            // 나머지 버튼(설정·확인 창 등)도 같은 버튼 모양으로
            if (changed)
                foreach (var ob in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (ob.GetComponent<SimpleMenuButton>() != null) continue;
                    Undo.RegisterFullObjectHierarchyUndo(ob.gameObject, "버튼 모양");
                    Hud.SkinButton(ob, ob.targetGraphic as UnityEngine.UI.Image ?? ob.GetComponent<UnityEngine.UI.Image>(), ob.GetComponentInChildren<UnityEngine.UI.Text>(true));
                    EditorUtility.SetDirty(ob.gameObject);
                }
            // 로고도 화면 가로 가운데로 (한 번만)
            if (changed)
            {
                var logo = GameObject.Find("Logo");
                if (logo != null && logo.transform is RectTransform lrt)
                {
                    Undo.RecordObject(lrt, "로고 가운데");
                    SimpleMenuButton.CenterX(lrt, lrt.rect.width, lrt.rect.height);
                    EditorUtility.SetDirty(lrt);
                }
            }
            if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); Debug.Log("[표본07] 메인 메뉴 버튼 모양을 씬에 입혔어요"); }
        }
    }
}
