# 새 맵 (v2) — 넓은 방 여러 개를 짧은 통로로 잇고 갈림길을 여러 개 둔 구조.
# 기획서의 층별 진행 순서·잠긴 문·함정은 그대로, 인원은 기획서보다 조금 많게.
# 사용: python3 Tools/floors_v2.py  → Tools/floors.json 을 만들고, gen.py로 FloorDefs.cs 생성
import json, random, math, os, sys

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), 'floors.json')

class Floor:
    def __init__(s, id, name, w, h, lethal, dark, sub):
        s.d = dict(id=id, name=name, w=w, h=h, lethal=lethal, dark=dark, sub=sub, rooms=[], gaps=[], glass=[], vents=[], fill=[],
                   doors=[], consoles=[], notices=[], stairs=[], arrive={}, objects=[], npcs=[], traps=[], spawns=[], reinforce=[], extraLights=[])
        s.keep = set()   # 가구를 두면 안 되는 칸
        s.opens = set()  # 통로 입구 (주변을 비움)
        s.noDecor = set()

    def room(s, name, x, y, w, h, kind, allow=None, label=True, decor=True):
        opt = {}
        if allow: opt['allow'] = allow
        if not label: opt['label'] = False
        s.d['rooms'].append([name, x, y, w, h, kind] + ([opt] if opt else []))
        if not decor: s.noDecor.add(len(s.d['rooms']) - 1)

    def gap(s, x, y, n=2, vertical=False):
        for i in range(n):
            t = (x, y + i) if vertical else (x + i, y)
            s.d['gaps'].append(list(t)); s.keep.add(t); s.opens.add(t)

    def door(s, x, y, req, label): s.d['doors'].append(dict(x=x, y=y, req=req, label=label)); s.keep.add((x, y)); s.opens.add((x, y))
    def vent(s, x, y, w=1, h=1):
        s.d['vents'].append([x, y, w, h])
        for i in range(w):
            for j in range(h): s.keep.add((x + i, y + j)); s.opens.add((x + i, y + j))
    def glass(s, x, y, w, h): s.d['glass'].append([x, y, w, h])
    def fill(s, ch, x, y, w=1, h=1): s.d['fill'].append([ch, x, y, w, h])
    def console(s, x, y, req, label, trap=None, heli=False):
        c = dict(x=x, y=y, req=req, label=label)
        if trap: c['trap'] = trap
        if heli: c['heli'] = True
        s.d['consoles'].append(c); s.keep.add((x, y)); s.opens.add((x, y))
    def notice(s, x, y, text): s.d['notices'].append(dict(x=x, y=y, text=text)); s.keep.add((x, y))
    def stairs(s, x, y, w, h, to, dir):
        s.d['stairs'].append(dict(x=x, y=y, w=w, h=h, to=to, dir=dir))
        for i in range(w):
            for j in range(h): s.keep.add((x + i, y + j)); s.opens.add((x + i, y + j))
    def lift(s, tiles):
        s.d['lift'] = dict(tiles=[list(t) for t in tiles])
        for t in tiles: s.keep.add(tuple(t)); s.opens.add(tuple(t))
    def arrive(s, via, x, y): s.d['arrive'][via] = [x, y]; s.opens.add((int(x), int(y)))
    def trap(s, id, kind, label, rects): s.d['traps'].append(dict(id=id, kind=kind, label=label, rects=rects))
    def obj(s, form, x, y, rot=0): s.d['objects'].append([form, x + .5, y + .5, rot])
    def npc(s, id, char, label, route, pal=None):
        r = []
        for i, p in enumerate(route):
            x, y = p[0], p[1]
            wait = p[2] if len(p) > 2 else 0.6
            nxt = route[(i + 1) % len(route)]
            look = p[3] if len(p) > 3 else (math.degrees(math.atan2(nxt[1] - y, nxt[0] - x)) if (nxt[0], nxt[1]) != (x, y) else 0)
            r.append([x + .5, y + .5, wait, round(look)])
        n = dict(id=id, char=char, label=label, x=r[0][0], y=r[0][1], face=r[0][3], route=r)
        if pal: n['pal'] = pal
        s.d['npcs'].append(n)

    # ---------- 자동 장식 (방 종류별로 벽을 따라 가구, 큰 방엔 섬) ----------
    def grid(s):
        d = s.d; w, h = d['w'], d['h']
        g = [['#'] * w for _ in range(h)]
        for r in d['rooms']:
            for yy in range(r[2], r[2] + r[4]):
                for xx in range(r[1], r[1] + r[3]): g[yy][xx] = '.'
        for x, y in d['gaps']: g[y][x] = '.'
        return g

    def decorate(s, seed):
        rnd = random.Random(seed)
        g = s.grid(); d = s.d
        used = set()
        for fl in d['fill']:   # 손으로 둔 가구
            for i in range(fl[3]):
                for j in range(fl[4]): used.add((fl[1] + i, fl[2] + j))
        for n in d['npcs']:   # 순찰 경로 위·주변은 비움
            for wp in n['route']:
                for ox in (-1, 0, 1):
                    for oy in (-1, 0, 1): s.keep.add((int(wp[0]) + ox, int(wp[1]) + oy))
        def free(x, y): return g[y][x] == '.' and (x, y) not in used and (x, y) not in s.keep and not any(abs(x - kx) <= 1 and abs(y - ky) <= 1 for kx, ky in s.opens)
        def put(ch, x, y, w=1, h=1):
            cells = [(x + i, y + j) for i in range(w) for j in range(h)]
            if all(0 <= cx < d['w'] and 0 <= cy < d['h'] and free(cx, cy) for cx, cy in cells):
                s.fill(ch, x, y, w, h)
                for c in cells: used.add(c)
                return True
            return False
        def near_open(x, y, r):   # 통로·문·환풍구 입구 근처인지
            return any(abs(x - kx) <= r and abs(y - ky) <= r for kx, ky in s.opens)
        def place(ch, x, y, w=1, h=1, clear=1):
            cells = [(x + i, y + j) for i in range(w) for j in range(h)]
            if any(not (0 <= cx < d['w'] and 0 <= cy < d['h']) or g[cy][cx] != '.' or (cx, cy) in used or (cx, cy) in s.keep or near_open(cx, cy, clear + 1) for cx, cy in cells): return False
            s.fill(ch, x, y, w, h)
            for c in cells: used.add(c)
            return True
        for idx, r in enumerate(d['rooms']):
            name, x0, y0, w, h, kind = r[:6]
            if idx in s.noDecor or kind in ('lift', 'roof'): continue
            if kind == 'hall':                                             # 넓은 홀: 기둥·화분 줄 (시야 가림)
                if w >= 14 and h >= 7:
                    for x in range(x0 + 4, x0 + w - 4, 7):
                        for y in (y0 + 2, y0 + h - 3):
                            place('S', x, y, 2, 1, clear=0)
                continue
            ix0, iy0, ix1, iy1 = x0 + 2, y0 + 2, x0 + w - 3, y0 + h - 3      # 벽에서 2칸 띄운 안쪽
            # 1) 윗벽을 따라 가구 (벽면 조명 아래)
            wallch = {'store': 'S', 'lab': 'T', 'office': 'S', 'rest': 'V', 'mech': 'M', 'cold': 'M', 'wing': 'T', 'dir': 'S', 'iso': 'M', 'sec': 'M', 'exit': 'S'}.get(kind, 'S')
            x = x0
            while x < x0 + w:
                run = rnd.randint(2, 4)
                if rnd.random() < 0.75:
                    for i in range(run):
                        if x + i < x0 + w: place(wallch if not (kind == 'rest' and i > 0) else 'S', x + i, y0)
                x += run + rnd.randint(1, 2)
            for side in (x0, x0 + w - 1):                                  # 옆벽 캐비닛
                for yy in range(y0 + 2, y0 + h - 1, 3):
                    if rnd.random() < 0.45: place('S', side, yy)
            # 2) 방 종류별 배치 (시야를 가리는 지형)
            if w < 8 or h < 7: pass
            elif kind == 'store':                                          # 선반 통로 (세로 줄, 가운데 1칸 끊김)
                mid = (y0 + 2 + y0 + h - 2) // 2
                for x in range(x0 + 2, x0 + w - 2, 3):
                    for y in range(y0 + 2, y0 + h - 1):
                        if y != mid: place('S', x, y, clear=0)
            elif kind in ('lab', 'sec', 'cold', 'mech'):                    # 실험대/기계 가로 줄 (가운데 2칸 끊김)
                ch = {'lab': 'T', 'sec': 'T', 'cold': 'M', 'mech': 'M'}[kind]
                mid = (x0 + 2 + x0 + w - 3) // 2
                for y in range(y0 + 3, y0 + h - 1, 3):
                    for x in range(x0 + 2, x0 + w - 2):
                        if x not in (mid, mid + 1): place(ch, x, y, clear=0)
            elif kind in ('office', 'wing', 'dir'):                         # 책상 묶음 (2x2)
                for y in range(y0 + 3, y0 + h - 3, 4):
                    for x in range(x0 + 2, x0 + w - 3, 4):
                        place('T', x, y, 2, 2, clear=0)
            elif kind == 'rest':                                           # 테이블 + 화분
                for y in range(y0 + 3, y0 + h - 2, 4):
                    for x in range(x0 + 2, x0 + w - 3, 5):
                        place('T', x, y, 2, 1, clear=0); place('S', x + 3, y, clear=0)
            elif kind == 'iso':
                for (x, y) in [(ix0 + 1, iy1), (ix1 - 1, iy1)]: place('M', x, y)
            # 물건 (먹고 변신할 수 있는 것): 쓰레기통은 벽에, 의자는 책상 옆, 카트는 창고
            k = 0
            for _ in range(30):
                if k >= max(2, (w * h) // 45): break
                form = rnd.choice(['trash', 'trash', 'chair', 'chair'] + (['cart', 'cart'] if kind in ('store', 'mech') else []))
                if form == 'trash' or form == 'cart':
                    side = rnd.choice(['l', 'r', 'b'])
                    if side == 'l': ox, oy = x0, rnd.randint(y0 + 1, y0 + h - 2)
                    elif side == 'r': ox, oy = x0 + w - 1, rnd.randint(y0 + 1, y0 + h - 2)
                    else: ox, oy = rnd.randint(x0 + 1, x0 + w - 2), y0 + h - 1
                else: ox, oy = rnd.randint(x0 + 1, x0 + w - 2), rnd.randint(y0 + 1, y0 + h - 2)
                if free(ox, oy):
                    s.obj(form, ox, oy, rnd.choice([0, 1.57, 3.14, -1.57]) if form == 'chair' else 0); used.add((ox, oy)); k += 1

    def json(s): return s.d


FLOORS = []

# ======================================================================= B1 격리동 (들키면 끝)
f = Floor('B1', '격리동', 56, 34, True, 0.66, '발각 즉시 끝 · 늙은 청소부를 노려라')
#   윗줄: 격리실 A | 관찰실 | 표본 보관실 | 격리실 B
f.room('격리실 A', 1, 1, 14, 10, 'iso', allow=['researcher', 'senior', 'director'])
f.room('관찰실', 16, 1, 12, 10, 'lab')
f.room('표본 보관실', 29, 1, 12, 10, 'store')
f.room('격리실 B', 42, 1, 13, 10, 'iso', allow=['researcher', 'senior', 'director'])
#   가운데: 소독실 | 중앙 홀 | 의무실
f.room('소독실', 1, 12, 10, 9, 'lab')
f.room('중앙 홀', 12, 12, 32, 9, 'hall', label=False)
f.room('의무실', 45, 12, 10, 9, 'office')
#   아랫줄: 세탁실 | 휴게실 | 기계실 | 직원 계단
f.room('세탁실', 1, 22, 12, 11, 'rest')
f.room('휴게실', 14, 22, 14, 11, 'rest')
f.room('기계실', 29, 22, 11, 11, 'mech')
f.room('직원 계단', 41, 22, 14, 11, 'exit', decor=False)
f.glass(15, 3, 1, 5)
f.gap(13, 11); f.gap(5, 11); f.gap(21, 11); f.gap(28, 5, vertical=True); f.gap(34, 11); f.gap(41, 7, vertical=True); f.gap(48, 11)
f.gap(11, 15, vertical=True); f.gap(44, 15, vertical=True)
f.gap(5, 21); f.gap(13, 26, vertical=True); f.gap(19, 21); f.gap(24, 21)
f.vent(9, 11)
f.door(34, 21, 'sec', '기계실'); f.door(49, 21, 'clean', '직원 출입문')
f.console(36, 32, 'sec', '레이저 차단 장치', trap='L3')
f.notice(20, 1, '[관찰 일지 · 표본 07]\n먹이 급여 절대 금지. 먹은 것과 똑같아짐.\n※ 청소부는 격리실 출입 금지 (또 의자 사라짐)')
f.notice(2, 32, '[세탁실 안내]\n직원 출입문(의무실 쪽) — 청소 카드로 열림.\n위층 엘리베이터는 연구원 카드 이상.')
f.notice(31, 32, '[기계실]\n3F 실험동 레이저 그리드 수동 차단 장치.\n보안 카드(경비원) 인증 필요.')
f.stairs(50, 29, 2, 2, '1F', '>')
f.arrive('start', 6.5, 5.5); f.d['tank'] = [6.5, 5.5]; f.arrive('stairs', 47.5, 25.5)
f.fill('W', 47, 3, 3, 3)
f.fill('M', 2, 23, 1, 4); f.fill('V', 26, 23)
f.npc('b1r1', 'researcher', '관찰실 연구원', [(19, 5, 2.5, 180), (25, 8), (22, 15, 0.6), (38, 15, 0.8), (35, 6, 1.2, 90), (22, 15, 0.4)])
f.npc('b1r2', 'researcher', '휴게실 연구원', [(20, 27, 2.6, 90), (20, 27, 2.2, 180), (20, 27, 2.0, -90), (20, 27, 2.2, 0)], pal='researcher2')
f.npc('b1c', 'cleaner', '청소부 이 씨', [(6, 28, 1.8, 180), (6, 16, 1.0), (16, 16, 0.8), (40, 16, 0.6), (49, 16, 2.0, 0), (40, 16, 0.6), (24, 28, 1.5, 90), (13, 27, 0.6)])
f.npc('b1r3', 'researcher', '격리실 B 연구원', [(45, 8, 2.0, 0), (52, 8, 1.4, -90), (52, 4, 1.2, 180), (44, 4, 1.4, 90)], pal='researcher2')
f.npc('b1r4', 'researcher', '소독실 연구원', [(5, 17, 2.2, 0), (9, 14, 1.4, 90), (3, 14, 1.4, 90)])
f.decorate(101); FLOORS.append(f)

# ======================================================================= 1F 연구동 (들키면 끝, 사람 변신)
f = Floor('1F', '연구동', 64, 38, True, 0.55, '발각 즉시 끝 · 연구원의 권한을 빼앗아라')
f.room('연구실 1', 1, 1, 14, 10, 'lab'); f.room('회의실', 16, 1, 14, 10, 'office'); f.room('사무실', 31, 1, 16, 10, 'office'); f.room('탕비실', 48, 1, 15, 10, 'rest')
f.room('연구실 2', 1, 12, 14, 13, 'lab'); f.room('연구 홀', 16, 12, 31, 13, 'hall', label=False)
f.room('자료실', 48, 12, 15, 6, 'store'); f.room('분석실', 48, 19, 15, 6, 'lab')
f.room('계단실', 1, 26, 12, 11, 'exit', decor=False); f.room('청소용품실', 14, 26, 10, 11, 'store'); f.room('휴게실', 25, 26, 16, 11, 'rest')
f.room('대기 복도', 42, 26, 7, 11, 'hall', label=False); f.room('엘리베이터 홀', 50, 26, 13, 11, 'lift')
f.gap(6, 11); f.gap(15, 5, vertical=True); f.gap(22, 11); f.gap(38, 11); f.gap(47, 4, vertical=True)
f.gap(15, 17, vertical=True); f.gap(5, 25); f.gap(18, 25); f.gap(30, 25); f.gap(38, 25); f.gap(44, 25)
f.gap(41, 30, vertical=True); f.gap(13, 30, vertical=True); f.gap(47, 21, vertical=True)
f.door(47, 14, 'lab', '자료실'); f.door(49, 31, 'lab', '연구동 보안문')
f.stairs(2, 34, 2, 2, 'B1', '<'); f.arrive('stairs', 6.5, 29.5)
f.lift([(55, 37), (56, 37), (57, 37)]); f.arrive('lift', 56.5, 35.5)
f.notice(20, 1, '[보안동 근무표]\n2F 검문소 — 경비원 상시 대기, 엘리베이터 쪽을 향함.\n물품 창고 카트는 검문 대상 아님.')
f.notice(8, 26, '[1F 연구동]\n엘리베이터 홀은 연구동 보안문 안쪽.\n보안문 — 연구원 카드 필요.')
f.notice(52, 1, '[공지] 커피머신 실종 사건\n마지막 목격 장소: B1 격리실 앞 복도.\n범인 찾습니다. — 총무팀')
# 연구 홀 기둥(시야 가림)
for (x, y) in [(21, 15), (21, 21), (31, 15), (31, 21), (41, 15), (41, 21)]: f.fill('S', x, y, 2, 1)
f.fill('V', 60, 2)
f.npc('f1r1', 'researcher', '연구원 김', [(5, 5, 2.0, 0), (12, 8), (7, 14, 0.6), (24, 18, 1.0), (7, 14, 0.6)])
f.npc('f1r2', 'researcher', '연구원 박', [(35, 6, 2.2, 90), (44, 4, 0.8), (55, 6, 2.0, 0), (44, 4, 0.6), (38, 16, 1.0, 90)], pal='researcher2')
f.npc('f1r3', 'researcher', '연구원 최', [(22, 6, 2.6, 0), (22, 6, 2.2, 90), (27, 4, 1.6, 180)])
f.npc('f1r4', 'researcher', '분석실 연구원', [(55, 22, 2.4, 180), (55, 22, 2.0, 90), (60, 21, 1.4, 0)])
f.npc('f1r5', 'researcher', '연구원 윤', [(18, 14, 0.6), (44, 14, 0.6), (44, 23, 0.6), (18, 23, 0.6)], pal='researcher2')
f.npc('f1r6', 'researcher', '연구실 2 연구원', [(4, 15, 1.8, 0), (11, 22, 1.6, 90), (4, 22, 1.2, -90)])
f.npc('f1r7', 'researcher', '휴게실 연구원', [(30, 31, 2.4, 0), (36, 28, 1.6, 180), (30, 34, 1.4, 90)])
f.npc('f1c', 'cleaner', '청소부 정 씨', [(18, 33, 2.0, 0), (19, 22, 1.0), (28, 30, 0.8), (45, 33, 1.6, 0), (28, 30, 0.6), (12, 18, 1.2)])
f.decorate(202); FLOORS.append(f)

# ======================================================================= 2F 보안동 (경보, 검문소, 카트)
f = Floor('2F', '보안동', 70, 40, False, 0.58, '이제부터 발각되면 경보 — 경비원이 달려온다')
f.room('장비실', 1, 1, 10, 11, 'store'); f.room('엘리베이터 홀', 1, 13, 10, 12, 'lift'); f.room('물품 창고', 1, 26, 10, 13, 'store')
f.room('검문소', 12, 15, 6, 8, 'sec', label=True, decor=False)
f.room('경비 대기실', 19, 1, 12, 11, 'sec', allow=['guard', 'soldier']); f.room('보안 통제실', 32, 1, 14, 11, 'sec')
f.room('취조실', 47, 1, 10, 11, 'office'); f.room('무기고', 58, 1, 11, 11, 'mech')
f.room('중앙 홀', 19, 13, 38, 14, 'hall', label=False); f.room('연구원 대기실', 58, 13, 11, 14, 'rest')
f.room('CCTV실', 19, 28, 12, 11, 'office'); f.room('샤워실', 32, 28, 10, 11, 'rest'); f.room('휴게실', 43, 28, 13, 11, 'rest'); f.room('창고', 57, 28, 12, 11, 'store')
f.gap(5, 12); f.gap(5, 25); f.gap(11, 18, 3, vertical=True); f.gap(18, 18, 3, vertical=True)
f.gap(24, 12); f.gap(51, 12); f.gap(57, 19, vertical=True); f.gap(36, 27); f.gap(48, 27); f.gap(56, 32, vertical=True); f.gap(62, 27)

f.door(38, 12, 'sec', '보안 통제실'); f.door(63, 12, 'mil', '무기고'); f.door(25, 27, 'sec', 'CCTV실')
f.console(39, 1, 'senior', '4F 가스 차단 장치', trap='G4')
f.notice(35, 1, '[보안 통제실]\n4F 소장실 전실 — 비상 가스 살포 장치.\n해제: 고위 연구원 카드.')
f.notice(65, 13, '[무기고 반출 기록]\n옥상 헬기는 소장 전용.\n호출 콘솔은 소장 카드로만 인증됨.')
f.notice(55, 13, '[경보 수칙]\n경보 시 경비원 출동, 엘리베이터 자동 정지.\n표본 발견 시 즉시 포획할 것.')
f.notice(1, 27, '[물품 창고]\n카트 반납 시 벽 쪽에 붙여 둘 것.\n검문소 통과 시 카트는 검사 생략.')
f.lift([(0, 17), (0, 18), (0, 19)]); f.arrive('lift', 2.5, 18.5)
f.d['spawns'] = [[63.5, 20.5], [24.5, 6.5]]; f.d['reinforce'] = [dict(char='guard', n=2)]
for (x, y) in [(26, 17), (26, 22), (36, 17), (36, 22), (46, 17), (46, 22)]: f.fill('S', x, y, 2, 1)
for (x, y) in [(3, 28), (8, 28), (3, 36)]: f.obj('cart', x, y)
f.npc('f2g1', 'guard', '검문소 경비', [(16, 19, 5.0, 180), (16, 19, 1.2, 90), (16, 19, 5.0, 180), (16, 19, 1.2, -90)])
f.npc('f2g2', 'guard', '순찰 경비 1', [(21, 15, 0.6), (54, 15, 0.8), (54, 25, 0.6), (21, 25, 0.8)])
f.npc('f2g3', 'guard', '휴식 중 경비', [(24, 6, 3.0, 90), (28, 8, 2.4, 180), (21, 4, 2.0, 0)])
f.npc('f2g4', 'guard', '통제실 경비', [(36, 6, 2.6, 0), (43, 7, 2.2, 180), (40, 9, 1.5, -90)])
f.npc('f2g5', 'guard', '순찰 경비 2', [(34, 31, 0.8), (40, 35, 0.6), (50, 33, 0.8), (49, 26, 0.5), (36, 26, 0.5)])
f.npc('f2g6', 'guard', '대기실 경비', [(60, 15, 1.8, 0), (66, 24, 1.4, 180), (60, 24, 1.2, -90)])
f.npc('f2g7', 'guard', '창고 경비', [(60, 31, 1.8, 0), (66, 36, 1.4, 180), (62, 26, 0.6)])
f.npc('f2r1', 'researcher', '대기실 연구원', [(63, 18, 2.4, 180), (63, 18, 2.0, 90), (66, 15, 1.4)], pal='researcher2')
f.npc('f2r2', 'researcher', 'CCTV 담당', [(24, 33, 3.0, -90), (28, 31, 2.0, 0), (21, 35, 1.6, 90)])
f.npc('f2r3', 'researcher', '취조실 연구원', [(51, 5, 2.4, 90), (54, 8, 1.6, 180), (49, 8, 1.4, 0)])
f.npc('f2r4', 'researcher', '휴게실 연구원', [(47, 32, 2.6, 0), (53, 35, 1.8, 180)], pal='researcher2')
f.decorate(303); FLOORS.append(f)

# ======================================================================= 3F 실험동 (레이저 + 환풍구)
f = Floor('3F', '실험동', 72, 42, False, 0.6, '레이저 그리드와 환풍구 · 고위 연구원을 찾아라')
f.room('엘리베이터 홀', 1, 1, 10, 10, 'lift'); f.room('화학 창고', 1, 12, 10, 29, 'store')
f.room('실험실 A', 12, 1, 16, 12, 'lab'); f.room('실험실 B', 29, 1, 23, 12, 'lab')
f.room('중앙 홀', 12, 14, 40, 8, 'hall', label=False)
f.room('저온 보관실', 12, 23, 14, 18, 'cold'); f.room('소독실', 27, 23, 10, 18, 'lab'); f.room('배양실', 38, 23, 14, 18, 'lab')
f.room('격리 윙', 53, 1, 4, 40, 'wing', label=False, decor=False)
f.room('박사 연구실', 58, 1, 13, 18, 'wing', allow=['senior', 'director']); f.room('표본실', 58, 20, 13, 21, 'wing')
f.glass(28, 4, 1, 6); f.glass(26, 28, 1, 4)
f.gap(11, 5, vertical=True); f.gap(5, 11); f.gap(11, 17, vertical=True)
f.gap(19, 13); f.gap(35, 13); f.gap(45, 13); f.gap(18, 22); f.gap(31, 22); f.gap(44, 22); f.gap(37, 35, vertical=True)
f.gap(52, 17, vertical=True); f.gap(52, 30, vertical=True); f.gap(57, 8, vertical=True); f.gap(57, 28, vertical=True)
f.vent(52, 6); f.vent(26, 36); f.vent(5, 22)
f.trap('L3', 'laser', '레이저 그리드', [[53, 16, 2, 4], [53, 29, 2, 4]])
f.lift([(0, 4), (0, 5), (0, 6)]); f.arrive('lift', 2.5, 5.5)
f.d['spawns'] = [[68.5, 38.5], [13.5, 39.5]]; f.d['reinforce'] = [dict(char='guard', n=2), dict(char='soldier', n=1)]
f.notice(47, 14, '[격리 윙 경고]\n레이저 그리드 가동 중. 인가 인원만 통과.\n수동 차단: B1 기계실 (보안 카드).')
f.notice(2, 40, '[환기 설비 점검표]\n실험실 B 동쪽 벽 환풍구 ↔ 격리 윙 연결.\n사람은 못 들어감. 쥐 주의.')
f.notice(62, 1, '[연구 노트 · 박사 한]\n표본 07 — 고위직 신원 복제 가능성.\n4F 소장실 전실 가스는 2F 통제실에서 관리.')
f.fill('W', 64, 30, 3, 3); f.fill('W', 40, 36, 2, 2); f.fill('W', 14, 36, 2, 2)
f.npc('f3s', 'senior', '박사 한', [(62, 8, 3.0, 0), (66, 14, 2.0, 90), (55, 9, 0.6), (55, 26, 0.6), (63, 26, 2.2, 0), (55, 26, 0.6), (55, 9, 0.6)])
f.npc('f3g1', 'guard', '실험동 경비', [(13, 16, 0.6), (50, 16, 0.8), (50, 20, 0.6), (13, 20, 0.8)])
f.npc('f3g2', 'guard', '북쪽 경비', [(14, 5, 1.0), (49, 5, 1.2, 0), (32, 10, 0.8), (14, 10, 0.8)])
f.npc('f3g3', 'guard', '남쪽 경비', [(14, 25, 0.8), (24, 38, 0.8), (35, 38, 0.8, 0), (49, 38, 1.0), (49, 25, 0.8), (31, 25, 0.6)])
f.npc('f3m', 'soldier', '경계병', [(50, 18, 3.0, 180), (50, 18, 2.0, 90), (50, 18, 3.0, 180), (50, 18, 2.0, -90)])
f.npc('f3r1', 'researcher', '실험실 A 연구원', [(16, 4, 2.2, 0), (24, 8, 1.6, 180), (16, 9, 1.4, -90)])
f.npc('f3r2', 'researcher', '실험실 A 조교', [(25, 3, 2.0, 180), (21, 10, 1.4, 0)], pal='researcher2')
f.npc('f3r3', 'researcher', '실험실 B 연구원', [(34, 5, 2.0, 0), (47, 9, 1.8, 180), (40, 4, 1.4, 90)])
f.npc('f3r4', 'researcher', '실험실 B 조교', [(44, 10, 2.4, -90), (49, 3, 1.6, 180)], pal='researcher2')
f.npc('f3r5', 'researcher', '저온실 연구원', [(15, 26, 2.0, 0), (22, 33, 1.8, 180), (15, 38, 1.4, -90)])
f.npc('f3r6', 'researcher', '소독실 연구원', [(31, 26, 2.2, 90), (33, 38, 1.8, -90)], pal='researcher2')
f.npc('f3r7', 'researcher', '배양실 연구원', [(41, 26, 2.0, 0), (49, 32, 1.8, 180), (42, 38, 1.4, 0)])
f.npc('f3r8', 'researcher', '창고 연구원', [(4, 16, 2.0, 90), (7, 30, 1.6, -90), (4, 37, 1.4, 0)], pal='researcher2')
f.npc('f3r9', 'researcher', '표본실 연구원', [(61, 23, 2.0, 0), (68, 37, 1.6, 180)])
f.decorate(404); FLOORS.append(f)

# ======================================================================= 4F 소장실층 (군인, 가스)
f = Floor('4F', '소장실층', 76, 44, False, 0.6, '군인은 사방을 본다 · 등 바로 뒤만 사각지대')
f.room('비품 창고', 1, 1, 10, 15, 'store'); f.room('엘리베이터 홀', 1, 17, 10, 10, 'lift'); f.room('청소 도구실', 1, 28, 10, 15, 'store')
f.room('회의실', 12, 1, 16, 12, 'office'); f.room('비서실', 29, 1, 12, 12, 'office'); f.room('군 대기실', 42, 1, 22, 12, 'sec', allow=['soldier']); f.room('무전실', 65, 1, 10, 12, 'sec')
f.room('중앙 로비', 12, 14, 31, 16, 'hall', label=False)
f.room('소장실 전실', 44, 19, 4, 6, 'wing', label=False, decor=False)
f.room('소장실', 49, 14, 15, 16, 'dir', allow=['soldier', 'senior', 'director']); f.room('경호실', 65, 14, 10, 16, 'sec')
f.room('기록 보관실', 12, 31, 16, 12, 'store'); f.room('라운지', 29, 31, 14, 12, 'rest'); f.room('서버실', 44, 31, 14, 12, 'mech'); f.room('의무실', 59, 31, 16, 12, 'office')
f.gap(11, 21, 3, vertical=True); f.gap(5, 16); f.gap(5, 27); f.gap(11, 5, vertical=True); f.gap(11, 36, vertical=True)
f.gap(19, 13); f.gap(34, 13); f.gap(42, 13, 1); f.gap(64, 5, vertical=True); f.gap(69, 13)
f.gap(43, 21, vertical=True); f.gap(69, 30); f.gap(43, 36, vertical=True); f.gap(34, 30); f.gap(19, 30); f.gap(58, 35, vertical=True)
f.vent(11, 32); f.vent(28, 8)
f.door(48, 21, 'mil', '소장실')
f.trap('G4', 'gas', '가스 함정', [[44, 19, 4, 6]])
f.lift([(0, 21), (0, 22), (0, 23)]); f.arrive('lift', 2.5, 22.5)
f.d['spawns'] = [[73.5, 41.5], [73.5, 2.5]]; f.d['reinforce'] = [dict(char='soldier', n=3)]
f.notice(41, 26, '[경고] 비상 가스 장치 가동 중.\n해제: 2F 보안 통제실.\n소장실 출입 — 경호(군) 카드.')
f.notice(36, 1, '[소장 일정]\n소장님은 소장실에서 거의 나오지 않으심.\n경호병 동행 시에만 면담 가능.')
f.notice(60, 1, '[군 대기실]\n순찰 — 로비·복도 상시. 사방 경계.\n※ 등 뒤 확인 철저히 할 것 (지난주 사고).')
for (x, y) in [(18, 18), (18, 25), (27, 18), (27, 25), (36, 18), (36, 25)]: f.fill('S', x, y, 2, 1)
f.npc('f4m1', 'soldier', '로비 순찰병', [(14, 16, 0.8), (40, 16, 1.0), (40, 27, 0.8), (14, 27, 1.0)])
f.npc('f4m2', 'soldier', '북쪽 순찰병', [(14, 9, 1.2, 180), (26, 4, 1.0), (32, 9, 0.8), (39, 4, 1.2, 0), (32, 9, 0.6)])
f.npc('f4m3', 'soldier', '소장 경호병', [(52, 17, 2.4, 0), (61, 17, 1.8, 90), (61, 27, 1.8, 180), (52, 27, 2.0, -90)])
f.npc('f4m4', 'soldier', '남쪽 순찰병', [(14, 34, 0.8), (26, 40, 1.0), (40, 34, 0.8), (55, 40, 1.2, 0), (40, 34, 0.6)])
f.npc('f4m5', 'soldier', '대기실 군인 1', [(45, 4, 2.4, 0), (60, 9, 2.0, 180)])
f.npc('f4m6', 'soldier', '대기실 군인 2', [(55, 10, 3.0, 90), (50, 4, 2.4, 0)])
f.npc('f4m7', 'soldier', '무전병', [(69, 6, 3.4, 180), (69, 6, 2.4, 90), (72, 10, 1.6, -90)])
f.npc('f4g1', 'guard', '경호실 경비', [(67, 17, 1.8, 0), (72, 27, 1.6, 180), (67, 27, 1.4, -90)])
f.npc('f4g2', 'guard', '의무실 경비', [(62, 34, 1.6, 0), (72, 40, 1.6, 180), (64, 40, 1.2, 90)])
f.npc('f4s', 'senior', '고위 연구원 오', [(32, 5, 2.4, 0), (38, 10, 1.4), (30, 20, 1.0), (32, 5, 1.0)])
f.npc('f4r1', 'researcher', '회의실 연구원 1', [(15, 4, 2.4, 0), (25, 10, 1.6, 180)])
f.npc('f4r2', 'researcher', '회의실 연구원 2', [(24, 4, 2.0, 180), (16, 10, 1.6, 0)], pal='researcher2')
f.npc('f4r3', 'researcher', '라운지 연구원', [(32, 34, 2.6, 0), (39, 40, 1.8, 180)])
f.npc('f4r4', 'researcher', '기록실 연구원', [(15, 34, 2.4, 0), (24, 40, 1.6, 180)], pal='researcher2')
f.npc('f4r5', 'researcher', '서버실 연구원', [(47, 34, 2.4, 0), (54, 40, 1.8, 180)])
f.npc('f4d', 'director', '연구소장', [(56, 22, 4.0, 180), (59, 18, 2.4, 90), (56, 22, 3.0, 0)])
f.decorate(505); FLOORS.append(f)

# ======================================================================= 5F 옥상 (헬기)
f = Floor('5F', '옥상', 76, 46, False, 0.72, '소장의 이름으로 헬기를 불러라')
f.room('옥상', 1, 1, 50, 30, 'roof', label=False); f.room('헬기 계류장', 52, 1, 23, 16, 'roof', label=False); f.room('옥상 동편', 52, 18, 23, 13, 'roof', label=False)
f.room('옥상 남서', 1, 32, 30, 13, 'roof', label=False); f.room('옥상 남동', 45, 32, 30, 13, 'roof', label=False)
f.room('엘리베이터 기계실', 32, 32, 12, 13, 'lift')
f.gap(51, 22, 3, vertical=True); f.gap(10, 31, 3); f.gap(60, 31, 3); f.gap(37, 31)
f.door(51, 9, 'dir', '헬기 계류장 게이트')
f.console(49, 7, 'dir', '헬기 호출 콘솔', heli=True)
f.d['pad'] = [58, 3, 11, 9]
f.lift([(37, 45), (38, 45)]); f.arrive('lift', 37.5, 42.5)
f.d['spawns'] = [[2.5, 43.5], [73.5, 43.5]]; f.d['reinforce'] = [dict(char='soldier', n=4)]
f.notice(33, 32, '[옥상 수칙]\n헬기 호출 — 소장 인증 필수. 콘솔은 게이트 옆.\n호출 후 착륙까지 약 15초.')
for (x, y, w, h) in [(6, 5, 3, 2), (16, 5, 3, 2), (6, 14, 3, 2), (28, 9, 3, 2), (40, 4, 3, 2), (40, 20, 3, 2), (16, 24, 3, 2), (60, 22, 3, 2), (8, 36, 3, 2), (22, 38, 3, 2), (52, 38, 3, 2), (66, 36, 3, 2)]: f.fill('A', x, y, w, h)
for (x, y) in [(25, 18), (12, 20), (35, 26), (68, 26), (48, 36)]: f.fill('W', x, y, 2, 2)
for (x, y) in [(20, 12), (33, 15), (45, 12), (57, 26), (30, 40), (62, 41)]: f.fill('M', x, y, 1, 2)
for (x, y) in [(3, 3), (47, 29), (3, 28), (73, 29), (29, 43), (46, 43)]: f.obj('trash', x, y)
f.obj('cart', 2, 15); f.obj('cart', 49, 2); f.obj('chair', 34, 35); f.obj('chair', 42, 40)
f.npc('f5m1', 'soldier', '옥상 순찰병', [(3, 3, 0.8), (46, 3, 1.0), (46, 28, 0.8), (3, 28, 1.0)])
f.npc('f5m2', 'soldier', '게이트 경계병', [(48, 11, 3.0, 180), (48, 11, 2.0, 90), (48, 11, 3.0, 0), (48, 11, 2.0, -90)])
f.npc('f5m3', 'soldier', '중앙 순찰병', [(12, 11, 0.8), (36, 11, 1.0), (36, 18, 0.8), (12, 18, 1.0)])
f.npc('f5m4', 'soldier', '기계실 경계병', [(38, 36, 2.6, 90), (38, 36, 2.0, 0), (38, 36, 2.6, 180)])
f.npc('f5m5', 'soldier', '동편 순찰병', [(54, 20, 1.0), (73, 20, 1.0), (73, 29, 0.8), (54, 29, 1.0)])
f.npc('f5m6', 'soldier', '계류장 경계병', [(55, 14, 2.4, 0), (72, 14, 2.0, 180), (72, 3, 1.6, 180)])
f.npc('f5m7', 'soldier', '남서 순찰병', [(3, 34, 1.0), (28, 34, 1.0), (28, 43, 0.8), (3, 43, 1.0)])
f.npc('f5m8', 'soldier', '남동 순찰병', [(47, 34, 1.0), (73, 34, 1.0), (73, 43, 0.8), (47, 43, 1.0)])
FLOORS.append(f)

json.dump([fl.json() for fl in FLOORS], open(OUT, 'w'), ensure_ascii=False, indent=1)
print('wrote', OUT, [(fl.d['id'], fl.d['w'], fl.d['h'], len(fl.d['npcs'])) for fl in FLOORS])
