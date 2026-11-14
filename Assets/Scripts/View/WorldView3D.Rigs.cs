// 2.5D 로우폴리 NPC: 참고 시트(Tools/art_src/npc_sheet.png)의 디자인을 상자로 조립한 큰 머리 캐릭터.
//  청소부(파란 모자·작업복·노란 장갑·대걸레), 연구원(부스스한 머리·안경·흰 가운·초록 넥타이),
//  경비원(남색 모자·뚱뚱한 몸·금 견장·손전등), 군인(헬멧·초록 고글·두꺼운 어깨·조끼),
//  소장(흰머리·검은 정장·금 견장), 고위 연구원(단정한 흰머리·가운·보라 넥타이·금 견장)
//  연구원은 사람마다 머리색·넥타이가 다름 (_v2, _v3)
// 걷기: 다리·팔을 반대로 흔들고 몸이 통통·좌우로 기우뚱. 진짜 모델이 생기면 Resources/Models/char_<종류> 프리팹으로 교체.
using System.Collections.Generic;
using UnityEngine;

namespace Specimen07
{
    public partial class WorldView3D
    {
        class Rig
        {
            public Transform root, body, head, armL, armR, legL, legR;
            public float armSwingR = 1, height = 1.4f, swingK = 1;
        }
        class Spec
        {
            public float legLen, legW, hipGap, torsoW, torsoH, torsoD, head, armLen, armW, belly;
            public string skin, top, sleeve, pants, shoe, hair, tie, hand;
        }
        // 큰 머리 비율 (머리가 키의 40% 가까이)
        static readonly Dictionary<string, Spec> SPECS = new Dictionary<string, Spec>
        {
            { "cleaner",    new Spec { legLen = 0.26f, legW = 0.13f, hipGap = 0.08f, torsoW = 0.4f,  torsoH = 0.38f, torsoD = 0.28f, head = 0.46f, armLen = 0.3f,  armW = 0.11f, belly = 0,     skin = "#e8b48e", top = "#3f6fae", sleeve = "#3f6fae", pants = "#3a64a0", shoe = "#2a2622", hair = "#6a4428", tie = null,      hand = "#f0c020" } },
            { "researcher", new Spec { legLen = 0.22f, legW = 0.12f, hipGap = 0.07f, torsoW = 0.38f, torsoH = 0.36f, torsoD = 0.26f, head = 0.48f, armLen = 0.28f, armW = 0.1f,  belly = 0,     skin = "#eab890", top = "#eef1f3", sleeve = "#eef1f3", pants = "#5a3e2c", shoe = "#3a2a20", hair = "#6e4626", tie = "#3f8a52", hand = null } },
            { "guard",      new Spec { legLen = 0.22f, legW = 0.15f, hipGap = 0.1f,  torsoW = 0.54f, torsoH = 0.42f, torsoD = 0.46f, head = 0.46f, armLen = 0.3f,  armW = 0.13f, belly = 0.12f, skin = "#ecb88e", top = "#1f2d5e", sleeve = "#1f2d5e", pants = "#1b2752", shoe = "#121218", hair = "#5a3a24", tie = "#14204a", hand = null } },
            { "soldier",    new Spec { legLen = 0.26f, legW = 0.16f, hipGap = 0.1f,  torsoW = 0.56f, torsoH = 0.46f, torsoD = 0.4f,  head = 0.46f, armLen = 0.34f, armW = 0.16f, belly = 0,     skin = "#7a5a3c", top = "#3e5e2e", sleeve = "#4a6e36", pants = "#3a5a2c", shoe = "#26321c", hair = null,      tie = null,      hand = "#2e4422" } },
            { "director",   new Spec { legLen = 0.24f, legW = 0.14f, hipGap = 0.08f, torsoW = 0.5f,  torsoH = 0.42f, torsoD = 0.4f,  head = 0.46f, armLen = 0.3f,  armW = 0.12f, belly = 0.08f, skin = "#ecbc96", top = "#1c1c22", sleeve = "#1c1c22", pants = "#18181e", shoe = "#0c0c10", hair = "#a2a2a8", tie = "#e8e8e8", hand = null } },
            { "senior",     new Spec { legLen = 0.24f, legW = 0.12f, hipGap = 0.07f, torsoW = 0.42f, torsoH = 0.4f,  torsoD = 0.3f,  head = 0.46f, armLen = 0.3f,  armW = 0.11f, belly = 0.04f, skin = "#ecbc96", top = "#f4f5f7", sleeve = "#f4f5f7", pants = "#2e2a3a", shoe = "#16141c", hair = "#b4b4bc", tie = "#7a4ab8", hand = null } },
        };
        // 연구원 변형: (머리, 넥타이, 피부)
        static readonly Dictionary<string, string[]> LOOKS = new Dictionary<string, string[]>
        {
            { "researcher",     new[] { "#6e4626", "#3f8a52", "#eab890" } },
            { "researcher_v2",  new[] { "#c4622c", "#4070c8", "#eab890" } },
            { "researcher_v3",  new[] { "#e2c46e", "#962e3c", "#f0c4a0" } },
            { "researcher2",    new[] { "#2c2634", "#c4363c", "#b07654" } },
            { "researcher2_v2", new[] { "#5a3c28", "#dca028", "#d8a47c" } },
        };

