// 함정 영역 하나. 부모의 LevelTrap에 속한다
using UnityEngine;

namespace Specimen07
{
    [AddComponentMenu("표본07/레벨/함정 영역")]
    public class LevelTrapArea : LevelRect
    {
        LevelTrap Trap { get { return GetComponentInParent<LevelTrap>(true); } }
        protected override Color RectColor { get { var t = Trap; return t != null && t.kind == "gas" ? new Color(0.6f, 0.9f, 0.35f) : new Color(1f, 0.3f, 0.3f); } }
        protected override string RectLabel { get { var t = Trap; return t != null ? t.trapId + " " + t.kind : "함정 (부모 없음)"; } }
    }
}
