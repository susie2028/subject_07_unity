# 맵 검증: 게임(FloorGeo)과 같은 규칙으로 격자를 만들고, NPC 순찰 경로와 층 진행이 가능한지 확인
import json, sys
from collections import deque
F = {f['id']: f for f in json.load(open(sys.argv[1]))}
GRID_OUT = sys.argv[2] if len(sys.argv) > 2 else None
SOLID = set('#SVTNCEGMAWDv'); OPAQUE = set('#SVNEMAWDv')
ACCESS = {'cleaner': ['clean'], 'researcher': ['clean', 'lab'], 'guard': ['clean', 'lab', 'sec'], 'senior': ['clean', 'lab', 'sec', 'senior'],
          'soldier': ['clean', 'lab', 'sec', 'mil'], 'director': ['clean', 'lab', 'sec', 'senior', 'mil', 'dir']}
def build(f):
    w, h = f['w'], f['h']; g = [['#'] * w for _ in range(h)]
    def put(ch, x, y, fw=1, fh=1):
        for yy in range(y, y + fh):
            for xx in range(x, x + fw):
                if 0 <= xx < w and 0 <= yy < h: g[yy][xx] = ch
    for r in f['rooms']: put('.', r[1], r[2], r[3], r[4])
    for x, y in f.get('gaps', []): put('.', x, y)
    for fl in f.get('fill', []): put(fl[0], fl[1], fl[2], fl[3], fl[4])
    for x, y, fw, fh in f.get('glass', []): put('G', x, y, fw, fh)
    for x, y, fw, fh in f.get('vents', []): put('v', x, y, fw, fh)
    for d in f.get('doors', []): put('D', d['x'], d['y'])
    for c in f.get('consoles', []): put('C', c['x'], c['y'])
    for n in f.get('notices', []): put('N', n['x'], n['y'])
    for t in f.get('lift', {}).get('tiles', []): put('E', t[0], t[1])
    for s in f.get('stairs', []): put(s['dir'], s['x'], s['y'], s['w'], s['h'])
    if 'pad' in f: p = f['pad']; put('H', p[0], p[1], p[2], p[3])
    return g
errors = []
def err(m): errors.append(m)
def bfs(g, f, starts, passable):
    w, h = f['w'], f['h']; seen = set(); q = deque()
    for s in starts:
        if passable(*s): seen.add(s); q.append(s)
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in seen and passable(nx, ny): seen.add((nx, ny)); q.append((nx, ny))
    return seen
def doorreq(f, x, y):
    for d in f.get('doors', []):
        if d['x'] == x and d['y'] == y: return d['req']
def traptiles(f, ids):
    t = set()
    for tr in f.get('traps', []):
        if tr['id'] in ids:
            for x, y, w, h in tr['rects']:
                for i in range(w):
                    for j in range(h): t.add((x + i, y + j))
    return t
def player_pass(g, f, cards, body=True, trapsOn=True):
    traps = traptiles(f, [tr['id'] for tr in f.get('traps', [])]) if trapsOn else set()
    def p(x, y):
        c = g[y][x]
        if (x, y) in traps: return False
        if c == 'D': return doorreq(f, x, y) in cards
        if c == 'v': return body
        return c not in SOLID
    return p
def near(region, x, y, r=1.4):
    return any(abs(rx + .5 - x) <= r and abs(ry + .5 - y) <= r for rx, ry in region)