        Material M(string hex) { return ColMat(Data.Hex(hex)); }
        Material Glow(Color c) { return ColMat(c, true); }
        Transform Pivot(Transform parent, string name, float x, float y, float z)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = new Vector3(x, y, z); return t;
        }

        Rig BuildHuman(string kind)
        {
            string baseKind = kind.StartsWith("researcher") ? "researcher" : kind;
            Spec sp; if (!SPECS.TryGetValue(baseKind, out sp)) sp = SPECS["researcher"];
            string hairHex = sp.hair, tieHex = sp.tie, skinHex = sp.skin;
            string[] look; if (LOOKS.TryGetValue(kind, out look)) { hairHex = look[0]; tieHex = look[1]; skinHex = look[2]; }

            var r = new Rig();
            r.root = new GameObject("char3d_" + kind).transform; r.root.SetParent(dynRoot, false);
            var prefab = Resources.Load<GameObject>("Models/char_" + baseKind);
            if (prefab != null) { Instantiate(prefab, r.root); return r; }

            Material skin = M(skinHex), top = M(sp.top), sleeve = M(sp.sleeve), pants = M(sp.pants), shoe = M(sp.shoe), dark = M("#14121a"), white = M("#f4f6f8");
            Material handM = sp.hand != null ? M(sp.hand) : skin;
            Material gold = M("#e2b23a");
            // 다리
            r.legL = Pivot(r.root, "legL", -sp.hipGap, sp.legLen, 0); r.legR = Pivot(r.root, "legR", sp.hipGap, sp.legLen, 0);
            foreach (var leg in new[] { r.legL, r.legR })
            {
                Part(leg, pants, 0, -sp.legLen, 0, sp.legW, sp.legLen, sp.legW * 1.1f);
                Part(leg, shoe, 0, -sp.legLen, 0.03f, sp.legW * 1.15f, 0.08f, sp.legW * 1.6f);
            }
            // 몸통
            r.body = Pivot(r.root, "body", 0, sp.legLen, 0);
            Part(r.body, top, 0, 0, 0, sp.torsoW, sp.torsoH, sp.torsoD);
            if (sp.belly > 0) Part(r.body, top, 0, 0.02f, sp.torsoD / 2 - 0.03f, sp.torsoW * 0.85f, sp.torsoH * 0.7f, sp.belly + 0.06f);
            float frontZ = sp.torsoD / 2 + sp.belly * 0.5f + 0.004f;
            // 머리 (크게)
            float hs = sp.head;
            r.head = Pivot(r.body, "head", 0, sp.torsoH - 0.02f, 0);
            Part(r.head, skin, 0, 0, 0, hs, hs * 0.92f, hs * 0.9f);
            float fz = hs * 0.45f + 0.004f, hh = hs * 0.92f;
            // 눈 (작고 동그란 느낌의 세로 점)
            if (baseKind != "soldier")
            {
                Part(r.head, dark, -hs * 0.17f, hh * 0.36f, fz, 0.045f, 0.065f, 0.015f);
                Part(r.head, dark, hs * 0.17f, hh * 0.36f, fz, 0.045f, 0.065f, 0.015f);
            }
            // 팔
            float ax = sp.torsoW / 2 + sp.armW / 2 + 0.005f, ay = sp.torsoH - 0.04f;
            r.armL = Pivot(r.body, "armL", -ax, ay, 0); r.armR = Pivot(r.body, "armR", ax, ay, 0);
            foreach (var arm in new[] { r.armL, r.armR })
            {
                Part(arm, sleeve, 0, -sp.armLen, 0, sp.armW, sp.armLen, sp.armW * 1.05f);
                Part(arm, handM, 0, -sp.armLen - 0.08f, 0, sp.armW * 1.05f, 0.09f, sp.armW * 1.05f);
            }
            float hand = -sp.armLen - 0.08f;
            r.height = sp.legLen + sp.torsoH + hh;

            switch (baseKind)
            {
                case "cleaner":
                {
                    var cap = M("#4a7cc0"); var capD = M("#335a92");
                    Part(r.head, M(hairHex), 0, hh * 0.25f, -hs * 0.2f, hs + 0.02f, hh * 0.45f, hs * 0.55f);           // 뒷머리
                    Part(r.head, cap, 0, hh * 0.62f, -0.01f, hs + 0.04f, hh * 0.42f, hs * 0.94f);                         // 모자
                    Part(r.head, capD, 0, hh * 0.6f, hs * 0.45f + 0.06f, hs * 0.9f, 0.04f, 0.16f);                      // 챙
                    Part(r.body, M("#2f5890"), 0, sp.torsoH * 0.45f, frontZ, 0.03f, sp.torsoH * 0.5f, 0.01f);          // 지퍼선
                    Part(r.body, white, 0.01f, sp.torsoH * 0.82f, frontZ, 0.025f, 0.025f, 0.01f);
                    // 대걸레: 오른손에, 걸레 머리는 바닥 쪽
                    Part(r.armR, M("#9a7448"), 0.02f, hand - 0.18f, 0.08f, 0.035f, 0.95f, 0.035f);
                    Part(r.armR, M("#c8ccd2"), 0.02f, hand - 0.3f, 0.12f, 0.24f, 0.14f, 0.16f);
                    Part(r.armR, M("#9ea4ac"), 0.02f, hand - 0.32f, 0.12f, 0.28f, 0.04f, 0.2f);
                    r.armSwingR = 0.2f;
                    break;
                }
                case "researcher": case "senior":
                {
                    bool senior = baseKind == "senior";
                    var hairM = M(hairHex);
                    // 가운 자락 + 넥타이 + 앞섶
                    Part(r.body, top, 0, -0.14f, 0, sp.torsoW + 0.04f, 0.16f, sp.torsoD + 0.04f);
                    Part(r.body, M("#2a3a2e"), 0, 0.02f, frontZ, 0.1f, sp.torsoH - 0.06f, 0.01f);                       // 안쪽 셔츠(어두운)
                    Part(r.body, M(tieHex), 0, 0.06f, frontZ + 0.004f, 0.05f, sp.torsoH - 0.12f, 0.01f);               // 넥타이
                    Part(r.body, M("#b8c2ca"), -0.07f, -0.14f, sp.torsoD / 2 + 0.024f, 0.012f, sp.torsoH + 0.12f, 0.01f);   // 가운 앞선
                    Part(r.body, M("#b8c2ca"), 0.07f, -0.14f, sp.torsoD / 2 + 0.024f, 0.012f, sp.torsoH + 0.12f, 0.01f);
                    if (senior)
                    {
                        // 단정하게 빗어 넘긴 흰머리 + 금 견장 + 금 배지
                        Part(r.head, hairM, 0, hh * 0.68f, -0.02f, hs + 0.03f, hh * 0.36f, hs * 0.92f);
                        Part(r.head, hairM, -hs * 0.48f, hh * 0.3f, -0.02f, 0.04f, hh * 0.45f, hs * 0.7f);
                        Part(r.head, hairM, hs * 0.48f, hh * 0.3f, -0.02f, 0.04f, hh * 0.45f, hs * 0.7f);
                        Part(r.head, hairM, 0, hh * 0.25f, -hs * 0.45f - 0.01f, hs * 0.94f, hh * 0.5f, 0.04f);
                        Part(r.body, gold, -sp.torsoW / 2 + 0.06f, sp.torsoH - 0.02f, 0, 0.12f, 0.035f, 0.16f);
                        Part(r.body, gold, sp.torsoW / 2 - 0.06f, sp.torsoH - 0.02f, 0, 0.12f, 0.035f, 0.16f);
                        Part(r.body, gold, sp.torsoW * 0.28f, sp.torsoH * 0.55f, sp.torsoD / 2 + 0.028f, 0.05f, 0.05f, 0.01f);
                    }
                    else
                    {
                        // 부스스한 큰 머리 (덩어리 몇 개를 삐죽삐죽)
                        Part(r.head, hairM, 0, hh * 0.62f, -0.02f, hs + 0.08f, hh * 0.46f, hs + 0.04f);
                        Part(r.head, hairM, 0, hh * 0.2f, -hs * 0.42f, hs + 0.08f, hh * 0.6f, hs * 0.2f);
                        Part(r.head, hairM, -hs * 0.52f, hh * 0.3f, -0.02f, 0.07f, hh * 0.5f, hs * 0.8f);
                        Part(r.head, hairM, hs * 0.52f, hh * 0.3f, -0.02f, 0.07f, hh * 0.5f, hs * 0.8f);
                        float[,] spikes = { { -0.14f, 0.1f }, { 0.04f, -0.06f }, { 0.16f, 0.08f }, { -0.04f, 0.14f }, { 0.12f, -0.14f }, { -0.18f, -0.1f } };
                        for (int i = 0; i < spikes.GetLength(0); i++)
                            Part(r.head, hairM, spikes[i, 0], hh * 1.04f, spikes[i, 1], 0.09f, 0.08f + 0.03f * (i % 2), 0.09f);
                        Part(r.head, hairM, -0.06f, hh * 0.84f, fz - 0.02f, 0.16f, 0.08f, 0.06f);                     // 앞머리
                        // 안경
                        var gl = M("#d8e8f0");
                        Part(r.head, gl, -hs * 0.17f, hh * 0.3f, fz + 0.006f, 0.12f, 0.09f, 0.012f);
                        Part(r.head, gl, hs * 0.17f, hh * 0.3f, fz + 0.006f, 0.12f, 0.09f, 0.012f);
                        Part(r.head, dark, -hs * 0.17f, hh * 0.33f, fz + 0.014f, 0.04f, 0.04f, 0.005f);
                        Part(r.head, dark, hs * 0.17f, hh * 0.33f, fz + 0.014f, 0.04f, 0.04f, 0.005f);
                    }
                    break;
                }
                case "guard":
                {
                    var cap = M("#1a2654");
                    Part(r.head, M(hairHex), 0, hh * 0.2f, -hs * 0.3f, hs + 0.02f, hh * 0.4f, hs * 0.4f);
                    Part(r.head, cap, 0, hh * 0.66f, -0.02f, hs + 0.08f, hh * 0.3f, hs + 0.04f);                        // 모자 (넓적)
                    Part(r.head, M("#0e1636"), 0, hh * 0.62f, hs * 0.45f + 0.05f, hs * 0.92f, 0.04f, 0.14f);             // 챙
                    Part(r.head, gold, 0, hh * 0.78f, hs * 0.47f + 0.025f, 0.06f, 0.06f, 0.01f);                         // 모자 배지
                    Part(r.body, gold, -sp.torsoW / 2 + 0.07f, sp.torsoH - 0.02f, 0, 0.14f, 0.035f, 0.18f);              // 금 견장
                    Part(r.body, gold, sp.torsoW / 2 - 0.07f, sp.torsoH - 0.02f, 0, 0.14f, 0.035f, 0.18f);
                    Part(r.body, gold, sp.torsoW * 0.24f, sp.torsoH * 0.62f, frontZ, 0.06f, 0.06f, 0.01f);               // 가슴 배지
                    Part(r.body, M(tieHex), 0, sp.torsoH * 0.35f, frontZ, 0.05f, sp.torsoH * 0.55f, 0.01f);              // 넥타이
                    Part(r.body, M("#0e1430"), 0, 0.02f, 0, sp.torsoW + 0.01f, 0.05f, sp.torsoD + sp.belly + 0.02f);    // 벨트
                    Part(r.body, M("#9aa0a8"), 0, 0.02f, frontZ + 0.02f, 0.07f, 0.05f, 0.01f);                           // 버클
                    // 손전등 (오른손)
                    Part(r.armR, M("#34363c"), 0, hand - 0.03f, 0.08f, 0.06f, 0.06f, 0.18f);
                    Part(r.armR, Glow(new Color(1f, 0.9f, 0.4f)), 0, hand - 0.03f, 0.175f, 0.065f, 0.065f, 0.02f);
                    break;
                }
                case "soldier":
                {
                    var g = M("#4e7a38"); var gd = M("#33502a"); var gl = M("#6a9e48");
                    // 두꺼운 어깨 보호대 + 조끼
                    Part(r.body, g, -sp.torsoW / 2 - 0.04f, sp.torsoH - 0.2f, 0, 0.22f, 0.22f, 0.32f);
                    Part(r.body, g, sp.torsoW / 2 + 0.04f, sp.torsoH - 0.2f, 0, 0.22f, 0.22f, 0.32f);
                    Part(r.body, gd, 0, 0.06f, sp.torsoD / 2 + 0.01f, sp.torsoW * 0.8f, sp.torsoH * 0.7f, 0.04f);       // 조끼 판
                    Part(r.body, M("#24381c"), 0, sp.torsoH * 0.3f, sp.torsoD / 2 + 0.032f, sp.torsoW * 0.5f, 0.05f, 0.01f);
                    Part(r.body, M("#24381c"), 0, 0.02f, 0, sp.torsoW + 0.02f, 0.06f, sp.torsoD + 0.02f);                // 벨트
                    foreach (var leg in new[] { r.legL, r.legR }) Part(leg, gd, 0, -sp.legLen * 0.6f, 0.02f, sp.legW + 0.02f, 0.08f, sp.legW * 1.2f);
                    // 헬멧 + 초록 고글, 얼굴은 어둡게
                    Part(r.head, g, 0, hh * 0.5f, -0.02f, hs + 0.08f, hh * 0.56f, hs + 0.06f);
                    Part(r.head, gl, -hs * 0.18f, hh * 1.02f, hs * 0.1f, 0.08f, 0.04f, 0.08f);                           // 헬멧 반짝
                    Part(r.head, M("#1a2214"), 0, hh * 0.38f, fz + 0.01f, hs * 0.86f, 0.1f, 0.03f);                      // 고글 띠
                    Part(r.head, Glow(new Color(0.45f, 1f, 0.35f)), -hs * 0.17f, hh * 0.39f, fz + 0.03f, 0.1f, 0.08f, 0.01f);
                    Part(r.head, Glow(new Color(0.45f, 1f, 0.35f)), hs * 0.17f, hh * 0.39f, fz + 0.03f, 0.1f, 0.08f, 0.01f);
                    r.swingK = 0.7f;   // 무거워서 팔을 덜 흔듦
                    break;
                }
                case "director":
                {
                    var hairM = M(hairHex);
                    Part(r.head, hairM, 0, hh * 0.7f, -0.02f, hs + 0.03f, hh * 0.32f, hs * 0.92f);                       // 흰머리
                    Part(r.head, hairM, -hs * 0.48f, hh * 0.3f, -0.03f, 0.04f, hh * 0.5f, hs * 0.75f);
                    Part(r.head, hairM, hs * 0.48f, hh * 0.3f, -0.03f, 0.04f, hh * 0.5f, hs * 0.75f);
                    Part(r.head, hairM, 0, hh * 0.25f, -hs * 0.45f - 0.01f, hs * 0.94f, hh * 0.55f, 0.04f);
                    Part(r.body, M(tieHex), 0, sp.torsoH * 0.55f, frontZ, 0.12f, sp.torsoH * 0.4f, 0.01f);               // 흰 셔츠
                    Part(r.body, dark, 0, sp.torsoH * 0.5f, frontZ + 0.004f, 0.04f, sp.torsoH * 0.42f, 0.01f);           // 넥타이
                    Part(r.body, gold, -sp.torsoW / 2 + 0.06f, sp.torsoH - 0.02f, 0, 0.1f, 0.03f, 0.14f);               // 금 견장
                    Part(r.body, gold, sp.torsoW / 2 - 0.06f, sp.torsoH - 0.02f, 0, 0.1f, 0.03f, 0.14f);
                    break;
                }
            }
            return r;
        }

