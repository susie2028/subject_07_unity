// 메인 화면 (새로하기 / 이어하기 / 설정 / 게임 종료) + 설정 창 + 새로하기 확인 창.
//
// ★ 그림 넣는 법 ★  Assets/Resources/Art 에 아래 이름으로 PNG를 넣으면 바로 바뀐다.
//   ui_title_bg.png       메인 화면 배경 (화면을 꽉 채우게 늘어남. 16:9 권장, 예: 1920x1080)
//   ui_title_logo.png     로고
//   ui_button.png         버튼 공통 바탕 (글자는 코드가 위에 씀)
//   ui_btn_new.png / ui_btn_continue.png / ui_btn_settings.png / ui_btn_quit.png
//                         버튼별 그림 (글자까지 그려 넣은 그림이면 이걸 쓰면 됨 → 코드 글자는 숨김)
//   ui_panel_bg.png       설정 창 바탕
// 그림이 없으면 회색 버튼과 글자 로고로 보인다.
//
// 위치·크기를 바꾸려면 아래 "배치" 숫자만 고치면 된다. 기준 화면은 1920x1080이고, 좌표는 왼쪽 위에서부터.
using UnityEngine;
using UnityEngine.UI;

namespace Specimen07
{
    public partial class Hud
    {
        /* ===== 배치 (1920x1080 기준, 왼쪽 위가 0,0) ===== */
        static readonly Vector2 LOGO_POS = new Vector2(120, 150), LOGO_SIZE = new Vector2(500, 330);
        static readonly Vector2 BTN_POS = new Vector2(176, 585), BTN_SIZE = new Vector2(240, 74);
        const float BTN_GAP = 12;
        static readonly Color BTN_COLOR = Data.Hex("#d9d9d9"), BTN_TEXT = Data.Hex("#111111");

        GameObject menuRoot, settingsPanel, confirmPanel, pausePanel;
        Button btnContinue; Text continueLabel;
        Text volText, fullText, miniText;

        void BuildMenu()
        {
            menuRoot = Stretch(canvasRt, "MainMenu").gameObject;
            var mr = menuRoot.transform;

            // 배경: 그림이 있으면 화면을 꽉 채우고, 없으면 게임 화면 위에 어두운 막
            var bgRt = Stretch(mr, "Background");
            var bgSprite = Art.TryLoad("ui_title_bg");
            var bg = bgRt.gameObject.AddComponent<Image>(); bg.raycastTarget = true;
            if (bgSprite != null)
            {
                bg.sprite = bgSprite; bg.color = Color.white;
                var fit = bgRt.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = bgSprite.rect.width / bgSprite.rect.height;
            }
            else
            {
                bg.sprite = Art.Get("ui_panel"); bg.color = new Color(10 / 255f, 12 / 255f, 30 / 255f, 0.72f);
                var hint = Txt(mr, "BgHint", "배경 그림 자리 — Resources/Art/ui_title_bg.png", 18, new Color(1, 1, 1, 0.25f), TextAnchor.LowerRight);
                var hrt = hint.rectTransform; hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(1, 0); hrt.anchoredPosition = new Vector2(-30, 24); hrt.sizeDelta = new Vector2(900, 30);
            }

            // 로고
            var logoRt = TopLeft(mr, "Logo", LOGO_POS, LOGO_SIZE);
            var logoSprite = Art.TryLoad("ui_title_logo") ?? Art.TryLoad("title01");
            if (logoSprite != null)
            {
                var im = logoRt.gameObject.AddComponent<Image>(); im.sprite = logoSprite; im.preserveAspect = true; im.raycastTarget = false;
            }
            else
            {
                var t = Txt(logoRt, "Text", "SUBJECT\n<size=200><color=#b48cf0>07</color></size>", 96, Color.white, TextAnchor.UpperLeft, true);
                t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.sizeDelta = Vector2.zero; t.lineSpacing = 0.8f;
                Shadow(t);
            }

            // 버튼
            string[] keys = { "new", "continue", "settings", "quit" };
            string[] labels = { "새로하기", "이어하기", "설정", "게임 종료" };
            UnityEngine.Events.UnityAction[] acts = { OnNewGame, () => game.Continue(), () => OpenPanel(settingsPanel), () => game.QuitGame() };
            for (int i = 0; i < keys.Length; i++)
            {
                var pos = BTN_POS + new Vector2(0, i * (BTN_SIZE.y + BTN_GAP));
                var b = MenuButton(mr, keys[i], labels[i], pos, acts[i]);
                if (keys[i] == "continue") { btnContinue = b; continueLabel = b.GetComponentInChildren<Text>(true); }
            }

            BuildSettings(mr);
            BuildConfirm(mr);
            menuRoot.SetActive(false);
            BuildPause();
        }

