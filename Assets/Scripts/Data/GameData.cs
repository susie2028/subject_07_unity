// 게임 수치와 텍스트. 프로토타입(index.html)의 CFG / CHARS / FORMS / CARDS 표를 그대로 옮겼다.
// 밸런스 조정은 이 파일의 숫자만 바꾸면 된다.
using System.Collections.Generic;
using UnityEngine;

namespace Specimen07
{
    public static class Cfg
    {
        public const float eatTime = 0.8f, eatRange = 1.15f, grace = 0.3f, stare = 2.0f;
        public const float moveEps = 0.05f, turnRate = 5.0f, inspectCooldown = 6f, playerR = 0.3f;
        public const float alarmTime = 20f, aimTime = 1.1f, catchDist = 0.8f, arriveGrace = 1.2f;
        public const float missingDelay = 20f, respawn = 45f, heliTime = 15f, waryStep = 0.75f;
        public const float patrolK = 0.32f, chaseK = 0.42f;

        // 화면에 보이는 칸 수 (작을수록 확대)
        public const float VTW = 22f, VTH = 12.375f;
        // 스프라이트 해상도: 1칸 = 32픽셀
        public const int PPU = 32;
    }

    public enum ViewShape { Cone, Half, Circle }

    public class CharDef
    {
        public string id, name, freq, chaser; // chaser: null | "catch" | "shoot"
        public int view, speed, dur;
        public ViewShape shape;
        public float wait;
        public string[] access;
        public CharDef(string id, string name, int view, ViewShape shape, int speed, string freq, float wait, string[] access, int dur, string chaser)
        { this.id = id; this.name = name; this.view = view; this.shape = shape; this.speed = speed; this.freq = freq; this.wait = wait; this.access = access; this.dur = dur; this.chaser = chaser; }
        public bool Has(string card) { return System.Array.IndexOf(access, card) >= 0; }
    }

    public enum FormType { Body, Object, Human }

    public class FormDef
    {
        public string id, name, zone; // zone: "any" | "wall" (물건만)
        public FormType type;
        public float speed, dur, seenSpeed;
    }

    public class CardDef
    {
        public string id, name, from;
        public Color color;
    }

    public static class Data
    {
        public static float Rad(float d) { return d * Mathf.PI / 180f; }

        public static readonly Dictionary<string, CharDef> Chars = new Dictionary<string, CharDef>
        {
            { "cleaner",    new CharDef("cleaner",    "늙은 청소부", 3, ViewShape.Cone,   4,  "낮음",     1.8f,  new[] { "clean" }, 22, null) },
            { "researcher", new CharDef("researcher", "연구원",      4, ViewShape.Cone,   4,  "보통",     1.0f,  new[] { "clean", "lab" }, 25, null) },
            { "guard",      new CharDef("guard",      "경비원",      5, ViewShape.Half,   6,  "높음",     0.6f,  new[] { "clean", "lab", "sec" }, 25, "catch") },
            { "senior",     new CharDef("senior",     "고위 연구원", 6, ViewShape.Half,   8,  "보통",     1.0f,  new[] { "clean", "lab", "sec", "senior" }, 28, null) },
            { "soldier",    new CharDef("soldier",    "군인",        8, ViewShape.Circle, 10, "매우 높음", 0.35f, new[] { "clean", "lab", "sec", "mil" }, 30, "shoot") },
            { "director",   new CharDef("director",   "연구소장",    4, ViewShape.Cone,   4,  "매우 낮음", 2.6f,  new[] { "clean", "lab", "sec", "senior", "mil", "dir" }, 40, null) },
        };
        public static readonly string[] CharOrder = { "cleaner", "researcher", "guard", "senior", "soldier", "director" };

        public static float ShapeHalf(ViewShape s) { return s == ViewShape.Cone ? Rad(40) : s == ViewShape.Half ? Rad(90) : Mathf.PI; }
        public static string ShapeLabel(ViewShape s) { return s == ViewShape.Cone ? "방사형" : s == ViewShape.Half ? "반원" : "원"; }

