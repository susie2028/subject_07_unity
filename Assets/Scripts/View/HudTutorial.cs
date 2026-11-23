// 튜토리얼 카드: 보라 판넬(frame_panel) + 위에 걸친 이름표(frame_tab) + 가운데 영상/움짤 + 설명.
// 카드 내용은 아래 Cards 표에서 바꾸면 된다. 띄우는 시점은 Game.cs의 UpdateTutorials.
//
// ★ 영상 넣는 법 (둘 중 하나)
//   1) 영상: Assets/Resources/Tutorial/<media>.mp4  (H.264 mp4 권장. GIF는 유니티가 못 읽으니 mp4로 바꿔서)
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
            new TutorialCard { id = "spit", title = "튜토리얼", media = "tut_spit",
                body = "<color=#ffd84a>Q</color>를 누르면 변신을 풀고\n먹었던 것을 그 자리에 뱉어냅니다.\n뱉어진 사람은 잠시 기절합니다." },
        };

        // ★ 배치 (1920×1080 기준)
        const float TUT_W = 620, TUT_H = 780;      // 판넬 크기
        const float TUT_MEDIA = 380;               // 영상 칸 (정사각형)
        const float TUT_TAB_W = 500, TUT_TAB_H = 96;   // 이름표 높이 = 16px × TUT_PX (줄무늬가 안 늘어나게)
        const float TUT_PX = 6f;                   // 판넬 그림 1픽셀이 화면에서 몇 px인지 (정수로 둬야 픽셀이 고르게 보임)
        const int FRAME_FPS = 10;

        GameObject ovTut; Text tutTitle, tutBody, tutMediaNote; RawImage tutMedia; Image tutFrames;
        VideoPlayer tutVideo; RenderTexture tutRT;
        Sprite[] tutFrameList; float tutFrameT;
        public bool TutorialOpen { get { return ovTut != null && ovTut.activeSelf; } }

        void BuildTutorial()
        {
            ovTut = Stretch(canvasRt, "Tutorial").gameObject;
            var dim = Img((RectTransform)ovTut.transform, new Color(0, 0, 0, 0.45f)); dim.sprite = null; dim.raycastTarget = true;
            ovTut.AddComponent<Button>().onClick.AddListener(() => game.CloseTutorial());

            float ppuMul = 100f / Art.PPU / TUT_PX;    // canvas 기준 100px = 1유닛
            var panel = R(ovTut.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(TUT_W, TUT_H));
            var pim = Img(panel, Color.white, "UI/frame_panel"); pim.type = Image.Type.Tiled; pim.pixelsPerUnitMultiplier = ppuMul;

            // 이름표: 판넬 위 테두리에 걸치게
            var tab = R(panel, "Tab", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(TUT_TAB_W, TUT_TAB_H));
            var tim = Img(tab, Color.white, "UI/frame_tab"); tim.type = Image.Type.Sliced; tim.pixelsPerUnitMultiplier = ppuMul;
            tutTitle = Txt(tab, "Title", "", 48, Color.white, TextAnchor.MiddleCenter, true);
            Fill(tutTitle.rectTransform, 40, 0, 0, 0); Shadow(tutTitle);

            // 영상 칸
            var media = R(panel, "Media", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(TUT_MEDIA, TUT_MEDIA));
            var mbg = Img(media, new Color(0.03f, 0.03f, 0.08f, 1)); mbg.sprite = null;
            tutMedia = R(media, "Video", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
            tutMedia.raycastTarget = false;
            tutFrames = Img(R(media, "Frames", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), Color.white); tutFrames.sprite = null;
            tutFrames.preserveAspect = true;
            tutMediaNote = Txt(media, "Note", "", 20, MUTED, TextAnchor.MiddleCenter);
            Fill(tutMediaNote.rectTransform, 20, 20, 20, 20);

            // 설명
            tutBody = Txt(panel, "Body", "", 36, FG, TextAnchor.UpperCenter);
            var brt = tutBody.rectTransform; brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0); brt.pivot = new Vector2(0.5f, 0);
            brt.anchoredPosition = new Vector2(0, 92); brt.sizeDelta = new Vector2(-90, 180); tutBody.lineSpacing = 1.15f;
            var more = Txt(panel, "Next", "E / Space  계속", 20, MUTED, TextAnchor.MiddleCenter);
            var mrt = more.rectTransform; mrt.anchorMin = new Vector2(0, 0); mrt.anchorMax = new Vector2(1, 0); mrt.pivot = new Vector2(0.5f, 0);
            mrt.anchoredPosition = new Vector2(0, 52); mrt.sizeDelta = new Vector2(0, 30);

            // 영상 재생기 (영상 파일이 있을 때만 씀)
            tutVideo = ovTut.AddComponent<VideoPlayer>();
            tutVideo.playOnAwake = false; tutVideo.isLooping = true; tutVideo.audioOutputMode = VideoAudioOutputMode.None;
            tutVideo.renderMode = VideoRenderMode.RenderTexture;
            ovTut.SetActive(false);
        }

        static void Fill(RectTransform rt, float l, float r, float t, float b)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        public void ShowTutorial(TutorialCard c)
        {
            if (ovTut == null) BuildTutorial();
            tutTitle.text = c.title; tutBody.text = c.body;
            // 영상 → 그림 여러 장 → 자리 표시 순으로 찾는다
            var clip = Resources.Load<VideoClip>("Tutorial/" + c.media);
            tutFrameList = null;
            if (clip == null)
            {
                var list = new System.Collections.Generic.List<Sprite>();
                for (int i = 0; ; i++) { var s = Art.TryLoad("UI/" + c.media + "_" + i); if (s == null) break; list.Add(s); }
                if (list.Count > 0) tutFrameList = list.ToArray();
            }
            tutMedia.gameObject.SetActive(clip != null);
            tutFrames.gameObject.SetActive(tutFrameList != null);
            tutMediaNote.gameObject.SetActive(clip == null && tutFrameList == null);
            tutMediaNote.text = "영상 자리\nResources/Tutorial/" + c.media + ".mp4";
            if (clip != null)
            {
                if (tutRT == null || tutRT.width != (int)clip.width || tutRT.height != (int)clip.height)
                {
                    if (tutRT != null) tutRT.Release();
                    tutRT = new RenderTexture((int)Mathf.Max(16, clip.width), (int)Mathf.Max(16, clip.height), 0);
                    tutRT.filterMode = FilterMode.Point;
                }
                tutVideo.clip = clip; tutVideo.targetTexture = tutRT; tutMedia.texture = tutRT;
                tutVideo.Play();
            }
            if (tutFrameList != null) { tutFrameT = 0; tutFrames.sprite = tutFrameList[0]; }
            ovTut.SetActive(true); ovTut.transform.SetAsLastSibling();
        }

        public void HideTutorial()
        {
            if (ovTut == null) return;
            if (tutVideo != null && tutVideo.isPlaying) tutVideo.Stop();
            ovTut.SetActive(false);
        }

        void UpdateTutorial(float dt)
        {
            if (!TutorialOpen || tutFrameList == null) return;
            tutFrameT += Time.unscaledDeltaTime;
            tutFrames.sprite = tutFrameList[(int)(tutFrameT * FRAME_FPS) % tutFrameList.Length];
        }
    }
}
