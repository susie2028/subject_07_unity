// UI Image에 그림 여러 장을 차례로 보여 주는 움짤 (튜토리얼 카드 영상 칸). 게임이 멈춰 있어도 돈다(unscaled time).
using UnityEngine;
using UnityEngine.UI;

namespace Specimen07
{
    [RequireComponent(typeof(Image))]
    public class FrameAnim : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 12;
        Image img; float t;

        public void Play(Sprite[] f, float rate)
        {
            frames = f; fps = rate; t = 0;
            if (img == null) img = GetComponent<Image>();
            if (frames != null && frames.Length > 0) img.sprite = frames[0];
        }

        void Update()
        {
            if (frames == null || frames.Length == 0) return;
            if (img == null) img = GetComponent<Image>();
            t += Time.unscaledDeltaTime;
            img.sprite = frames[(int)(t * fps) % frames.Length];
        }
    }
}