        public static readonly Dictionary<string, CardDef> Cards = new Dictionary<string, CardDef>
        {
            { "clean",  new CardDef { id = "clean",  name = "청소", from = "늙은 청소부", color = Hex("#a9b4bb") } },
            { "lab",    new CardDef { id = "lab",    name = "연구", from = "연구원",      color = Hex("#7fb8e6") } },
            { "sec",    new CardDef { id = "sec",    name = "보안", from = "경비원",      color = Hex("#f3b04a") } },
            { "senior", new CardDef { id = "senior", name = "고위", from = "고위 연구원", color = Hex("#b48cf0") } },
            { "mil",    new CardDef { id = "mil",    name = "군",   from = "군인",        color = Hex("#86c46e") } },
            { "dir",    new CardDef { id = "dir",    name = "소장", from = "연구소장",    color = Hex("#ff5148") } },
        };
        public static readonly string[] CardOrder = { "clean", "lab", "sec", "senior", "mil", "dir" };

        public static readonly Dictionary<string, FormDef> Forms = BuildForms();
        static Dictionary<string, FormDef> BuildForms()
        {
            var d = new Dictionary<string, FormDef>
            {
                { "body",  new FormDef { id = "body",  name = "본모습",    type = FormType.Body,   speed = 3.5f, dur = float.PositiveInfinity } },
                { "chair", new FormDef { id = "chair", name = "의자",      type = FormType.Object, speed = 2.5f, dur = 25, seenSpeed = 0,  zone = "any" } },
                { "trash", new FormDef { id = "trash", name = "쓰레기통",  type = FormType.Object, speed = 2.5f, dur = 20, seenSpeed = 0,  zone = "wall" } },
                { "cart",  new FormDef { id = "cart",  name = "바퀴 카트", type = FormType.Object, speed = 3.0f, dur = 18, seenSpeed = 1f, zone = "wall" } },
            };
            foreach (var kv in Chars)
            {
                var c = kv.Value;
                d[c.id] = new FormDef { id = c.id, name = c.name, type = FormType.Human, speed = Mathf.Round((2.4f + c.speed * 0.08f) * 100f) / 100f, dur = c.dur };
            }
            return d;
        }

        public static readonly string[] FloorOrder = { "B1", "1F", "2F", "3F", "4F", "5F" };
        // 엘리베이터: [층, 필요한 카드]
        public static readonly string[][] LiftFloors = { new[] { "5F", "dir" }, new[] { "4F", "senior" }, new[] { "3F", "sec" }, new[] { "2F", "lab" }, new[] { "1F", null } };

        public static readonly Dictionary<string, string[]> Chatter = new Dictionary<string, string[]>
        {
            { "cleaner",    new[] { "허리야…", "바닥이 또 끈적하네…", "요즘 의자가 자꾸 없어져.", "누가 자판기에 동전 두고 갔네.", "격리동은 청소 안 해도 된대서 좋아." } },
            { "researcher", new[] { "커피머신 또 없어졌대.", "표본 07 오늘 밥 줬나?", "야근 수당은 언제 나오지…", "소장님 또 회의래.", "보고서 마감이 내일이라니." } },
            { "guard",      new[] { "이상 무.", "교대 시간 아직이야?", "CCTV 3번 또 먹통이네.", "경보 훈련 좀 그만했으면." } },
            { "senior",     new[] { "표본 07 데이터가 흥미롭군.", "레이저 그리드 또 오작동이야?", "소장님께 보고 드려야겠어.", "처분하기엔 아까운 표본인데." } },
            { "soldier",    new[] { "구역 이상 없음.", "…", "헬기 정비 끝났나.", "경계 유지." } },
            { "director",   new[] { "처분은 예정대로 09시에.", "헬기 대기시켜.", "이 연구소는 내 거야.", "보고서는 내일 아침까지." } },
        };
        public static readonly string[] InspectStart = { "음?", "저게 원래 저기 있었나?" };
        public static readonly string[] InspectEnd = { "…기분 탓인가.", "누가 옮겨 놨나 보지." };
        public static readonly Dictionary<string, string[]> AlarmLines = new Dictionary<string, string[]>
        {
            { "guard",   new[] { "저기다!", "멈춰!", "표본 발견!" } },
            { "soldier", new[] { "표적 확인!", "조준!", "사격 허가!" } },
            { "other",   new[] { "괴, 괴물이다!", "경비! 경비!", "저게 뭐야!" } },
        };

