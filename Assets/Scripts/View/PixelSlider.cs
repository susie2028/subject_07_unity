// 음량 바: 얇은 픽셀 바 + 금속 손잡이 (Art/UI/slider_track, slider_fill, slider_knob — Tools/make_ui_frames.py).
// 0~10 칸(10% 단위)으로 끌거나 눌러서 바꾼다. 게임 안 설정 창과 메인 메뉴 설정 창에서 같이 쓴다.
using UnityEngine;
using UnityEngine.UI;

namespace Specimen07
{
    public static class PixelSlider
    {
        static Image Piece(Transform p, string n, string spr, Vector2 aMin, Vector2 aMax, Vector2 size, Image.Type type, float mul)
        {
            var rt = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(p, false); rt.anchorMin = aMin; rt.anchorMax = aMax; rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
            var im = rt.gameObject.AddComponent<Image>(); im.sprite = Art.TryLoad("UI/" + spr); im.type = type; im.pixelsPerUnitMultiplier = mul;
            return im;
        }
        public const float PX = 3f;   // 그림 1픽셀 = 화면 3px (버튼과 같음)

        public static Slider Create(Transform parent, string name, float width, float value01, System.Action<float> onChange)
        {
            float mul = 100f / Art.PPU / PX;
            float barH = 7 * PX, knobW = 7 * PX, knobH = 9 * PX;
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.sizeDelta = new Vector2(width, knobH);
            var le = root.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = width; le.preferredHeight = knobH; le.minWidth = width;

            // 빈 칸 (가운데 가로줄)
            var track = Piece(root, "Track", "slider_track", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, barH), Image.Type.Sliced, mul);
            // 채운 칸
            var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(root, false); fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f); fillArea.sizeDelta = new Vector2(0, barH);
            var fill = Piece(fillArea, "Fill", "slider_fill", Vector2.zero, new Vector2(0, 1), Vector2.zero, Image.Type.Sliced, mul);
            fill.rectTransform.sizeDelta = new Vector2(0, 0);
            // 손잡이
            var handleArea = new GameObject("Handle Area", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(root, false); handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(knobW / 2, 0); handleArea.offsetMax = new Vector2(-knobW / 2, 0);
            var knob = Piece(handleArea, "Knob", "slider_knob", new Vector2(0, 0), new Vector2(0, 1), new Vector2(knobW, 0), Image.Type.Simple, mul);

            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform; s.handleRect = knob.rectTransform; s.targetGraphic = knob;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = 0; s.maxValue = 10; s.wholeNumbers = true;
            s.transition = Selectable.Transition.ColorTint;
            var cb = s.colors; cb.normalColor = new Color(0.9f, 0.9f, 0.95f); cb.highlightedColor = Color.white; cb.pressedColor = new Color(0.75f, 0.72f, 0.82f); s.colors = cb;
            s.SetValueWithoutNotify(Mathf.Round(value01 * 10));
            s.onValueChanged.AddListener(v => onChange(v / 10f));
            return s;
        }
    }
}
