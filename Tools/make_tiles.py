# 연구소 타일 PNG 생성기 (16x16 픽셀아트, 레퍼런스 팔레트)
# 사용: python3 Tools/make_tiles.py Assets/Resources/Art
# 만들어진 PNG는 그냥 그림 파일이라, 포토샵/Aseprite에서 열어 고쳐도 된다. (이 스크립트를 다시 돌리면 덮어씀)
import os, sys, random
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/Resources/Art"
os.makedirs(OUT, exist_ok=True)
T = 16

def C(h, a=255):
    h = h.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)

# ===== 팔레트 (레퍼런스에서 뽑음) =====
VOID = C('#05061c'); VOID2 = C('#0a0b26')
TRIM_L = C('#aaa2cc'); TRIM_M = C('#8a82b3'); TRIM_D = C('#4a4475')
FACE = C('#393d7d'); FACE_HI = C('#4a4f92'); FACE_D = C('#2e3170'); LEDGE = C('#1c1e4c'); LEDGE_HI = C('#565ba0')
LIGHT = C('#9ff8ff'); LIGHT_G = C('#46c6ec')
FLOOR = C('#212353'); FLOOR_LN = C('#16173d'); FLOOR_HI = C('#2a2d62'); FLOOR_SPOT = C('#1a1b46')
DESK = C('#c6c1da'); DESK_E = C('#9e98bc'); DESK_S = C('#6e6992'); DESK_D = C('#4a4670')
MON = C('#d8d5e6'); MON_D = C('#9f9bb8'); SCR = C('#16263a'); SCR_T = C('#6fe8f2')
CAB = C('#2f3159'); CAB_F = C('#43467a'); CAB_S = C('#262848'); CAB_H = C('#8d91bd')
LEAF1 = C('#2c6e45'); LEAF2 = C('#4aa65b'); LEAF3 = C('#1c4f33'); POT = C('#7a3e58'); POT_D = C('#552a40'); POT_H = C('#9a5674')
GLASS = C('#8fe9ff', 120); LIQ = C('#2fb4d8'); LIQ_H = C('#6be6f6'); LIQ_D = C('#1d7fb0'); METAL = C('#4a4f86'); METAL_H = C('#6d73ad'); METAL_D = C('#2d3060')
DOOR = C('#6e78ad'); DOOR_H = C('#8e98c9'); DOOR_D = C('#4c5589'); DOOR_F = C('#2a2d5e'); SEAM = C('#1e2048')
GRATE = C('#5c608f'); GRATE_D = C('#26284f')
SRV = C('#1b1d3d'); SRV_F = C('#262952'); LED_G = C('#59f2a6'); LED_R = C('#ff5b6e'); LED_C = C('#7ff3ff'); LED_Y = C('#f2c230')
VEND = C('#3b3170'); VEND_D = C('#2a2252'); VEND_W = C('#5fe0f4'); RED = C('#ff5b6e')
PAPER = C('#d9dcef'); PAPER_D = C('#a3a8c8'); INK = C('#6c7199')
STAIN = C('#0f1030', 170)
HAZ_Y = C('#f2c230'); HAZ_K = C('#16162a')
WHITE = C('#ffffff')
CLEAR = (0, 0, 0, 0)

class P:
    def __init__(s, w=T, h=T, fill=CLEAR):
        s.w, s.h = w, h; s.im = Image.new('RGBA', (w, h), fill); s.px = s.im.load()
    def p(s, x, y, c):
        if 0 <= x < s.w and 0 <= y < s.h:
            if len(c) == 4 and c[3] < 255:
                r, g, b, a = s.px[x, y]; ca = c[3] / 255
                if a == 0: s.px[x, y] = c; return
                s.px[x, y] = (round(c[0] * ca + r * (1 - ca)), round(c[1] * ca + g * (1 - ca)), round(c[2] * ca + b * (1 - ca)), max(a, c[3]))
            else: s.px[x, y] = c
    def r(s, x, y, w, h, c):
        for yy in range(y, y + h):
            for xx in range(x, x + w): s.p(xx, yy, c)
    def hl(s, x0, x1, y, c):
        for x in range(x0, x1 + 1): s.p(x, y, c)
    def vl(s, x, y0, y1, c):
        for y in range(y0, y1 + 1): s.p(x, y, c)
    def circ(s, cx, cy, rad, c):
        for y in range(s.h):
            for x in range(s.w):
                if (x + .5 - cx) ** 2 + (y + .5 - cy) ** 2 <= rad * rad: s.p(x, y, c)
    def save(s, name): s.im.save(os.path.join(OUT, name + '.png'))

