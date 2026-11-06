using System.Collections.Generic;
using UnityEngine;

namespace Specimen07
{
    public static class U
    {
        public static float Rand(float a, float b) { return Random.Range(a, b); }
        public static T Pick<T>(IList<T> arr) { return arr[Random.Range(0, arr.Count)]; }
        public static float Dist(float ax, float ay, float bx, float by) { float dx = ax - bx, dy = ay - by; return Mathf.Sqrt(dx * dx + dy * dy); }
        public static float AngDiff(float a, float b)
        {
            float d = a - b;
            while (d > Mathf.PI) d -= 2 * Mathf.PI;
            while (d < -Mathf.PI) d += 2 * Mathf.PI;
            return d;
        }
        public static float ApproachAng(float cur, float target, float max)
        {
            float d = AngDiff(target, cur);
            return Mathf.Abs(d) <= max ? target : cur + Mathf.Sign(d) * max;
        }
        public static int Floor(float v) { return Mathf.FloorToInt(v); }

        // 게임 좌표(y 아래쪽) → 유니티 월드 좌표(y 위쪽)
        public static Vector3 W(float x, float y, float z = 0) { return new Vector3(x, -y, z); }
        // 게임 각도(라디안, y 아래쪽) → 유니티 Z 회전(도)
        public static float Deg(float ang) { return -ang * Mathf.Rad2Deg; }
    }

    // 입력 (기존 Input Manager). 조작 키를 바꾸려면 여기만 고치면 된다.
    public static class Inp
    {
        public static bool Held(KeyCode k) { return Input.GetKey(k); }
        public static bool Down(KeyCode k) { return Input.GetKeyDown(k); }
        public static bool AnyDown() { return Input.anyKeyDown; }


        public static Vector2 Move()
        {
            float x = 0, y = 0;
            if (Held(KeyCode.A) || Held(KeyCode.LeftArrow)) x -= 1;
            if (Held(KeyCode.D) || Held(KeyCode.RightArrow)) x += 1;
            if (Held(KeyCode.W) || Held(KeyCode.UpArrow)) y -= 1; // 게임 좌표는 y가 아래쪽
            if (Held(KeyCode.S) || Held(KeyCode.DownArrow)) y += 1;
            var v = new Vector2(x, y);
            if (v.magnitude > 1) v.Normalize();
            return v;
        }
    }
}
