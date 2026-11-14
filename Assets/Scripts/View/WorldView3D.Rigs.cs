// 2.5D 로우폴리 캐릭터: 상자·기둥을 조립한 사람들과 짧은 지렁이 주인공. 걷기·기어가기·먹기 동작은 코드로.
// 참고 그림의 체형을 따른다: 키다리 청소부(대걸레), 작은 연구원(가운·서류·커피), 배 나온 경비원(손전등·도넛),
// 거대한 군인(팔짱·초록 고글), 정장 소장(콧수염·서류가방).
// 나중에 진짜 3D 모델이 생기면 Resources/Models/char_<종류> 프리팹을 넣으면 그걸 쓴다 (걷기 동작은 직접 넣어야 함).
using System.Collections.Generic;
using UnityEngine;

namespace Specimen07
{
    public partial class WorldView3D
    {
        class Rig
        {
            public Transform root, body, head, armL, armR, legL, legR, jawU;
            public Transform[] segs;          // 지렁이 몸 마디 (머리 제외)
            public bool crossed;              // 팔짱 (팔 안 흔듦)
            public float armSwingR = 1, height = 1.6f;
        }
        class Spec
        {
            public float legLen, legW, hipGap, torsoW, torsoH, torsoD, head, armLen, armW, belly;
            public string skin, top, sleeve, pants, shoe, hair;
        }
        static readonly Dictionary<string, Spec> SPECS = new Dictionary<string, Spec>
        {
            { "cleaner",     new Spec { legLen = 0.78f, legW = 0.11f, hipGap = 0.09f, torsoW = 0.34f, torsoH = 0.5f,  torsoD = 0.2f,  head = 0.25f, armLen = 0.66f, armW = 0.08f, belly = 0,     skin = "#d69c78", top = "#61809c", sleeve = "#d69c78", pants = "#61809c", shoe = "#5a3d27", hair = null } },
            { "researcher",  new Spec { legLen = 0.42f, legW = 0.12f, hipGap = 0.07f, torsoW = 0.36f, torsoH = 0.42f, torsoD = 0.24f, head = 0.3f,  armLen = 0.4f,  armW = 0.1f,  belly = 0,     skin = "#d69c78", top = "#e8edf1", sleeve = "#e8edf1", pants = "#4e3a2e", shoe = "#2a2626", hair = "#704a2a" } },
            { "researcher2", new Spec { legLen = 0.44f, legW = 0.12f, hipGap = 0.07f, torsoW = 0.36f, torsoH = 0.44f, torsoD = 0.24f, head = 0.29f, armLen = 0.42f, armW = 0.1f,  belly = 0,     skin = "#a87052", top = "#dde6ee", sleeve = "#dde6ee", pants = "#3a3a46", shoe = "#2a2626", hair = "#24202a" } },
            { "senior",      new Spec { legLen = 0.48f, legW = 0.12f, hipGap = 0.07f, torsoW = 0.38f, torsoH = 0.46f, torsoD = 0.26f, head = 0.28f, armLen = 0.44f, armW = 0.1f,  belly = 0.05f, skin = "#d69c78", top = "#f0f2f6", sleeve = "#f0f2f6", pants = "#3e3a4a", shoe = "#1e1c22", hair = "#b4b4bc" } },
            { "guard",       new Spec { legLen = 0.5f,  legW = 0.15f, hipGap = 0.1f,  torsoW = 0.5f,  torsoH = 0.52f, torsoD = 0.42f, head = 0.3f,  armLen = 0.48f, armW = 0.13f, belly = 0.16f, skin = "#dca883", top = "#30406f", sleeve = "#30406f", pants = "#2a385f", shoe = "#18181c", hair = "#3a2a24" } },
            { "soldier",     new Spec { legLen = 0.72f, legW = 0.17f, hipGap = 0.1f,  torsoW = 0.58f, torsoH = 0.62f, torsoD = 0.34f, head = 0.27f, armLen = 0.58f, armW = 0.16f, belly = 0,     skin = "#b47e5c", top = "#5a7842", sleeve = "#5a7842", pants = "#56723f", shoe = "#323a22", hair = null } },
            { "director",    new Spec { legLen = 0.55f, legW = 0.14f, hipGap = 0.08f, torsoW = 0.46f, torsoH = 0.55f, torsoD = 0.36f, head = 0.28f, armLen = 0.52f, armW = 0.12f, belly = 0.12f, skin = "#d69c78", top = "#2a2a33", sleeve = "#2a2a33", pants = "#24242c", shoe = "#121214", hair = "#7a5c44" } },
        };

