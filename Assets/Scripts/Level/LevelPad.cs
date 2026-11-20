// 헬리패드 영역 (옥상)
using UnityEngine;

namespace Specimen07
{
    [AddComponentMenu("표본07/레벨/헬리패드")]
    public class LevelPad : LevelRect
    {
        protected override Color RectColor { get { return Color.white; } }
        protected override string RectLabel { get { return "헬리패드"; } }
    }
}