def tint(c, d):
    return tuple(max(0, min(255, c[i] + d[i])) for i in range(3)) + (c[3],)

# ===== 바닥 =====
KINDS = {  # 방 종류별 미세한 색 차이 (r,g,b 더하기)
    'iso': (0, 5, 8), 'lab': (0, 0, 0), 'store': (4, 0, -6), 'hall': (-1, 0, 2), 'rest': (6, 0, -4), 'mech': (-2, -2, -6),
    'exit': (-3, 6, 0), 'office': (2, 2, 0), 'sec': (3, 0, -2), 'lift': (-2, 4, 5), 'cold': (-4, 6, 10), 'wing': (8, -3, 5),
    'dir': (14, -4, -6), 'roof': (8, 10, -12),
}

def floor_base(kind, variant, seed=0):
    d = KINDS[kind]
    f, ln, hi, sp = tint(FLOOR, d), tint(FLOOR_LN, d), tint(FLOOR_HI, d), tint(FLOOR_SPOT, d)
    q = P(fill=f)
    if kind == 'roof':  # 옥상: 콘크리트 판 (줄 대신 점)
        rnd = random.Random(hash(('roof', variant)) & 0xffff)
        q.hl(0, 15, 15, ln); q.vl(15, 0, 15, ln)
        for _ in range(10): q.p(rnd.randrange(16), rnd.randrange(16), sp)
    else:
        q.hl(0, 15, 15, ln); q.vl(15, 0, 15, ln)          # 아래·오른쪽 줄눈
        q.hl(0, 14, 0, hi); q.vl(0, 0, 14, hi)            # 위·왼쪽 밝은 모서리
    rnd = random.Random(variant * 977 + len(kind) * 13 + seed * 7919)
    if variant == 2:   # 금 간 타일
        x, y = rnd.randrange(3, 9), rnd.randrange(3, 7)
        for i in range(6):
            q.p(x, y, sp); x += rnd.choice([0, 1, 1]); y += 1
        q.p(x + 1, y - 2, sp)
    elif variant == 3:  # 얼룩
        cx, cy = rnd.randrange(5, 11), rnd.randrange(5, 11)
        for y in range(16):
            for x in range(16):
                if (x - cx) ** 2 / 9 + (y - cy) ** 2 / 4 <= 1 and rnd.random() < .85: q.p(x, y, STAIN)
    elif variant == 4:  # 흩어진 종이 (위치·개수 무작위)
        for _ in range(rnd.choice([1, 1, 2])):
            x, y = rnd.randrange(1, 10), rnd.randrange(1, 10); w, h = rnd.choice([(5, 4), (4, 5), (4, 3)])
            q.r(x, y, w, h, PAPER); q.hl(x + 1, x + w - 2, y + 1, INK); q.r(x, y + h, w, 1, PAPER_D)
    return q

for k in KINDS:
    floor_base(k, 1).save('tile_floor_' + k)
    floor_base(k, 2).save('tile_floor_' + k + '_2')
    floor_base(k, 3).save('tile_floor_' + k + '_3')
    if k != 'roof':  # 옥상엔 종이 안 흩어짐
        floor_base(k, 4).save('tile_floor_' + k + '_4')
        floor_base(k, 4, 1).save('tile_floor_' + k + '_5')