        Material M(string hex) { return ColMat(Data.Hex(hex)); }
        Material Glow(Color c) { return ColMat(c, true); }
        Transform Pivot(Transform parent, string name, float x, float y, float z)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = new Vector3(x, y, z); return t;
        }

        // ---------------------------------------------------------------- 사람
        Rig BuildHuman(string kind)
        {
            Spec sp; if (!SPECS.TryGetValue(kind, out sp)) { sp = SPECS["researcher"]; }
            var r = new Rig();
            r.root = new GameObject("char3d_" + kind).transform; r.root.SetParent(dynRoot, false);
            var prefab = Resources.Load<GameObject>("Models/char_" + kind);
            if (prefab != null) { Instantiate(prefab, r.root); return r; }

            Material skin = M(sp.skin), top = M(sp.top), sleeve = M(sp.sleeve), pants = M(sp.pants), shoe = M(sp.shoe), dark = M("#16141a"), white = M("#f2f4f6");
            // 다리 (엉덩이에 매달림)
            r.legL = Pivot(r.root, "legL", -sp.hipGap, sp.legLen, 0); r.legR = Pivot(r.root, "legR", sp.hipGap, sp.legLen, 0);
            foreach (var leg in new[] { r.legL, r.legR })
            {
                Part(leg, pants, 0, -sp.legLen, 0, sp.legW, sp.legLen, sp.legW * 1.1f);
                Part(leg, shoe, 0, -sp.legLen, 0.03f, sp.legW * 1.15f, 0.08f, sp.legW * 1.7f);
            }
            // 몸통
            r.body = Pivot(r.root, "body", 0, sp.legLen, 0);
            Part(r.body, top, 0, 0, 0, sp.torsoW, sp.torsoH, sp.torsoD);
            if (sp.belly > 0) Part(r.body, top, 0, 0.04f, sp.torsoD / 2 - 0.02f, sp.torsoW * 0.82f, sp.torsoH * 0.62f, sp.belly + 0.04f);
            // 머리
            float hs = sp.head;
            r.head = Pivot(r.body, "head", 0, sp.torsoH + 0.03f, 0);
            Part(r.head, skin, 0, -0.04f, 0, hs * 0.45f, 0.06f, hs * 0.45f);      // 목
            Part(r.head, skin, 0, 0, 0, hs, hs, hs);
            float fz = hs / 2 + 0.005f;                                          // 얼굴 앞면
            Part(r.head, dark, -hs * 0.2f, hs * 0.5f, fz, 0.035f, 0.045f, 0.02f); Part(r.head, dark, hs * 0.2f, hs * 0.5f, fz, 0.035f, 0.045f, 0.02f);   // 눈
            // 팔 (어깨에 매달림)
            float ax = sp.torsoW / 2 + sp.armW / 2 + 0.005f, ay = sp.torsoH - 0.03f;
            r.armL = Pivot(r.body, "armL", -ax, ay, 0); r.armR = Pivot(r.body, "armR", ax, ay, 0);
            foreach (var arm in new[] { r.armL, r.armR })
            {
                Part(arm, sleeve, 0, -sp.armLen, 0, sp.armW, sp.armLen, sp.armW);
                Part(arm, skin, 0, -sp.armLen - 0.07f, 0, sp.armW * 0.9f, 0.07f, sp.armW * 0.9f);
            }
            float hand = -sp.armLen - 0.07f;
            r.height = sp.legLen + sp.torsoH + hs + 0.05f;

            switch (kind)
            {
                case "cleaner":
                {
                    // 멜빵바지: 가슴판 + 끈, 어깨는 맨살
                    Part(r.body, skin, 0, sp.torsoH - 0.12f, 0, sp.torsoW + 0.002f, 0.12f, sp.torsoD + 0.002f);
                    Part(r.body, top, -0.09f, sp.torsoH - 0.12f, 0, 0.05f, 0.122f, sp.torsoD + 0.006f); Part(r.body, top, 0.09f, sp.torsoH - 0.12f, 0, 0.05f, 0.122f, sp.torsoD + 0.006f);
                    Part(r.body, M("#d9b456"), -0.09f, sp.torsoH - 0.15f, sp.torsoD / 2 + 0.004f, 0.03f, 0.03f, 0.01f); Part(r.body, M("#d9b456"), 0.09f, sp.torsoH - 0.15f, sp.torsoD / 2 + 0.004f, 0.03f, 0.03f, 0.01f);
                    Part(r.head, M("#2a2d33"), 0, -0.07f, 0, hs * 0.75f, 0.05f, hs * 0.75f);            // 목에 건 헤드폰
                    Part(r.head, M("#1b1d21"), -hs * 0.36f, -0.1f, 0.02f, 0.06f, 0.08f, 0.08f); Part(r.head, M("#1b1d21"), hs * 0.36f, -0.1f, 0.02f, 0.06f, 0.08f, 0.08f);
                    Part(r.head, M("#a8705a"), 0, hs * 0.72f, fz, 0.06f, 0.015f, 0.01f);                 // 처진 눈썹 느낌
                    Part(r.head, M("#7a4a3a"), 0, hs * 0.22f, fz, 0.08f, 0.02f, 0.01f);                  // 입
                    // 대걸레: 오른손에 세워 듦 (팔은 덜 흔듦)
                    Part(r.armR, M("#9a7448"), 0, hand - 0.05f, 0.07f, 0.035f, 1.55f, 0.035f);
                    Part(r.armR, M("#646b74"), 0, hand + 1.48f, 0.07f, 0.1f, 0.05f, 0.1f);
                    Part(r.armR, M("#c4b685"), 0, hand + 1.5f, 0.07f, 0.24f, 0.32f, 0.24f);
                    Part(r.armR, M("#e6dab0"), -0.06f, hand + 1.52f, 0.13f, 0.08f, 0.3f, 0.06f);
                    r.armSwingR = 0.25f;
                    break;
                }
                case "researcher": case "researcher2": case "senior":
                {
                    // 가운 자락 (다리 위를 덮음)
                    Part(r.body, top, 0, -0.26f, 0, sp.torsoW + 0.05f, 0.28f, sp.torsoD + 0.05f);
                    Part(r.body, M("#a7b2bc"), 0, -0.26f, sp.torsoD / 2 + 0.027f, 0.03f, sp.torsoH + 0.24f, 0.01f);   // 앞섶
                    if (kind == "senior") { Part(r.body, M("#b48cf0"), -sp.torsoW / 2 - 0.02f, -0.26f, 0, 0.012f, sp.torsoH + 0.26f, sp.torsoD + 0.052f); Part(r.body, M("#b48cf0"), sp.torsoW / 2 + 0.02f, -0.26f, 0, 0.012f, sp.torsoH + 0.26f, sp.torsoD + 0.052f); }
                    // 머리카락
                    var hairM = M(sp.hair);
                    Part(r.head, hairM, 0, hs * 0.72f, -0.01f, hs + 0.04f, hs * 0.34f, hs + 0.04f);
                    Part(r.head, hairM, 0, hs * 0.25f, -hs * 0.4f, hs + 0.04f, hs * 0.5f, hs * 0.25f);
                    if (kind == "researcher")   // 부스스한 머리
                    {
                        Part(r.head, hairM, -0.08f, hs, 0.02f, 0.07f, 0.06f, 0.07f); Part(r.head, hairM, 0.06f, hs + 0.01f, -0.05f, 0.08f, 0.07f, 0.08f);
                        Part(r.head, hairM, hs * 0.5f, hs * 0.6f, 0.02f, 0.06f, 0.06f, 0.07f); Part(r.head, hairM, -hs * 0.5f, hs * 0.55f, -0.03f, 0.06f, 0.07f, 0.06f);
                    }
                    // 안경
                    var gl = M("#9fd2ee");
                    Part(r.head, gl, -hs * 0.2f, hs * 0.44f, fz + 0.005f, 0.09f, 0.07f, 0.012f); Part(r.head, gl, hs * 0.2f, hs * 0.44f, fz + 0.005f, 0.09f, 0.07f, 0.012f);
                    Part(r.head, M("#7a5a48"), 0, hs * 0.2f, fz, 0.05f, 0.015f, 0.01f);
                    if (kind != "researcher2")   // 가슴에 안은 서류
                    {
                        Part(r.body, white, -0.02f, sp.torsoH * 0.3f, sp.torsoD / 2 + 0.04f, 0.22f, 0.16f, 0.07f);
                        Part(r.body, M("#b4bcc6"), -0.02f, sp.torsoH * 0.3f + 0.05f, sp.torsoD / 2 + 0.076f, 0.2f, 0.012f, 0.005f);
                        Part(r.body, M("#b4bcc6"), -0.02f, sp.torsoH * 0.3f + 0.1f, sp.torsoD / 2 + 0.076f, 0.2f, 0.012f, 0.005f);
                    }
                    if (kind != "senior")       // 커피
                    {
                        Cyl(r.armL, M("#6a4428"), 0, hand - 0.08f, 0.06f, 0.05f, 0.11f);
                        Cyl(r.armL, M("#e9e2d6"), 0, hand + 0.025f, 0.06f, 0.045f, 0.01f);
                    }
                    break;
                }
                case "guard":
                {
                    var cap = M("#222d52");
                    Part(r.head, cap, 0, hs - 0.03f, 0, hs + 0.05f, 0.11f, hs + 0.05f);
                    Part(r.head, M("#121828"), 0, hs - 0.03f, hs / 2 + 0.06f, hs + 0.04f, 0.025f, 0.12f);   // 챙
                    Part(r.head, M("#cfa23a"), 0, hs + 0.01f, hs / 2 + 0.03f, 0.05f, 0.05f, 0.01f);         // 모자 배지
                    Part(r.head, M("#8a4a44"), 0, hs * 0.22f, fz, 0.05f, 0.04f, 0.01f);                      // 입 (오)
                    Part(r.head, skin, 0, -0.02f, 0.02f, hs * 0.8f, 0.06f, hs * 0.8f);                       // 두 턱
                    var gold = M("#cfa23a");
                    Part(r.body, gold, -sp.torsoW / 2 + 0.06f, sp.torsoH - 0.03f, 0, 0.1f, 0.03f, 0.14f); Part(r.body, gold, sp.torsoW / 2 - 0.06f, sp.torsoH - 0.03f, 0, 0.1f, 0.03f, 0.14f);   // 견장
                    Part(r.body, M("#151824"), 0, 0, 0, sp.torsoW + 0.01f, 0.06f, sp.torsoD + sp.belly * 0.6f);   // 벨트
                    // 손전등 (오른손)
                    Part(r.armR, M("#3a3d44"), 0, hand - 0.03f, 0.08f, 0.06f, 0.06f, 0.2f);
                    Part(r.armR, Glow(new Color(1f, 0.95f, 0.6f)), 0, hand - 0.03f, 0.185f, 0.065f, 0.065f, 0.02f);
                    // 도넛 (왼손)
                    Cyl(r.armL, M("#ec7c9e"), 0, hand - 0.04f, 0.07f, 0.08f, 0.035f);
                    Cyl(r.armL, M("#6a3a2a"), 0, hand - 0.005f, 0.07f, 0.03f, 0.002f);
                    break;
                }
                case "soldier":
                {
                    var g = M("#5a7842"); var gd = M("#3a5230");
                    // 거대한 어깨
                    Part(r.body, g, -sp.torsoW / 2 - 0.05f, sp.torsoH - 0.24f, 0, 0.26f, 0.26f, 0.32f);
                    Part(r.body, g, sp.torsoW / 2 + 0.05f, sp.torsoH - 0.24f, 0, 0.26f, 0.26f, 0.32f);
                    // 팔짱 (팔 대신 가슴 앞을 가로지르는 두 팔뚝)
                    r.armL.gameObject.SetActive(false); r.armR.gameObject.SetActive(false); r.crossed = true;
                    Part(r.body, g, 0, sp.torsoH * 0.5f, sp.torsoD / 2 + 0.06f, sp.torsoW + 0.16f, 0.13f, 0.14f);
                    Part(r.body, g, 0, sp.torsoH * 0.5f + 0.12f, sp.torsoD / 2 + 0.07f, sp.torsoW + 0.1f, 0.12f, 0.13f);
                    Part(r.body, gd, -sp.torsoW / 2 - 0.06f, sp.torsoH * 0.5f + 0.02f, sp.torsoD / 2 + 0.04f, 0.12f, 0.16f, 0.14f);   // 주먹·팔꿈치
                    Part(r.body, gd, sp.torsoW / 2 + 0.06f, sp.torsoH * 0.5f + 0.1f, sp.torsoD / 2 + 0.04f, 0.12f, 0.16f, 0.14f);
                    Part(r.body, gd, 0, 0, 0, sp.torsoW + 0.02f, 0.08f, sp.torsoD + 0.02f);                 // 벨트
                    Part(r.body, gd, sp.torsoW / 2 + 0.02f, -0.12f, 0, 0.08f, 0.16f, 0.12f);                // 권총집
                    foreach (var leg in new[] { r.legL, r.legR }) Part(leg, gd, 0, -sp.legLen * 0.55f, 0.02f, sp.legW + 0.02f, 0.1f, sp.legW * 1.2f);   // 무릎 보호대
                    // 헬멧 + 초록 고글
                    Part(r.head, g, 0, hs * 0.55f, -0.01f, hs + 0.07f, hs * 0.55f, hs + 0.08f);
                    Part(r.head, M("#2a3a20"), 0, hs * 1.1f, 0.02f, 0.04f, 0.06f, 0.04f);
                    Part(r.head, M("#1c2418"), 0, hs * 0.48f, fz + 0.01f, hs + 0.04f, 0.08f, 0.03f);
                    Part(r.head, Glow(new Color(0.55f, 1f, 0.55f)), -hs * 0.22f, hs * 0.49f, fz + 0.03f, 0.08f, 0.06f, 0.01f);
                    Part(r.head, Glow(new Color(0.55f, 1f, 0.55f)), hs * 0.22f, hs * 0.49f, fz + 0.03f, 0.08f, 0.06f, 0.01f);
                    Part(r.head, M("#86583e"), 0, hs * 0.18f, fz, 0.1f, 0.015f, 0.01f);
                    break;
                }
                case "director":
                {
                    Part(r.body, white, 0, sp.torsoH - 0.2f, sp.torsoD / 2 + 0.004f, 0.13f, 0.2f, 0.01f);          // 셔츠
                    Part(r.body, M("#1a1414"), 0, sp.torsoH - 0.24f, sp.torsoD / 2 + 0.01f, 0.04f, 0.22f, 0.01f);  // 넥타이
                    var gold = M("#cfa23a");
                    Part(r.body, gold, -0.1f, 0.04f, sp.torsoD / 2 + sp.belly * 0.5f + 0.01f, 0.02f, sp.torsoH - 0.24f, 0.01f);   // 금색 테
                    Part(r.body, gold, 0.1f, 0.04f, sp.torsoD / 2 + sp.belly * 0.5f + 0.01f, 0.02f, sp.torsoH - 0.24f, 0.01f);
                    Part(r.body, gold, 0, 0, 0, sp.torsoW + 0.01f, 0.025f, sp.torsoD + sp.belly + 0.01f);
                    foreach (var arm in new[] { r.armL, r.armR }) Part(arm, gold, 0, -sp.armLen + 0.02f, 0, sp.armW + 0.01f, 0.025f, sp.armW + 0.01f);   // 소매 끝
                    Part(r.head, M("#1a1414"), 0, hs * 0.26f, fz + 0.005f, hs * 0.6f, 0.045f, 0.02f);              // 콧수염
                    Part(r.head, M(sp.hair), -hs / 2 - 0.01f, hs * 0.3f, -0.02f, 0.03f, hs * 0.3f, hs * 0.7f);     // 옆머리
                    Part(r.head, M(sp.hair), hs / 2 + 0.01f, hs * 0.3f, -0.02f, 0.03f, hs * 0.3f, hs * 0.7f);
                    Part(r.head, M(sp.hair), 0, hs * 0.3f, -hs / 2 - 0.01f, hs * 0.9f, hs * 0.3f, 0.03f);
                    // 서류가방 (오른손), 열쇠 (왼손)
                    Part(r.armR, M("#282834"), 0, hand - 0.26f, 0.02f, 0.08f, 0.22f, 0.3f);
                    Part(r.armR, gold, 0.045f, hand - 0.12f, 0.02f, 0.005f, 0.03f, 0.05f);
                    Part(r.armL, gold, 0, hand - 0.12f, 0.02f, 0.015f, 0.08f, 0.015f);
                    break;
                }
            }
            return r;
        }

