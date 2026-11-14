// MainMenu 씬의 버튼 동작. 화면 구성(배경, 로고, 버튼 위치·그림)은 씬에서 직접 마우스로 고치면 된다.
// 버튼의 On Click에 아래 public 함수들이 연결되어 있다. (Inspector → Button → On Click)
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Specimen07
{
    // 메뉴 씬 → 게임 씬으로 넘어갈 때 무엇을 할지 전달
    public static class Launch
    {
        public enum Request { None, New, Continue }
        public static Request request = Request.None;
        public const string MenuScene = "MainMenu";
        public static string GameScene = "Main";   // 2.5D 씬에서 메뉴로 나오면 "Main_2_5D"로 바뀐다
        public static bool MenuSceneInBuild { get { return Application.CanStreamedLevelBeLoaded(MenuScene); } }
    }

    public class MainMenuController : MonoBehaviour
    {
        [Header("버튼")]
        public Button newGameButton;
        public Button continueButton;
        public Button settingsButton;
        public Button quitButton;

        [Header("창")]
        public GameObject settingsPanel;
        public GameObject confirmPanel;

        [Header("설정 값 표시")]
        public Text volumeValue;     // 효과음 %
        public Text musicValue;      // 배경음악 % (비어 있으면 실행할 때 효과음 줄을 복사해서 만듦)
        public Text fullscreenValue;
        public Text minimapValue;

        [Header("이어하기 저장이 없을 때 버튼 글자 색")]
        public Color disabledTextColor = new Color(0.07f, 0.07f, 0.07f, 0.35f);

        void Start()
        {
            AudioListener.volume = 1f;
            EnsureMusicRow();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) c.pixelPerfect = true;   // 글자 또렷하게
            // 이 씬의 모든 버튼에 클릭 소리
            foreach (var btn in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)) btn.onClick.AddListener(UiSound.Click);
            // 기본 글꼴(LegacyRuntime)로 된 글자는 한글이 나오는 글꼴로 바꿔 준다
            foreach (var t in GetComponentsInChildren<Text>(true))
            {
                if (t.font == null || t.font.name == "LegacyRuntime" || t.font.name == "Arial") t.font = Fonts.Main;
                if (t.fontStyle == FontStyle.Bold && Fonts.Bold != Fonts.Main) { t.font = Fonts.Bold; t.fontStyle = FontStyle.Normal; }
            }
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (confirmPanel != null) confirmPanel.SetActive(false);
            bool has = SaveFile.Exists();
            if (continueButton != null)
            {
                continueButton.interactable = has;
                var label = continueButton.GetComponentInChildren<Text>(true);
                if (label != null && !has) label.color = disabledTextColor;
            }
            RefreshSettings();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (settingsPanel != null) settingsPanel.SetActive(false);
                if (confirmPanel != null) confirmPanel.SetActive(false);
            }
        }

        /* ===== 메인 버튼 ===== */
        public void OnNewGame()
        {
            if (SaveFile.Exists() && confirmPanel != null) { confirmPanel.SetActive(true); confirmPanel.transform.SetAsLastSibling(); return; }
            StartNew();
        }
        public void OnConfirmNew() { StartNew(); }
        public void OnCancel() { if (confirmPanel != null) confirmPanel.SetActive(false); }

        void StartNew()
        {
            Launch.request = Launch.Request.New;
            SceneManager.LoadScene(Launch.GameScene);
        }

        public void OnContinue()
        {
            if (!SaveFile.Exists()) return;
            Launch.request = Launch.Request.Continue;
            SceneManager.LoadScene(Launch.GameScene);
        }

        public void OnSettings()
        {
            if (settingsPanel == null) return;
            RefreshSettings();
            settingsPanel.SetActive(true);
            settingsPanel.transform.SetAsLastSibling();
        }
        public void OnCloseSettings() { if (settingsPanel != null) settingsPanel.SetActive(false); }

        public void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /* ===== 설정 ===== */
        public void OnMusicDown() { Settings.MusicVolume = Settings.Step(Settings.MusicVolume, -0.1f); RefreshSettings(); }
        public void OnMusicUp() { Settings.MusicVolume = Settings.Step(Settings.MusicVolume, 0.1f); RefreshSettings(); }

        // 예전에 만든 메인 메뉴 씬(소리 크기 한 줄)도 그대로 쓰도록: 그 줄을 '효과음'으로 바꾸고, 복사해서 '배경음악' 줄을 위에 추가
        void EnsureMusicRow()
        {
            if (musicValue != null || volumeValue == null) return;
            Transform row = volumeValue.transform;
            while (row.parent != null && row.parent.GetComponent<VerticalLayoutGroup>() == null) row = row.parent;
            if (row.parent == null) return;
            var copy = Instantiate(row.gameObject, row.parent);
            copy.name = "Row_배경음악";
            copy.transform.SetSiblingIndex(row.GetSiblingIndex());
            foreach (var t in row.GetComponentsInChildren<Text>(true)) if (t.text == "소리 크기") t.text = "효과음";
            var buttons = copy.GetComponentsInChildren<Button>(true);
            foreach (var t in copy.GetComponentsInChildren<Text>(true))
            {
                if (t.text == "소리 크기" || t.text == "효과음") t.text = "배경음악";
                else if (t.GetComponentInParent<Button>() == null && t.text.EndsWith("%")) musicValue = t;
            }
            if (musicValue == null)   // 값 칸을 이름으로 찾기
                foreach (var t in copy.GetComponentsInChildren<Text>(true)) if (t.GetComponentInParent<Button>() == null && t.text != "배경음악") musicValue = t;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].onClick = new Button.ButtonClickedEvent();         // 복사된 연결(효과음) 지우기
                if (i == 0 && buttons.Length > 1) buttons[i].onClick.AddListener(OnMusicDown);
                else buttons[i].onClick.AddListener(OnMusicUp);
            }
        }

        public void OnVolumeDown() { ChangeVolume(-0.1f); }
        public void OnVolumeUp() { ChangeVolume(0.1f); }
        void ChangeVolume(float d)
        {
            Settings.SfxVolume = Settings.Step(Settings.SfxVolume, d);
            RefreshSettings();
        }
        public void OnToggleFullscreen() { Screen.fullScreen = !Screen.fullScreen; Invoke("RefreshSettings", 0.1f); }
        public void OnToggleMinimap() { Settings.Minimap = !Settings.Minimap; RefreshSettings(); }

        void RefreshSettings()
        {
            if (volumeValue != null) volumeValue.text = Settings.Pct(Settings.SfxVolume);
            if (musicValue != null) musicValue.text = Settings.Pct(Settings.MusicVolume);
            if (fullscreenValue != null) fullscreenValue.text = Screen.fullScreen ? "켜짐" : "꺼짐";
            if (minimapValue != null) minimapValue.text = Settings.Minimap ? "켜짐" : "꺼짐";
        }
    }
}
