// 메인 메뉴 버튼을 심플하게: 반투명 어두운 띠 + 가운데 흰 글씨. 마우스를 올리면 띠가 양쪽으로 길어지고 진해진다.
// 에디터 스크립트 MainMenuStyle이 MainMenu 씬의 메인 버튼 4개에 붙여서 저장한다(씬 화면에서도 보임). 값은 Play 중 인스펙터에서 바꿔 보고 아래 기본값에 옮겨 적으면 된다.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Specimen07
{
    public class SimpleMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public Color barColor = new Color(0.86f, 0.86f, 0.9f, 1f);      // 버튼 그림(frame_button)에 곱하는 색
        public Color barHoverColor = Color.white;
        public Color textColor = new Color(0.93f, 0.90f, 0.97f, 1f);
        public Color textHoverColor = new Color(0.82f, 0.70f, 1f, 1f);   // 주인공 보라 쪽 연한 색
        public Color textDisabledColor = new Color(0.62f, 0.60f, 0.70f, 1f);
        public float hoverExtra = 80;      // 마우스를 올리면 늘어나는 길이(px, 양쪽 합)
        public float textPad = 28;         // 글자 왼쪽 여백(px)
        public float speed = 14;

        Button btn; Image bar; Text label; RectTransform rt;
        float baseW, k; bool over;

        void Awake()
        {
            btn = GetComponent<Button>(); bar = GetComponent<Image>(); rt = (RectTransform)transform;
            label = GetComponentInChildren<Text>(true);
            baseW = rt.sizeDelta.x;
        }

        public const int STYLE = 3;   // 모양 버전 (바뀌면 에디터가 씬에 다시 입힘)
        public int styleVersion;

        // 모양 입히기: 원래 크기의 반투명 띠 + 가운데 글씨, 화면 가로 가운데에 놓는다.
        // 에디터 스크립트(MainMenuStyle)가 씬에 한 번 적용해서 저장하므로 씬 화면에서도 그대로 보인다.
        public void ApplyStyle()
        {
            btn = GetComponent<Button>(); bar = GetComponent<Image>(); rt = (RectTransform)transform;
            label = GetComponentInChildren<Text>(true);
            float w = rt.rect.width, h = rt.rect.height;
            if (styleVersion < STYLE && textPad > 29) { w -= textPad - 28; }   // 예전(왼쪽 끝까지 붙인) 모양 → 원래 길이로
            textPad = 28;
            CenterX(rt, w, h);
            if (bar != null)
            {
                bar.sprite = Resources.Load<Sprite>("Art/UI/frame_button"); bar.type = Image.Type.Sliced; bar.color = barColor;
                bar.pixelsPerUnitMultiplier = 100f / Art.PPU / Hud.BTN_PX;
            }
            if (btn != null) btn.transition = Selectable.Transition.None;
            if (label != null)
            {
                label.alignment = TextAnchor.MiddleCenter;
                var lr = label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.pivot = new Vector2(0.5f, 0.5f);
                lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.color = btn != null && !btn.interactable ? textDisabledColor : textColor;
            }
            baseW = rt.sizeDelta.x;
            styleVersion = STYLE;
        }

        // 세로 위치는 그대로, 가로는 부모(화면) 가운데. 가운데 기준이라 호버 때 양쪽으로 늘어난다
        public static void CenterX(RectTransform rt, float w, float h)
        {
            var parent = rt.parent as RectTransform;
            float cy = 0;
            if (parent != null)
            {
                var c = new Vector3[4]; rt.GetWorldCorners(c);
                cy = parent.InverseTransformPoint((c[0] + c[1]) * 0.5f).y - parent.rect.center.y;
            }
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(0, cy);
        }

        public void OnPointerEnter(PointerEventData e) { over = true; }
        public void OnPointerExit(PointerEventData e) { over = false; }
        public void OnSelect(BaseEventData e) { over = true; }
        public void OnDeselect(BaseEventData e) { over = false; }

        void Update()
        {
            bool on = over && (btn == null || btn.interactable);
            k = Mathf.MoveTowards(k, on ? 1 : 0, Time.unscaledDeltaTime * speed);
            float e = 1 - (1 - k) * (1 - k);
            rt.sizeDelta = new Vector2(baseW + hoverExtra * e, rt.sizeDelta.y);
            if (bar != null) bar.color = Color.Lerp(barColor, barHoverColor, e);
            if (label != null)
                label.color = btn != null && !btn.interactable ? textDisabledColor : Color.Lerp(textColor, textHoverColor, e);
        }
    }
}