        // 걷기: 다리·팔 반대로 흔들기 + 몸 통통 + 좌우 기우뚱 (참고 영상 느낌)
        static void AnimateHuman(Rig r, float ph, float amt)
        {
            if (r.legL == null) return;
            float sn = Mathf.Sin(ph), sw = sn * 32f * amt;
            r.legL.localRotation = Quaternion.Euler(sw, 0, 0); r.legR.localRotation = Quaternion.Euler(-sw, 0, 0);
            r.armL.localRotation = Quaternion.Euler(-sw * 0.9f * r.swingK, 0, -6f * amt); r.armR.localRotation = Quaternion.Euler(sw * 0.9f * r.swingK * r.armSwingR, 0, 6f * amt);
            float bob = Mathf.Abs(sn) * 0.05f * amt;
            var b = r.body.localPosition; b.y = r.legL.localPosition.y + bob; r.body.localPosition = b;
            r.body.localRotation = Quaternion.Euler(3f * amt, sn * 5f * amt, sn * 4f * amt);
            float sq = 1 + (Mathf.Abs(sn) - 0.5f) * 0.06f * amt;   // 디딜 때 눌림, 들 때 늘어남
            r.body.localScale = new Vector3(2 - sq, sq, 2 - sq);
            r.head.localRotation = Quaternion.Euler(0, 0, -sn * 3f * amt);
        }

