// 메인 메뉴 버튼을 심플하게: 반투명 어두운 띠 + 왼쪽 정렬 흰 글씨. 마우스를 올리면 띠가 오른쪽으로 길어지고 진해진다.
// MainMenuController가 실행할 때 메인 버튼 4개에 자동으로 붙인다. 값은 Play 중 인스펙터에서 바꿔 보고 아래 기본값에 옮겨 적으면 된다.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Specimen07
{
    public class SimpleMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public Color barColor = new Color(0.06f, 0.05f, 0.12f, 0.55f);
        public Color barHoverColor = new Color(0.05f, 0.04f, 0.10f, 0.88f);
        public Color textColor = new Color(0.96f, 0.95f, 1f, 1f);
        public Color textHoverColor = new Color(0.82f, 0.70f, 1f, 1f);   // 주인공 보라 쪽 연한 색
        public Color textDisabledColor = new Color(0.62f, 0.60f, 0.70f, 1f);
        public float hoverExtra = 120;     // 마우스를 올리면 오른쪽으로 늘어나는 길이(px)
        public float textPad = 28;         // 글자 왼쪽 여백(px)
        public float speed = 14;

        Button btn; Image bar; Text label; RectTransform rt;
        float baseW, k; bool over;

        void Awake()
        {
            btn = GetComponent<Button>(); bar = GetComponent<Image>(); rt = (RectTransform)transform;
            label = GetComponentInChildren<Text>(true);
            // 왼쪽 끝을 고정하고 오른쪽으로만 늘어나게 (보이는 위치는 그대로)
            float w = rt.rect.width;
            var p = rt.pivot; rt.pivot = new Vector2(0, p.y);
            rt.anchoredPosition += new Vector2((0 - p.x) * w, 0);
            baseW = rt.sizeDelta.x;
            if (bar != null) { bar.sprite = null; bar.type = Image.Type.Simple; bar.color = barColor; }
            if (btn != null) btn.transition = Selectable.Transition.None;
            if (label != null)
            {
                label.alignment = TextAnchor.MiddleLeft;
                var lr = label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                lr.offsetMin = new Vector2(textPad, 0); lr.offsetMax = new Vector2(-8, 0);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
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