        /* ===== 일시 정지 (게임 중 Esc, MainMenu 씬이 있을 때) ===== */
        void BuildPause()
        {
            pausePanel = ModalPanel(canvasRt, "Pause", "설정", 640);
            var card = pausePanel.transform.Find("Card");
            pauseVol = SettingRow(card, "소리 크기", "－", () => { ChangeVolume(-0.1f); RefreshPause(); }, "＋", () => { ChangeVolume(0.1f); RefreshPause(); });
            pauseMini = SettingRow(card, "지도", null, null, "바꾸기", () => { game.showMini = !game.showMini; Settings.Minimap = game.showMini; SetMiniVisible(game.showMini); RefreshPause(); });
            Txt(card, "Msg", "메인 화면으로 나가면 이어하기는 이 층에 도착한 시점부터예요.", 20, MUTED);
            var row = Row(card, "Buttons", 12);
            Btn(row, "계속하기", FLESH, Data.Hex("#1a0d10"), () => game.Resume(), 220, 60);
            Btn(row, "게임 방법", PANEL, FG, ShowHowTo, 180, 60);
            Btn(row, "메인 화면으로", PANEL, FG, () => game.GoToMenu(), 200, 60);
        }
        Text pauseVol, pauseMini;
        void RefreshPause() { if (pauseVol != null) { pauseVol.text = Mathf.RoundToInt(Settings.Volume * 100) + "%"; pauseMini.text = game.showMini ? "켜짐" : "꺼짐"; } }
        public void ShowPause() { RefreshPause(); pausePanel.SetActive(true); pausePanel.transform.SetAsLastSibling(); }
        public void HidePause() { pausePanel.SetActive(false); }

        static RectTransform TopLeft(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            return R(parent, name, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(pos.x, -pos.y), size);
        }

        Button MenuButton(Transform parent, string key, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var rt = TopLeft(parent, "Btn_" + key, pos, BTN_SIZE);
            var im = rt.gameObject.AddComponent<Image>(); im.raycastTarget = true;
            var own = Art.TryLoad("ui_btn_" + key);      // 버튼별 그림 (글자 포함)
            var common = Art.TryLoad("ui_button");        // 공통 바탕
            bool showText = true;
            if (own != null) { im.sprite = own; im.color = Color.white; im.preserveAspect = true; showText = false; }
            else if (common != null) { im.sprite = common; im.color = Color.white; if (common.border != Vector4.zero) im.type = Image.Type.Sliced; }
            else { im.sprite = Art.Get("ui_panel"); im.color = BTN_COLOR; }

            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = im; b.onClick.AddListener(onClick);
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(0.85f, 0.85f, 0.9f, 1); cb.pressedColor = new Color(0.7f, 0.7f, 0.75f, 1); cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f); b.colors = cb;

            var t = Txt(rt, "Label", label, 32, BTN_TEXT, TextAnchor.MiddleLeft, true);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.sizeDelta = new Vector2(-80, 0); t.rectTransform.anchoredPosition = new Vector2(0, 0);
            t.gameObject.SetActive(showText);
            return b;
        }

        /* ===== 설정 창 ===== */
        void BuildSettings(Transform parent)
        {
            settingsPanel = ModalPanel(parent, "Settings", "설정", 640);
            var card = settingsPanel.transform.Find("Card");
            volText = SettingRow(card, "소리 크기", "－", () => ChangeVolume(-0.1f), "＋", () => ChangeVolume(0.1f));
            fullText = SettingRow(card, "전체 화면", null, null, "바꾸기", () => { Screen.fullScreen = !Screen.fullScreen; Later(RefreshSettings); });
            miniText = SettingRow(card, "미니맵", null, null, "바꾸기", () => { game.showMini = !game.showMini; Settings.Minimap = game.showMini; RefreshSettings(); });
            var row = Row(card, "Buttons", 12);
            Btn(row, "게임 방법", PANEL, FG, ShowHowTo, 220, 60);
            Btn(row, "닫기", Data.Hex("#d9d9d9"), BTN_TEXT, () => settingsPanel.SetActive(false), 220, 60);
        }

        Text SettingRow(Transform card, string label, string aLabel, UnityEngine.Events.UnityAction a, string bLabel, UnityEngine.Events.UnityAction b)
        {
            var row = Row(card, "Row_" + label, 12);
            var l = Txt(row, "L", label, 26, FG); Fixed(l, 200, 56);
            Text value = Txt(row, "V", "", 26, Data.Hex("#b48cf0"), TextAnchor.MiddleCenter, true); Fixed(value, 140, 56);
            if (aLabel != null) Btn(row, aLabel, PANEL, FG, a, 70, 52);
            Btn(row, bLabel, PANEL, FG, b, aLabel != null ? 70 : 152, 52);
            return value;
        }

