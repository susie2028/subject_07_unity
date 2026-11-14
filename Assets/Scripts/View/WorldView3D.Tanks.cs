// 2.5D 실험용 수조: 연구소 분위기용 장식. 실험실·격리실·냉동실의 벽 쪽 기계/실험대 칸 일부를 수조로 바꿔 그린다.
//  - 유리 원통 + 푸른 액체 + 안에 떠 있는 표본(촉수 생물 / 지렁이 친척 / 뇌·눈알 / 뼈) + 올라가는 기포
//  - 은은한 파란 빛 (점광원), 위아래 발광 링, 앞쪽 작은 모니터
//  - 가끔 깨진 수조 (유리 조각 + 초록 웅덩이) = 실험 흔적
// 게임 규칙은 그대로 (원래 칸이 막힌 칸이라 지나갈 수 없음). 그림만 바뀐다.
using System.Collections.Generic;
using UnityEngine;

namespace Specimen07
{
    public partial class WorldView3D
    {
        class Bubble { public Transform t; public float x, z, phase, speed; }
        readonly List<Bubble> bubbles = new List<Bubble>();

        // 수조로 그릴 칸인가: 실험실/격리실/냉동실 안, 바로 위가 벽인 기계·실험대 칸 (격리실 기계는 전부)
        public static bool IsTankTile(FloorGeo g, int x, int y, int salt)
        {
            char ch = g.Tile(x, y);
            if (ch != 'M' && ch != 'T') return false;
            var z = g.ZoneAt(x + 0.5f, y + 0.5f);
            if (z == null) return false;
            if (z.kind == "iso") return ch == 'M';
            if (z.kind != "lab" && z.kind != "cold") return false;
            char up = g.Tile(x, y - 1);
            if (up != '#' && up != 'v') return false;
            return WorldView.Hash(x, y, salt + 41) < 55;
        }