        public static readonly string[] IntroLines =
        {
            "실험 기록 · 제3연구소 B1 격리동",
            "표본 07 — 섭취한 대상의 형태와 소지품을 일시적으로 복제함.",
            "          유지 시간은 대상에 따라 다름.",
            "외부 반출 경로 — 옥상 헬기(소장 전용)뿐.",
            "처분 예정: 내일 09:00",
        };

        public static readonly Dictionary<string, string> Tips = new Dictionary<string, string>
        {
            { "body", "본모습은 어떤 시야에도 들어가면 안 된다. 바닥의 부채꼴(군인은 원)이 시야다. 시야가 지나간 뒤에 움직이자." },
            { "moved", "물건 모습일 때는 누가 보고 있으면 멈춰야 한다. 바퀴 카트만 보는 앞에서 천천히 굴러갈 수 있다." },
            { "eating", "먹는 데 0.8초가 걸린다. 대상 말고 다른 사람의 시야가 비었는지 확인하자. 변장한 상태라면 대상의 눈앞에서도 덮칠 수 있다." },
            { "restricted", "사람 모습이라도 그 인물이 있을 이유가 없는 구역이 있다. 들킨 모습은 경보 동안 계속 수배된다." },
            { "laser", "레이저 그리드는 B1 기계실의 차단 장치로 끌 수 있다. 실험실 B의 환풍구로 우회하는 길도 있다." },
            { "gas", "가스 함정은 2F 보안 통제실에서 해제한다. 고위 연구원 카드가 필요하다." },
            { "caught", "경비원은 본모습이나 들킨 모습을 보면 쫓아온다. 시야를 끊고, 안 보이는 곳에서 다른 것을 먹어 모습을 바꾸자. 환풍구도 숨을 곳이다." },
            { "shot", "군인은 사방을 본다. 보이면 멈춰 서서 조준한다 — 조준선이 다 차기 전에 벽 뒤로 숨어야 한다." },
        };
        public static readonly Dictionary<string, string> Heads = new Dictionary<string, string>
        { { "shot", "사살" }, { "caught", "포획" }, { "laser", "감전" }, { "gas", "질식" } };

        public static Color Hex(string hex)
        {
            Color c; ColorUtility.TryParseHtmlString(hex, out c); return c;
        }

        // 한국어 조사: josa("의자", "이", "가") → "의자가"
        public static string Josa(string word, string withFinal, string without)
        {
            if (string.IsNullOrEmpty(word)) return word;
            int c = word[word.Length - 1];
            if (c < 0xAC00 || c > 0xD7A3) return word + without;
            return word + (((c - 0xAC00) % 28) != 0 ? withFinal : without);
        }

        public static string FmtTime(float s)
        {
            int m = Mathf.FloorToInt(s / 60f), sec = Mathf.FloorToInt(s % 60f);
            return m + ":" + sec.ToString("00");
        }
    }

    /* ===== 층 정의용 자료형 (FloorDefs.cs에서 사용) ===== */
    public struct RectI { public int x, y, w, h; public RectI(int x, int y, int w, int h) { this.x = x; this.y = y; this.w = w; this.h = h; } }

