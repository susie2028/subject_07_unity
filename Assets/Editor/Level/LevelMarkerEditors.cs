// 표시물 인스펙터: 고르는 칸(직업·그림·물건·카드·방 종류)을 목록으로, 영역 모서리 손잡이, 사람 보는 방향 손잡이
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Specimen07.EditorTools
{
    static class Pick
    {
        public static readonly string[] KINDS = { "hall", "office", "lab", "store", "rest", "iso", "sec", "cold", "mech", "wing", "dir", "exit", "lift", "roof" };
        public static readonly string[] KIND_NAMES = { "hall 복도·홀", "office 사무실", "lab 실험실", "store 창고", "rest 휴게실", "iso 격리실", "sec 보안실", "cold 냉동실", "mech 기계실", "wing 표본동", "dir 소장실", "exit 계단실", "lift 엘리베이터", "roof 옥상" };
        public static readonly string[] PALS = { "cleaner", "researcher", "researcher2", "guard", "senior", "soldier", "director" };

        // 문자열 속성을 목록에서 고르게 (목록에 없는 값이면 글자로)
        public static void Popup(SerializedProperty p, string label, string[] values, string[] names)
        {
            int i = System.Array.IndexOf(values, p.stringValue);
            var opts = names.Concat(new[] { "직접 입력…" }).ToArray();
            int ni = EditorGUILayout.Popup(label, i < 0 ? names.Length : i, opts);
            if (ni < names.Length) p.stringValue = values[ni];
            else p.stringValue = EditorGUILayout.TextField(" ", i < 0 ? p.stringValue : "");
        }
        public static void Rest(SerializedObject so, params string[] skip)
        {
            var it = so.GetIterator(); bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.name == "m_Script" || skip.Contains(it.name)) continue;
                EditorGUILayout.PropertyField(it, true);
            }
        }
        public static string[] Cards() { return Data.CardOrder; }
        public static string[] CardNames() { return Data.CardOrder.Select(c => Data.Cards[c].name + " 카드").ToArray(); }
    }

    [CustomEditor(typeof(LevelRect), true)]
    public class LevelRectEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (target is LevelRoom) { Pick.Popup(serializedObject.FindProperty("kind"), "종류", Pick.KINDS, Pick.KIND_NAMES); Pick.Rest(serializedObject, "kind"); }
            else if (target is LevelStairs) { Pick.Popup(serializedObject.FindProperty("to"), "이어지는 층", Data.FloorOrder, Data.FloorOrder); Pick.Rest(serializedObject, "to"); }
            else Pick.Rest(serializedObject);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Scene의 노란 모서리 손잡이로 크기를 바꿔요. 오브젝트 위치 = 왼쪽 위 모서리.", MessageType.None);
        }
        void OnSceneGUI()
        {
            var m = (LevelRect)target; var f = m.Floor; if (f == null) return;
            var r = m.Rect; var t = f.transform;
            Vector2[] cs = { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
            for (int k = 0; k < 4; k++)
            {
                var wp = t.TransformPoint(new Vector3(cs[k].x, 0.1f, -cs[k].y));
                Handles.color = new Color(1f, 0.85f, 0.3f);
                EditorGUI.BeginChangeCheck();
                var np = Handles.Slider2D(wp, t.up, t.right, t.forward, HandleUtility.GetHandleSize(wp) * 0.09f, Handles.CubeHandleCap, Vector2.zero);
                if (!EditorGUI.EndChangeCheck()) continue;
                var l = t.InverseTransformPoint(np); int gx = Mathf.RoundToInt(l.x), gy = Mathf.RoundToInt(-l.z);
                int xa = k == 0 || k == 3 ? gx : r.xMin, xb = k == 1 || k == 2 ? gx : r.xMax;
                int ya = k == 0 || k == 1 ? gy : r.yMin, yb = k == 2 || k == 3 ? gy : r.yMax;
                Undo.RecordObjects(new Object[] { m, m.transform }, "영역 크기");
                m.Rect = new RectInt(Mathf.Min(xa, xb - 1), Mathf.Min(ya, yb - 1), Mathf.Max(1, Mathf.Abs(xb - xa)), Mathf.Max(1, Mathf.Abs(yb - ya)));
            }
        }
    }

    [CustomEditor(typeof(LevelNpc))]
    public class LevelNpcEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var chs = Data.CharOrder; var chNames = chs.Select(c => Data.Chars[c].name).ToArray();
            var ch = serializedObject.FindProperty("ch"); string oldCh = ch.stringValue;
            Pick.Popup(ch, "직업", chs, chNames);
            var pal = serializedObject.FindProperty("pal");
            if (ch.stringValue != oldCh && (pal.stringValue == oldCh || pal.stringValue.StartsWith(oldCh))) pal.stringValue = ch.stringValue;   // 직업을 바꾸면 그림도 따라가게
            Pick.Popup(pal, "그림", Pick.PALS, Pick.PALS);
            Pick.Rest(serializedObject, "ch", "pal");
            serializedObject.ApplyModifiedProperties();

            var n = (LevelNpc)target;
            EditorGUILayout.Space(4);
            GUILayout.Label("순찰 지점 " + n.Route.Count + "개 (자식 오브젝트, 위에서부터 순서대로 돌아요)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 순찰 지점"))
                {
                    var route = n.Route; var at = route.Count > 0 ? route[route.Count - 1].GamePos + new Vector2(2, 0) : n.GamePos;
                    var go = new GameObject((route.Count + 1).ToString()); Undo.RegisterCreatedObjectUndo(go, "순찰 지점");
                    go.transform.SetParent(n.transform, false);
                    var w = go.AddComponent<LevelWaypoint>(); w.SetGamePos(at);
                    Selection.activeGameObject = go;
                }
                if (GUILayout.Button("경로 지점 고르기")) Selection.objects = n.Route.Select(w => (Object)w.gameObject).ToArray();
            }
            EditorGUILayout.HelpBox("사람을 옮기면 경로도 같이 옮겨져요. 지점 하나만 옮기려면 그 지점(자식)을 골라요. 보는 방향은 Scene의 흰 손잡이로.", MessageType.None);
        }
        void OnSceneGUI()
        {
            var n = (LevelNpc)target; var f = n.Floor; if (f == null) return;
            var p = n.GamePos; float a = n.face * Mathf.Deg2Rad;
            var t = f.transform;
            var tip = t.TransformPoint(new Vector3(p.x + Mathf.Cos(a) * 1.1f, 0.1f, -(p.y + Mathf.Sin(a) * 1.1f)));
            Handles.color = Color.white;
            Handles.DrawLine(t.TransformPoint(new Vector3(p.x, 0.1f, -p.y)), tip);
            EditorGUI.BeginChangeCheck();
            var np = Handles.Slider2D(tip, t.up, t.right, t.forward, HandleUtility.GetHandleSize(tip) * 0.08f, Handles.SphereHandleCap, Vector2.zero);
            if (EditorGUI.EndChangeCheck())
            {
                var l = t.InverseTransformPoint(np);
                Undo.RecordObject(n, "보는 방향");
                n.face = Mathf.Round(Mathf.Atan2(-l.z - p.y, l.x - p.x) * Mathf.Rad2Deg / 15f) * 15f;   // 15도 단위
            }
        }
    }

    [CustomEditor(typeof(LevelObject))]
    public class LevelObjectEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var forms = Data.Forms.Values.Where(f => f.type == FormType.Object && !f.Big).Select(f => f.id).ToArray();
            var p = serializedObject.FindProperty("form"); string old = p.stringValue;
            Pick.Popup(p, "물건", forms, forms.Select(f => Data.Forms[f].name).ToArray());
            serializedObject.ApplyModifiedProperties();
            var o = (LevelObject)target;
            if (old != p.stringValue && Data.Forms.ContainsKey(p.stringValue)) { Undo.RecordObject(o.gameObject, "이름"); o.gameObject.name = Data.Forms[p.stringValue].name; }
            EditorGUILayout.HelpBox("쓰레기통·카트·화분·정수기는 벽에 붙여야 자연스러워요 (아니면 NPC가 수상하게 봐요). 의자 방향은 회전 툴(E).", MessageType.None);
        }
    }

    [CustomEditor(typeof(LevelDoor))]
    public class LevelDoorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Pick.Popup(serializedObject.FindProperty("req"), "필요한 카드", Pick.Cards(), Pick.CardNames());
            Pick.Rest(serializedObject, "req");
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(LevelConsole))]
    public class LevelConsoleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Pick.Popup(serializedObject.FindProperty("req"), "필요한 카드", Pick.Cards(), Pick.CardNames());
            var ids = Object.FindObjectsByType<LevelTrap>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(t => t.trapId).Distinct().OrderBy(s => s).ToArray();
            var trap = serializedObject.FindProperty("trap");
            var opts = new[] { "(없음)" }.Concat(ids).ToArray();
            int ti = string.IsNullOrEmpty(trap.stringValue) ? 0 : System.Array.IndexOf(opts, trap.stringValue);
            ti = EditorGUILayout.Popup("끄는 함정", ti < 0 ? 0 : ti, opts);
            trap.stringValue = ti <= 0 ? "" : opts[ti];
            Pick.Rest(serializedObject, "req", "trap");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
