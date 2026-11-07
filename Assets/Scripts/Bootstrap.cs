// 어떤 씬에서 Play를 눌러도 게임이 시작되게 한다.
// 씬에 Game 컴포넌트가 이미 있으면 아무것도 하지 않는다.
using UnityEngine;

namespace Specimen07
{
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindFirstObjectByType<Game>() != null) return;
            if (Object.FindFirstObjectByType<MainMenuController>() != null) return; // 메인 메뉴 씬에서는 게임을 만들지 않음
            new GameObject("Game").AddComponent<Game>();
        }
    }
}
