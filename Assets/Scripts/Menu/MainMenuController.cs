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
        public const string GameScene = "Main";
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
        public Text volumeValue;
        public Text fullscreenValue;
        public Text minimapValue;

        [Header("이어하기 저장이 없을 때 버튼 글자 색")]
        public Color disabledTextColor = new Color(0.07f, 0.07f, 0.07f, 0.35f);

        void Start()
        {
            AudioListener.volume = Settings.Volume;
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
        public void OnVolumeDown() { ChangeVolume(-0.1f); }
        public void OnVolumeUp() { ChangeVolume(0.1f); }
        void ChangeVolume(float d)
        {
            Settings.Volume = Mathf.Clamp01(Mathf.Round((Settings.Volume + d) * 10) / 10f);
            AudioListener.volume = Settings.Volume;
            RefreshSettings();
        }
        public void OnToggleFullscreen() { Screen.fullScreen = !Screen.fullScreen; Invoke("RefreshSettings", 0.1f); }
        public void OnToggleMinimap() { Settings.Minimap = !Settings.Minimap; RefreshSettings(); }

        void RefreshSettings()
        {
            if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(Settings.Volume * 100) + "%";
            if (fullscreenValue != null) fullscreenValue.text = Screen.fullScreen ? "켜짐" : "꺼짐";
            if (minimapValue != null) minimapValue.text = Settings.Minimap ? "켜짐" : "꺼짐";
        }
    }
}