        // ---------------------------------------------------------------- 풀
        readonly Dictionary<string, List<Rig>> rigs = new Dictionary<string, List<Rig>>();
        readonly Dictionary<string, int> rigUsed = new Dictionary<string, int>();
        Rig playerHuman; string playerHumanKind;
        void BeginRigs() { var keys = new List<string>(rigUsed.Keys); foreach (var k in keys) rigUsed[k] = 0; }
        Rig NpcRig(string kind)
        {
            List<Rig> list; if (!rigs.TryGetValue(kind, out list)) { list = new List<Rig>(); rigs[kind] = list; }
            int used; rigUsed.TryGetValue(kind, out used); rigUsed[kind] = used + 1;
            while (list.Count <= used) list.Add(BuildHuman(kind));
            var r = list[used]; r.root.gameObject.SetActive(true); return r;
        }
        void EndRigs()
        {
            foreach (var kv in rigs)
            {
                int used; rigUsed.TryGetValue(kv.Key, out used);
                for (int i = used; i < kv.Value.Count; i++) if (kv.Value[i].root.gameObject.activeSelf) kv.Value[i].root.gameObject.SetActive(false);
            }
        }
        // 게임 각도(0=오른쪽, 아래가 +) → 모형이 +z를 앞으로 볼 때의 회전
        static Quaternion Facing(float face) { return Quaternion.Euler(0, 90f + face * Mathf.Rad2Deg, 0); }
    }
}
