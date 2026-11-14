// UI 버튼 클릭 소리 (메인 메뉴·게임 화면 공통). Resources/Sounds/ui_click 을 쓴다.
// 씬이 바뀌어도 소리가 끊기지 않게 따로 남는 오브젝트에서 재생.
using UnityEngine;

namespace Specimen07
{
    public static class UiSound
    {
        static AudioSource src;
        static AudioClip clip;
        const float VOLUME = 0.8f;

        public static void Click()
        {
            if (src == null)
            {
                var go = new GameObject("UiSound");
                Object.DontDestroyOnLoad(go);
                src = go.AddComponent<AudioSource>(); src.playOnAwake = false;
                clip = Resources.Load<AudioClip>("Sounds/ui_click");
            }
            if (clip != null) src.PlayOneShot(clip, VOLUME);
        }
    }
}