# ===== 벽 (검은 틈) + 테두리 조각 =====
q = P(fill=VOID); q.save('tile_wall')
q = P(fill=VOID); q.save('tile_wall_edge')  # 예전 이름 호환
# 테두리: 바닥 쪽 면에 라벤더 띠 (바깥 1px 어두운 선 + 2px 밝은 띠). 회전해서 4방향.
def trim_side():  # 아래쪽(S) 면 기준
    q = P(); q.hl(0, 15, 12, TRIM_D); q.hl(0, 15, 13, TRIM_L); q.hl(0, 15, 14, TRIM_M); q.hl(0, 15, 15, TRIM_D); return q
def trim_corner():  # 오른쪽 아래(SE) 안쪽 모서리 조각
    q = P(); q.r(12, 12, 4, 4, TRIM_D); q.r(13, 13, 2, 2, TRIM_L); q.p(14, 14, TRIM_M); return q
s = trim_side().im
s.save(os.path.join(OUT, 'wall_trim_s.png'))
s.rotate(180).save(os.path.join(OUT, 'wall_trim_n.png'))
s.rotate(90).save(os.path.join(OUT, 'wall_trim_e.png'))     # PIL 회전은 반시계
s.rotate(-90).save(os.path.join(OUT, 'wall_trim_w.png'))
c = trim_corner().im
c.save(os.path.join(OUT, 'wall_corner_se.png'))
c.transpose(Image.FLIP_LEFT_RIGHT).save(os.path.join(OUT, 'wall_corner_sw.png'))
c.transpose(Image.FLIP_TOP_BOTTOM).save(os.path.join(OUT, 'wall_corner_ne.png'))
c.rotate(180).save(os.path.join(OUT, 'wall_corner_nw.png'))

# ===== 벽면 (북쪽 벽 아래 칸 윗부분) =====
def face(light, red=False):
    q = P()
    q.r(0, 0, 16, 11, FACE); q.hl(0, 15, 0, FACE_D); q.hl(0, 15, 1, FACE_HI)
    q.hl(0, 15, 10, FACE_D); q.hl(0, 15, 11, LEDGE_HI); q.r(0, 12, 16, 1, LEDGE); q.r(0, 13, 16, 1, C('#000000', 70))
    if light:
        col, glow = (RED, C('#ff9aa6', 120)) if red else (LIGHT, C('#46c6ec', 110))
        q.hl(2, 13, 2, glow); q.hl(2, 13, 3, col); q.hl(3, 12, 4, glow)
    return q
face(False).save('wall_face'); face(True).save('wall_face_light'); face(True, True).save('wall_face_alarm')
q = face(False); q.r(4, 4, 6, 4, MON_D); q.r(5, 5, 4, 2, SCR); q.hl(5, 7, 5, SCR_T); q.save('wall_face_poster')   # 벽 모니터
q = face(False); q.r(10, 3, 3, 4, PAPER); q.hl(10, 11, 4, INK); q.r(3, 5, 3, 3, PAPER_D); q.save('wall_face_note')   # 벽 메모

# ===== 환풍구 (벽 속 통로) =====
q = P(fill=VOID); q.r(2, 3, 12, 10, GRATE_D)
for y in range(4, 12, 2): q.hl(3, 12, y, GRATE)
q.r(2, 3, 12, 1, METAL_D); q.save('tile_vent')

# ===== 유리벽 =====
q = P(); q.r(0, 0, 16, 16, C('#7fdcf0', 70)); q.vl(5, 0, 15, C('#c8f6ff', 140)); q.vl(6, 0, 15, C('#c8f6ff', 70)); q.hl(0, 15, 0, C('#aaa2cc', 160)); q.hl(0, 15, 15, C('#aaa2cc', 160))
q.save('tile_glass')