        /* ===== 새로하기 확인 창 (저장된 진행이 있을 때) ===== */
        void BuildConfirm(Transform parent)
        {
            confirmPanel = ModalPanel(parent, "Confirm", "새로 시작할까요?", 640);
            var card = confirmPanel.transform.Find("Card");
            Txt(card, "Msg", "이어하던 진행이 사라집니다.", 24, MUTED);
            var row = Row(card, "Buttons", 12);
            Btn(row, "새로 시작", FLESH, Data.Hex("#1a0d10"), () => { confirmPanel.SetActive(false); game.StartGame(); }, 220, 60);
            Btn(row, "취소", PANEL, FG, () => confirmPanel.SetActive(false), 220, 60);
        }

        GameObject ModalPanel(Transform parent, string name, string title, float width)
        {
            var root = Stretch(parent, name);
            var dim = root.gameObject.AddComponent<Image>(); dim.sprite = Art.Get("ui_panel"); dim.color = new Color(0, 0, 0, 0.55f); dim.raycastTarget = true;
            var card = R(root, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 400));
            var panelSprite = Art.TryLoad("ui_panel_bg");
            var bg = card.gameObject.AddComponent<Image>(); bg.raycastTarget = true;
            if (panelSprite != null) { bg.sprite = panelSprite; bg.color = Color.white; if (panelSprite.border != Vector4.zero) bg.type = Image.Type.Sliced; }
            else { bg.sprite = Art.Get("ui_panel"); bg.color = new Color(BG.r, BG.g, BG.b, 0.96f); }
            VStack(card, 18, 40);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Txt(card, "Title", title, 40, FG, TextAnchor.UpperLeft, true);
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        void OpenPanel(GameObject panel) { RefreshSettings(); panel.SetActive(true); panel.transform.SetAsLastSibling(); }

        /* ===== 동작 ===== */
        void OnNewGame()
        {
            if (game.HasSave()) OpenPanel(confirmPanel);
            else game.StartGame();
        }

        void ChangeVolume(float d)
        {
            Settings.Volume = Mathf.Clamp01(Mathf.Round((Settings.Volume + d) * 10) / 10f);
            AudioListener.volume = Settings.Volume;
            RefreshSettings();
        }

        void RefreshSettings()
        {
            if (volText == null) return;
            volText.text = Mathf.RoundToInt(Settings.Volume * 100) + "%";
            fullText.text = Screen.fullScreen ? "켜짐" : "꺼짐";
            miniText.text = game.showMini ? "켜짐" : "꺼짐";
        }

        void Later(System.Action a) { StartCoroutine(LaterCo(a)); }
        System.Collections.IEnumerator LaterCo(System.Action a) { yield return null; yield return null; a(); }

        public bool MenuOpen { get { return menuRoot != null && menuRoot.activeSelf; } }
        public bool MenuPanelOpen { get { return (settingsPanel != null && settingsPanel.activeSelf) || (confirmPanel != null && confirmPanel.activeSelf) || ovHowTo.activeSelf; } }
        public bool HowToOpen { get { return ovHowTo.activeSelf; } }
        public void CloseHowTo() { ovHowTo.SetActive(false); }
        public void CloseMenuPanels() { settingsPanel.SetActive(false); confirmPanel.SetActive(false); ovHowTo.SetActive(false); }

        public void ShowMenu()
        {
            menuRoot.SetActive(true);
            CloseMenuPanels();
            bool has = game.HasSave();
            btnContinue.interactable = has;
            if (continueLabel != null) continueLabel.color = has ? BTN_TEXT : new Color(BTN_TEXT.r, BTN_TEXT.g, BTN_TEXT.b, 0.35f);
        }
        public void HideMenu() { if (menuRoot != null) { CloseMenuPanels(); menuRoot.SetActive(false); } }
    }

    // 설정 저장 (PlayerPrefs)
    public static class Settings
    {
        public static float Volume { get { return PlayerPrefs.GetFloat("specimen07.volume", 0.8f); } set { PlayerPrefs.SetFloat("specimen07.volume", value); PlayerPrefs.Save(); } }
        public static bool Minimap { get { return PlayerPrefs.GetInt("specimen07.minimap", 1) == 1; } set { PlayerPrefs.SetInt("specimen07.minimap", value ? 1 : 0); PlayerPrefs.Save(); } }
    }
}
