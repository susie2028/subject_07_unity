// MainMenu 씬을 만들어 주는 에디터 도구.
// 메뉴: 표본07 → 메인 메뉴 씬 만들기
// 만들고 나면 씬 안의 오브젝트를 마우스로 옮기고, Image의 Source Image에 그림을 끌어다 넣으면 된다.
// (한 번 만든 뒤에는 이 도구를 다시 누를 필요 없음. 다시 누르면 씬을 새로 덮어쓴다)
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Specimen07.EditorTools
{
    [InitializeOnLoad]
    public static class MenuSceneBuilder
    {
        const string MenuPath = "Assets/Scenes/MainMenu.unity";
        const string GamePath = "Assets/Scenes/Main.unity";

        static readonly Color BG = new Color(10 / 255f, 12 / 255f, 30 / 255f), BTN = new Color(0.85f, 0.85f, 0.85f), BTN_TEXT = new Color(0.07f, 0.07f, 0.07f);
        static readonly Color PANEL = new Color(12 / 255f, 17 / 255f, 21 / 255f, 0.96f), PANEL_BTN = new Color(20 / 255f, 27 / 255f, 33 / 255f), FG = new Color(0.87f, 0.9f, 0.89f);

        static MenuSceneBuilder()
        {
            // 메뉴 씬이 아직 없으면 한 번 자동으로 만든다
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(MenuPath) || !File.Exists(GamePath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                // 열려 있는 씬에 저장 안 한 변경이 있으면 자동으로 만들지 않는다 (날아가지 않게)
                if (EditorSceneManager.GetActiveScene().isDirty) { Debug.Log("[표본07] 메뉴 '표본07 → 메인 메뉴 씬 만들기'로 MainMenu 씬을 만들 수 있어요."); return; }
                Build(false);
            };
        }

        [MenuItem("표본07/메인 메뉴 씬 만들기", false, 1)]
        static void BuildFromMenu()
        {
            if (File.Exists(MenuPath) && !EditorUtility.DisplayDialog("메인 메뉴 씬", "MainMenu 씬이 이미 있어요. 새로 만들면 지금까지 씬에서 옮기고 바꾼 것이 사라져요. 덮어쓸까요?", "덮어쓰기", "취소")) return;
            Build(true);
        }

        [MenuItem("표본07/메인 메뉴 씬 열기", false, 2)]
        static void OpenMenu() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() && File.Exists(MenuPath)) EditorSceneManager.OpenScene(MenuPath); }
        [MenuItem("표본07/게임 씬 열기", false, 3)]
        static void OpenGame() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() && File.Exists(GamePath)) EditorSceneManager.OpenScene(GamePath); }

        static Sprite LoadSprite(params string[] names)
        {
            foreach (var n in names)
                foreach (var ext in new[] { ".png", ".jpg", ".psd" })
                {
                    var s = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/" + n + ext);
                    if (s != null) return s;
                }
            return null;
        }

        static Font font;
        static Font MenuFont()
        {
            if (font != null) return font;
            font = Resources.Load<Font>("Fonts/Main");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }

        public static void Build(bool interactive)
        {
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 카메라 (UI만 있으므로 배경색만)
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = BG;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();

            var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

            // 캔버스
            var cGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = cGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.pixelPerfect = true;
            var scaler = cGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            cGo.AddComponent<GraphicRaycaster>();
            var ctrl = cGo.AddComponent<MainMenuController>();
            var root = cGo.transform;

            // 배경
            var bgRt = Full(root, "Background");
            var bg = bgRt.gameObject.AddComponent<Image>();
            var bgSprite = LoadSprite("ui_title_bg", "title_bg", "main_bg", "bg");
            bg.color = bgSprite != null ? Color.white : BG; bg.sprite = bgSprite;
            var fit = bgRt.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = bgSprite != null ? bgSprite.rect.width / bgSprite.rect.height : 16f / 9f;

            // 로고
            var logoRt = TopLeft(root, "Logo", new Vector2(120, 150), new Vector2(500, 330));
            var logo = logoRt.gameObject.AddComponent<Image>(); logo.preserveAspect = true; logo.raycastTarget = false;
            logo.sprite = LoadSprite("ui_title_logo", "title01", "logo");
            if (logo.sprite == null)
            {
                logo.color = new Color(1, 1, 1, 0);
                Label(logoRt, "SUBJECT\n07", 110, Color.white, TextAnchor.UpperLeft, true);
            }

            // 버튼 (각자 따로 옮길 수 있게 하나씩 배치)
            Vector2 pos = new Vector2(176, 585), size = new Vector2(240, 74); float gap = 12;
            ctrl.newGameButton = MainButton(root, "Btn_NewGame", "새로하기", "ui_btn_new", pos, size, ctrl.OnNewGame);
            ctrl.continueButton = MainButton(root, "Btn_Continue", "이어하기", "ui_btn_continue", pos + new Vector2(0, (size.y + gap) * 1), size, ctrl.OnContinue);
            ctrl.settingsButton = MainButton(root, "Btn_Settings", "설정", "ui_btn_settings", pos + new Vector2(0, (size.y + gap) * 2), size, ctrl.OnSettings);
            ctrl.quitButton = MainButton(root, "Btn_Quit", "게임 종료", "ui_btn_quit", pos + new Vector2(0, (size.y + gap) * 3), size, ctrl.OnQuit);

            // 설정 창
            Transform card;
            ctrl.settingsPanel = Modal(root, "SettingsPanel", "설정", out card);
            ctrl.musicValue = SettingRow(card, "배경음악", "－", ctrl.OnMusicDown, "＋", ctrl.OnMusicUp);
            ctrl.volumeValue = SettingRow(card, "효과음", "－", ctrl.OnVolumeDown, "＋", ctrl.OnVolumeUp);
            ctrl.fullscreenValue = SettingRow(card, "전체 화면", null, null, "바꾸기", ctrl.OnToggleFullscreen);
            ctrl.minimapValue = SettingRow(card, "미니맵", null, null, "바꾸기", ctrl.OnToggleMinimap);
            var row = Row(card, "Buttons");
            SmallButton(row, "닫기", 200, BTN, BTN_TEXT, ctrl.OnCloseSettings);

            // 새로하기 확인 창
            ctrl.confirmPanel = Modal(root, "ConfirmPanel", "새로 시작할까요?", out card);
            var msg = Label(card, "이어하던 진행이 사라집니다.", 24, new Color(0.54f, 0.6f, 0.6f), TextAnchor.MiddleLeft); Pref(msg.gameObject, 0, 40);
            row = Row(card, "Buttons");
            SmallButton(row, "새로 시작", 200, new Color(1, 0.49f, 0.55f), new Color(0.1f, 0.05f, 0.06f), ctrl.OnConfirmNew);
            SmallButton(row, "취소", 200, PANEL_BTN, FG, ctrl.OnCancel);

            ctrl.settingsPanel.SetActive(false);
            ctrl.confirmPanel.SetActive(false);

            EditorUtility.SetDirty(ctrl);
            EditorSceneManager.SaveScene(scene, MenuPath);

            // 빌드 순서: 메인 메뉴가 먼저
            var list = EditorBuildSettings.scenes.Where(s => s.path != MenuPath && s.path != GamePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(GamePath, true));
            list.Insert(0, new EditorBuildSettingsScene(MenuPath, true));
            EditorBuildSettings.scenes = list.ToArray();

            Selection.activeGameObject = logoRt.gameObject;
            Debug.Log("[표본07] MainMenu 씬을 만들었어요: " + MenuPath + " — 로고·버튼을 마우스로 옮기고, Image의 Source Image에 그림을 끌어다 넣으세요. 저장(Ctrl/Cmd+S) 잊지 말기!");
        }

        /* ===== 만들기 도우미 ===== */
        static RectTransform Make(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        static RectTransform Full(Transform parent, string name)
        {
            var rt = Make(parent, name); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero; return rt;
        }
        static RectTransform TopLeft(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var rt = Make(parent, name); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(pos.x, -pos.y); rt.sizeDelta = size; return rt;
        }
        static Text Label(Transform parent, string text, int size, Color c, TextAnchor align, bool bold = false)
        {
            var rt = Full(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            var bf = Resources.Load<Font>("Fonts/Bold");
            t.font = bold && bf != null ? bf : MenuFont(); t.text = text; t.fontSize = size; t.color = c; t.alignment = align; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        static void Pref(GameObject go, float w, float h)
        {
            var le = go.GetComponent<LayoutElement>(); if (le == null) le = go.AddComponent<LayoutElement>();
            if (w > 0) le.preferredWidth = w; if (h > 0) le.preferredHeight = h;
        }
        static void Wire(Button b, UnityAction a)
        {
            UnityEventTools.AddPersistentListener(b.onClick, a);
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
        }

        static Button MainButton(Transform parent, string name, string text, string spriteKey, Vector2 pos, Vector2 size, UnityAction a)
        {
            var rt = TopLeft(parent, name, pos, size);
            var im = rt.gameObject.AddComponent<Image>();
            var own = LoadSprite(spriteKey); var common = LoadSprite("ui_button");
            bool showText = true;
            if (own != null) { im.sprite = own; im.preserveAspect = true; showText = false; }
            else if (common != null) { im.sprite = common; if (common.border != Vector4.zero) im.type = Image.Type.Sliced; }
            else im.color = BTN;
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = im;
            var cb = b.colors; cb.highlightedColor = new Color(0.85f, 0.85f, 0.9f); cb.pressedColor = new Color(0.7f, 0.7f, 0.75f); cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f); b.colors = cb;
            Wire(b, a);
            var t = Label(rt, text, 32, BTN_TEXT, TextAnchor.MiddleLeft);
            t.rectTransform.offsetMin = new Vector2(40, 0); t.rectTransform.offsetMax = new Vector2(-10, 0);
            t.gameObject.SetActive(showText);
            return b;
        }

        static GameObject Modal(Transform parent, string name, string title, out Transform card)
        {
            var root = Full(parent, name);
            var dim = root.gameObject.AddComponent<Image>(); dim.color = new Color(0, 0, 0, 0.55f);
            var c = Make(root, "Card"); c.anchorMin = c.anchorMax = c.pivot = new Vector2(0.5f, 0.5f); c.sizeDelta = new Vector2(640, 400);
            var bg = c.gameObject.AddComponent<Image>();
            var panelSprite = LoadSprite("ui_panel_bg");
            if (panelSprite != null) { bg.sprite = panelSprite; if (panelSprite.border != Vector4.zero) bg.type = Image.Type.Sliced; } else bg.color = PANEL;
            var v = c.gameObject.AddComponent<VerticalLayoutGroup>(); v.padding = new RectOffset(40, 40, 36, 36); v.spacing = 18;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            c.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var t = Label(c, title, 40, FG, TextAnchor.MiddleLeft, true); Pref(t.gameObject, 0, 56);
            card = c;
            return root.gameObject;
        }
        static Transform Row(Transform parent, string name)
        {
            var rt = Make(parent, name);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>(); h.spacing = 12; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return rt;
        }
        static Button SmallButton(Transform parent, string text, float w, Color bg, Color fg, UnityAction a)
        {
            var rt = Make(parent, "Btn_" + text);
            var im = rt.gameObject.AddComponent<Image>(); im.color = bg;
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = im; Wire(b, a);
            Label(rt, text, 26, fg, TextAnchor.MiddleCenter, true);
            Pref(rt.gameObject, w, 56);
            return b;
        }
        static Text SettingRow(Transform card, string label, string aText, UnityAction a, string bText, UnityAction b)
        {
            var row = Row(card, "Row_" + label);
            var l = Label(Make(row, "Label"), label, 26, FG, TextAnchor.MiddleLeft); Pref(l.transform.parent.gameObject, 200, 56);
            var vHolder = Make(row, "Value"); Pref(vHolder.gameObject, 130, 56);
            var v = Label(vHolder, "", 26, new Color(0.71f, 0.55f, 0.94f), TextAnchor.MiddleCenter, true);
            if (aText != null) SmallButton(row, aText, 70, PANEL_BTN, FG, a);
            SmallButton(row, bText, aText != null ? 70 : 152, PANEL_BTN, FG, b);
            return v;
        }
    }
}