# ===== 가구 =====
def desk(variant):
    q = P(); q.r(0, 0, 16, 13, DESK); q.hl(0, 15, 0, C('#dedaea')); q.r(0, 13, 16, 3, DESK_S); q.hl(0, 15, 13, DESK_E)
    if variant == 2:  # 컴퓨터
        q.r(4, 1, 8, 7, MON); q.r(5, 2, 6, 4, SCR); q.hl(6, 8, 3, SCR_T); q.hl(6, 7, 4, SCR_T); q.r(5, 8, 6, 1, MON_D)
        q.r(3, 10, 9, 2, C('#8d89a8')); q.hl(4, 10, 10, C('#b5b1cc')); q.r(13, 10, 2, 2, C('#8d89a8'))
    elif variant == 3:  # 서류
        q.r(2, 3, 5, 6, PAPER); q.hl(3, 5, 4, INK); q.hl(3, 4, 6, INK); q.r(8, 5, 6, 5, PAPER); q.hl(9, 12, 6, INK); q.hl(9, 11, 8, INK)
    elif variant == 4:  # 머그컵 + 메모
        q.r(10, 3, 3, 4, C('#e46b7f')); q.p(13, 4, C('#e46b7f')); q.r(10, 3, 3, 1, C('#7a2c3a')); q.r(3, 6, 4, 4, C('#f2e08a'))
    return q
for v, n in [(1, 'tile_table'), (2, 'tile_table_2'), (3, 'tile_table_3'), (4, 'tile_table_4')]: desk(v).save(n)
# 아래에도 책상이 이어질 때 (앞면 없이 윗면만)
for v, n in [(1, 'tile_table_inner'), (3, 'tile_table_inner_2'), (2, 'tile_table_inner_3')]:
    q = desk(v); q.r(0, 13, 16, 3, DESK); q.hl(0, 15, 15, C('#d0cbe2')); q.save(n)

def cabinet():
    q = P(); q.r(1, 0, 14, 16, CAB); q.r(2, 1, 12, 13, CAB_F); q.hl(2, 13, 7, CAB_S); q.r(6, 3, 4, 1, CAB_H); q.r(6, 10, 4, 1, CAB_H); q.r(1, 14, 14, 2, CAB_S); return q
def plant():
    q = P(); q.r(4, 10, 8, 5, POT); q.hl(4, 11, 10, POT_H); q.r(5, 15, 6, 1, POT_D); q.r(4, 13, 8, 1, POT_D)
    for (x, y, c) in [(7, 2, LEAF2), (8, 1, LEAF2), (5, 3, LEAF1), (10, 3, LEAF1), (3, 5, LEAF2), (12, 5, LEAF2), (6, 5, LEAF3), (9, 5, LEAF3),
                      (4, 7, LEAF1), (11, 7, LEAF1), (7, 6, LEAF2), (8, 7, LEAF1), (6, 8, LEAF2), (9, 8, LEAF2), (2, 6, LEAF1), (13, 6, LEAF1)]:
        q.r(x, y, 2, 2, c)
    return q
def shelf():
    q = P(); q.r(1, 0, 14, 16, CAB); q.r(2, 1, 12, 14, CAB_S)
    for y in (5, 10): q.hl(2, 13, y, CAB_F)
    for x, c in [(3, LED_C), (5, C('#b48cf0')), (8, C('#e46b7f')), (11, LED_G)]: q.r(x, 2, 2, 3, c); q.p(x, 1, MON)
    for x, c in [(3, PAPER), (6, C('#f2e08a')), (10, PAPER_D)]: q.r(x, 7, 3, 3, c)
    q.r(3, 12, 10, 2, CAB_F); return q
cabinet().save('tile_shelf'); plant().save('tile_shelf_2'); shelf().save('tile_shelf_3')

q = P(); q.r(1, 0, 14, 15, VEND); q.r(3, 2, 7, 8, VEND_W); q.r(4, 3, 5, 6, C('#a9f3fb'))
for y in (4, 6): q.hl(4, 8, y, C('#2fb4d8'))
q.r(11, 3, 2, 2, RED); q.r(11, 6, 2, 1, LED_Y); q.r(3, 11, 7, 2, VEND_D); q.r(1, 15, 14, 1, VEND_D); q.save('tile_vending')

