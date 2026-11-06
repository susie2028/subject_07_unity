import json,re,sys
F=json.load(open('floors.json'))
def s(x): return json.dumps(x,ensure_ascii=False)
def f(x):
    v=float(x); r=repr(v)
    return (r if ('.' in r or 'e' in r) else r+'.0')+'f'
def col(t):
    m=re.match(r'rgba\((\d+),(\d+),(\d+),([\d.]+)\)',t.replace(' ',''))
    r,g,b,a=m.groups(); return f"C({r},{g},{b},{f(a)})"
def rect(r): return f"new RectI({r[0]},{r[1]},{r[2]},{r[3]})"
def arr(t,items): return f"new {t}[] {{ {', '.join(items)} }}" if items else f"new {t}[0]"
out=[]
out.append("// 자동 생성: tools/gen.py — 프로토타입 index.html의 FLOOR_DEFS를 그대로 옮긴 것.\n// 좌표계는 프로토타입과 같다 (x 오른쪽, y 아래쪽, 1칸 = 1유닛).\nusing UnityEngine;\n\nnamespace Specimen07\n{\n    public static partial class FloorDefs\n    {\n        static Color C(int r, int g, int b, float a) { return new Color(r / 255f, g / 255f, b / 255f, a); }\n\n        public static readonly FloorDef[] All = new FloorDef[]\n        {")
for d in F:
    L=[]
    L.append(f"            new FloorDef {{")
    L.append(f"                id = {s(d['id'])}, name = {s(d['name'])}, w = {d['w']}, h = {d['h']}, lethal = {'true' if d['lethal'] else 'false'}, dark = {f(d['dark'])},")
    L.append(f"                sub = {s(d['sub'])},")
    rooms=[]
    for r in d['rooms']:
        opt=r[6] if len(r)>6 else {}
        allow=arr('string',[s(a) for a in opt.get('allow',[])]) if 'allow' in opt else 'null'
        lab='false' if opt.get('label') is False else 'true'
        rooms.append(f"new RoomDef({s(r[0])}, {r[1]}, {r[2]}, {r[3]}, {r[4]}, {s(r[5])}, {allow}, {lab})")
    L.append("                rooms = new RoomDef[] {\n                    "+",\n                    ".join(rooms)+" },")
    L.append(f"                gaps = {arr('Vector2Int',[f'new Vector2Int({g[0]},{g[1]})' for g in d.get('gaps',[])])},")
    L.append(f"                glass = {arr('RectI',[rect(g) for g in d.get('glass',[])])},")
    L.append(f"                vents = {arr('RectI',[rect(g) for g in d.get('vents',[])])},")
    L.append("                fill = new FillDef[] {\n                    "+",\n                    ".join(f"new FillDef('{x[0]}', {x[1]}, {x[2]}, {x[3]}, {x[4]})" for x in d.get('fill',[]))+" },")
    L.append(f"                doors = {arr('DoorDef',[f'new DoorDef({x[chr(120)]}, {x[chr(121)]}, {s(x[chr(114)+chr(101)+chr(113)])}, {s(x[chr(108)+chr(97)+chr(98)+chr(101)+chr(108)])})' for x in d.get('doors',[])])},")
    cons=[]
    for c in d.get('consoles',[]):
        cons.append(f"new ConsoleDef({c['x']}, {c['y']}, {s(c['req'])}, {s(c.get('trap'))  if c.get('trap') else 'null'}, {'true' if c.get('heli') else 'false'}, {s(c['label'])})")
    L.append(f"                consoles = {arr('ConsoleDef',cons)},")
    L.append("                notices = "+arr('NoticeDef',[f"new NoticeDef({n['x']}, {n['y']}, {s(n['text'])})" for n in d.get('notices',[])])+",")
    L.append(f"                stairs = {arr('StairsDef',[f'new StairsDef({x[chr(120)]}, {x[chr(121)]}, {x[chr(119)]}, {x[chr(104)]}, {s(x[chr(116)+chr(111)])}, {chr(39)}{x[chr(100)+chr(105)+chr(114)]}{chr(39)})' for x in d.get('stairs',[])])},")
    ar=d['arrive']
    L.append("                arriveStart = "+(f"new Vector2({f(ar['start'][0])}, {f(ar['start'][1])})" if 'start' in ar else "null")+", arriveStairs = "+(f"new Vector2({f(ar['stairs'][0])}, {f(ar['stairs'][1])})" if 'stairs' in ar else "null")+", arriveLift = "+(f"new Vector2({f(ar['lift'][0])}, {f(ar['lift'][1])})" if 'lift' in ar else "null")+",")
    if 'tank' in d: L.append(f"                tank = new Vector2({f(d['tank'][0])}, {f(d['tank'][1])}),")
    if 'lift' in d: L.append(f"                lift = {arr('Vector2Int',[f'new Vector2Int({t[0]},{t[1]})' for t in d['lift']['tiles']])},")
    if 'pad' in d: L.append(f"                pad = {rect(d['pad'])},")
    traps=[]
    for t in d.get('traps',[]):
        traps.append(f"new TrapDef({s(t['id'])}, {s(t['kind'])}, {s(t['label'])}, {arr('RectI',[rect(r) for r in t['rects']])})")
    L.append(f"                traps = {arr('TrapDef',traps)},")
    L.append(f"                spawns = {arr('Vector2',[f'new Vector2({f(p[0])}, {f(p[1])})' for p in d.get('spawns',[])])},")
    L.append(f"                reinforce = {arr('ReinforceDef',[f'new ReinforceDef({s(r[chr(99)+chr(104)+chr(97)+chr(114)])}, {r[chr(110)]})' for r in d.get('reinforce',[])])},")
    el=[]
    for l in d.get('extraLights',[]):
        el.append(f"new LightDef({f(l['x'])}, {f(l['y'])}, {f(l['r'])}, {f(l['i'])}, {l.get('flick',0)}, {col(l['tint']) if l.get('tint') else 'Color.clear'})")
    L.append(f"                extraLights = {arr('LightDef',el)},")
    objs=[]
    for o in d.get('objects',[]):
        objs.append(f"new ObjDef({s(o[0])}, {f(o[1])}, {f(o[2])}, {f(o[3] if len(o)>3 else 0)})")
    L.append("                objects = new ObjDef[] {\n                    "+",\n                    ".join(objs)+" },")
    npcs=[]
    for n in d.get('npcs',[]):
        route=", ".join(f"new RouteWp({f(w[0])}, {f(w[1])}, {f(w[2])}, {f(w[3])})" for w in n['route'])
        npcs.append(f"new NpcDef({s(n['id'])}, {s(n['char'])}, {s(n.get('pal',n['char']))}, {s(n['label'])}, {f(n['x'])}, {f(n['y'])}, {f(n.get('face',0))},\n                        new RouteWp[] {{ {route} }})")
    L.append("                npcs = new NpcDef[] {\n                    "+",\n                    ".join(npcs)+" },")
    L.append("            },")
    out.append("\n".join(L))
out.append("        };\n    }\n}\n")
open(sys.argv[1],'w').write("\n".join(out))
