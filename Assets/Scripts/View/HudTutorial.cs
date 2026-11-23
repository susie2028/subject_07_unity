// 튜토리얼 카드: 보라 판넬(frame_panel) + 위에 걸친 이름표(frame_tab) + 가운데 영상/움짤 + 설명.
// 카드 내용은 아래 Cards 표에서 바꾸면 된다. 띄우는 시점은 Game.cs의 UpdateTutorials.
//
// ★ 영상 넣는 법 (둘 중 하나)
//   1) 영상: Assets/StreamingAssets/Tutorial/<media>.mp4  (H.264 mp4 권장. GIF는 유니티가 못 읽으니 mp4로 바꿔서)
//      유니티가 영상을 변환(임포트)하지 않고 그대로 재생 → 임포트 실패로 안 뜨는 일이 없다
//   2) 그림 여러 장: Assets/Resources/Art/UI/<media>_0.png, _1.png, ... (초당 FRAME_FPS장으로 반복)
//   아무것도 없으면 "영상 자리" 표시가 나온다.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Specimen07
{
    public class TutorialCard
    {
        public string id, title, media, body;
    }

    public partial class Hud
    {
        public static readonly TutorialCard[] Cards =
        {
            new TutorialCard { id = "eat", title = "튜토리얼", media = "tut_eat",
                body = "사물이나 사람에 접근한 후\n<color=#ffd84a>E</color>를 누르면 그것을 먹어\n변신할 수 있습니다." },
            new TutorialCard { id = "sight", title = "튜토리얼", media = "tut_sight",
                body = "사람들의 시야 안에 들어가면\n걸리게 됩니다.\n시야를 잘 피해서 움직이세요." },
            new TutorialCard { id = "spit", title = "튜토리얼", media = "tut_spit",
                body = "<color=#ffd84a>Q</color>를 누르면 변신을 풀고\n먹었던 것을 그 자리에 뱉어냅니다.\n뱉어진 사람은 잠시 기절합니다." },   // 한 줄이 길면 자동으로 줄이 바뀜
        };

        // ★ 배치 (1920×1080 기준)
        const float TUT_W = 640, TUT_H = 880;      // 판넬 크기 (설명 4줄 + '계속'이 안 겹치게)
        const float TUT_MEDIA = 340;               // 영상 칸 (정사각형)
        const float TUT_TAB_W = 500, TUT_TAB_H = 96;   // 이름표 높이 = 16px × TUT_PX (줄무늬가 안 늘어나게)
        const float TUT_PX = 6f;                   // 판넬 그림 1픽셀이 화면에서 몇 px인지 (정수로 둬야 픽셀이 고르게 보임)
        const int FRAME_FPS = 12;   // Tools/art_src/tutorial/README.txt 의 fps와 같게

        GameObject ovTut; Text tutTitle, tutBody, tutMediaNote; RawImage tutMedia; Image tutFrames;
        VideoPlayer tutVideo; RenderTexture tutRT;
        Sprite[] tutFrameList; float tutFrameT;
        public bool TutorialOpen { get { return ovTut != null && ovTut.activeSelf; } }

        // ★ 디자인: 메뉴 [표본07 > 튜토리얼 카드 편집] → Assets/Resources/UI/TutorialCard.prefab 이 열린다 (씬·하이어라키에서 바로 수정).
        //   게임은 그 프리팹이 있으면 그걸 띄우고, 없으면 아래 BuildTutorialObject()로 코드에서 만든다.
        //   이름으로 찾으니 자식 이름(Panel, Tab/Title, Media/Video, Media/Frames, Media/Note, Body, Next)은 바꾸지 말 것. 위치·크기·색·글꼴은 마음대로.
        void BuildTutorial()
        {
            var pf = Resources.Load<GameObject>("UI/TutorialCard");
            GameObject root;
            if (pf != null) { root = Instantiate(pf); root.name = "Tutorial"; }
            else root = BuildTutorialObject();
            var rt = (RectTransform)root.transform;
            rt.SetParent(canvasRt, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            ovTut = root;
            var dim = root.GetComponent<Image>(); if (dim != null) dim.raycastTarget = true;
            var btn = root.GetComponent<Button>() ?? root.AddComponent<Button>();
            btn.onClick.AddListener(() => game.CloseTutorial());
            tutTitle = Child<Text>(root, "Panel/Tab/Title");
            tutMedia = Child<RawImage>(root, "Panel/Media/Video");
            tutFrames = Child<Image>(root, "Panel/Media/Frames");
            tutMediaNote = Child<Text>(root, "Panel/Media/Note");
            tutBody = Child<Text>(root, "Panel/Body");
            // 영상 재생기 (영상 파일이 있을 때만 씀)
            tutVideo = root.GetComponent<VideoPlayer>() ?? root.AddComponent<VideoPlayer>();
            tutVideo.playOnAwake = false; tutVideo.isLooping = true; tutVideo.audioOutputMode = VideoAudioOutputMode.None;
            tutVideo.renderMode = VideoRenderMode.RenderTexture;
            ovTut.SetActive(false);
        }

        static T Child<T>(GameObject root, string path) where T : Component
        {
            var t = root.transform.Find(path);
            if (t == null) { Debug.LogWarning("[표본07] 튜토리얼 카드에 '" + path + "' 이(가) 없어요 (이름을 바꾸면 안 돼요)"); return null; }
            return t.GetComponent<T>();
        }

        // 기본 카드 모양 (프리팹이 없을 때 + 프리팹을 처음 만들 때)
        public static GameObject BuildTutorialObject()
        {
            var root = new GameObject("TutorialCard", typeof(RectTransform));
            var rrt = (RectTransform)root.transform; rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            var dim = Img(rrt, new Color(0, 0, 0, 0.45f)); dim.sprite = null; dim.raycastTarget = true;

            float ppuMul = 100f / Art.PPU / TUT_PX;    // canvas 기준 100px = 1유닛
            var panel = R(root.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(TUT_W, TUT_H));
            var pim = Img(panel, Color.white, "UI/frame_panel"); pim.type = Image.Type.Tiled; pim.pixelsPerUnitMultiplier = ppuMul;

            // 이름표: 판넬 위 테두리에 걸치게
            var tab = R(panel, "Tab", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(TUT_TAB_W, TUT_TAB_H));
            var tim = Img(tab, Color.white, "UI/frame_tab"); tim.type = Image.Type.Sliced; tim.pixelsPerUnitMultiplier = ppuMul;
            var title = Txt(tab, "Title", "튜토리얼", 48, Color.white, TextAnchor.MiddleCenter, true);
            Fill(title.rectTransform, 40, 0, 0, 0); Shadow(title);

            // 영상 칸
            var media = R(panel, "Media", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(TUT_MEDIA, TUT_MEDIA));
            var mbg = Img(media, new Color(0.03f, 0.03f, 0.08f, 1)); mbg.sprite = null;
            var video = R(media, "Video", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
            video.raycastTarget = false; video.color = new Color(1, 1, 1, 0);
            var frames = Img(R(media, "Frames", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), Color.white);
            frames.sprite = Art.TryLoad("UI/tut_eat_0"); frames.preserveAspect = true;   // 디자인할 때 보이게 첫 장면 (게임에선 바뀜)
            var note = Txt(media, "Note", "", 20, MUTED, TextAnchor.MiddleCenter);
            Fill(note.rectTransform, 20, 20, 20, 20);

            // 설명: 영상 칸 바로 아래부터 (4줄까지 '계속' 글자와 안 겹침)
            var body = Txt(panel, "Body", Cards[0].body, 36, FG, TextAnchor.UpperCenter);
            var brt = body.rectTransform; brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0.5f, 1);
            brt.anchoredPosition = new Vector2(0, -110 - TUT_MEDIA - 26); brt.sizeDelta = new Vector2(-80, 210); body.lineSpacing = 1.1f;
            var more = Txt(panel, "Next", "E / Space  계속", 20, MUTED, TextAnchor.MiddleCenter);
            var mrt = more.rectTransform; mrt.anchorMin = new Vector2(0, 0); mrt.anchorMax = new Vector2(1, 0); mrt.pivot = new Vector2(0.5f, 0);
            mrt.anchoredPosition = new Vector2(0, 40); mrt.sizeDelta = new Vector2(0, 30);
            return root;
        }

        static void Fill(RectTransform rt, float l, float r, float t, float b)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        public void ShowTutorial(TutorialCard c)
        {
            if (ovTut == null) BuildTutorial();
            if (tutTitle != null) tutTitle.text = c.title;
            if (tutBody != null) tutBody.text = c.body;
            // 꺼진 오브젝트의 VideoPlayer는 재생이 안 되므로 카드부터 켠다
            ovTut.SetActive(true); ovTut.transform.SetAsLastSibling();
            // 영상(StreamingAssets/Tutorial/<media>.mp4) → 그림 여러 장 → 자리 표시 순으로 찾는다
            // 그림 여러 장(Art/UI/<media>_N.png)이 있으면 그걸 우선 (영상 재생기보다 확실하게 뜬다)
            tutFrameList = null;
            var list = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; ; i++) { var s = Art.TryLoad("UI/" + c.media + "_" + i); if (s == null) break; list.Add(s); }
            if (list.Count > 0) tutFrameList = list.ToArray();
            string url = System.IO.Path.Combine(Application.streamingAssetsPath, "Tutorial", c.media + ".mp4");
            bool hasVideo = tutFrameList == null && System.IO.File.Exists(url);
            if (tutMedia != null) { tutMedia.gameObject.SetActive(hasVideo); tutMedia.color = new Color(1, 1, 1, 0); }   // 첫 장면이 준비되기 전 검은 화면이 번쩍이지 않게
            if (tutFrames != null) tutFrames.gameObject.SetActive(tutFrameList != null);
            if (tutMediaNote != null) { tutMediaNote.gameObject.SetActive(!hasVideo && tutFrameList == null); tutMediaNote.text = "영상 자리\nArt/UI/" + c.media + "_0.png …"; }
            if (tutMedia == null) hasVideo = false;
            if (hasVideo)
            {
                tutVideo.Stop();
                tutVideo.source = VideoSource.Url; tutVideo.url = url;
                tutVideo.prepareCompleted -= OnTutPrepared; tutVideo.prepareCompleted += OnTutPrepared;
                tutVideo.errorReceived -= OnTutError; tutVideo.errorReceived += OnTutError;
                tutVideo.Prepare();
            }
            if (tutFrameList != null && tutFrames != null) { tutFrameT = 0; tutFrames.sprite = tutFrameList[0]; }
        }

        void OnTutPrepared(VideoPlayer vp)
        {
            int w = (int)Mathf.Max(16, vp.width), h = (int)Mathf.Max(16, vp.height);
            if (tutRT == null || tutRT.width != w || tutRT.height != h)
            {
                if (tutRT != null) tutRT.Release();
                tutRT = new RenderTexture(w, h, 0);
                tutRT.filterMode = FilterMode.Bilinear;   // 녹화 영상을 줄인 거라 부드럽게
            }
            vp.targetTexture = tutRT; tutMedia.texture = tutRT; tutMedia.color = Color.white;
            vp.Play();
        }

        void OnTutError(VideoPlayer vp, string msg)
        {
            Debug.LogWarning("[표본07] 튜토리얼 영상을 재생하지 못했어요: " + vp.url + " → " + msg);
            tutMedia.gameObject.SetActive(false);
            tutMediaNote.gameObject.SetActive(true); tutMediaNote.text = "영상을 재생하지 못했어요\n" + msg;
        }

        public void HideTutorial()
        {
            if (ovTut == null) return;
            if (tutVideo != null && tutVideo.isPlaying) tutVideo.Stop();
            ovTut.SetActive(false);
        }

        void UpdateTutorial(float dt)
        {
            if (!TutorialOpen || tutFrameList == null || tutFrames == null) return;
            tutFrameT += Time.unscaledDeltaTime;
            tutFrames.sprite = tutFrameList[(int)(tutFrameT * FRAME_FPS) % tutFrameList.Length];
        }
    }
}
