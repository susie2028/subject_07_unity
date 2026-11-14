// 상호작용 테두리 그리기 도구 (2D·2.5D 공용)
//  - SpriteOutline : 스프라이트 둘레에 한 색 실루엣 8장을 1~2픽셀씩 밀어 깔아서 테두리를 만든다
//  - RectFrame     : 칸 단위 사각형 테두리 (2D: 책상·문·콘솔)
//  - BoxFrame      : 칸 단위 상자 모서리 12개 (2.5D: 책상·문·콘솔)
//  - Hull          : 3D 모형 둘레 선 (Specimen07/OutlineHull 셰이더를 두 번째 재질로 얹는다)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Specimen07
{
    public static class OutlineFx
    {
        static Material sil;
        public static Material Silhouette
        {
            get
            {
                if (sil == null)
                {
                    var sh = Resources.Load<Shader>("Shaders/Silhouette");
                    if (sh == null) sh = Shader.Find("GUI/Text Shader");   // 셰이더를 못 찾으면 비슷하게 동작하는 기본 셰이더
                    sil = new Material(sh) { name = "Silhouette" };
                }
                return sil;
            }
        }

        static Shader hull; static bool hullTried;
        public static Shader HullShader { get { if (!hullTried) { hullTried = true; hull = Resources.Load<Shader>("Shaders/OutlineHull"); } return hull; } }

        static Material white;
        public static Material Unlit
        {
            get
            {
                if (white == null)
                {
                    var sh = Shader.Find("Sprites/Default");
                    white = new Material(sh) { name = "OutlineUnlit" };
                }
                return white;
            }
        }

        static Sprite px;
        public static Sprite Pixel
        {
            get
            {
                if (px == null)
                {
                    var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "outline_px" };
                    var c = new Color[16]; for (int i = 0; i < 16; i++) c[i] = Color.white;
                    t.SetPixels(c); t.Apply();
                    px = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
                }
                return px;
            }
        }
    }

    public class SpriteOutline : MonoBehaviour
    {
        static readonly Vector2[] DIRS = { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1), new Vector2(0.7f, 0.7f), new Vector2(-0.7f, 0.7f), new Vector2(0.7f, -0.7f), new Vector2(-0.7f, -0.7f) };
        SpriteRenderer[] cp;

        public static SpriteOutline Make(Transform parent)
        {
            var go = new GameObject("Outline"); go.transform.SetParent(parent, false);
            var o = go.AddComponent<SpriteOutline>();
            o.cp = new SpriteRenderer[DIRS.Length];
            for (int i = 0; i < DIRS.Length; i++)
            {
                var c = new GameObject("o" + i); c.transform.SetParent(go.transform, false);
                var sr = c.AddComponent<SpriteRenderer>(); sr.sharedMaterial = OutlineFx.Silhouette;
                sr.shadowCastingMode = ShadowCastingMode.Off; sr.receiveShadows = false;
                o.cp[i] = sr;
            }
            return o;
        }

        // src 둘레에 thick 픽셀 두께로. back = 카메라 반대쪽으로 살짝 (2.5D에서 정렬이 엉키지 않게)
        public void Show(SpriteRenderer src, Color c, float thick, Vector3 back)
        {
            if (src == null || src.sprite == null || !src.gameObject.activeInHierarchy) { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);
            var t = src.transform;
            float step = thick / src.sprite.pixelsPerUnit;
            Vector3 sc = t.lossyScale;
            Vector3 right = t.right * step * Mathf.Abs(sc.x), up = t.up * step * Mathf.Abs(sc.y);
            for (int i = 0; i < cp.Length; i++)
            {
                var sr = cp[i];
                sr.sprite = src.sprite; sr.flipX = src.flipX; sr.flipY = src.flipY; sr.color = c;
                sr.sortingLayerID = src.sortingLayerID; sr.sortingOrder = src.sortingOrder - 1;
                sr.transform.SetPositionAndRotation(t.position + right * DIRS[i].x + up * DIRS[i].y + back, t.rotation);
                sr.transform.localScale = sc;
            }
        }
    }

    // 2D: 칸 사각형 테두리 (막대 4개)
    public class RectFrame : MonoBehaviour
    {
        SpriteRenderer[] bars;
        public static RectFrame Make(Transform parent, int order)
        {
            var go = new GameObject("RectFrame"); go.transform.SetParent(parent, false);
            var f = go.AddComponent<RectFrame>(); f.bars = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var b = new GameObject("b" + i); b.transform.SetParent(go.transform, false);
                var sr = b.AddComponent<SpriteRenderer>(); sr.sprite = OutlineFx.Pixel; sr.sharedMaterial = OutlineFx.Unlit; sr.sortingOrder = order;
                f.bars[i] = sr;
            }
            return f;
        }
        // 월드 좌표의 왼쪽 위(x0,y0)~오른쪽 아래(x1,y1), 두께 w
        public void Show(Vector2 tl, Vector2 br, float w, Color c)
        {
            gameObject.SetActive(true);
            float x0 = tl.x - w / 2, x1 = br.x + w / 2, y0 = tl.y + w / 2, y1 = br.y - w / 2;   // 2D 월드: 위가 +y
            Set(bars[0], (x0 + x1) / 2, y0, x1 - x0, w, c);
            Set(bars[1], (x0 + x1) / 2, y1, x1 - x0, w, c);
            Set(bars[2], x0, (y0 + y1) / 2, w, y0 - y1, c);
            Set(bars[3], x1, (y0 + y1) / 2, w, y0 - y1, c);
        }
        static void Set(SpriteRenderer sr, float x, float y, float sw, float sh, Color c)
        {
            sr.transform.position = new Vector3(x, y, 0); sr.transform.localScale = new Vector3(sw, sh, 1); sr.color = c;
        }
    }

    // 2.5D: 상자 모서리 12개 (게임 x,y → 월드 x,z)
    public class BoxFrame : MonoBehaviour
    {
        MeshRenderer[] bars; Material mat;
        public static BoxFrame Make(Transform parent, Mesh unitBox)
        {
            var go = new GameObject("BoxFrame"); go.transform.SetParent(parent, false);
            var f = go.AddComponent<BoxFrame>(); f.bars = new MeshRenderer[12];
            var sh = Shader.Find("Unlit/Color"); if (sh == null) sh = Shader.Find("Sprites/Default");
            f.mat = new Material(sh) { name = "BoxFrame" };
            for (int i = 0; i < 12; i++)
            {
                var b = new GameObject("e" + i); b.transform.SetParent(go.transform, false);
                b.AddComponent<MeshFilter>().sharedMesh = unitBox;
                var mr = b.AddComponent<MeshRenderer>(); mr.sharedMaterial = f.mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
                f.bars[i] = mr;
            }
            return f;
        }
        // unitBox: 바닥 가운데가 원점인 1×1×1 상자. 게임 좌표 (x0,y0)~(x1,y1), 높이 h, 두께 w
        public void Show(float x0, float y0, float x1, float y1, float h, float w, Color c)
        {
            gameObject.SetActive(true);
            float k = 0.35f + 0.65f * c.a;   // 불투명 재질이라 알파 대신 밝기로
            mat.color = new Color(c.r * k, c.g * k, c.b * k, 1);
            int n = 0;
            float lw = x1 - x0 + w, ld = y1 - y0 + w;
            foreach (var hy in new[] { w / 2, h })
            {
                float b = Mathf.Max(0, hy - w / 2);
                Bar(n++, (x0 + x1) / 2, b, y0, lw, w, w); Bar(n++, (x0 + x1) / 2, b, y1, lw, w, w);
                Bar(n++, x0, b, (y0 + y1) / 2, w, w, ld); Bar(n++, x1, b, (y0 + y1) / 2, w, w, ld);
            }
            foreach (var xx in new[] { x0, x1 }) foreach (var yy in new[] { y0, y1 }) Bar(n++, xx, 0, yy, w, h, w);
        }
        // 게임 좌표 (x, y), 바닥 높이 h0 에서 크기 (sx, sy=높이, sz)
        void Bar(int i, float x, float h0, float y, float sx, float sy, float sz)
        {
            var t = bars[i].transform;
            t.position = new Vector3(x, h0, -y);
            t.localScale = new Vector3(sx, sy, sz);
        }
    }

    // 3D 모형 테두리: 모든 MeshRenderer에 두 번째 재질로 껍데기를 얹었다 뗐다 한다
    public class Hull : MonoBehaviour
    {
        MeshRenderer[] rs; Material[][] baseMats, withHull; Material hullMat; int state = -1;
        public static Hull Of(Transform model)
        {
            var h = model.GetComponent<Hull>();
            if (h != null) return h;
            h = model.gameObject.AddComponent<Hull>();
            h.rs = model.GetComponentsInChildren<MeshRenderer>(true);
            var sh = OutlineFx.HullShader;
            h.hullMat = sh != null ? new Material(sh) { name = "Hull" } : null;
            h.baseMats = new Material[h.rs.Length][]; h.withHull = new Material[h.rs.Length][];
            for (int i = 0; i < h.rs.Length; i++)
            {
                var b = h.rs[i].sharedMaterials; h.baseMats[i] = b;
                var w = new Material[b.Length + 1]; System.Array.Copy(b, w, b.Length); w[b.Length] = h.hullMat; h.withHull[i] = w;
            }
            return h;
        }
        public void Set(bool on, Color c, float width)
        {
            if (hullMat == null) return;
            int s = on ? 1 : 0;
            if (on) { hullMat.SetColor("_Color", c); hullMat.SetFloat("_Width", width); }
            if (s == state) return;
            state = s;
            for (int i = 0; i < rs.Length; i++) rs[i].sharedMaterials = on ? withHull[i] : baseMats[i];
        }
    }
}
