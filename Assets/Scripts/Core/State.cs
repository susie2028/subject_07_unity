// 게임 진행 상태. 체크포인트(층 도착 시점)를 위해 모두 깊은 복사(Clone)가 된다.
using System.Collections.Generic;
using UnityEngine;

namespace Specimen07
{
    public enum NpcState { Patrol, Inspect, Chase }

    public class Bubble { public string text; public float t; }
    public class Inspect { public float x, y, t; public bool staring; public string end; }
    public class Search { public float x, y, t; public bool looking; }

    public class Npc
    {
        public string id, ch, label, pal;
        public float x, y, face, desFace;
        public RouteWp[] route;
        public int wp;
        public List<Vector2> path;
        public float wait;
        public bool alive = true;
        public NpcState state = NpcState.Patrol;
        public Bubble bubble;
        public float chatT, cool;
        public Vector2? lastInspect;
        public Inspect inspect;
        public bool seesPlayer, frozen, temp;
        public float lockT, aim, repath, shoutCool;
        public Search search;

        public CharDef C { get { return Data.Chars[ch]; } }

        public static Npc Make(NpcDef d, bool temp)
        {
            return new Npc
            {
                id = d.id, ch = d.ch, label = d.label, pal = d.pal ?? d.ch,
                x = d.x, y = d.y, face = Data.Rad(d.face), desFace = Data.Rad(d.face),
                route = d.route ?? new RouteWp[0], chatT = Random.Range(4f, 10f), temp = temp,
            };
        }

        public Npc Clone()
        {
            var n = (Npc)MemberwiseClone();
            n.path = path == null ? null : new List<Vector2>(path);
            n.bubble = bubble == null ? null : new Bubble { text = bubble.text, t = bubble.t };
            n.inspect = inspect == null ? null : (Inspect)inspect.MemberwiseCloneShallow();
            n.search = search == null ? null : new Search { x = search.x, y = search.y, t = search.t, looking = search.looking };
            return n;
        }
    }

    static class InspectExt
    {
        public static Inspect MemberwiseCloneShallow(this Inspect i) { return new Inspect { x = i.x, y = i.y, t = i.t, staring = i.staring, end = i.end }; }
    }

    public class WorldObj
    {
        public string form; public float x, y, rot; public bool alive = true; public float respawnT;
        public WorldObj Clone() { return (WorldObj)MemberwiseClone(); }
    }

    public class DoorState { public bool open; public float temp; }
    public class Missing { public float t, x, y; public string label; public bool done; }

    public class FloorState
    {
        public List<Npc> npcs = new List<Npc>();
        public List<WorldObj> objects = new List<WorldObj>();
        public List<DoorState> doors = new List<DoorState>();
        public int wary;
        public List<Missing> missing = new List<Missing>();

        public static FloorState New(FloorGeo g)
        {
            var f = new FloorState();
            foreach (var d in g.def.npcs) f.npcs.Add(Npc.Make(d, false));
            foreach (var o in g.def.objects) f.objects.Add(new WorldObj { form = o.form, x = o.x, y = o.y, rot = o.rot });
            foreach (var d in g.def.doors) f.doors.Add(new DoorState());
            return f;
        }

        public FloorState Clone()
        {
            var f = new FloorState { wary = wary };
            foreach (var n in npcs) f.npcs.Add(n.Clone());
            foreach (var o in objects) f.objects.Add(o.Clone());
            foreach (var d in doors) f.doors.Add(new DoorState { open = d.open, temp = d.temp });
            foreach (var m in missing) f.missing.Add(new Missing { t = m.t, x = m.x, y = m.y, label = m.label, done = m.done });
            return f;
        }
    }

    public class EatTarget { public bool isNpc; public Npc npc; public WorldObj obj; public string form; public float X { get { return isNpc ? npc.x : obj.x; } } public float Y { get { return isNpc ? npc.y : obj.y; } } public object Ref { get { return isNpc ? (object)npc : obj; } } }
    public class Eating { public EatTarget target; public float t; }

    public class Player
    {
        public float x, y; public string form = "body"; public float timer, face, speedNow; public Eating eating; public bool moved;
        public Player Clone() { var p = (Player)MemberwiseClone(); p.eating = null; return p; }
    }

    public class Heli { public float t; public bool arrived; }
    public class Alarm { public float t, lx, ly, spawnT, started; public string mark; public bool spawned; }
    public class Particle { public float x, y, vx, vy, life, max, r; public Color color; }
    public class Trail { public float x, y, t, r; }

    // 체크포인트로 저장되는 부분
    public class SaveData
    {
        public string floorId;
        public Dictionary<string, FloorState> floors = new Dictionary<string, FloorState>();
        public Player player;
        public List<string> cards = new List<string>(), trapsOff = new List<string>(), visited = new List<string>(), seenForm = new List<string>();
        public Heli heli;
        // 미니맵: 주인공이 본 적 있는 칸 (층별)
        public Dictionary<string, bool[]> explored = new Dictionary<string, bool[]>();

        public SaveData Clone()
        {
            var s = new SaveData { floorId = floorId, player = player.Clone(), heli = heli == null ? null : new Heli { t = heli.t, arrived = heli.arrived } };
            foreach (var kv in floors) s.floors[kv.Key] = kv.Value.Clone();
            s.cards = new List<string>(cards); s.trapsOff = new List<string>(trapsOff);
            s.visited = new List<string>(visited); s.seenForm = new List<string>(seenForm);
            foreach (var kv in explored) s.explored[kv.Key] = (bool[])kv.Value.Clone();
            return s;
        }
    }

    public class GameState : SaveData
    {
        public float elapsed; public int alarms;
        // 층을 옮기거나 다시 시작하면 초기화되는 값
        public float time; public Alarm alarm; public float spot; public Npc spotNpc; public bool anySee, violationNow;
        public List<Trail> trail = new List<Trail>(); public List<Particle> particles = new List<Particle>();
        public float trailT, dieT; public string deathReason = "", deathKind = "";
        public Vector2 cam; public float arriveT = Cfg.arriveGrace, sirenT, aimBeep;
        public Vector2? shotFrom;

        public FloorState Fl { get { return floors[floorId]; } }

        public void ResetTransient()
        {
            time = 0; alarm = null; spot = 0; spotNpc = null; anySee = false; violationNow = false;
            trail.Clear(); particles.Clear(); trailT = 0; dieT = 0; deathReason = ""; deathKind = "";
            cam = Vector2.zero; arriveT = Cfg.arriveGrace; sirenT = 0; aimBeep = 0; shotFrom = null;
        }

        public void Load(SaveData s)
        {
            var c = s.Clone();
            floorId = c.floorId; floors = c.floors; player = c.player; cards = c.cards; trapsOff = c.trapsOff;
            visited = c.visited; seenForm = c.seenForm; heli = c.heli; explored = c.explored;
            ResetTransient();
        }
    }
}