grids = {}
for fid, f in F.items():
    g = build(f); grids[fid] = g
    # 문·통로 칸이 실제로 두 칸 사이에 있는지
    for x, y in f.get('gaps', []):
        nb = [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
        if sum(1 for a, b in nb if 0 <= a < f['w'] and 0 <= b < f['h'] and g[b][a] not in '#') < 2: err(f'{fid} 쓸모없는 통로 ({x},{y})')
    # NPC 경로
    for n in f['npcs']:
        acc = ACCESS[n['char']]
        def npass(x, y, acc=acc):
            c = g[y][x]
            if c == 'D': return doorreq(f, x, y) in acc
            return c not in SOLID
        prev = None
        for wp in n['route']:
            tx, ty = int(wp[0]), int(wp[1])
            if not npass(tx, ty): err(f'{fid} {n["label"]} 경유지 ({tx},{ty})가 막힌 칸 "{g[ty][tx]}"'); continue
            if prev and (tx, ty) not in bfs(g, f, [prev], npass): err(f'{fid} {n["label"]} {prev}→({tx},{ty}) 길 없음')
            prev = (tx, ty)
    # 모든 방이 (모든 카드·함정 꺼짐) 도착 지점에서 닿는지
    allc = ['clean', 'lab', 'sec', 'senior', 'mil', 'dir']
    arr = [(int(v[0]), int(v[1])) for v in f['arrive'].values()]
    reach = bfs(g, f, arr, player_pass(g, f, allc, True, False))
    for r in f['rooms']:
        cells = [(x, y) for x in range(r[1], r[1] + r[3]) for y in range(r[2], r[2] + r[4]) if g[y][x] not in SOLID]
        if cells and not any(c in reach for c in cells): err(f'{fid} 방 "{r[0]}"에 갈 수 없음')
    for a in arr:
        if g[a[1]][a[0]] in SOLID: err(f'{fid} 도착 지점 {a}가 막힘')

def region(fid, cards, via, body=True, trapsOn=True):
    f = F[fid]; g = grids[fid]; a = f['arrive'][via]
    return bfs(g, f, [(int(a[0]), int(a[1]))], player_pass(g, f, cards, body, trapsOn))
def npc_reachable(fid, reg, char):
    return [n['label'] for n in F[fid]['npcs'] if n['char'] == char and any(near(reg, wp[0], wp[1], 1.2) for wp in n['route'])]
def check(name, ok, extra=''):
    print(('PASS ' if ok else 'FAIL ') + name + (f'  ({extra})' if extra else ''))
    if not ok: errors.append(name)
def door_tile(fid, label): d = [d for d in F[fid]['doors'] if d['label'] == label][0]; return d['x'] + .5, d['y'] + .5
def stairs_c(fid): s = F[fid]['stairs'][0]; return s['x'] + .5, s['y'] + .5
def lift_c(fid): t = F[fid]['lift']['tiles'][1]; return t[0] + .5, t[1] + .5
def cons_c(fid, i=0): c = F[fid]['consoles'][i]; return c['x'] + .5, c['y'] + .5

r = region('B1', [], 'start'); check('B1: 카드 없이 청소부에게 닿음', bool(npc_reachable('B1', r, 'cleaner')))
check('B1: 카드 없이 직원 출입문 앞까지', near(r, *door_tile('B1', '직원 출입문')))
check('B1: 카드 없이는 계단 못 감', not near(r, *stairs_c('B1'), 0.6))
r = region('B1', ['clean'], 'start'); check('B1: 청소 카드로 계단', near(r, *stairs_c('B1'), 0.6))
r = region('B1', ['clean', 'lab', 'sec'], 'stairs'); check('B1: 보안 카드로 기계실 콘솔', near(r, *cons_c('B1'), 1.4))
r = region('1F', ['clean'], 'stairs'); check('1F: 청소 카드로 연구원에게 닿음', bool(npc_reachable('1F', r, 'researcher')))
check('1F: 연구원 카드 없이는 엘리베이터 못 감', not near(r, *lift_c('1F'), 1.6))
r = region('1F', ['clean', 'lab'], 'stairs'); check('1F: 연구 카드로 엘리베이터', near(r, *lift_c('1F'), 1.6))
r = region('2F', ['clean', 'lab'], 'lift'); check('2F: 경비원에게 닿음', len(npc_reachable('2F', r, 'guard')) >= 3)
check('2F: 보안 카드 없이는 통제실 콘솔 못 감', not near(r, *cons_c('2F'), 1.4))
r = region('2F', ['clean', 'lab', 'sec'], 'lift'); check('2F: 보안 카드로 통제실 콘솔', near(r, *cons_c('2F'), 1.4))
r = region('3F', ['clean', 'lab', 'sec'], 'lift', body=False); check('3F: 레이저 켜짐 + 환풍구 안 쓰면 박사에게 못 감', not npc_reachable('3F', r, 'senior'))
r = region('3F', ['clean', 'lab', 'sec'], 'lift', body=True); check('3F: 환풍구로 우회하면 박사에게 닿음', bool(npc_reachable('3F', r, 'senior')))
r = region('3F', ['clean', 'lab', 'sec'], 'lift', body=False, trapsOn=False); check('3F: 레이저 끄면 걸어서 박사에게 닿음', bool(npc_reachable('3F', r, 'senior')))
r = region('4F', ['clean', 'lab', 'sec', 'senior'], 'lift'); check('4F: 군인에게 닿음', len(npc_reachable('4F', r, 'soldier')) >= 3)
check('4F: 군 카드 없이는 소장에게 못 감', not npc_reachable('4F', r, 'director'))
r = region('4F', ['clean', 'lab', 'sec', 'senior', 'mil'], 'lift'); check('4F: 가스 켜져 있으면 소장에게 못 감', not npc_reachable('4F', r, 'director'))
r = region('4F', ['clean', 'lab', 'sec', 'senior', 'mil'], 'lift', trapsOn=False); check('4F: 가스 끄고 군 카드로 소장에게', bool(npc_reachable('4F', r, 'director')))
r = region('5F', ['clean', 'lab', 'sec', 'senior', 'mil', 'dir'], 'lift'); check('5F: 헬기 콘솔', near(r, *cons_c('5F'), 1.4))
p = F['5F']['pad']; check('5F: 소장 카드로 헬리패드', near(r, p[0] + p[2] / 2, p[1] + p[3] / 2, 3))
r2 = region('5F', ['clean', 'lab', 'sec', 'senior', 'mil'], 'lift'); check('5F: 소장 카드 없이는 헬리패드 못 감', not near(r2, p[0] + p[2] / 2, p[1] + p[3] / 2, 3))

for e in [e for e in errors if not e.startswith(('B1:', '1F:', '2F:', '3F:', '4F:', '5F:'))]: print('ERR', e)
for fid, f in F.items(): print(fid, f['w'], 'x', f['h'], '인원', len(f['npcs']), '문', len(f['doors']), '함정', len(f.get('traps', [])), '가구', len(f['fill']), '물건', len(f['objects']))
if GRID_OUT:
    with open(GRID_OUT, 'w') as o:
        for fid, g in grids.items():
            o.write(f'== {fid} {F[fid]["w"]}x{F[fid]["h"]}\n'); o.write('\n'.join(''.join(r) for r in g) + '\nlights\n')
print('총 오류', len(errors))
