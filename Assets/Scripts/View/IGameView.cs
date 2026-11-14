// 게임 화면(2D / 2.5D)이 공통으로 해야 하는 일. Game과 Hud는 이것만 보고 쓴다.
using UnityEngine;

namespace Specimen07
{
    public interface IGameView
    {
        void BuildFloor(FloorGeo g);                        // 층이 바뀔 때
        void Render();                                      // 매 프레임
        bool InView(float x, float y, float margin);        // 게임 좌표 (x,y)가 화면 안인가
        Vector3 HeadScreen(float x, float y, float extra);  // (x,y)에 선 사람 머리 위의 화면 좌표(픽셀). extra = 칸 단위로 더 위
    }
}