    public class RoomDef
    {
        public string name, kind; public int x, y, w, h; public string[] allow; public bool label;
        public RoomDef(string name, int x, int y, int w, int h, string kind, string[] allow, bool label)
        { this.name = name; this.x = x; this.y = y; this.w = w; this.h = h; this.kind = kind; this.allow = allow; this.label = label; }
    }
    public class FillDef { public char ch; public int x, y, w, h; public FillDef(char ch, int x, int y, int w, int h) { this.ch = ch; this.x = x; this.y = y; this.w = w; this.h = h; } }
    public class DoorDef { public int x, y; public string req, label; public DoorDef(int x, int y, string req, string label) { this.x = x; this.y = y; this.req = req; this.label = label; } }
    public class ConsoleDef
    {
        public int x, y; public string req, trap, label; public bool heli;
        public ConsoleDef(int x, int y, string req, string trap, bool heli, string label) { this.x = x; this.y = y; this.req = req; this.trap = trap; this.heli = heli; this.label = label; }
    }
    public class NoticeDef { public int x, y; public string text; public NoticeDef(int x, int y, string text) { this.x = x; this.y = y; this.text = text; } }
    public class StairsDef
    {
        public int x, y, w, h; public string to; public char dir;
        public StairsDef(int x, int y, int w, int h, string to, char dir) { this.x = x; this.y = y; this.w = w; this.h = h; this.to = to; this.dir = dir; }
    }
    public class TrapDef { public string id, kind, label; public RectI[] rects; public TrapDef(string id, string kind, string label, RectI[] rects) { this.id = id; this.kind = kind; this.label = label; this.rects = rects; } }
    public class ObjDef { public string form; public float x, y, rot; public ObjDef(string form, float x, float y, float rot) { this.form = form; this.x = x; this.y = y; this.rot = rot; } }
    public class RouteWp { public float x, y, wait, look; public RouteWp(float x, float y, float wait, float look) { this.x = x; this.y = y; this.wait = wait; this.look = look; } }
    public class NpcDef
    {
        public string id, ch, pal, label; public float x, y, face; public RouteWp[] route;
        public NpcDef(string id, string ch, string pal, string label, float x, float y, float face, RouteWp[] route)
        { this.id = id; this.ch = ch; this.pal = pal; this.label = label; this.x = x; this.y = y; this.face = face; this.route = route; }
    }
    public class LightDef
    {
        public float x, y, r, i; public int flick; public Color tint;
        public LightDef(float x, float y, float r, float i, int flick, Color tint) { this.x = x; this.y = y; this.r = r; this.i = i; this.flick = flick; this.tint = tint; }
    }
    public class ReinforceDef { public string ch; public int n; public ReinforceDef(string ch, int n) { this.ch = ch; this.n = n; } }

    public class FloorDef
    {
        public string id, name, sub;
        public int w, h; public bool lethal; public float dark;
        public RoomDef[] rooms; public Vector2Int[] gaps; public RectI[] glass, vents; public FillDef[] fill;
        public DoorDef[] doors; public ConsoleDef[] consoles; public NoticeDef[] notices; public StairsDef[] stairs;
        public Vector2? arriveStart, arriveStairs, arriveLift, tank;
        public Vector2Int[] lift; public RectI? pad;
        public TrapDef[] traps; public Vector2[] spawns; public ReinforceDef[] reinforce; public LightDef[] extraLights;
        public ObjDef[] objects; public NpcDef[] npcs;

        public Vector2 Arrive(string via)
        {
            if (via == "start" && arriveStart.HasValue) return arriveStart.Value;
            if (via == "stairs" && arriveStairs.HasValue) return arriveStairs.Value;
            if (via == "lift" && arriveLift.HasValue) return arriveLift.Value;
            return arriveLift ?? arriveStairs ?? arriveStart ?? new Vector2(1.5f, 1.5f);
        }
    }
}
