// ★ 아트 교체의 핵심 ★
// 모든 그림은 Art.Get("이름")으로 가져온다.
//   1) Assets/Resources/Art/이름.png 가 있으면 그 스프라이트를 쓴다.
//   2) 없으면 프로토타입과 비슷한 임시 그림을 코드로 그려서 쓴다.
// 즉, 같은 이름의 PNG를 Resources/Art 폴더에 넣기만 하면 게임 안의 그림이 바뀐다.
// 이름 목록은 Assets/Resources/Art/_스프라이트_이름표.txt 참고.
//
// 캐릭터는 4방향 그림도 지원한다: char_guard_right / _down / _left / _up 네 장이 다 있으면
// 회전 대신 방향에 맞는 그림을 보여준다. 한 장(char_guard)만 있으면 오른쪽을 보는 그림으로 보고 회전시킨다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Specimen07
{
    public static class Art
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite[]> dirCache = new Dictionary<string, Sprite[]>();
        public const int PPU = Cfg.PPU;

        // 에디터에서 PNG를 새로 넣거나 바꿨을 때 다시 불러오도록 비운다
        public static void ClearCache() { cache.Clear(); dirCache.Clear(); has.Clear(); frames.Clear(); }

        // 애니메이션 프레임: 이름_0, 이름_1, … 이 있는 만큼 (없으면 빈 목록)
        static readonly Dictionary<string, List<Sprite>> frames = new Dictionary<string, List<Sprite>>();
        public static List<Sprite> Frames(string key)
        {
            List<Sprite> l;
            if (frames.TryGetValue(key, out l)) return l;
            l = new List<Sprite>();
            for (int i = 0; i < 64; i++) { var s = Resources.Load<Sprite>("Art/" + key + "_" + i); if (s == null) break; l.Add(s); }
            frames[key] = l;
            return l;
        }

        // Resources/Art에 그 이름의 그림이 있는지 (변형 타일·테두리 조각용)
        static readonly Dictionary<string, bool> has = new Dictionary<string, bool>();
        public static bool Has(string key)
        {
            bool b;
            if (!has.TryGetValue(key, out b)) { b = Resources.Load<Sprite>("Art/" + key) != null; has[key] = b; }
            return b;
        }

        // Resources/Art에 그림이 있을 때만 돌려준다 (임시 그림은 만들지 않음)
        public static Sprite TryLoad(string key) { return Resources.Load<Sprite>("Art/" + key); }

        public static Sprite Get(string key)
        {
            Sprite s;
            if (cache.TryGetValue(key, out s)) return s;
            s = Resources.Load<Sprite>("Art/" + key);
            if (s == null) s = Generate(key);
            cache[key] = s;
            return s;
        }

        /* ===== 층·방마다 다른 바닥 =====
           원래 바닥 그림(Resources/Art/tile_floor_<방>) 위에 방 종류별 무늬를 얹고, 층마다 색을 살짝 바꾼다.
           원래 그림을 바꾸면 그대로 반영된다 (무늬만 위에 덧칠). */
        public static readonly Dictionary<string, Color> FloorTint = new Dictionary<string, Color>
        {
            { "B1", new Color(0.92f, 1.12f, 1.04f) }, { "1F", new Color(1.04f, 1.04f, 1.14f) }, { "2F", new Color(1.14f, 1.02f, 0.9f) },
            { "3F", new Color(0.9f, 1.12f, 1.2f) }, { "4F", new Color(1.2f, 0.95f, 1.0f) }, { "5F", Color.white },
        };
        static readonly Dictionary<string, Sprite> floorCache = new Dictionary<string, Sprite>();
        public static Sprite FloorTile(string baseKey, string kind, string floorId)
        {
            string ck = baseKey + "@" + floorId;
            Sprite s;
            if (floorCache.TryGetValue(ck, out s)) return s;
            var src = Get(baseKey);
            const int N = 32;
            var outPx = ReadScaled(src, N);
            if (outPx == null) { floorCache[ck] = src; return src; }
            Color tint; if (!FloorTint.TryGetValue(floorId, out tint)) tint = Color.white;
            var pat = FloorPattern(kind, N);
            for (int i = 0; i < outPx.Length; i++)
            {
                var c = outPx[i];
                c = new Color(Mathf.Clamp01(c.r * tint.r), Mathf.Clamp01(c.g * tint.g), Mathf.Clamp01(c.b * tint.b), c.a);
                var o = pat[i];
                if (o.a > 0) c = new Color(Mathf.Lerp(c.r, o.r, o.a), Mathf.Lerp(c.g, o.g, o.a), Mathf.Lerp(c.b, o.b, o.a), c.a);
                outPx[i] = c;
            }
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = ck };
            t.SetPixels(outPx); t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N); s.name = ck;
            floorCache[ck] = s;
            return s;
        }
        // 스프라이트 픽셀을 N×N으로 (읽기 금지 텍스처는 렌더 텍스처를 거쳐 읽는다)
        static Color[] ReadScaled(Sprite sp, int N)
        {
            if (sp == null) return null;
            var tex = sp.texture; var r = sp.textureRect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            Color[] px = null;
            try { if (tex.isReadable) px = tex.GetPixels(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y), w, h); } catch (System.Exception) { px = null; }
            if (px == null)
            {
                var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var prev = RenderTexture.active;
                Graphics.Blit(tex, rt); RenderTexture.active = rt;
                var t2 = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t2.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0); t2.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                px = t2.GetPixels(); UnityEngine.Object.Destroy(t2);
            }
            var o = new Color[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) o[y * N + x] = px[(y * h / N) * w + (x * w / N)];
            return o;
        }
        // 방 종류별 무늬 (알파 = 덧칠 세기). Painter와 같은 행 순서(아래가 0)로 돌려준다
        static Color[] FloorPattern(string kind, int N)
        {
            var q = new Painter(N, N);
            switch (kind)
            {
                case "dir":     // 짙은 원목 마루: 가로 널 + 엇갈린 이음매
                    q.Rect(0, 0, N, N, HA("#5a3324", 0.55f));
                    for (int y = 0; y < N; y += 8) { q.Rect(0, y, N, 1, HA("#24130c", 0.55f)); q.Rect(((y / 8) % 2) * 16 + 6, y, 1, 8, HA("#24130c", 0.4f)); q.Rect(0, y + 2, N, 1, HA("#8a5a3c", 0.18f)); }
                    break;
                case "office":  // 카펫 타일 체크
                    for (int y = 0; y < N; y += 16) for (int x = 0; x < N; x += 16) if (((x + y) / 16) % 2 == 0) q.Rect(x, y, 16, 16, HA("#3c4a7a", 0.22f));
                    q.StrokeRect(0, 0, N, N, HA("#0c0f22", 0.25f));
                    break;
                case "lab": case "cold":   // 밝은 위생 타일 (줄눈)
                    q.Rect(0, 0, N, N, HA(kind == "cold" ? "#9fd8ff" : "#c8d4ff", kind == "cold" ? 0.14f : 0.1f));
                    q.Rect(0, 0, N, 1, HA("#e8f0ff", 0.18f)); q.Rect(0, 0, 1, N, HA("#e8f0ff", 0.18f)); q.Rect(0, 16, N, 1, HA("#e8f0ff", 0.12f)); q.Rect(16, 0, 1, N, HA("#e8f0ff", 0.12f));
                    break;
                case "rest":    // 따뜻한 마름모 리놀륨
                    q.Rect(0, 0, N, N, HA("#7a4a3a", 0.18f));
                    for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) if ((Mathf.Abs(x - 16) + Mathf.Abs(y - 16)) == 12) q.Rect(x, y, 1, 1, HA("#e0a080", 0.16f));
                    break;
                case "store": case "mech":   // 콘크리트 얼룩 + (기계실) 체크 철판
                    q.Rect(0, 0, N, N, HA("#4a4640", 0.18f));
                    for (int i = 0; i < 9; i++) q.Rect((i * 11 + 5) % N, (i * 7 + 3) % N, 2, 1, HA("#0d0d0c", 0.25f));
                    if (kind == "mech") for (int y = 2; y < N; y += 6) for (int x = (y / 6 % 2) * 3 + 1; x < N; x += 6) q.Rect(x, y, 2, 1, HA("#9aa4b0", 0.22f));
                    break;
                case "iso":     // 배수구 격자
                    q.Rect(0, 0, N, N, HA("#2c5048", 0.16f));
                    for (int y = 4; y < N; y += 8) for (int x = 4; x < N; x += 8) q.Rect(x, y, 2, 2, HA("#071210", 0.4f));
                    break;
                case "sec":     // 어두운 철판 + 노란 테두리 선
                    q.Rect(0, 0, N, N, HA("#20242e", 0.25f));
                    q.Rect(0, 0, N, 1, HA("#d9b23a", 0.14f)); q.Rect(0, 0, 1, N, HA("#d9b23a", 0.14f));
                    break;
                case "wing":    // 보라빛 헤링본
                    for (int y = 0; y < N; y += 4) for (int x = 0; x < N; x += 8) q.Rect(x + ((y / 4) % 2) * 4, y, 4, 1, HA("#b48cf0", 0.12f));
                    break;
                case "hall":    // 큰 타일
                    q.StrokeRect(0, 0, N, N, HA("#9aa8ff", 0.08f));
                    break;
                case "exit": case "lift":  // 바닥 안내선
                    q.Rect(0, 14, N, 3, HA("#7fe0b0", 0.12f));
                    break;
            }
            return q.Pixels;
        }

        // 4방향 스프라이트 (오른쪽, 아래, 왼쪽, 위). 없으면 null.
        public static Sprite[] Dir(string key)
        {
            Sprite[] arr;
            if (dirCache.TryGetValue(key, out arr)) return arr;
            string[] suf = { "_right", "_down", "_left", "_up" };
            arr = new Sprite[4];
            for (int i = 0; i < 4; i++) { arr[i] = Resources.Load<Sprite>("Art/" + key + suf[i]); if (arr[i] == null) { arr = null; break; } }
            dirCache[key] = arr;
            return arr;
        }

        // 게임 각도(0=오른쪽, π/2=아래) → 0..3
        public static int DirIndex(float ang)
        {
            float a = Mathf.Repeat(ang + Mathf.PI / 4, Mathf.PI * 2);
            return Mathf.FloorToInt(a / (Mathf.PI / 2)) % 4;
        }

        /* ================= 임시 그림 생성 ================= */
        static readonly Dictionary<string, string> FloorKind = new Dictionary<string, string>
        {
            { "iso", "#1a2524" }, { "lab", "#1a1f27" }, { "store", "#221f1b" }, { "hall", "#1c2127" }, { "rest", "#25201d" }, { "mech", "#1f1d24" },
            { "exit", "#172420" }, { "office", "#1e2226" }, { "sec", "#202329" }, { "lift", "#18221f" }, { "cold", "#18232a" }, { "wing", "#211b26" },
            { "dir", "#2a1c1c" }, { "roof", "#1a1d1f" },
        };

        static Color H(string hex) { return Data.Hex(hex); }
        static Color HA(string hex, float a) { var c = Data.Hex(hex); c.a = a; return c; }

        static Sprite Generate(string key)
        {
            if (key.StartsWith("tile_floor_"))
            {
                string kind = key.Substring(11); string hex;
                if (!FloorKind.TryGetValue(kind, out hex)) hex = "#1c2127";
                var p = new Painter(32, 32);
                p.Rect(0, 0, 32, 32, H(hex));
                p.StrokeRect(0, 0, 32, 32, new Color(1, 1, 1, 0.035f));
                return p.ToSprite(key);
            }
            if (key.StartsWith("char_")) return Human(key, key.Substring(5));
            if (key.StartsWith("stand_")) return Standing(key);
            if (key.StartsWith("wall_face")) { var f = new Painter(16, 16); f.Rect(0, 0, 16, 11, H("#393d7d")); f.Rect(0, 11, 16, 2, H("#1c1e4c")); return f.ToSprite(key, 16); }
            Painter q;
            switch (key)
            {
                case "tile_wall":
                    q = new Painter(32, 32); q.Rect(0, 0, 32, 32, H("#2b343c")); q.Rect(0, 0, 32, 5, H("#36414a")); return q.ToSprite(key);
                case "tile_wall_edge": // 아래가 바닥인 벽: 그림자 띠
                    q = new Painter(32, 32); q.Rect(0, 0, 32, 32, H("#2b343c")); q.Rect(0, 0, 32, 5, H("#36414a")); q.Rect(0, 25, 32, 7, H("#20282e")); return q.ToSprite(key);
                case "tile_vent":
                    q = new Painter(32, 32); q.Rect(0, 0, 32, 32, H("#2b343c")); q.Rect(0, 0, 32, 5, H("#36414a"));
                    q.Rect(3, 3, 26, 26, H("#15121c"));
                    for (int i = 0; i < 4; i++) q.Rect(6, 8 + i * 5.5f, 20, 2, H("#4b3f6b"));
                    return q.ToSprite(key);
                case "tile_glass":
                    q = new Painter(32, 32); q.Rect(9, 0, 14, 32, new Color(150 / 255f, 210 / 255f, 230 / 255f, 0.18f));
                    q.Rect(10, 0, 1.5f, 32, new Color(190 / 255f, 235 / 255f, 250 / 255f, 0.55f)); q.Rect(21, 0, 1.5f, 32, new Color(190 / 255f, 235 / 255f, 250 / 255f, 0.55f));
                    return q.ToSprite(key);
                case "tile_shelf":
                    q = new Painter(32, 32); q.Rect(2, 2, 28, 28, H("#2f3a42"));
                    q.Rect(6, 7, 9, 8, H("#4a5862")); q.Rect(17, 9, 8, 6, H("#4a5862")); q.Rect(7, 19, 18, 6, H("#4a5862"));
                    return q.ToSprite(key);
                case "tile_vending":
                    q = new Painter(32, 32); q.Rect(2, 1, 28, 26, H("#6e2a33")); q.Rect(6, 5, 13, 14, H("#c9e7ff")); q.Rect(22, 6, 4, 4, H("#e35b6a")); q.Rect(6, 21, 20, 3, H("#1b0e10"));
                    return q.ToSprite(key);
                case "tile_table":
                    q = new Painter(32, 32); q.Rect(0, 0, 32, 32, H("#5b4434")); q.Rect(1, 1, 30, 30, H("#6d533f")); return q.ToSprite(key);
                case "tile_notice":
                    q = new Painter(32, 32); q.Rect(2, 3, 28, 24, H("#3a2e22")); q.Rect(4, 5, 24, 20, H("#9c7a52"));
                    q.Rect(7, 7, 9, 10, H("#efe6cf")); q.Rect(18, 9, 8, 7, H("#efe6cf")); q.Rect(10, 6, 3, 3, H("#ff7d8c"));
                    return q.ToSprite(key);
                case "tile_machine":
                    q = new Painter(32, 32); q.Rect(1, 1, 30, 30, H("#262d33")); q.Rect(4, 4, 24, 24, H("#323b42")); q.Rect(7, 7, 4, 3, H("#7fe0b0"));
                    q.Rect(7, 16, 18, 2, H("#1a1f23")); q.Rect(7, 21, 18, 2, H("#1a1f23"));
                    return q.ToSprite(key);
                case "tile_console":
                    q = new Painter(32, 32); q.Rect(3, 3, 26, 26, H("#20303a")); q.Rect(7, 7, 18, 11, H("#0e1418")); q.Rect(7, 21, 18, 4, H("#4e6170"));
                    return q.ToSprite(key);
                case "tile_lift":
                    q = new Painter(32, 32); q.Rect(0, 0, 32, 32, H("#3c474f")); q.Rect(3, 3, 26, 26, H("#56646d")); q.Rect(15, 3, 2, 26, H("#2b343a"));
                    return q.ToSprite(key);
                case "tile_stairs":
                    q = new Painter(32, 32); for (int i = 0; i < 4; i++) q.Rect(0, i * 8, 32, 8, i % 2 == 1 ? H("#24302c") : H("#2f3d38")); return q.ToSprite(key);
                case "big_aircon": // 실외기 (늘려서 사용)
                    q = new Painter(96, 64); q.Rect(2, 2, 92, 60, H("#3a4248")); q.Rect(5, 5, 86, 54, H("#2a3035"));
                    q.StrokeCircle(48, 32, 23, 2, H("#59646c")); q.Rect(25, 31, 46, 2, H("#59646c")); q.Rect(47, 9, 2, 46, H("#59646c"));
                    return q.ToSprite(key);
                case "big_tank":
                    q = new Painter(64, 64); q.Circle(35, 36, 29, new Color(0, 0, 0, 0.35f)); q.Circle(32, 32, 29, H("#6a8794")); q.Circle(26, 26, 14, H("#9fb9c4"));
                    q.StrokeCircle(32, 32, 29, 2, H("#2e4049")); q.StrokeCircle(32, 32, 16, 2, H("#2e4049"));
                    return q.ToSprite(key);
                case "helipad":
                    q = new Painter(11 * 32, 9 * 32); q.Rect(0, 0, q.w, q.h, H("#23282b"));
                    for (int x = 4; x < q.w - 4; x += 24) { q.Rect(x, 4, 14, 4, H("#f3b04a")); q.Rect(x, q.h - 8, 14, 4, H("#f3b04a")); }
                    for (int y = 4; y < q.h - 4; y += 24) { q.Rect(4, y, 4, 14, H("#f3b04a")); q.Rect(q.w - 8, y, 4, 14, H("#f3b04a")); }
                    q.StrokeCircle(q.w / 2f, q.h / 2f, q.h / 2f - 6, 6, new Color(0.9f, 0.92f, 0.91f, 0.6f));
                    { float cx = q.w / 2f, cy = q.h / 2f; var c = new Color(0.9f, 0.92f, 0.91f, 0.6f); q.Rect(cx - 40, cy - 55, 18, 110, c); q.Rect(cx + 22, cy - 55, 18, 110, c); q.Rect(cx - 40, cy - 9, 80, 18, c); }
                    return q.ToSprite(key);
                case "tank_broken":
                    q = new Painter(128, 128); var cc = 64f;
                    q.Ellipse(cc + 6, cc + 10, 44, 34, new Color(120 / 255f, 200 / 255f, 190 / 255f, 0.10f));
                    q.Ellipse(cc - 4, cc + 4, 22, 16, new Color(1f, 125 / 255f, 140 / 255f, 0.10f));
                    q.StrokeCircle(cc, cc, 30, 2, H("#3d4a52"));
                    q.StrokeArc(cc, cc, 26, 3, 0.2f, 1.4f, new Color(170 / 255f, 225 / 255f, 220 / 255f, 0.55f));
                    q.StrokeArc(cc, cc, 26, 3, 1.9f, 2.9f, new Color(170 / 255f, 225 / 255f, 220 / 255f, 0.55f));
                    q.StrokeArc(cc, cc, 26, 3, 3.4f, 4.6f, new Color(170 / 255f, 225 / 255f, 220 / 255f, 0.55f));
                    q.StrokeArc(cc, cc, 26, 3, 5.0f, 5.9f, new Color(170 / 255f, 225 / 255f, 220 / 255f, 0.55f));
                    for (int i = 0; i < 9; i++) { float a = i * 0.71f + 0.3f, d = 30 + (i % 4) * 7; q.Rect(cc + Mathf.Cos(a) * d, cc + Mathf.Sin(a) * d, 4, 4, new Color(190 / 255f, 235 / 255f, 230 / 255f, 0.6f)); }
                    return q.ToSprite(key);
                case "door_closed":
                    q = new Painter(32, 32); q.Rect(0, 0, 32, 32, H("#2a2a24")); q.Rect(8, 9, 16, 14, H("#121619")); return q.ToSprite(key);
                case "door_stripes": // 카드 색으로 칠해지는 줄무늬 (흰색으로 그려두고 코드에서 색을 입힘)
                    q = new Painter(32, 32);
                    for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) if (((x - y) % 11 + 11) % 11 < 4 && !(x >= 8 && x < 24 && y >= 9 && y < 23)) q.Px(x, y, new Color(1, 1, 1, 0.85f));
                    return q.ToSprite(key);
                case "door_open":
                    q = new Painter(32, 32); q.Rect(0, 0, 32, 32, H("#10171a")); q.Rect(0, 0, 4, 32, H("#3a4248")); q.Rect(28, 0, 4, 32, H("#3a4248")); return q.ToSprite(key);
                case "obj_chair":
                    q = new Painter(32, 32); q.RoundRect(7, 9, 22, 20, 4, new Color(0, 0, 0, 0.35f)); q.RoundRect(6, 6, 20, 20, 4, H("#46535d")); q.RoundRect(8, 10, 16, 14, 3, H("#5f707c")); q.RoundRect(5, 4, 22, 6, 2, H("#2f3940"));
                    return q.ToSprite(key);
                case "obj_trash":
                    q = new Painter(32, 32); q.Circle(18, 19, 10, new Color(0, 0, 0, 0.35f)); q.Circle(16, 16, 10, H("#5b676e")); q.Circle(16, 16, 7.5f, H("#2a3236"));
                    q.Circle(14, 14.5f, 2.8f, H("#d9dcd2")); q.Circle(18.5f, 17.8f, 2.2f, H("#d9dcd2"));
                    return q.ToSprite(key);
                case "obj_cart":
                    q = new Painter(40, 32); float ox = 20, oy = 16;
                    q.RoundRect(ox - 12, oy - 7, 28, 19, 3, new Color(0, 0, 0, 0.35f));
                    q.Circle(ox - 11, oy - 9, 3, H("#1b2125")); q.Circle(ox + 11, oy - 9, 3, H("#1b2125")); q.Circle(ox - 11, oy + 9, 3, H("#1b2125")); q.Circle(ox + 11, oy + 9, 3, H("#1b2125"));
                    q.RoundRect(ox - 13, oy - 9, 26, 18, 3, H("#86939c")); q.RoundRect(ox - 10, oy - 6, 20, 12, 2, H("#5b6870"));
                    q.Rect(ox - 17, oy - 8, 2.5f, 16, H("#cfd6da")); q.Rect(ox - 6, oy - 4, 7, 6, H("#e7d9a8")); q.Rect(ox + 3, oy - 3, 5, 5, H("#9fc6e0"));
                    return q.ToSprite(key);
                case "obj_plant":   // 화분 (위에서 본 모습): 테라코타 화분 + 잎사귀
                    q = new Painter(32, 32);
                    q.Circle(17.5f, 18.5f, 10, new Color(0, 0, 0, 0.35f));
                    q.Circle(16, 16, 9.5f, H("#5a2e1e")); q.Circle(16, 16, 8.3f, H("#b4603c")); q.Circle(16, 16, 6.6f, H("#3b2a20"));
                    q.Ellipse(11, 12, 6, 3.2f, H("#2f7a45")); q.Ellipse(21, 13, 5.5f, 3, H("#3d9456")); q.Ellipse(15, 21, 3.2f, 6, H("#2f7a45"));
                    q.Ellipse(20, 20, 4.5f, 3, H("#4fae68")); q.Ellipse(13, 15, 3.5f, 2.2f, H("#5cc277")); q.Circle(16, 16, 1.6f, H("#7fd88f"));
                    return q.ToSprite(key);
                case "obj_water":   // 정수기: 흰 몸통 + 파란 물통
                    q = new Painter(32, 32);
                    q.RoundRect(8, 9, 20, 20, 3, new Color(0, 0, 0, 0.35f));
                    q.RoundRect(6, 6, 20, 20, 3, H("#3c4650")); q.RoundRect(7, 7, 18, 18, 2.5f, H("#e3e8ec"));
                    q.Circle(16, 14.5f, 6.5f, H("#2b6fa8")); q.Circle(16, 14.5f, 5.4f, H("#5fb0e8")); q.Circle(14, 12.5f, 2, H("#bfe6ff"));
                    q.Rect(11, 22, 3, 2, H("#e0505a")); q.Rect(18, 22, 3, 2, H("#4a8ee0"));
                    return q.ToSprite(key);
                case "obj_box":     // 골판지 상자: 테이프 십자
                    q = new Painter(32, 32);
                    q.Rect(8, 9, 21, 20, new Color(0, 0, 0, 0.35f));
                    q.Rect(6, 6, 21, 20, H("#5e4026")); q.Rect(7, 7, 19, 18, H("#c08a52")); q.Rect(7, 7, 19, 3, H("#d6a066"));
                    q.Rect(15, 7, 3, 18, H("#e3c79a")); q.Rect(9, 18, 5, 4, H("#f2efe6")); q.Rect(10, 19, 3, 1, H("#5e4026"));
                    return q.ToSprite(key);
                case "obj_desk":    // 책상: 나무 상판 + 모니터 + 서류 (변신하면 이 크기로 보인다)
                    q = new Painter(40, 32);
                    q.Rect(4, 6, 35, 24, new Color(0, 0, 0, 0.35f));
                    q.Rect(2, 4, 35, 24, H("#4a3326")); q.Rect(3, 5, 33, 22, H("#9a6e4c")); q.Rect(3, 5, 33, 3, H("#b48460"));
                    q.Rect(6, 8, 12, 8, H("#1e2329")); q.Rect(7, 9, 10, 6, H("#5fc0e8")); q.Rect(11, 16, 2, 2, H("#1e2329"));
                    q.Rect(22, 10, 8, 10, H("#f2efe6")); q.Rect(24, 12, 5, 1, H("#9aa4ad")); q.Rect(24, 14, 4, 1, H("#9aa4ad"));
                    q.Circle(31, 22, 2.2f, H("#e8e2d4")); q.Circle(31, 22, 1.3f, H("#6a3e24"));
                    return q.ToSprite(key);
                case "obj_shelf":   // 선반: 철제 틀 + 칸칸이 상자
                    q = new Painter(40, 32);
                    q.Rect(4, 8, 35, 20, new Color(0, 0, 0, 0.35f));
                    q.Rect(2, 6, 35, 20, H("#20272d")); q.Rect(3, 7, 33, 18, H("#46535d"));
                    q.Rect(5, 9, 8, 7, H("#c08a52")); q.Rect(14, 10, 6, 6, H("#7fb0d0")); q.Rect(22, 9, 9, 7, H("#d6a066")); q.Rect(31, 11, 3, 5, H("#e3e8ec"));
                    q.Rect(3, 16, 33, 1, H("#20272d"));
                    q.Rect(6, 18, 6, 6, H("#9fc6a0")); q.Rect(13, 18, 9, 6, H("#c08a52")); q.Rect(24, 19, 5, 5, H("#e0505a")); q.Rect(30, 18, 4, 6, H("#7fb0d0"));
                    return q.ToSprite(key);
                case "player_blob":
                    q = new Painter(32, 32);
                    q.Ellipse(16, 16.5f, 13.5f, 13.5f, H("#6e1f33"));
                    q.Ellipse(16, 16.5f, 12.3f, 12.3f, H("#d9566c"));
                    q.Ellipse(15, 15, 9.5f, 9.5f, H("#ff7d8c"));
                    q.Ellipse(13, 12, 4, 4, H("#ffc0c8"));
                    // 눈 (오른쪽을 보는 상태)
                    q.Circle(20.5f, 20.5f, 3.4f, H("#fff6ea")); q.Circle(20.5f, 11.5f, 3.4f, H("#fff6ea")); q.Circle(17, 16.5f, 2, H("#fff6ea"));
                    q.Circle(21.9f, 20.5f, 1.7f, H("#2a0d14")); q.Circle(21.9f, 11.5f, 1.7f, H("#2a0d14")); q.Circle(17.8f, 16.5f, 1, H("#2a0d14"));
                    return q.ToSprite(key);
                case "heli":
                    q = new Painter(224, 128); float hx = 96, hy = 64;
                    q.Rect(hx + 10, hy - 6, 90, 12, H("#2b3138")); q.Ellipse(hx, hy, 46, 24, H("#3c4650")); q.Ellipse(hx - 26, hy, 14, 16, H("#9fd8e6")); q.Rect(hx + 94, hy - 10, 6, 20, H("#ff5148"));
                    return q.ToSprite(key);
                case "heli_rotor":
                    q = new Painter(192, 192); q.Rect(6, 93, 180, 6, new Color(0.78f, 0.82f, 0.84f, 0.6f)); q.Rect(93, 6, 6, 180, new Color(0.78f, 0.82f, 0.84f, 0.6f)); return q.ToSprite(key);
                case "fx_dot":
                    q = new Painter(16, 16); q.Circle(8, 8, 7.5f, Color.white); return q.ToSprite(key, 16);
                case "fx_ring":
                    q = new Painter(64, 64); q.StrokeCircle(32, 32, 30, 2.5f, Color.white); return q.ToSprite(key, 64);
                case "fx_glow":
                    q = new Painter(64, 64); q.Radial(32, 32, 32, Color.white, new Color(1, 1, 1, 0)); return q.ToSprite(key, 64);
                case "fx_white":
                    q = new Painter(4, 4); q.Rect(0, 0, 4, 4, Color.white); return q.ToSprite(key, 4);
                case "ui_ring":
                    q = new Painter(64, 64); q.StrokeCircle(32, 32, 26, 6, Color.white); return q.ToSprite(key, 64);
                case "ui_vignette":
                    q = new Painter(128, 72);
                    for (int y = 0; y < q.h; y++) for (int x = 0; x < q.w; x++)
                    {
                        float dx = (x - 64f) / 64f, dy = (y - 36f) / 36f; float d = Mathf.Sqrt(dx * dx + dy * dy);
                        q.Px(x, y, new Color(1, 1, 1, Mathf.Clamp01((d - 0.55f) / 0.6f)));
                    }
                    return q.ToSprite(key, 64, true);
                case "ui_round": // 둥근 모서리 패널 (9-slice)
                    q = new Painter(32, 32); q.RoundRect(0, 0, 32, 32, 11, Color.white); return q.ToSprite(key, 32, true, null, new Vector4(12, 12, 12, 12));
                case "ui_card": // 모서리만 살짝 둥근 사각형 (출입 카드). 화면에서 모서리 반지름 약 5픽셀
                    q = new Painter(24, 24); q.RoundRect(0, 0, 24, 24, 4, Color.white); return q.ToSprite(key, 80, true, null, new Vector4(5, 5, 5, 5));
                case "ui_panel":
                    q = new Painter(16, 16); q.Rect(0, 0, 16, 16, Color.white); return q.ToSprite(key, 16);
            }
            // 알 수 없는 이름: 분홍 네모 (빠진 그림을 눈에 띄게)
            q = new Painter(32, 32); q.Rect(0, 0, 32, 32, Color.magenta);
            Debug.LogWarning("[Art] 임시 그림이 없는 이름: " + key);
            return q.ToSprite(key);
        }

        class Pal { public string coat, trim, skin, hair, cap, badge, helmet, tie; public bool mop, glasses, rifle; }
        static readonly Dictionary<string, Pal> PAL = new Dictionary<string, Pal>
        {
            { "cleaner",     new Pal { coat = "#4e6c86", trim = "#2f4558", skin = "#d8a587", hair = "#bdbdbd", cap = "#e9bf45", mop = true } },
            { "researcher",  new Pal { coat = "#e4e9e6", trim = "#a8b6b1", skin = "#e2b79c", hair = "#3b2a22" } },
            { "researcher2", new Pal { coat = "#dae3ec", trim = "#9fb0c2", skin = "#c79677", hair = "#17171d", glasses = true } },
            { "guard",       new Pal { coat = "#2c3e5c", trim = "#1a2638", skin = "#d9ab8c", hair = "#222222", cap = "#1d2b44", badge = "#f3b04a" } },
            { "senior",      new Pal { coat = "#eef0f4", trim = "#b48cf0", skin = "#e0b292", hair = "#9a9a9a", glasses = true } },
            { "soldier",     new Pal { coat = "#4d5b3a", trim = "#2f3a22", skin = "#c99878", hair = "#333333", helmet = "#3c4a2c", rifle = true } },
            { "director",    new Pal { coat = "#23262d", trim = "#0f1114", skin = "#e3b89c", hair = "#e2e2e2", tie = "#c62d3a" } },
        };

        // 위에서 내려다본 사람 (오른쪽을 보는 상태). 프로토타입 drawHuman과 같은 배치.
        static Sprite Human(string key, string kind)
        {
            Pal p; if (!PAL.TryGetValue(kind, out p)) p = PAL["researcher"];
            var q = new Painter(56, 40); float cx = 22, cy = 20;
            q.Ellipse(cx + 2, cy + 3, 13, 11, new Color(0, 0, 0, 0.35f));
            if (p.mop) { q.Line(cx + 5, cy + 11, cx + 20, cy + 13, 2.2f, H("#a9865a")); q.Ellipse(cx + 22, cy + 13, 3.5f, 6, H("#d7d2c4")); }
            if (p.rifle) { q.Rect(cx + 3, cy + 6, 19, 3.2f, H("#16190f")); q.Rect(cx + 7, cy + 8, 3, 4, H("#16190f")); }
            q.Ellipse(cx - 1, cy, 9.25f, 13.75f, H(p.trim));
            q.Ellipse(cx - 1, cy, 8.5f, 13, H(p.coat));
            if (p.tie != null) { q.Rect(cx + 4, cy - 3, 3, 6, H("#e8e8e8")); q.Rect(cx + 5.5f, cy - 1, 3, 2, H(p.tie)); }
            if (p.badge != null) q.Circle(cx + 1, cy - 10, 1.8f, H(p.badge));
            q.Circle(cx + 6, cy - 11, 3.4f, H(p.skin)); q.Circle(cx + 6, cy + 11, 3.4f, H(p.skin));
            q.Circle(cx + 1, cy, 7.5f, H(p.skin));
            if (p.helmet != null) { q.Circle(cx + 0.5f, cy, 8.4f, H(p.helmet)); q.StrokeCircle(cx + 0.5f, cy, 5.5f, 1.2f, H("#26301b")); }
            else
            {
                // 뒤통수 쪽 머리카락(또는 모자)
                q.HalfDisc(cx + 1, cy, 7.9f, H(p.cap ?? p.hair));
                if (p.cap != null) q.Rect(cx + 6, cy - 4, 4, 8, H(p.cap));
            }
            if (p.glasses) q.Rect(cx + 7, cy - 4.5f, 1.4f, 9, H("#1b1b1b"));
            return q.ToSprite(key, PPU, false, new Vector2(cx / q.w, 1 - cy / q.h));
        }

        // 2.5D용 서 있는 사람: stand_<종류>_<front|back|side> (16×24 픽셀, 발밑 기준, 16픽셀 = 1칸). side는 오른쪽을 본다.
        static Sprite Standing(string key)
        {
            string rest = key.Substring(6); int us = rest.LastIndexOf('_');
            string kind = us > 0 ? rest.Substring(0, us) : rest, view = us > 0 ? rest.Substring(us + 1) : "front";
            Pal p; if (!PAL.TryGetValue(kind, out p)) p = PAL["researcher"];
            bool front = view == "front", back = view == "back", side = view == "side";
            var q = new Painter(16, 24);
            Color coat = H(p.coat), trim = H(p.trim), skin = H(p.skin), hair = H(p.hair), dark = H("#16181c");
            if (p.mop && !back) { q.Rect(side ? 2 : 13, 9, 1, 13, H("#a9865a")); q.Rect(side ? 1 : 12, 21, 3, 2, H("#d7d2c4")); }
            // 다리·신발
            if (side) { q.Rect(6, 17, 2, 5, trim); q.Rect(8.5f, 17, 2, 5, trim); q.Rect(6, 22, 3, 2, dark); q.Rect(8.5f, 22, 3, 2, dark); }
            else { q.Rect(5, 17, 2.5f, 5, trim); q.Rect(8.5f, 17, 2.5f, 5, trim); q.Rect(5, 22, 2.5f, 2, dark); q.Rect(8.5f, 22, 2.5f, 2, dark); }
            // 몸통
            if (side) { q.Rect(5, 10, 6, 8, coat); q.Rect(5, 10, 1, 8, trim); q.Rect(7, 11, 2, 6, trim); q.Rect(7, 17, 2, 1, skin); }
            else
            {
                q.Rect(4, 10, 8, 8, coat); q.Rect(4, 10, 1, 8, trim); q.Rect(11, 10, 1, 8, trim);
                q.Rect(3, 11, 1, 6, trim); q.Rect(12, 11, 1, 6, trim); q.Rect(3, 17, 1, 1, skin); q.Rect(12, 17, 1, 1, skin);
                if (front && p.tie != null) { q.Rect(7, 10, 2, 2, H("#e8e8e8")); q.Rect(7.5f, 11, 1, 4, H(p.tie)); }
                if (front && p.badge != null) q.Rect(10, 12, 1, 1, H(p.badge));
            }
            // 머리
            q.Rect(5, 3, 6, 7, skin);
            if (back) q.Rect(5, 2, 6, 7, hair);
            else if (side) { q.Rect(5, 2, 6, 2, hair); q.Rect(5, 3, 3, 5, hair); q.Rect(9, 6, 1, 1, dark); }
            else { q.Rect(5, 2, 6, 2, hair); q.Rect(5, 3, 1, 3, hair); q.Rect(10, 3, 1, 3, hair); q.Rect(6, 6, 1, 1, dark); q.Rect(9, 6, 1, 1, dark); }
            if (p.glasses && !back) { if (side) q.Rect(8, 5.5f, 3, 1, dark); else q.Rect(5.5f, 5.5f, 5, 1, dark); }
            if (p.cap != null) { q.Rect(4.5f, 1, 7, 2.5f, H(p.cap)); if (!back) q.Rect(side ? 8 : 4.5f, 3, side ? 4 : 7, 1, H(p.cap)); }
            if (p.helmet != null) { q.Rect(4, 1, 8, 4, H(p.helmet)); q.Rect(4, 4, 8, 1, H("#26301b")); }
            if (p.rifle && !back) q.Rect(side ? 6 : 2, 13, side ? 9 : 12, 1.5f, H("#16190f"));
            var s = q.ToSprite(key, 16, false, new Vector2(0.5f, 0));
            s.texture.filterMode = FilterMode.Point;
            return s;
        }

        /* 간단한 픽셀 붓. 좌표는 왼쪽 위 (0,0), y 아래쪽. */
        class Painter
        {
            public int w, h; Color[] px;
            public Painter(int w, int h) { this.w = w; this.h = h; px = new Color[w * h]; }
            public Color[] Pixels { get { return px; } }
            public void Px(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                int i = (h - 1 - y) * w + x; var d = px[i];
                float a = c.a + d.a * (1 - c.a);
                if (a <= 0) return;
                px[i] = new Color((c.r * c.a + d.r * d.a * (1 - c.a)) / a, (c.g * c.a + d.g * d.a * (1 - c.a)) / a, (c.b * c.a + d.b * d.a * (1 - c.a)) / a, a);
            }
            void Fill(float x0, float y0, float x1, float y1, Func<float, float, bool> inside, Color c)
            {
                for (int y = Mathf.Max(0, Mathf.FloorToInt(y0)); y < Mathf.Min(h, Mathf.CeilToInt(y1)); y++)
                    for (int x = Mathf.Max(0, Mathf.FloorToInt(x0)); x < Mathf.Min(w, Mathf.CeilToInt(x1)); x++)
                        if (inside(x + 0.5f, y + 0.5f)) Px(x, y, c);
            }
            public void Rect(float x, float y, float rw, float rh, Color c) { Fill(x, y, x + rw, y + rh, (a, b) => a >= x && a < x + rw && b >= y && b < y + rh, c); }
            public void StrokeRect(float x, float y, float rw, float rh, Color c) { Rect(x, y, rw, 1, c); Rect(x, y + rh - 1, rw, 1, c); Rect(x, y + 1, 1, rh - 2, c); Rect(x + rw - 1, y + 1, 1, rh - 2, c); }
            public void RoundRect(float x, float y, float rw, float rh, float r, Color c)
            {
                Fill(x, y, x + rw, y + rh, (a, b) =>
                {
                    float qx = Mathf.Max(Mathf.Max(x + r - a, a - (x + rw - r)), 0), qy = Mathf.Max(Mathf.Max(y + r - b, b - (y + rh - r)), 0);
                    return a >= x && a < x + rw && b >= y && b < y + rh && qx * qx + qy * qy <= r * r;
                }, c);
            }
            public void Circle(float cx, float cy, float r, Color c) { Ellipse(cx, cy, r, r, c); }
            public void Ellipse(float cx, float cy, float rx, float ry, Color c) { Fill(cx - rx, cy - ry, cx + rx, cy + ry, (a, b) => { float dx = (a - cx) / rx, dy = (b - cy) / ry; return dx * dx + dy * dy <= 1; }, c); }
            public void HalfDisc(float cx, float cy, float r, Color c) { Fill(cx - r, cy - r, cx + r, cy + r, (a, b) => { float dx = a - cx, dy = b - cy; return dx * dx + dy * dy <= r * r && dx < -1.2f; }, c); }
            public void StrokeCircle(float cx, float cy, float r, float lw, Color c) { Fill(cx - r - lw, cy - r - lw, cx + r + lw, cy + r + lw, (a, b) => { float d = Mathf.Sqrt((a - cx) * (a - cx) + (b - cy) * (b - cy)); return Mathf.Abs(d - r) <= lw / 2; }, c); }
            public void StrokeArc(float cx, float cy, float r, float lw, float a0, float a1, Color c)
            {
                Fill(cx - r - lw, cy - r - lw, cx + r + lw, cy + r + lw, (a, b) =>
                {
                    float d = Mathf.Sqrt((a - cx) * (a - cx) + (b - cy) * (b - cy)); float ang = Mathf.Repeat(Mathf.Atan2(b - cy, a - cx), Mathf.PI * 2);
                    return Mathf.Abs(d - r) <= lw / 2 && ang >= a0 && ang <= a1;
                }, c);
            }
            public void Line(float x0, float y0, float x1, float y1, float lw, Color c)
            {
                Fill(Mathf.Min(x0, x1) - lw, Mathf.Min(y0, y1) - lw, Mathf.Max(x0, x1) + lw, Mathf.Max(y0, y1) + lw, (a, b) =>
                {
                    float vx = x1 - x0, vy = y1 - y0, t = Mathf.Clamp01(((a - x0) * vx + (b - y0) * vy) / (vx * vx + vy * vy));
                    float dx = a - (x0 + vx * t), dy = b - (y0 + vy * t); return dx * dx + dy * dy <= lw * lw / 4;
                }, c);
            }
            public void Radial(float cx, float cy, float r, Color inner, Color outer)
            {
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) / r;
                    px[(h - 1 - y) * w + x] = Color.Lerp(inner, outer, Mathf.Clamp01(d));
                }
            }
            public Sprite ToSprite(string name, int ppu = PPU, bool smooth = false, Vector2? pivot = null, Vector4? border = null)
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t.name = name; t.filterMode = smooth || ppu != PPU ? FilterMode.Bilinear : FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp;
                t.SetPixels(px); t.Apply();
                var s = border.HasValue
                    ? Sprite.Create(t, new Rect(0, 0, w, h), pivot ?? new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border.Value)
                    : Sprite.Create(t, new Rect(0, 0, w, h), pivot ?? new Vector2(0.5f, 0.5f), ppu);
                s.name = name;
                return s;
            }
        }
    }
}