        // 걷기: 다리·팔을 반대로 흔들고 몸이 살짝 통통
        static void AnimateHuman(Rig r, float ph, float amt)
        {
            if (r.legL == null) return;
            float sw = Mathf.Sin(ph) * 30f * amt;
            r.legL.localRotation = Quaternion.Euler(sw, 0, 0); r.legR.localRotation = Quaternion.Euler(-sw, 0, 0);
            if (!r.crossed) { r.armL.localRotation = Quaternion.Euler(-sw * 0.8f, 0, 0); r.armR.localRotation = Quaternion.Euler(sw * 0.8f * r.armSwingR, 0, 0); }
            var b = r.body.localPosition; b.y = r.legL.localPosition.y + Mathf.Abs(Mathf.Sin(ph)) * 0.035f * amt; r.body.localPosition = b;
            r.body.localRotation = Quaternion.Euler(4f * amt, Mathf.Sin(ph) * 4f * amt, 0);
        }

        // ---------------------------------------------------------------- 지렁이 주인공
        // 2D 스프라이트 모양 그대로: 둥근 돔 머리 + 바닥에 깔리며 가늘어지는 꼬리, 앞면에 이빨 보이는 입,
        // 몸 아래쪽에 진한 줄무늬 2줄, 왼쪽 위 반짝이. 각진 로우폴리(면마다 평평한 음영).
        const float WH = 0.52f, WW = 0.29f, WZC = 0.06f, WFRONT = 0.3f, WTAIL = -0.66f;   // 높이, 반폭, 머리 중심 z, 앞 끝, 꼬리 끝
        static float WormH(float z)
        {
            if (z >= WZC) { float u = (z - WZC) / (WFRONT - WZC); return u >= 1 ? 0 : WH * Mathf.Sqrt(1 - u * u); }
            // 머리 뒤쪽은 둥글게 내려가고, 그 뒤는 바닥에 깔린 납작한 꼬리
            float B = WZC - 0.24f;
            if (z >= B) { float u = (WZC - z) / 0.24f; return 0.15f + (WH - 0.15f) * Mathf.Sqrt(Mathf.Max(0, 1 - u * u)); }
            float t = Mathf.Min(1, (B - z) / (B - WTAIL));
            return t < 1 ? 0.15f * (1 - t) + 0.03f * t : 0;
        }
        static float WormW(float z)
        {
            if (z >= WZC) { float u = (z - WZC) / (WFRONT - WZC + 0.02f); return u >= 1 ? 0 : WW * Mathf.Pow(1 - u * u, 0.45f); }
            float B = WZC - 0.24f;
            if (z >= B) { float u = (WZC - z) / 0.24f; return 0.22f + (WW - 0.22f) * Mathf.Sqrt(Mathf.Max(0, 1 - u * u)); }
            float t = Mathf.Min(1, (B - z) / (B - WTAIL));
            return t < 1 ? 0.22f * Mathf.Pow(1 - t, 0.8f) + 0.02f : 0;
        }
        // 단면 높이 비율 (0 = 바닥, 1 = 꼭대기). 1~2, 3~4 구간이 줄무늬
        static readonly float[] RING = { 0f, 0.14f, 0.25f, 0.35f, 0.46f, 0.64f, 0.83f, 0.96f, 1f };
        Mesh WormMesh(float[] zs, bool headPart, Material[] mats)
        {
            // mats: 0 몸, 1 줄무늬, 2 반짝이, 3 바닥 테두리
            var v = new List<Vector3>(); var tris = new List<int>[4]; for (int i = 0; i < 4; i++) tris[i] = new List<int>();
            int M = RING.Length;
            System.Func<float, int, Vector3> pt = (z, j) =>
            {
                // j: 0..2M-2, 오른쪽 바닥 → 꼭대기 → 왼쪽 바닥
                int k = j < M ? j : 2 * M - 2 - j; float sgn = j < M ? 1 : -1;
                float sy = RING[k], th = Mathf.Asin(sy);
                float w = WormW(z) * (k == 0 ? 1.08f : 1f), h = WormH(z);
                return new Vector3(sgn * w * Mathf.Cos(th), h * sy, z);
            };
            int J = 2 * M - 2;
            for (int a = 0; a < zs.Length - 1; a++)
                for (int j = 0; j < J; j++)
                {
                    Vector3 p0 = pt(zs[a], j), p1 = pt(zs[a], j + 1), p2 = pt(zs[a + 1], j + 1), p3 = pt(zs[a + 1], j);
                    int k = Mathf.Min(j < M ? j : J - j, j + 1 < M ? j + 1 : J - j - 1);   // 아래쪽 링 번호
                    int mat = 0;
                    if (k == 1 || k == 3) mat = 1;
                    if (k == 0) mat = 3;
                    float zm = (zs[a] + zs[a + 1]) / 2;
                    if (headPart && k >= 5 && j >= M - 1 && zm > -0.02f && zm < 0.2f && j <= M + 1) mat = 2;   // 왼쪽 위 반짝
                    AddTri(v, tris[mat], p0, p1, p2, new Vector3(0, 0.12f, zm));
                    AddTri(v, tris[mat], p0, p2, p3, new Vector3(0, 0.12f, zm));
                }
            var m = new Mesh(); m.SetVertices(v); m.subMeshCount = 4;
            for (int i = 0; i < 4; i++) m.SetTriangles(tris[i], i);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
        // 바깥(center에서 멀어지는 쪽)을 보게 감기 방향을 맞춘 삼각형 (면마다 꼭짓점을 따로 → 각진 음영)
        static void AddTri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 center)
        {
            if ((a - b).sqrMagnitude < 1e-8f || (b - c).sqrMagnitude < 1e-8f || (a - c).sqrMagnitude < 1e-8f) return;
            var n = Vector3.Cross(b - a, c - a); var mid = (a + b + c) / 3f;
            if (Vector3.Dot(n, mid - center) < 0) { var tmp = b; b = c; c = tmp; }   // 유니티는 보는 쪽에서 시계 방향이 앞면 (Cross가 바깥을 향해야 함)
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        Rig BuildWorm()
        {
            var r = new Rig();
            r.root = new GameObject("worm3d").transform; r.root.SetParent(dynRoot, false);
            var prefab = Resources.Load<GameObject>("Models/player_worm");
            if (prefab != null) { Instantiate(prefab, r.root); return r; }
            var mats = new[] { M("#9a68dc"), M("#6a40b4"), M("#c9a8f6"), M("#4e2c8c") };
            // 머리 (돔) — 숨쉬기·덮치기에 따라 크기가 변함
            r.head = Pivot(r.root, "head", 0, 0, 0);
            var hz = new[] { WFRONT, 0.29f, 0.26f, 0.21f, 0.15f, WZC, -0.04f, -0.11f };
            MeshObj(r.head, WormMesh(hz, true, mats), mats);
            // 꼬리 (좌우로 살랑) — 머리 뒤끝에 경첩
            var tail = Pivot(r.root, "tail", 0, 0, -0.11f);
            var tz = new[] { -0.11f, -0.18f, -0.26f, -0.36f, -0.48f, -0.58f, WTAIL };
            for (int i = 0; i < tz.Length; i++) tz[i] += 0.11f;   // 경첩 기준 좌표
            var tm = WormMeshOffset(tz, -0.11f, mats);
            MeshObj(tail, tm, mats);
            r.segs = new[] { tail };
            // 입: 앞면의 어두운 구멍 + 위쪽 이빨 + 분홍 혀 (먹을 때 크게 벌어짐)
            float my = 0.21f, mz = WZC + (WFRONT - WZC) * Mathf.Sqrt(1 - (my / WH) * (my / WH)) - 0.055f;
            r.jawU = Pivot(r.head, "mouth", 0, my, mz);
            Part(r.jawU, M("#22091e"), 0, -0.065f, 0, 0.28f, 0.13f, 0.12f);
            Part(r.jawU, M("#e06a8a"), 0, -0.058f, 0.035f, 0.15f, 0.03f, 0.06f);
            var tooth = M("#f6f2f8");
            foreach (var tx in new[] { -0.08f, 0f, 0.08f }) Part(r.jawU, tooth, tx, 0.02f, 0.06f, 0.045f, 0.035f, 0.02f);
            r.height = WH;
            return r;
        }
        Mesh WormMeshOffset(float[] zsLocal, float off, Material[] mats)
        {
            // 꼬리는 경첩(off) 기준 로컬 좌표로 만들되 모양은 원래 위치의 단면을 씀
            var world = new float[zsLocal.Length]; for (int i = 0; i < world.Length; i++) world[i] = zsLocal[i] + off;
            var m = WormMesh(world, false, mats);
            var vs = new List<Vector3>(); m.GetVertices(vs);
            for (int i = 0; i < vs.Count; i++) vs[i] = new Vector3(vs[i].x, vs[i].y, vs[i].z - off);
            m.SetVertices(vs); m.RecalculateBounds();
            return m;
        }
        void MeshObj(Transform parent, Mesh m, Material[] mats)
        {
            var go = new GameObject("mesh"); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        }

        // 2D 스프라이트처럼: 가만히 있으면 숨쉬듯 들썩, 기어가면 꿀렁(앞뒤로 늘었다 줄었다) + 꼬리 살랑, 먹으면 입 쩍
        static void AnimateWorm(Rig r, float t, float move, float eatK)
        {
            if (r.segs == null) return;
            float ph = t * 10f;
            float breathe = 0.03f * Mathf.Sin(t * 2.5f);
            float sq = move * 0.09f * Mathf.Sin(ph);
            float eat = eatK >= 0 ? Mathf.Sin(Mathf.Clamp01(eatK) * Mathf.PI) : 0;
            r.head.localScale = new Vector3(1 + sq * 0.4f + eat * 0.1f, 1 + breathe - sq + eat * 0.12f, 1 + sq + eat * 0.15f);
            r.head.localPosition = new Vector3(0, 0, eat * 0.12f);
            r.segs[0].localRotation = Quaternion.Euler(0, Mathf.Sin(ph * 0.5f) * 16f * move + Mathf.Sin(t * 1.7f) * 4f, 0);
            r.segs[0].localScale = new Vector3(1, 1 + breathe * 0.5f, 1 - sq * 0.6f);
            float open = 1f + 0.12f * Mathf.Sin(t * 2.5f) + eat * 1.6f;                    // 기본: 조금 벌림
            r.jawU.localScale = new Vector3(1 + eat * 0.3f, open, 1);
        }

        // ---------------------------------------------------------------- 풀
        readonly Dictionary<string, List<Rig>> rigs = new Dictionary<string, List<Rig>>();
        readonly Dictionary<string, int> rigUsed = new Dictionary<string, int>();
        Rig worm, playerHuman; string playerHumanKind;
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
