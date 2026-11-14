// UI 전부 (HUD, 미니맵, 말풍선, 타이틀/인트로/엘리베이터/게임오버/클리어 화면).
// 코드로 만들어지므로 씬에 아무것도 없어도 된다. 글꼴은 Resources/Fonts/Main.ttf가 있으면 그걸 쓰고,
// 없으면 컴퓨터에 설치된 한글 글꼴(맑은 고딕 등)을 쓴다.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Specimen07
{
    public static class Fonts
    {
        static Font main, bold;
        // 굵은 글씨는 가짜 굵게(FontStyle.Bold) 대신 진짜 Bold 글꼴 파일을 쓴다 → 뭉개지지 않음
        public static Font Bold
        {
            get
            {
                if (bold == null) { bold = Resources.Load<Font>("Fonts/Bold"); if (bold == null) bold = Main; }
                return bold;
            }
        }
        public static Font Main
        {
            get
            {
                if (main == null)
                {
                    main = Resources.Load<Font>("Fonts/Main");
                    if (main == null) main = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "AppleGothic", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic", "Arial" }, 32);
                }
                return main;
            }
        }
    }

    public partial class Hud : MonoBehaviour
    {
        static readonly Color BG = Data.Hex("#0c1115"), PANEL = Data.Hex("#141b21"), FG = Data.Hex("#dde6e3"), MUTED = Data.Hex("#8a9a99"),
                              FLESH = Data.Hex("#ff7d8c"), HAZARD = Data.Hex("#f3b04a"), ALARM = Data.Hex("#ff5148"), EXIT = Data.Hex("#7fe0b0");

        Game game; Camera cam;
        RectTransform canvasRt;
        // HUD
        GameObject hudRoot;
        Image formIcon; Text formName;
        RectTransform formCard, timerBox; Image timerFill, timerBg; Text timerLabel, timerNum, countdown; float heartT;
        readonly List<Image> cardBoxes = new List<Image>(), cardStrips = new List<Image>(); readonly List<Text> cardTexts = new List<Text>();
        GameObject pillSeen, pillOdd, pillVent;
        Text floorTag, objective, alarmText, meta; GameObject alarmBox;
        RawImage mini; Texture2D miniTex; Color32[] miniBase, miniPx; int miniTick;
        Image hintBg; Text hint;
        Text toast; float toastT = -1; Text bannerTitle, bannerSub; float bannerT = -1;
        Image fadeImg, flashImg; float fadeT = -1, flashT = -1;
        Image dangerVig, timerVig;
        readonly List<Image> bubbleBgs = new List<Image>(); readonly List<Text> bubbleTexts = new List<Text>(); readonly List<Text> marks = new List<Text>();
        RectTransform bubbleLayer;
        // 오버레이
        GameObject ovHowTo, ovIntro, ovLift, ovOver, ovClear;
        Text introLog; Coroutine introCo;
        RectTransform liftList;
        Text overHead, overReason, overTip;
        Text stTime, stDeaths, stAlarms;
        string lastObj = "", lastHint = null, lastForm = null, lastCards = null;

        void Awake()
        {
            game = GetComponent<Game>();
            cam = Camera.main;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();
            }
            var cgo = new GameObject("UI");
            var canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = cgo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();
            canvasRt = cgo.GetComponent<RectTransform>();
            BuildHud();
            BuildOverlays();
        }

        /* ================= 만들기 도우미 ================= */
        static RectTransform R(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }
        static RectTransform Stretch(Transform parent, string name) { var rt = R(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero); return rt; }
        static Image Img(RectTransform rt, Color c, string sprite = "ui_panel")
        {
            var im = rt.gameObject.AddComponent<Image>(); im.sprite = Art.Get(sprite); im.color = c; im.raycastTarget = false; return im;
        }
        static Text Txt(Transform parent, string name, string text, int size, Color c, TextAnchor align = TextAnchor.UpperLeft, bool bold = false)
        {
            var rt = R(parent, name, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(400, size * 1.6f));
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Fonts.Main; t.text = text; t.fontSize = size; t.color = c; t.alignment = align; t.supportRichText = true; t.raycastTarget = false;
            if (bold) { t.font = Fonts.Bold; t.fontStyle = Fonts.Bold == Fonts.Main ? FontStyle.Bold : FontStyle.Normal; }
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        static void Shadow(Graphic g) { var s = g.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, 0.6f); s.effectDistance = new Vector2(0, -2); }
        Button Btn(Transform parent, string label, Color bg, Color fg, UnityEngine.Events.UnityAction onClick, float w = 360, float h = 64)
        {
            var rt = R(parent, "Btn_" + label, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(w, h));
            var im = Img(rt, bg); im.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = im; b.onClick.AddListener(onClick);
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav; // 키보드 Space/Enter가 게임 조작과 겹치지 않게
            var cb = b.colors; cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1); cb.disabledColor = new Color(1, 1, 1, 0.45f); b.colors = cb;
            var t = Txt(rt, "Label", label, 28, fg, TextAnchor.MiddleCenter, true);
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = Vector2.zero; trt.anchoredPosition = Vector2.zero;
            var le = rt.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = w; le.preferredHeight = h;
            return b;
        }
        static VerticalLayoutGroup VStack(RectTransform rt, float spacing, int pad = 0)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>(); v.spacing = spacing; v.padding = new RectOffset(pad, pad, pad, pad);
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false; v.childAlignment = TextAnchor.UpperLeft;
            return v;
        }
        static HorizontalLayoutGroup HStack(RectTransform rt, float spacing)
        {
            var hz = rt.gameObject.AddComponent<HorizontalLayoutGroup>(); hz.spacing = spacing;
            hz.childControlWidth = true; hz.childControlHeight = true; hz.childForceExpandWidth = false; hz.childForceExpandHeight = false; hz.childAlignment = TextAnchor.MiddleLeft;
            return hz;
        }
        static RectTransform Row(Transform parent, string name, float spacing) { var rt = R(parent, name, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(100, 40)); HStack(rt, spacing); return rt; }
        static void Fixed(Component c, float w, float h) { var le = c.gameObject.GetComponent<LayoutElement>(); if (le == null) le = c.gameObject.AddComponent<LayoutElement>(); if (w > 0) le.preferredWidth = w; if (h > 0) le.preferredHeight = h; }

        /* ================= HUD ================= */
        // ★ HUD 배치 (1920x1080 기준). 위치를 바꾸려면 여기 숫자만 고치면 된다.
        //   왼쪽 아래: 현재 형태 카드 + 변신 해제까지 남은 시간 / 오른쪽 위: 설정 / 오른쪽 아래: 현재 층 + 목표 / 왼쪽 위: 지도
        void BuildHud()
        {
            hudRoot = Stretch(canvasRt, "HUD").gameObject;
            var hr = hudRoot.transform;
            Color panel = new Color(10 / 255f, 12 / 255f, 34 / 255f, 0.86f);

            dangerVig = Img(Stretch(hr, "DangerVignette"), new Color(1, 40 / 255f, 40 / 255f, 0), "ui_vignette");
            timerVig = Img(Stretch(hr, "TimerVignette"), new Color(1, 125 / 255f, 140 / 255f, 0), "ui_vignette");
            bubbleLayer = Stretch(hr, "Bubbles");

            // ── 왼쪽 아래: 현재 형태 카드
            formCard = R(hr, "FormCard", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(60, 104), new Vector2(230, 190));
            var cardImg = Img(formCard, panel, "ui_round"); cardImg.type = Image.Type.Sliced;
            var lab = Txt(formCard, "Label", "현재 형태", 22, MUTED, TextAnchor.UpperCenter, true);
            lab.rectTransform.anchorMin = new Vector2(0, 1); lab.rectTransform.anchorMax = new Vector2(1, 1); lab.rectTransform.pivot = new Vector2(0.5f, 1);
            lab.rectTransform.anchoredPosition = new Vector2(0, -14); lab.rectTransform.sizeDelta = new Vector2(0, 30);
            formIcon = Img(R(formCard, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(120, 90)), Color.white, "player_blob");
            formIcon.preserveAspect = true;
            formName = Txt(formCard, "Name", "본모습", 26, FG, TextAnchor.LowerCenter, true);
            formName.rectTransform.anchorMin = Vector2.zero; formName.rectTransform.anchorMax = new Vector2(1, 0); formName.rectTransform.pivot = new Vector2(0.5f, 0);
            formName.rectTransform.anchoredPosition = new Vector2(0, 12); formName.rectTransform.sizeDelta = new Vector2(0, 36);

            // ── 왼쪽 아래: 변신 해제까지 남은 시간 (카드 바로 아래, 크게)
            timerBox = R(hr, "Timer", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(60, 34), new Vector2(330, 62));
            timerBg = Img(timerBox, panel, "ui_round"); timerBg.type = Image.Type.Sliced;
            var fillRt = R(timerBox, "Fill", Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, new Vector2(-12, -12));
            timerFill = Img(fillRt, FLESH, "ui_panel"); timerFill.type = Image.Type.Filled; timerFill.fillMethod = Image.FillMethod.Horizontal; timerFill.fillOrigin = 0;
            timerLabel = Txt(timerBox, "Label", "변신 해제까지", 20, Color.white, TextAnchor.MiddleLeft, true); Shadow(timerLabel);
            timerLabel.rectTransform.anchorMin = Vector2.zero; timerLabel.rectTransform.anchorMax = Vector2.one; timerLabel.rectTransform.sizeDelta = new Vector2(-36, 0);
            timerNum = Txt(timerBox, "Num", "", 36, Color.white, TextAnchor.MiddleRight, true); Shadow(timerNum);
            timerNum.rectTransform.anchorMin = Vector2.zero; timerNum.rectTransform.anchorMax = Vector2.one; timerNum.rectTransform.sizeDelta = new Vector2(-30, 0);

            // ── 카드 위: 출입 카드 / 오른쪽: 상태 표시
            // 출입 카드: 6장을 한 줄로 (가진 카드는 카드 색으로 밝게, 없는 카드는 어둡게)
            const float CW = 62, CH = 46, CG = 7;
            var cards = R(hr, "Cards", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(60, 306), new Vector2(6 * CW + 5 * CG, CH + 26));
            var cardsLab = Txt(cards, "Label", "출입 카드", 17, MUTED, TextAnchor.UpperLeft, true);
            cardsLab.rectTransform.anchorMin = new Vector2(0, 1); cardsLab.rectTransform.anchorMax = new Vector2(1, 1); cardsLab.rectTransform.pivot = new Vector2(0, 1);
            cardsLab.rectTransform.anchoredPosition = Vector2.zero; cardsLab.rectTransform.sizeDelta = new Vector2(0, 22);
            for (int i = 0; i < Data.CardOrder.Length; i++)
            {
                var c = Data.CardOrder[i];
                var box = R(cards, "Card_" + c, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(i * (CW + CG), 0), new Vector2(CW, CH));
                var im = Img(box, panel, "ui_round"); im.type = Image.Type.Sliced;
                var strip = R(box, "Strip", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -5), new Vector2(-14, 5));
                var st = Img(strip, Data.Cards[c].color);
                var t = Txt(box, "T", Data.Cards[c].name, 19, MUTED, TextAnchor.MiddleCenter, true);
                t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.sizeDelta = Vector2.zero; t.rectTransform.anchoredPosition = new Vector2(0, -3);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                cardBoxes.Add(im); cardTexts.Add(t); cardStrips.Add(st);
            }
            var pills = R(hr, "Pills", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(304, 110), new Vector2(200, 120));
            var pv = VStack(pills, 8); pv.childForceExpandWidth = false; pv.childAlignment = TextAnchor.LowerLeft;
            pillSeen = Pill(pills, "시야 안", HAZARD); pillOdd = Pill(pills, "어색한 위치", ALARM); pillVent = Pill(pills, "환풍구 안", Data.Hex("#b9a6ff"));

            // ── 왼쪽 위: 지도 (본 곳만)
            var mrt = R(hr, "Minimap", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -26), new Vector2(300, 170));
            mini = mrt.gameObject.AddComponent<RawImage>(); mini.raycastTarget = false;

            // ── 오른쪽 위: 설정 버튼 + 기록
            var setBtn = Btn(hr, "설정", panel, FG, () => game.Pause(), 128, 58);
            var srt = (RectTransform)setBtn.transform; srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(1, 1); srt.anchoredPosition = new Vector2(-30, -30);
            setBtn.GetComponent<Image>().sprite = Art.Get("ui_round"); setBtn.GetComponent<Image>().type = Image.Type.Sliced;
            meta = Txt(hr, "Meta", "", 17, MUTED, TextAnchor.UpperRight);
            meta.rectTransform.anchorMin = meta.rectTransform.anchorMax = meta.rectTransform.pivot = new Vector2(1, 1); meta.rectTransform.anchoredPosition = new Vector2(-30, -96); meta.rectTransform.sizeDelta = new Vector2(500, 26);
            // 2.5D 비교 씬 표시 (설정 버튼 왼쪽)
            if (game.use25D)
            {
                var tag = R(hr, "Tag25D", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-172, -36), new Vector2(92, 46));
                var tagImg = Img(tag, new Color(0.62f, 0.42f, 1f, 0.92f), "ui_round"); tagImg.type = Image.Type.Sliced;
                var tt = Txt(tag, "T", "2.5D", 24, Color.white, TextAnchor.MiddleCenter, true);
                tt.rectTransform.anchorMin = Vector2.zero; tt.rectTransform.anchorMax = Vector2.one; tt.rectTransform.sizeDelta = Vector2.zero;
            }

            // ── 위 가운데: 경보
            alarmBox = R(hr, "Alarm", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(440, 50)).gameObject;
            Img((RectTransform)alarmBox.transform, ALARM, "ui_round").type = Image.Type.Sliced;
            alarmText = Txt(alarmBox.transform, "T", "", 26, Color.white, TextAnchor.MiddleCenter, true);
            alarmText.rectTransform.anchorMin = Vector2.zero; alarmText.rectTransform.anchorMax = Vector2.one; alarmText.rectTransform.sizeDelta = Vector2.zero;

            // ── 오른쪽 아래: 현재 층 + 목표 (글 길이에 맞춰 늘어나는 상자)
            floorTag = AutoBox(hr, "Floor", new Vector2(-30, 100), 30, panel);
            objective = AutoBox(hr, "Objective", new Vector2(-30, 34), 21, panel);

            // ── 아래 가운데: 안내
            var hrt = R(hr, "Hint", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(40, 34), new Vector2(700, 60));
            hintBg = Img(hrt, new Color(10 / 255f, 12 / 255f, 34 / 255f, 0.88f), "ui_round"); hintBg.type = Image.Type.Sliced;
            hint = Txt(hrt, "T", "", 22, FG, TextAnchor.MiddleCenter);
            hint.rectTransform.anchorMin = Vector2.zero; hint.rectTransform.anchorMax = Vector2.one; hint.rectTransform.sizeDelta = new Vector2(-40, -16);

            // ── 화면 가운데: 변신 풀리기 직전 카운트다운
            countdown = Txt(hr, "Countdown", "", 220, new Color(1, 0.35f, 0.42f, 0), TextAnchor.MiddleCenter, true); Shadow(countdown);
            Center(countdown.rectTransform, new Vector2(0.5f, 0.56f), new Vector2(600, 300));

            toast = Txt(canvasRt, "Toast", "", 46, FG, TextAnchor.MiddleCenter, true); Shadow(toast);
            Center(toast.rectTransform, new Vector2(0.5f, 0.8f), new Vector2(1600, 80)); toast.gameObject.SetActive(false);
            bannerTitle = Txt(canvasRt, "BannerTitle", "", 120, Color.white, TextAnchor.MiddleCenter, true); Shadow(bannerTitle);
            Center(bannerTitle.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(1800, 160)); bannerTitle.gameObject.SetActive(false);
            bannerSub = Txt(canvasRt, "BannerSub", "", 32, HAZARD, TextAnchor.MiddleCenter); Shadow(bannerSub);
            Center(bannerSub.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(1600, 60)); bannerSub.gameObject.SetActive(false);
            fadeImg = Img(Stretch(canvasRt, "Fade"), new Color(0, 0, 0, 0)); fadeImg.gameObject.SetActive(false);

            hudRoot.SetActive(false);
        }

        // 오른쪽 아래에 붙는, 글 길이에 맞춰 늘어나는 상자
        Text AutoBox(Transform parent, string name, Vector2 pos, int size, Color bg)
        {
            var rt = R(parent, name, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), pos, new Vector2(200, 56));
            var im = Img(rt, bg, "ui_round"); im.type = Image.Type.Sliced;
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>(); h.padding = new RectOffset(22, 22, 12, 12);
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false; h.childAlignment = TextAnchor.MiddleRight;
            var fit = rt.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var t = Txt(rt, "T", "", size, FG, TextAnchor.MiddleRight, size >= 28);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }
        static void Center(RectTransform rt, Vector2 anchor, Vector2 size) { rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = size; }
        GameObject Pill(Transform parent, string text, Color c)
        {
            var rt = R(parent, "Pill_" + text, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(130, 32));
            Img(rt, new Color(BG.r, BG.g, BG.b, 0.9f));
            var t = Txt(rt, "T", text, 18, c, TextAnchor.MiddleCenter, true);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.sizeDelta = Vector2.zero;
            Fixed(rt, t.preferredWidth + 24, 32);
            rt.gameObject.SetActive(false);
            return rt.gameObject;
        }

        /* ================= 오버레이 ================= */
        RectTransform Overlay(string name, out RectTransform card, float width = 1000)
        {
            var ov = Stretch(canvasRt, name);
            var bg = Img(ov, new Color(7 / 255f, 10 / 255f, 12 / 255f, 0.86f)); bg.raycastTarget = true;
            card = R(ov, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 600));
            VStack(card, 22);
            var fit = card.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ov.gameObject.SetActive(false);
            return ov;
        }
        Text Tag(Transform parent, string text)
        {
            var t = Txt(parent, "Tag", text, 18, Data.Hex("#e9dcc0"), TextAnchor.UpperLeft, true);
            return t;
        }

        void BuildOverlays()
        {
            RectTransform card;
            // 게임 방법 (메인 화면 → 설정 → 게임 방법)
            ovHowTo = Overlay("HowTo", out card, 1100).gameObject;
            Tag(card, "SPECIMEN 07 · 모방성 유기체 · 처분 예정 09:00");
            Txt(card, "Title", "표본 <color=#ff7d8c>07</color>", 150, FG, TextAnchor.UpperLeft, true);
            Txt(card, "Lede", "먹은 것의 모습이 되고, 그 사람의 출입 카드도 내 것이 된다. 지하 격리동에서 깨어나 층을 오르내리며 잠긴 문과 함정을 풀고, 4층 소장실에서 연구소장을 먹은 뒤 옥상 헬기로 빠져나가자.", 26, FG);
            var rules = Row(card, "Rules", 24); rules.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true; rules.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
            Rule(rules, "본모습", "B1~1F에서는 누구 눈에 띄어도 끝. 2F부터는 경보가 울리고 경비원·군인이 달려온다.", FLESH);
            Rule(rules, "물건", "보는 앞에서는 꼼짝 말 것. 제자리가 아니면 의심받는다. 카트만 천천히 굴러갈 수 있다.", HAZARD);
            Rule(rules, "사람", "먹으면 그 사람의 카드를 얻는다. 보는 앞에서도 걷고, 문을 연다. 단, 있으면 안 되는 구역이 있다.", Data.Hex("#8fb4d6"));
            CharTable(card);
            Txt(card, "Keys", "<b>WASD / 방향키</b> 이동     <b>E / Space</b> 먹기 · 열기 · 조작     <b>M</b> 지도     <b>R</b> 층 도착 시점부터", 20, MUTED);
            var row = Row(card, "Buttons", 12);
            Btn(row, "닫기", PANEL, FG, () => ovHowTo.SetActive(false), 220, 64);

            // 인트로
            ovIntro = Overlay("Intro", out card, 1100).gameObject;
            introLog = Txt(card, "Log", "", 30, Data.Hex("#cfe0d8")); Fixed(introLog, -1, 280);
            Txt(card, "Skip", "아무 키나 누르면 건너뜁니다", 18, MUTED);
            ovIntro.GetComponent<Image>().gameObject.AddComponent<Button>().onClick.AddListener(() => game.FinishIntro());

            // 엘리베이터
            ovLift = Overlay("Lift", out card, 560).gameObject;
            Tag(card, "엘리베이터 · 층 선택");
            liftList = R(card, "List", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(560, 400));
            VStack(liftList, 8);
            Txt(card, "Skip", "1~5 층 선택 · Esc 닫기", 18, MUTED);

            // 게임 오버
            ovOver = Overlay("Over", out card, 1000).gameObject;
            overHead = Txt(card, "Head", "발각", 150, ALARM, TextAnchor.UpperLeft, true);
            overReason = Txt(card, "Reason", "", 34, FG);
            overTip = Txt(card, "Tip", "", 22, MUTED);
            row = Row(card, "Buttons", 14);
            Btn(row, "층 도착 시점부터 (R)", FLESH, Data.Hex("#1a0d10"), () => game.Retry(), 380, 70);
            Btn(row, "처음부터", PANEL, FG, () => game.NewRun(), 220, 70);
            Btn(row, "메인 화면", PANEL, FG, () => game.GoToMenu(), 220, 70);

            // 클리어
            ovClear = Overlay("Clear", out card, 1000).gameObject;
            Tag(card, "비행 기록 · 소장 전용기 이륙 · 탑승자 1명(소장)");
            Txt(card, "Head", "탈출 성공", 130, EXIT, TextAnchor.UpperLeft, true);
            Txt(card, "Lede", "연구소장의 얼굴로 헬기에 올랐다. 조종사는 아무것도 묻지 않았다. 남은 비행 시간은 40분 — 그동안 이 모습이 버텨 주기만 하면 된다.", 26, FG);
            var stats = Row(card, "Stats", 60);
            stTime = Stat(stats, "걸린 시간"); stDeaths = Stat(stats, "재시도"); stAlarms = Stat(stats, "경보");
            row = Row(card, "Buttons", 12);
            Btn(row, "처음부터 다시", FLESH, Data.Hex("#1a0d10"), () => game.NewRun(), 300, 70);
            Btn(row, "메인 화면", PANEL, FG, () => game.GoToMenu(), 220, 70);

            BuildMenu(); // 메인 화면 (HudMenu.cs)

            flashImg = Img(Stretch(canvasRt, "Flash"), new Color(1, 1, 1, 0)); flashImg.gameObject.SetActive(false);
        }
        void Rule(Transform parent, string head, string body, Color c)
        {
            var col = R(parent, "Rule_" + head, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(300, 160));
            VStack(col, 6);
            var line = R(col, "Line", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(300, 3)); Img(line, c); Fixed(line, -1, 3);
            Txt(col, "H", head, 26, FG, TextAnchor.UpperLeft, true);
            Txt(col, "B", body, 19, MUTED);
            Fixed(col, 320, -1);
        }
        void CharTable(Transform parent)
        {
            var tb = R(parent, "Chars", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(1000, 200)); VStack(tb, 2);
            string[] heads = { "인물", "시야", "모양", "속도", "움직이는 빈도", "카드" };
            float[] widths = { 200, 100, 120, 100, 200, 120 };
            var hr = Row(tb, "Head", 0);
            for (int i = 0; i < heads.Length; i++) { var t = Txt(hr, "H", heads[i], 17, MUTED, TextAnchor.MiddleLeft, true); Fixed(t, widths[i], 30); }
            foreach (var id in Data.CharOrder)
            {
                var c = Data.Chars[id]; var r = Row(tb, id, 0);
                string card = c.access[c.access.Length - 1];
                string[] cells = { c.name, c.view + "칸", Data.ShapeLabel(c.shape), c.speed.ToString(), c.freq, "<color=#" + ColorUtility.ToHtmlStringRGB(Data.Cards[card].color) + ">■ " + Data.Cards[card].name + "</color>" };
                for (int i = 0; i < cells.Length; i++) { var t = Txt(r, "C", cells[i], 18, i == 0 ? FG : Data.Hex("#b9c6c3"), TextAnchor.MiddleLeft, i == 0); Fixed(t, widths[i], 28); }
            }
        }
        Text Stat(Transform parent, string label)
        {
            var col = R(parent, "Stat_" + label, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(200, 100)); VStack(col, 0); Fixed(col, 200, 100);
            Txt(col, "L", label, 18, MUTED);
            return Txt(col, "V", "0", 48, FG, TextAnchor.UpperLeft, true);
        }

        /* ================= 화면 전환 ================= */
        public void HideAllOverlays() { HideMenu(); HidePause(); ovHowTo.SetActive(false); ovIntro.SetActive(false); ovLift.SetActive(false); ovOver.SetActive(false); ovClear.SetActive(false); }
        public void ShowTitle() { HideAllOverlays(); ShowMenu(); hudRoot.SetActive(false); }
        public void ShowHowTo() { ovHowTo.SetActive(true); ovHowTo.transform.SetAsLastSibling(); }

        public void ShowIntro()
        {
            game.sfx.Init();
            HideMenu(); ovIntro.SetActive(true);
            if (introCo != null) StopCoroutine(introCo);
            introCo = StartCoroutine(IntroCo());
        }
        IEnumerator IntroCo()
        {
            string full = string.Join("\n", Data.IntroLines);
            for (int i = 1; i <= full.Length; i++)
            {
                introLog.text = full.Substring(0, i) + "<color=#cfe0d8>▌</color>";
                char ch = full[i - 1];
                if (ch != ' ' && ch != '\n' && i % 2 == 1) game.sfx.Play("blip");
                yield return new WaitForSeconds(ch == '\n' ? 0.38f : 0.03f);
            }
            yield return new WaitForSeconds(1.1f);
            game.FinishIntro();
        }
        public void HideIntro() { if (introCo != null) StopCoroutine(introCo); introCo = null; ovIntro.SetActive(false); }

        public void OnBeginPlay()
        {
            HideAllOverlays();
            hudRoot.SetActive(true);
            mini.gameObject.SetActive(game.showMini);
            lastHint = null; lastForm = null; lastCards = null; lastObj = "";
            Refresh(true);
        }
        public void SetMiniVisible(bool v) { mini.gameObject.SetActive(v); }

        public void OnFloorChanged()
        {
            var G = game.G;
            floorTag.text = "<color=#b48cf0>" + G.id + "</color>  " + G.name;
            BuildMini();
        }

        public void ShowLift()
        {
            foreach (Transform c in liftList) Destroy(c.gameObject);
            var S = game.S;
            foreach (var e in Data.LiftFloors)
            {
                string fid = e[0], req = e[1];
                bool here = fid == S.floorId, ok = game.HasCard(req);
                string small = here ? "현재 층" : ok ? (req != null ? Data.Cards[req].name + " 카드 인증됨" : "") : Data.Cards[req].name + " 카드 필요 · " + Data.Cards[req].from;
                var b = Btn(liftList, fid, here ? Data.Hex("#1d2a26") : PANEL, FG, () => game.ChooseLift(fid), 560, 60);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft; t.rectTransform.sizeDelta = new Vector2(-40, 0);
                t.text = "<color=#ff7d8c>" + fid + "</color> " + FloorGeo.All[fid].name + "   <size=18><color=#8a9a99>" + small + "</color></size>";
                b.interactable = !here && ok;
            }
            ovLift.SetActive(true);
        }
        public void HideLift() { ovLift.SetActive(false); }

        public void ShowOver()
        {
            var S = game.S; string head;
            overHead.text = Data.Heads.TryGetValue(S.deathKind, out head) ? head : "발각";
            overReason.text = S.deathReason;
            string tip; overTip.text = Data.Tips.TryGetValue(S.deathKind, out tip) ? tip : "";
            ovOver.SetActive(true);
            hint.transform.parent.gameObject.SetActive(false);
        }
        public void ShowClear()
        {
            var S = game.S;
            stTime.text = Data.FmtTime(S.elapsed); stDeaths.text = game.deaths + "회"; stAlarms.text = S.alarms + "회";
            ovClear.SetActive(true);
            hint.transform.parent.gameObject.SetActive(false);
        }

        /* ================= 연출 ================= */
        public void Toast(string text) { toast.text = text; toastT = 0; toast.gameObject.SetActive(true); }
        public void Banner(string fid, string sub)
        {
            bannerTitle.text = "<color=#ff7d8c>" + fid + "</color> " + FloorGeo.All[fid].name; bannerSub.text = sub; bannerT = 0;
            bannerTitle.gameObject.SetActive(true); bannerSub.gameObject.SetActive(true);
        }
        public void Fade() { fadeT = 0; fadeImg.gameObject.SetActive(true); }
        public void Flash() { flashT = 0; flashImg.gameObject.SetActive(true); }

        void Update()
        {
            float dt = Time.deltaTime;
            if (toastT >= 0)
            {
                toastT += dt; float a = toastT < 0.22f ? toastT / 0.22f : toastT > 1.44f ? 1 - (toastT - 1.44f) / 0.36f : 1;
                toast.color = new Color(FG.r, FG.g, FG.b, Mathf.Clamp01(a));
                toast.rectTransform.anchoredPosition = new Vector2(0, toastT < 0.22f ? -8 * (1 - toastT / 0.22f) : 0);
                if (toastT > 1.8f) { toastT = -1; toast.gameObject.SetActive(false); }
            }
            if (bannerT >= 0)
            {
                bannerT += dt; float a = bannerT < 0.3f ? bannerT / 0.3f : bannerT > 2.25f ? 1 - (bannerT - 2.25f) / 0.75f : 1;
                bannerTitle.color = new Color(1, 1, 1, Mathf.Clamp01(a)); bannerSub.color = new Color(HAZARD.r, HAZARD.g, HAZARD.b, Mathf.Clamp01(a));
                if (bannerT > 3f) { bannerT = -1; bannerTitle.gameObject.SetActive(false); bannerSub.gameObject.SetActive(false); }
            }
            if (fadeT >= 0) { fadeT += dt; fadeImg.color = new Color(0, 0, 0, Mathf.Clamp01(1 - fadeT / 0.7f)); if (fadeT > 0.7f) { fadeT = -1; fadeImg.gameObject.SetActive(false); } }
            if (flashT >= 0) { flashT += dt; flashImg.color = new Color(1, 1, 1, Mathf.Clamp01(0.85f * (1 - flashT / 0.5f))); if (flashT > 0.5f) { flashT = -1; flashImg.gameObject.SetActive(false); } }
        }

        /* ================= 매 프레임 갱신 ================= */
        public void Refresh(bool force)
        {
            var S = game.S;
            if (S == null || !hudRoot.activeSelf) return;
            var mode = game.mode;
            if (mode != Mode.Play && mode != Mode.Dying && !force) { UpdateBubbles(); return; }
            var p = S.player; var f = Data.Forms[p.form];

            if (force || lastForm != p.form)
            {
                lastForm = p.form;
                formName.text = f.name;
                timerBox.gameObject.SetActive(f.type != FormType.Body);
                formIcon.sprite = Art.Get(f.type == FormType.Body ? "player_blob" : f.type == FormType.Human ? "char_" + p.form : "obj_" + p.form);
                formIcon.rectTransform.localRotation = Quaternion.Euler(0, 0, f.type == FormType.Human ? -90 : 0);
                if (f.type == FormType.Body) { var fr = Art.Frames("player_idle_down"); if (fr.Count > 0) formIcon.sprite = fr[0]; }
            }
            UpdateTimer(p, f, mode);
            string ck = string.Join(",", S.cards.ToArray());
            if (force || ck != lastCards)
            {
                lastCards = ck;
                for (int i = 0; i < Data.CardOrder.Length; i++)
                {
                    var c = Data.CardOrder[i]; bool on = S.cards.Contains(c);
                    var cc = Data.Cards[c].color;
                    cardBoxes[i].color = on ? cc : new Color(BG.r, BG.g, BG.b, 0.72f);
                    cardTexts[i].color = on ? Data.Hex("#11151a") : new Color(1, 1, 1, 0.28f);
                    cardStrips[i].color = on ? new Color(1, 1, 1, 0.55f) : new Color(cc.r, cc.g, cc.b, 0.35f);
                }
            }
            string ob = game.Objective();
            if (ob != lastObj) { lastObj = ob; objective.text = "목표  " + ob.Replace("<b>", "<color=#ff7d8c>").Replace("</b>", "</color>"); }
            if (S.alarm != null)
            {
                alarmBox.SetActive(true);
                alarmText.text = "경보 · " + (game.AnyLocked() ? "추적 중" : "수색 중") + " " + Mathf.CeilToInt(S.alarm.t);
                alarmBox.GetComponent<Image>().color = Mathf.Repeat(Time.time, 0.8f) < 0.4f ? ALARM : Data.Hex("#8f1f1a");
            }
            else alarmBox.SetActive(false);
            bool hidden = game.PlayerHidden();
            pillSeen.SetActive(mode == Mode.Play && S.anySee);
            pillOdd.SetActive(mode == Mode.Play && f.type == FormType.Object && !game.IsNatural(p));
            pillVent.SetActive(mode == Mode.Play && hidden);
            meta.text = "재시도 " + game.deaths + "회 · 경보 " + S.alarms + "회 · " + Data.FmtTime(S.elapsed);

            // 위험 테두리
            float danger = mode == Mode.Dying ? 1 : Mathf.Max(Mathf.Min(1, S.spot / Cfg.grace), S.Fl.npcs.Exists(n => n.alive && n.lockT > 0) ? 0.6f + 0.3f * Mathf.Sin(S.time * 10) : 0);
            dangerVig.color = new Color(1, 40 / 255f, 40 / 255f, 0.45f * danger);
            float k3 = 0;
            if (mode == Mode.Play && f.type != FormType.Body && p.timer < f.dur * 0.3f) k3 = (1 - p.timer / (f.dur * 0.3f)) * (0.6f + 0.4f * Mathf.Sin(S.time * 8));
            timerVig.color = new Color(1, 90 / 255f, 120 / 255f, 0.45f * k3);

            UpdateBubbles();
            if (game.showMini && ++miniTick % 4 == 0) DrawMini();

            if (mode != Mode.Play) return;
            bool note;
            string h = game.Hint(out note);
            string key = h + note;
            if (key != lastHint)
            {
                lastHint = key;
                var hg = hint.transform.parent.gameObject;
                hg.SetActive(!string.IsNullOrEmpty(h));
                hint.text = h;
                hint.alignment = note ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                hint.color = note ? Data.Hex("#f1e3c4") : FG;
                hintBg.color = note ? new Color(36 / 255f, 30 / 255f, 20 / 255f, 0.94f) : new Color(10 / 255f, 14 / 255f, 17 / 255f, 0.88f);
                float maxW = 1100;
                var settings = hint.GetGenerationSettings(new Vector2(maxW - 40, 0));
                float pw = Mathf.Min(maxW, hint.cachedTextGeneratorForLayout.GetPreferredWidth(h, settings) / hint.pixelsPerUnit + 44);
                settings = hint.GetGenerationSettings(new Vector2(pw - 40, 0));
                float ph = hint.cachedTextGeneratorForLayout.GetPreferredHeight(h, settings) / hint.pixelsPerUnit + 22;
                hintBg.rectTransform.sizeDelta = new Vector2(pw, ph);
            }
        }

        // 변신 해제까지 남은 시간: 30% 아래로 떨어지면 빨갛게 맥박 치고 흔들리며 심장 소리, 5초부터는 화면 가운데 큰 숫자
        void UpdateTimer(Player p, FormDef f, Mode mode)
        {
            bool disguised = f.type != FormType.Body && mode == Mode.Play;
            if (!disguised) { countdown.color = new Color(1, 0.35f, 0.42f, 0); timerBox.localScale = Vector3.one; return; }
            float frac = Mathf.Clamp01(p.timer / f.dur), time = Time.time;
            bool urgent = frac < 0.3f;
            timerFill.fillAmount = frac;
            timerFill.color = urgent ? Color.Lerp(ALARM, Color.white, Mathf.PingPong(time * 4, 0.35f)) : Color.Lerp(Data.Hex("#b48cf0"), FLESH, 1 - frac);
            timerNum.text = Mathf.CeilToInt(Mathf.Max(0, p.timer)) + "초";
            if (urgent)
            {
                float beat = 1 - Mathf.Clamp01(p.timer / (f.dur * 0.3f));                // 0 → 1 (점점 빨라짐)
                float pulse = 1 + 0.08f * Mathf.Abs(Mathf.Sin(time * (6 + beat * 10)));
                timerBox.localScale = Vector3.one * pulse;
                timerBox.anchoredPosition = new Vector2(60, 34) + new Vector2(Mathf.Sin(time * 47), Mathf.Cos(time * 39)) * (2 + beat * 4);
                timerNum.fontSize = 36 + Mathf.RoundToInt(beat * 10);
                heartT -= Time.deltaTime;
                if (heartT <= 0) { heartT = Mathf.Lerp(1.0f, 0.35f, beat); if (game.sfx != null) game.sfx.Play("heart"); }
            }
            else { timerBox.localScale = Vector3.one; timerBox.anchoredPosition = new Vector2(60, 34); timerNum.fontSize = 36; }
            // 5, 4, 3, 2, 1
            if (p.timer <= 5.0f && p.timer > 0)
            {
                int n = Mathf.CeilToInt(p.timer); float part = n - p.timer;               // 0 → 1 (숫자가 바뀐 뒤 흐른 시간)
                if (countdown.text != n.ToString()) { countdown.text = n.ToString(); if (game.sfx != null) game.sfx.Play("tick"); }
                countdown.color = new Color(1, 0.35f, 0.42f, 0.55f * (1 - part));
                countdown.rectTransform.localScale = Vector3.one * (1.25f - 0.25f * part);
            }
            else countdown.color = new Color(1, 0.35f, 0.42f, 0);
        }

        // 말풍선과 머리 위 ! ? 표시
        void UpdateBubbles()
        {
            var S = game.S; if (S == null) return;
            if (cam == null) cam = Camera.main;
            int bi = 0, mi = 0;
            foreach (var n in S.Fl.npcs)
            {
                if (!n.alive) continue;
                if (game.view == null || !game.view.InView(n.x, n.y, 0)) continue;
                if ((game.mode == Mode.Play || game.mode == Mode.Dying || game.mode == Mode.Pause) && !game.PlayerCanSee(n.x, n.y)) continue;   // 안 보이는 사람은 말풍선도 없음
                Vector2 bubblePos, markPos;   // 머리 위 위치 (2D/2.5D 화면이 각자 계산)
                RectTransformUtility.ScreenPointToLocalPointInRectangle(bubbleLayer, game.view.HeadScreen(n.x, n.y, 0.55f), null, out bubblePos);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(bubbleLayer, game.view.HeadScreen(n.x, n.y, 0.3f), null, out markPos);
                if (n.bubble != null)
                {
                    while (bubbleBgs.Count <= bi)
                    {
                        var rt = R(bubbleLayer, "Bubble", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0), Vector2.zero, new Vector2(200, 34));
                        bubbleBgs.Add(Img(rt, new Color(236 / 255f, 240 / 255f, 238 / 255f, 0.95f)));
                        var t = Txt(rt, "T", "", 19, Data.Hex("#1a2024"), TextAnchor.MiddleCenter, true);
                        t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.sizeDelta = Vector2.zero; t.horizontalOverflow = HorizontalWrapMode.Overflow;
                        bubbleTexts.Add(t);
                    }
                    var bg = bubbleBgs[bi]; var tx = bubbleTexts[bi]; bi++;
                    bg.gameObject.SetActive(true);
                    if (tx.text != n.bubble.text) tx.text = n.bubble.text;
                    float w = Mathf.Min(420, tx.preferredWidth + 26);
                    bg.rectTransform.sizeDelta = new Vector2(w, 34);
                    bg.rectTransform.anchoredPosition = bubblePos;
                    float a = Mathf.Min(1, n.bubble.t * 3);
                    bg.color = new Color(236 / 255f, 240 / 255f, 238 / 255f, 0.95f * a); tx.color = new Color(26 / 255f, 32 / 255f, 36 / 255f, a);
                }
                string mark = ""; Color col = HAZARD; int size = 40;
                if (n.lockT > 0 || (S.spotNpc == n && S.spot > 0)) { mark = n.lockT > 0 ? "!" : "?"; col = n.lockT > 0 ? ALARM : HAZARD; size = 54; }
                else if (n.state == NpcState.Chase || n.state == NpcState.Inspect) mark = "?";
                if (mark != "" && game.mode != Mode.Title && game.mode != Mode.Intro)
                {
                    while (marks.Count <= mi) { var t = Txt(bubbleLayer, "Mark", "", 40, HAZARD, TextAnchor.LowerCenter, true); Shadow(t); t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); t.rectTransform.pivot = new Vector2(0.5f, 0); t.rectTransform.sizeDelta = new Vector2(80, 70); marks.Add(t); }
                    var m = marks[mi++]; m.gameObject.SetActive(true);
                    m.text = mark; m.color = col; m.fontSize = size;
                    m.rectTransform.anchoredPosition = markPos;
                }
            }
            for (int i = bi; i < bubbleBgs.Count; i++) bubbleBgs[i].gameObject.SetActive(false);
            for (int i = mi; i < marks.Count; i++) marks[i].gameObject.SetActive(false);
        }

        /* ================= 미니맵 ================= */
        const int MS = 3;
        void BuildMini()
        {
            var G = game.G;
            int w = G.w * MS, h = G.h * MS;
            miniTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            miniBase = new Color32[w * h]; miniPx = new Color32[w * h];
            var bg = (Color32)Data.Hex("#07090b");
            for (int i = 0; i < miniBase.Length; i++) miniBase[i] = bg;
            var colMap = new Dictionary<char, Color32>
            {
                { '.', Data.Hex("#2c3841") }, { 'G', Data.Hex("#5f8a99") }, { 'v', Data.Hex("#4b3f6b") }, { 'E', Data.Hex("#7fe0b0") }, { '>', Data.Hex("#7fe0b0") },
                { '<', Data.Hex("#7fe0b0") }, { 'H', Data.Hex("#6b4a52") }, { 'N', Data.Hex("#9c7a52") },
            };
            var other = (Color32)Data.Hex("#1c242a");
            for (int y = 0; y < G.h; y++) for (int x = 0; x < G.w; x++)
            {
                char ch = G.grid[y, x]; if (ch == '#') continue;
                Color32 c; if (!colMap.TryGetValue(ch, out c)) c = other;
                MiniRect(miniBase, x * MS, y * MS, MS, MS, c);
            }
            miniTex.SetPixels32(miniBase); miniTex.Apply();
            mini.texture = miniTex;
            float scale = Mathf.Min(300f / w, 200f / h);
            mini.rectTransform.sizeDelta = new Vector2(w * scale, h * scale);
        }
        void MiniRect(Color32[] buf, int x, int y, int w, int h, Color32 c)
        {
            int W = game.G.w * MS, H = game.G.h * MS;
            for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++)
                if (xx >= 0 && yy >= 0 && xx < W && yy < H) buf[(H - 1 - yy) * W + xx] = c;
        }
        // 지도: 주인공이 본 칸만 보인다 (피드백 2). 사람은 표시하지 않음.
        void DrawMini()
        {
            if (miniTex == null) return;
            var S = game.S; var G = game.G; var F = S.Fl;
            var ex = game.Explored(S.floorId);
            var dark = (Color32)Data.Hex("#07090b");
            int W = G.w * MS, H = G.h * MS;
            for (int ty = 0; ty < G.h; ty++) for (int tx = 0; tx < G.w; tx++)
            {
                bool seen = ex[ty * G.w + tx];
                for (int yy = 0; yy < MS; yy++)
                {
                    int row = (H - 1 - (ty * MS + yy)) * W;
                    for (int xx = 0; xx < MS; xx++) { int i = row + tx * MS + xx; miniPx[i] = seen ? miniBase[i] : dark; }
                }
            }
            System.Func<int, int, bool> seenAt = (x, y) => x >= 0 && y >= 0 && x < G.w && y < G.h && ex[y * G.w + x];
            for (int i = 0; i < G.def.doors.Length; i++) { var d = G.def.doors[i]; if (seenAt(d.x, d.y)) MiniRect(miniPx, d.x * MS - 1, d.y * MS - 1, MS + 2, MS + 2, F.doors[i].open ? (Color32)EXIT : (Color32)Data.Cards[d.req].color); }
            foreach (var tr in G.def.traps)
            {
                if (S.trapsOff.Contains(tr.id)) continue;
                var c = tr.kind == "gas" ? new Color32(150, 230, 90, 180) : new Color32(255, 60, 60, 190);
                foreach (var r in tr.rects) if (seenAt(r.x, r.y)) MiniRect(miniPx, r.x * MS, r.y * MS, r.w * MS, r.h * MS, c);
            }
            foreach (var c in G.def.consoles) if (seenAt(c.x, c.y)) MiniRect(miniPx, c.x * MS - 1, c.y * MS - 1, MS + 2, MS + 2, (c.heli ? S.heli != null : S.trapsOff.Contains(c.trap)) ? (Color32)EXIT : (Color32)ALARM);
            if (Mathf.Sin(S.time * 8) > -0.4f) MiniRect(miniPx, Mathf.RoundToInt(S.player.x * MS) - 2, Mathf.RoundToInt(S.player.y * MS) - 2, 5, 5, (Color32)FLESH);
            miniTex.SetPixels32(miniPx); miniTex.Apply();
        }
    }
}
