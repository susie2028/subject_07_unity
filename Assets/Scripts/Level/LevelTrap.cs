// 함정 (레이저·가스). 영역은 자식 LevelTrapArea 들
using UnityEngine;

namespace Specimen07
{
    [AddComponentMenu("표본07/레벨/함정")]
    public class LevelTrap : LevelMarker
    {
        [Tooltip("콘솔이 이 id로 함정을 끈다")] public string trapId = "T1";
        [Tooltip("laser | gas")] public string kind = "laser";
        public string label = "";
    }
}