q = P(); q.r(1, 1, 14, 12, C('#4b4f86')); q.r(2, 2, 12, 10, SCR)       # 게시물 = 벽 화면 (DNA)
for x in range(3, 13):
    import math
    y1 = 7 + round(2.5 * math.sin(x * 0.9)); y2 = 7 - round(2.5 * math.sin(x * 0.9))
    q.p(x, y1, SCR_T); q.p(x, y2, LED_G)
    if x % 2 == 0: q.vl(x, min(y1, y2) + 1, max(y1, y2) - 1, C('#3b7d8f'))
q.r(1, 13, 14, 2, METAL_D); q.save('tile_notice')

def server(v):
    q = P(); q.r(1, 0, 14, 16, SRV); q.r(2, 1, 12, 13, SRV_F)
    rnd = random.Random(v * 31)
    for y in range(2, 13, 3):
        q.hl(3, 12, y + 1, SRV)
        for x in (4, 6, 8):
            q.p(x, y, rnd.choice([LED_G, LED_G, LED_C, LED_R, SRV_F]))
    q.r(1, 14, 14, 2, C('#121330')); return q
server(1).save('tile_machine'); server(2).save('tile_machine_2')

q = P(); q.r(0, 3, 16, 10, C('#3a3e72')); q.hl(0, 15, 3, C('#565ba0')); q.r(0, 13, 16, 3, C('#25285a'))   # 콘솔
q.r(3, 0, 10, 7, C('#2a2d5e')); q.r(4, 1, 8, 5, SCR); q.hl(5, 9, 2, SCR_T); q.hl(5, 7, 3, LED_G); q.hl(5, 10, 4, SCR_T)
q.r(4, 9, 8, 2, C('#565ba0')); q.p(13, 9, RED); q.p(13, 11, LED_G); q.save('tile_console')

q = P(); q.r(0, 0, 16, 16, DOOR_F); q.r(1, 3, 14, 13, DOOR); q.vl(1, 3, 15, DOOR_H); q.vl(8, 3, 15, DOOR_H); q.vl(7, 3, 15, SEAM)   # 엘리베이터
q.hl(2, 13, 1, LIGHT); q.hl(2, 13, 0, LIGHT_G); q.hl(1, 14, 2, DOOR_F); q.save('tile_lift')

q = P(fill=tint(FLOOR, (0, 4, 4)))   # 계단
for i, y in enumerate(range(0, 16, 4)):
    q.r(0, y, 16, 3, C('#2e3270') if i % 2 == 0 else C('#383c7c')); q.hl(0, 15, y, C('#8f87bb')); q.hl(0, 15, y + 3, C('#17183f'))
q.save('tile_stairs')

# ===== 문 (1칸) =====
q = P(); q.r(0, 0, 16, 16, DOOR_F); q.r(1, 2, 14, 14, DOOR); q.vl(1, 2, 15, DOOR_H); q.vl(8, 2, 15, DOOR_H); q.vl(7, 2, 15, SEAM)
q.hl(2, 13, 0, LIGHT); q.hl(1, 14, 1, DOOR_F); q.save('door_closed')
q = P()   # 카드 색으로 칠해지는 줄무늬 (흰색 = 카드 색)
for y in range(6, 11):
    for x in range(2, 14):
        if (x + y) % 4 < 2: q.p(x, y, WHITE)
        else: q.p(x, y, C('#000000', 200))
q.save('door_stripes')
q = P(); q.r(0, 0, 16, 16, VOID2); q.r(0, 2, 3, 14, DOOR); q.vl(0, 2, 15, DOOR_H); q.r(13, 2, 3, 14, DOOR); q.vl(13, 2, 15, DOOR_H)
q.hl(2, 13, 0, LED_G); q.r(3, 2, 10, 14, tint(FLOOR, (-6, -6, -10))); q.save('door_open')

