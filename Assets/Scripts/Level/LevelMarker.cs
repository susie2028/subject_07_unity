// 씬에 놓는 레벨 표시물의 공통 부분. 위치는 층(LevelFloor) 기준 게임 좌표로 읽는다.
// 게임 좌표: x 오른쪽, y 아래쪽(= 월드 -z), 1칸 = 1
using UnityEngine;

namespace Specimen07
{
    [SelectionBase]
    public abstract class LevelMarker : MonoBehaviour
    {
        public LevelFloor Floor { get { return GetComponentInParent<LevelFloor>(true); } }

        public Vector2 GamePos
        {
            get
            {
                var f = Floor;
                var l = f != null ? f.transform.InverseTransformPoint(transform.position) : transform.position;
                return new Vector2(l.x, -l.z);
            }
        }
        public void SetGamePos(Vector2 p)
        {
            var f = Floor; var l = new Vector3(p.x, 0, -p.y);
            transform.position = f != null ? f.transform.TransformPoint(l) : l;
        }
        public Vector2Int Cell { get { var p = GamePos; return new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)); } }

        // 보는 방향(도): 0 = 오른쪽, 90 = 아래. 오브젝트의 Y 회전과 같다
        public float Yaw { get { var f = Floor; float fy = f != null ? f.transform.eulerAngles.y : 0; return Mathf.DeltaAngle(0, transform.eulerAngles.y - fy); } }

        // 칸 맞춤: 0 = 자유, 1 = 칸 가운데, 2 = 칸 모서리(사각형 영역)
        public virtual int SnapMode { get { return 0; } }

        // 이 표시물이 켜져 있나 (층 자체를 꺼서 숨긴 것은 상관없이)
        public bool IsOn
        {
            get
            {
                var f = Floor;
                for (var t = transform; t != null && (f == null || t != f.transform); t = t.parent) if (!t.gameObject.activeSelf) return false;
                return enabled;
            }
        }

        /* ===== 기즈모 (씬에서 눌러 고를 수 있게) ===== */
        protected Vector3 W(float x, float y, float h)
        {
            var f = Floor; var l = new Vector3(x, h, -y);
            return f != null ? f.transform.TransformPoint(l) : l;
        }
        protected void GBox(float x, float y, float w, float d, float h, float h0, Color fill, Color line)
        {
            var c = W(x, y, h0 + h / 2);
            var s = new Vector3(w, h, d);
            Gizmos.color = fill; Gizmos.DrawCube(c, s);
            Gizmos.color = line; Gizmos.DrawWireCube(c, s);
        }
        protected void GRect(RectInt r, float h, Color fill, Color line)
        {
            GBox(r.x + r.width / 2f, r.y + r.height / 2f, r.width, r.height, 0.02f, h, fill, line);
        }
        protected void GLabel(float x, float y, float h, string text, Color c)
        {
#if UNITY_EDITOR
            var st = new GUIStyle(UnityEditor.EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            st.normal.textColor = c;
            UnityEditor.Handles.Label(W(x, y, h), text, st);
#endif
        }
        // 나 또는 내 자식(순찰 지점 등)이 선택돼 있으면 진하게
        protected bool Selected
        {
            get
            {
#if UNITY_EDITOR
                foreach (var o in UnityEditor.Selection.transforms) if (o == transform || o.IsChildOf(transform)) return true;
#endif
                return false;
            }
        }
    }

    // 사각형 영역 (방·계단·함정 영역·헬리패드). 오브젝트 위치 = 왼쪽 위 모서리
    public abstract class LevelRect : LevelMarker
    {
        public Vector2Int size = new Vector2Int(4, 3);
        public override int SnapMode { get { return 2; } }
        public RectInt Rect
        {
            get { var p = GamePos; return new RectInt(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.Max(1, size.x), Mathf.Max(1, size.y)); }
            set { SetGamePos(new Vector2(value.x, value.y)); size = new Vector2Int(value.width, value.height); }
        }
        protected abstract Color RectColor { get; }
        protected virtual string RectLabel { get { return null; } }
        void OnDrawGizmos()
        {
            var r = Rect; var c = RectColor; bool s = Selected;
            GRect(r, 0.015f, new Color(c.r, c.g, c.b, s ? 0.18f : 0.03f), new Color(c.r, c.g, c.b, s ? 1f : 0.45f));
            var l = RectLabel;
            if (l != null) GLabel(r.x + r.width / 2f, r.y + r.height / 2f, 0.3f, l, new Color(1, 1, 1, s ? 1 : 0.85f));
        }
    }
}