        void BuildTank(int tx, int ty, int salt)
        {
            int hsh = WorldView.Hash(tx, ty, salt + 77);
            bool broken = hsh < 12;
            int kind = hsh % 4;
            var root = new GameObject("Tank").transform; root.SetParent(floorRoot, false);
            root.position = P(tx + 0.5f, 0, ty + 0.5f);

            Material metal = ColMat(Data.Hex("#2b3244")), metal2 = ColMat(Data.Hex("#3a4358")), dark = ColMat(Data.Hex("#141822"));
            Material ring = ColMat(new Color(0.35f, 0.72f, 1f), true);
            Cyl(root, metal, 0, 0, 0, 0.44f, 0.24f);                                  // 받침
            Cyl(root, ring, 0, 0.24f, 0, 0.41f, 0.025f);                              // 아래 발광 링
            Part(root, metal2, 0, 0.02f, -0.42f, 0.24f, 0.18f, 0.08f);                // 앞 모니터 받침
            Part(root, ColMat(new Color(0.3f, 0.85f, 1f), true), 0, 0.07f, -0.465f, 0.16f, 0.09f, 0.01f);   // 모니터 화면
            Part(root, metal2, 0, 1.45f, 0.28f, 0.08f, 0.08f, 0.62f);                 // 벽으로 가는 배관

            if (broken)
            {
                // 깨진 수조: 받침 위로 들쭉날쭉한 유리 조각, 앞에 초록 웅덩이, 빈 안쪽
                var shard = ColMat(new Color(0.7f, 0.9f, 1f, 0.35f));
                for (int i = 0; i < 9; i++)
                {
                    float a = i * Mathf.PI * 2 / 9, hgt = 0.12f + 0.1f * ((hsh + i * 7) % 4);
                    Part(root, shard, Mathf.Sin(a) * 0.38f, 0.25f, Mathf.Cos(a) * 0.38f, 0.1f, hgt, 0.03f);
                }
                Cyl(root, metal, 0, 1.0f, 0, 0.45f, 0.15f);                            // 기울어진 뚜껑 (떨어져 나옴)
                var puddle = new GameObject("Puddle"); puddle.transform.SetParent(root, false);
                puddle.transform.localPosition = new Vector3(0.1f, 0.012f, -0.55f);
                var mf = puddle.AddComponent<MeshFilter>(); mf.sharedMesh = prism ?? (prism = MakePrism(12));
                puddle.AddComponent<MeshRenderer>().sharedMaterial = ColMat(new Color(0.35f, 0.85f, 0.45f, 0.45f));
                puddle.transform.localScale = new Vector3(1.1f, 0.004f, 0.7f);
                AddLamp(null, root.position + new Vector3(0, 0.5f, -0.2f), 1.8f, 0.6f, new Color(0.4f, 0.95f, 0.55f));
                return;
            }

            Material liquid = ColMat(kind == 3 ? new Color(0.3f, 0.8f, 0.6f, 0.32f) : new Color(0.25f, 0.55f, 1f, 0.3f));
            Material glass = ColMat(new Color(0.75f, 0.92f, 1f, 0.12f));
            // 표본 (불투명, 액체 안)
            switch (kind)
            {
                case 0:   // 촉수 생물
                    {
                        var body = ColMat(Data.Hex("#4a2a5e"));
                        Cyl(root, body, 0, 0.72f, 0, 0.15f, 0.24f);
                        Cyl(root, body, 0, 0.96f, 0, 0.1f, 0.06f);
                        for (int i = 0; i < 5; i++)
                        {
                            float a = i * Mathf.PI * 2 / 5;
                            Part(root, ColMat(Data.Hex("#3a1f4a")), Mathf.Sin(a) * 0.08f, 0.42f + 0.04f * (i % 2), Mathf.Cos(a) * 0.08f, 0.035f, 0.3f, 0.035f);
                        }
                        Part(root, ColMat(new Color(0.9f, 0.95f, 0.6f), true), 0.05f, 0.85f, -0.13f, 0.03f, 0.03f, 0.01f);   // 희미하게 빛나는 눈
                        break;
                    }
                case 1:   // 지렁이 친척 (보라색 마디)
                    {
                        var p1 = ColMat(Data.Hex("#7a4ab8")); var p2 = ColMat(Data.Hex("#553088"));
                        Cyl(root, p1, 0.02f, 0.5f, 0, 0.13f, 0.17f);
                        Cyl(root, p2, -0.02f, 0.67f, 0.02f, 0.11f, 0.15f);
                        Cyl(root, p1, 0.03f, 0.82f, 0, 0.09f, 0.13f);
                        Cyl(root, p2, 0, 0.95f, -0.02f, 0.06f, 0.1f);
                        break;
                    }
                case 2:   // 뇌·눈알
                    {
                        Cyl(root, ColMat(Data.Hex("#c27c8c")), 0, 0.62f, 0, 0.17f, 0.3f);
                        Part(root, ColMat(Data.Hex("#8a4a5a")), 0, 0.92f, 0, 0.02f, 0.02f, 0.3f);
                        Part(root, ColMat(Data.Hex("#efe9dc")), 0, 0.7f, -0.16f, 0.12f, 0.12f, 0.02f);
                        Part(root, dark, 0, 0.74f, -0.175f, 0.05f, 0.05f, 0.01f);
                        Part(root, ColMat(Data.Hex("#7a3a4a")), 0, 0.3f, 0, 0.04f, 0.32f, 0.04f);    // 늘어진 신경
                        break;
                    }
                default:  // 초록 액체 속 뼈
                    {
                        var bone = ColMat(Data.Hex("#d8d4c4"));
                        Part(root, bone, 0, 0.45f, 0, 0.05f, 0.45f, 0.05f);
                        Part(root, bone, 0, 0.78f, 0, 0.24f, 0.04f, 0.04f);
                        Part(root, bone, 0, 0.62f, 0, 0.18f, 0.04f, 0.04f);
                        Cyl(root, bone, 0, 0.9f, 0, 0.09f, 0.14f);
                        Part(root, dark, -0.035f, 0.96f, -0.085f, 0.03f, 0.03f, 0.01f); Part(root, dark, 0.035f, 0.96f, -0.085f, 0.03f, 0.03f, 0.01f);
                        break;
                    }
            }
            Cyl(root, liquid, 0, 0.25f, 0, 0.37f, 1.04f);                             // 액체
            Cyl(root, glass, 0, 0.24f, 0, 0.405f, 1.1f);                              // 유리
            Cyl(root, ring, 0, 1.33f, 0, 0.41f, 0.025f);                              // 위 발광 링
            Cyl(root, metal, 0, 1.34f, 0, 0.45f, 0.17f);                              // 뚜껑
            Cyl(root, metal2, 0, 1.51f, 0, 0.14f, 0.07f);                             // 뚜껑 손잡이
            // 기포
            var bub = ColMat(new Color(0.75f, 0.95f, 1f), true);
            for (int i = 0; i < 3; i++)
            {
                var b = new Bubble { x = -0.18f + 0.17f * i, z = -0.12f + 0.08f * (i % 2), phase = (hsh * 0.13f + i * 0.37f) % 1f, speed = 0.18f + 0.06f * i };
                var go = new GameObject("bubble"); go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = unitBox; go.AddComponent<MeshRenderer>().sharedMaterial = bub;
                go.transform.localScale = Vector3.one * 0.03f;
                b.t = go.transform; bubbles.Add(b);
            }
            // 은은한 파란 빛
            Color lc = kind == 3 ? new Color(0.35f, 0.95f, 0.7f) : new Color(0.35f, 0.62f, 1f);
            AddLamp(null, root.position + new Vector3(0, 0.85f, -0.15f), 2.6f, 1.1f, lc);
        }

        void UpdateBubbles(float t)
        {
            foreach (var b in bubbles)
            {
                if (b.t == null) continue;
                float k = Mathf.Repeat(t * b.speed + b.phase, 1f);
                b.t.localPosition = new Vector3(b.x + Mathf.Sin(t * 3 + b.phase * 9) * 0.02f, 0.3f + k * 0.95f, b.z);
            }
        }
    }
}
