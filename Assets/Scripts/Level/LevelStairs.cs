// 계단: 영역 + 이어지는 층
using UnityEngine;

namespace Specimen07
{
    [AddComponentMenu("표본07/레벨/계단")]
    public class LevelStairs : LevelRect
    {
        [Tooltip("이어지는 층 id (B1 1F 2F 3F 4F 5F)")] public string to = "1F";
        [Tooltip("> 올라가기, < 내려가기")] public string dir = ">";
        protected override Color RectColor { get { return new Color(0.6f, 0.9f, 1f); } }
        protected override string RectLabel { get { return "계단 " + dir + " " + to; } }
    }
}