# ===== 큰 설비 =====
q = P(48, 32)   # 실외기 3x2
q.r(1, 1, 46, 30, C('#3a3e72')); q.r(2, 2, 44, 27, C('#2b2e60')); q.hl(1, 46, 1, C('#565ba0'))
for cx in (14, 34):
    q.circ(cx, 15, 10, C('#1b1d3d')); q.circ(cx, 15, 9, GRATE_D)
    for k in range(-8, 9, 3): q.hl(cx - 8, cx + 8, 15 + k, GRATE) if abs(k) < 8 else None
    q.circ(cx, 15, 2, METAL_H)
q.r(1, 29, 46, 2, C('#17183f')); q.save('big_aircon')

q = P(32, 32)   # 표본 수조 2x2 (위에서 비스듬히 본 원통)
q.r(4, 4, 24, 26, METAL_D)
q.r(6, 6, 20, 20, LIQ); q.r(6, 6, 20, 3, LIQ_H); q.vl(9, 6, 25, C('#c8f6ff')); q.vl(10, 6, 25, LIQ_H); q.vl(23, 6, 25, LIQ_D)
for (x, y) in [(13, 12), (18, 17), (15, 21), (20, 10)]: q.p(x, y, C('#d6fbff'))
q.r(3, 2, 26, 4, METAL); q.hl(3, 28, 2, METAL_H); q.r(3, 26, 26, 4, METAL); q.hl(3, 28, 26, METAL_H); q.r(3, 30, 26, 1, C('#16173d'))
q.hl(12, 19, 3, LIGHT); q.hl(12, 19, 28, LED_C); q.save('big_tank')

q = P(176, 144)  # 헬리패드 11x9
q.r(0, 0, 176, 144, C('#23263f'))
for x in range(0, 176, 16): q.vl(x, 0, 143, C('#1b1d33'))
for y in range(0, 144, 16): q.hl(0, 175, y, C('#1b1d33'))
for i in range(4, 172, 12):
    q.r(i, 3, 6, 3, HAZ_Y); q.r(i, 138, 6, 3, HAZ_Y)
for i in range(4, 140, 12):
    q.r(3, i, 3, 6, HAZ_Y); q.r(170, i, 3, 6, HAZ_Y)
cx, cy = 88, 72
for y in range(144):
    for x in range(176):
        d = ((x + .5 - cx) ** 2 + (y + .5 - cy) ** 2) ** .5
        if 52 <= d <= 56: q.p(x, y, C('#d9dcef'))
q.r(cx - 20, cy - 28, 9, 56, HAZ_Y); q.r(cx + 11, cy - 28, 9, 56, HAZ_Y); q.r(cx - 20, cy - 4, 40, 8, HAZ_Y)
q.save('helipad')

q = P(64, 64)    # B1 깨진 수조 (바닥 장식)
for y in range(64):
    for x in range(64):
        d = ((x + .5 - 34) ** 2 / 1.0 + (y + .5 - 36) ** 2 / .7) ** .5
        if d < 26 and random.Random(x * 64 + y).random() < .9: q.p(x, y, C('#2fb4d8', 60))
for y in range(64):
    for x in range(64):
        d = ((x + .5 - 32) ** 2 + (y + .5 - 32) ** 2) ** .5
        if 13 <= d <= 15: q.p(x, y, METAL)
        if 11 <= d < 13: q.p(x, y, METAL_D)
rnd = random.Random(7)
for i in range(22):
    import math
    a = rnd.random() * 6.283; dd = rnd.uniform(16, 28)
    x, y = int(32 + math.cos(a) * dd), int(32 + math.sin(a) * dd)
    q.r(x, y, rnd.choice([1, 2]), rnd.choice([1, 2]), C('#b8f4ff', 220))
q.save('tank_broken')

print('done', len([f for f in os.listdir(OUT) if f.endswith('.png')]), 'png')
