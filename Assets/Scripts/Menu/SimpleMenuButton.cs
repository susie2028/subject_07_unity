// 메인 메뉴 버튼을 심플하게: 반투명 어두운 띠 + 왼쪽 정렬 흰 글씨. 마우스를 올리면 띠가 오른쪽으로 길어지고 진해진다.
// 에디터 스크립트 MainMenuStyle이 MainMenu 씬의 메인 버튼 4개에 붙여서 저장한다(씬 화면에서도 보임). 값은 Play 중 인스펙터에서 바꿔 보고 아래 기본값에 옮겨 적으면 된다.
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
            baseW = rt.sizeDelta.x;
        }

        // 모양 입히기: 띠 왼쪽 끝을 화면 왼쪽 끝에 붙이고, 글자는 원래 버튼이 있던 자리에 둔다.
        // 에디터 스크립트(MainMenuStyle)가 씬에 한 번 적용해서 저장하므로 씬 화면에서도 그대로 보인다.
        public void ApplyStyle()
        {
            btn = GetComponent<Button>(); bar = GetComponent<Image>(); rt = (RectTransform)transform;
            label = GetComponentInChildren<Text>(true);
            var parent = rt.parent as RectTransform;
            var canvas = GetComponentInParent<Canvas>();
            if (parent != null && canvas != null)
            {
                var croot = (RectTransform)canvas.rootCanvas.transform;
                var cs = new Vector3[4]; croot.GetWorldCorners(cs);           // 화면 왼쪽 끝
                var bc = new Vector3[4]; rt.GetWorldCorners(bc);              // 버튼 원래 왼쪽·오른쪽 끝
                float screenL = parent.InverseTransformPoint(cs[0]).x;
                float oldL = parent.InverseTransformPoint(bc[0]).x, oldR = parent.InverseTransformPoint(bc[2]).x;
                float h = rt.rect.height, cy = parent.InverseTransformPoint((bc[0] + bc[1]) * 0.5f).y;
                var pr = parent.rect;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0, 0.5f);
                rt.anchoredPosition = new Vector2(screenL - pr.xMin, cy - pr.yMin);
                rt.sizeDelta = new Vector2(oldR - screenL, h);
                if (oldL - screenL > 1) textPad = (oldL - screenL) + textPad;   // 이미 붙어 있으면(다시 입히기) 여백은 그대로
            }
            if (bar != null) { bar.sprite = null; bar.type = Image.Type.Simple; bar.color = barColor; }
            if (btn != null) btn.transition = Selectable.Transition.None;
            if (label != null)
            {
                label.alignment = TextAnchor.MiddleLeft;
                var lr = label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.pivot = new Vector2(0.5f, 0.5f);
                lr.offsetMin = new Vector2(textPad, 0); lr.offsetMax = new Vector2(-8, 0);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.color = btn != null && !btn.interactable ? textDisabledColor : textColor;
            }
            baseW = rt.sizeDelta.x;
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
