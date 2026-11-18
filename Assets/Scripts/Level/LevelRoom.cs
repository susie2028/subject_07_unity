// 방: 이름·종류(조명·바닥 무늬·소품)·출입 제한의 단위. 벽은 층의 타일로 칠한다
using UnityEngine;

namespace Specimen07
{
    [AddComponentMenu("표본07/레벨/방")]
    public class LevelRoom : LevelRect
    {
        public string roomName = "새 방";
        [Tooltip("hall office lab store rest iso sec cold mech wing dir exit lift roof")]
        public string kind = "office";
        [Tooltip("들어가도 이상하지 않은 사람 (쉼표로: researcher,guard). 비우면 누구나")]
        public string allow = "";
        [Tooltip("바닥에 방 이름 쓰기")]
        public bool label = true;
        protected override Color RectColor { get { return new Color(1f, 0.86f, 0.35f); } }
        protected override string RectLabel { get { return roomName + "\n" + kind; } }
    }
}
