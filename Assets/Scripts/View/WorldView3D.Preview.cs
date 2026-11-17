// 레벨 에디터용: Play를 누르지 않고 씬 창에 층을 2.5D 그대로 그린다.
// 그린 것은 씬에 저장되지 않는다 (HideFlags.DontSave). 게임 중에는 쓰지 않는다.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Specimen07
{
    public partial class WorldView3D
    {
        Transform actorRoot;

        // 벽·가구·문·조명 등 붙박이를 그린다 (타일·방·문이 바뀌었을 때)
        public void BuildPreview(FloorGeo g)
        {
            InitRes();
            BuildFloor(g);
            floorRoot.SetParent(transform, false);
            dynRoot.SetParent(transform, false);
            dynRoot.gameObject.SetActive(false);   // 주인공·헬기·시야 같은 움직이는 것은 안 보이게
            MarkPreview(transform);
        }

        // 사람·물건·도착 지점 (위치만 바뀌었을 때는 이것만 다시 그린다)
        public void BuildPreviewActors(FloorDef d)
        {
            InitRes();
            if (actorRoot != null) Kill(actorRoot.gameObject);
            actorRoot = new GameObject("Actors").transform; actorRoot.SetParent(transform, false);
            if (G == null) return;
            foreach (var o in d.objects)
            {
                var m = BuildModel(o.form); m.SetParent(actorRoot, true);
                m.position = P(o.x, 0, o.y);
                m.rotation = Quaternion.Euler(0, o.form == "chair" ? o.rot * Mathf.Rad2Deg - 90 : 0, 0);
            }
            foreach (var n in d.npcs)
            {
                var sr = Stand(actorRoot, "stand_researcher_front"); sr.name = n.label;
                float sc0;
                string kind = ViewKit.Variant(n.pal, n.id);
                if (!ViewKit.ApplyStand(sr, kind, Data.Rad(n.face), out sc0)) sr.sprite = Art.Get("char_" + n.pal);
                if (sr.sprite != null) PlaceStand(sr, n.x, n.y, sr.sprite.pixelsPerUnit / ViewKit.CHAR_PX);
                var sh = Flat(actorRoot, "fx_dot", 0.8f); sh.transform.position = P(n.x, 0.02f, n.y); sh.transform.localScale = new Vector3(0.75f, 0.45f, 1); sh.color = new Color(0, 0, 0, 0.45f);
            }
            MarkPreview(actorRoot);
        }

        static void MarkPreview(Transform t)
        {
            foreach (var tf in t.GetComponentsInChildren<Transform>(true)) tf.gameObject.hideFlags = HideFlags.DontSave;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = r is SpriteRenderer ? ShadowCastingMode.Off : r.shadowCastingMode;
        }
    }
}
