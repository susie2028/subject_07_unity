# 주인공(표본 07) 스프라이트 생성기 — 눈 없는 보라색 지렁이 괴물, 옆모습(오른쪽을 봄)
# 사용: python3 Tools/make_player.py Assets/Resources/Art
# 만드는 그림:
#   player_<동작>_<방향>_<번호>
#     동작: idle(숨쉬기 2장) / crawl(기어가기 4장) / eat(먹기 4장, 입이 점점 크게)
#     방향: side(옆, 오른쪽을 봄 — 왼쪽은 게임이 좌우 반전) / down(앞모습) / up(뒷모습)
# 평소에도 입을 조금 벌리고 있고(MOUTH_IDLE), 몸에 줄무늬 2줄.
import os, sys, math
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/Resources/Art"
W, H, G = 28, 20, 17  # 캔버스, 바닥선(y)

def C(h, a=255):
    h = h.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)
OUTL = C('#24123a'); DARK = C('#5b2f8f'); MID = C('#7a48bd'); LIGHT = C('#9a6bd9'); HI = C('#e2d4f7'); HI2 = C('#b996e8')
STRIPE = C('#4a2478'); MOUTH = C('#170a24'); MOUTH2 = C('#2c1240'); TEETH = C('#f1ecf9'); DROOL = C('#7ab6ef'); SHADOW = C('#000000', 70)

def body(L, R, x_front=22, bump=0.0, lift=0.0, squash=1.0):
    """둥근 머리(타원) + 뒤로 가늘어지는 몸 + 위로 살짝 말린 꼬리 끝. 반환: mask[y][x]"""
    m = [[False] * W for _ in range(H)]
    hx, hy, hrx, hry = x_front - R * 1.05, G - R * squash - lift, R * 1.05, R * squash   # 머리
    bx0 = x_front - L                                                                 # 꼬리 끝 x
    for y in range(H):
        for x in range(W):
            X, Y = x + .5, y + .5
            inside = ((X - hx) / hrx) ** 2 + ((Y - hy) / hry) ** 2 <= 1
            u = (X - bx0) / max(1, (hx - bx0))                                          # 0=꼬리 끝, 1=머리 중심
            if 0 <= u <= 1:
                th = R * squash * (0.18 + 0.9 * u ** 0.85) + bump * R * math.exp(-((u - 0.5) / 0.22) ** 2)
                bottom = G - lift - (3.0 * (1 - u / 0.2) ** 1.6 if u < 0.2 else 0)          # 꼬리 끝이 위로 말림
                if bottom - th <= Y <= bottom: inside = True
            if inside and Y <= G + 0.5: m[y][x] = True
    return m

def paint(m, mouth=0.0, closed_mouth=True, gloss=True):
    im = Image.new('RGBA', (W, H)); px = im.load()
    cols = [x for x in range(W) if any(m[y][x] for y in range(H))]
    if not cols: return im
    xf = max(cols); xb = min(cols)
    jaw = None
    if 0 < mouth < 0.5:   # 평소 입: 머리 앞쪽을 쐐기 모양으로 파서 벌어진 턱
        ys = [y for y in range(H) if m[y][xf - 1]]
        jcy = (min(ys) + max(ys)) / 2 + 2.2; jcx = xf - 0.5; jrx, jry = 4.2, 2.4
        for y in range(H):
            for x in range(W):
                if m[y][x] and ((x + .5 - jcx) / jrx) ** 2 + ((y + .5 - jcy) / jry) ** 2 <= 1 and x >= xf - 1.5 - (1 - abs(y + .5 - jcy) / jry) * 1.5:
                    m[y][x] = False
        jaw = (jcx, jcy, jrx, jry)
    # 바닥 그림자
    for x in range(xb, xf + 1):
        px[x, G + 1] = SHADOW
    for x in range(W):
        ys = [y for y in range(H) if m[y][x]]
        if not ys: continue
        top, bot = min(ys), max(ys); th = bot - top + 1
        for y in ys:
            f = (y - top) / max(1, th - 1)
            c = LIGHT if f < 0.28 else MID if f < 0.72 else DARK
            # 가로 줄무늬 (지렁이 마디) — 몸 뒤쪽에 은은하게
            d = bot - y
            if x < xf - 2 and th >= 5 and (d == 2 or d == 5): c = STRIPE
            px[x, y] = c
    # 광택
    if gloss:
        ys = [y for y in range(H) if m[y][xf - 4]] if xf - 4 >= 0 else []
        if ys:
            gy = min(ys) + 1
            for (dx, dy, c) in [(-4, 0, HI), (-3, 0, HI), (-4, 1, HI2), (-5, 1, HI2), (-2, 1, HI2), (-3, 1, HI)]:
                x, y = xf + dx, gy + dy
                if 0 <= x < W and 0 <= y < H and m[y][x]: px[x, y] = c
    # 입
    ys_front = [y for y in range(H) if m[y][xf - 1]] or [y for y in range(H) if m[y][xf - 4]]
    if jaw:  # 파낸 턱 안쪽 (목구멍) + 혀 + 윗니
        jcx, jcy, jrx, jry = jaw
        for y in range(H):
            for x in range(W):
                if m[y][x] and ((x + .5 - jcx) / (jrx + 1.6)) ** 2 + ((y + .5 - jcy) / (jry + 0.6)) ** 2 <= 1: px[x, y] = MOUTH
        tx = int(jcx - jrx + 1.5); ty = int(jcy + 1)
        for x in (tx, tx + 1):
            if 0 <= ty < H and m[ty][x]: px[x, ty] = C('#9a3a6c')
        ux, uy = int(jcx - 1.5), int(jcy - jry + 0.5)
        if 0 <= uy < H and m[uy][ux]: px[ux, uy] = TEETH
    elif mouth > 0 and ys_front:
        cy = (min(ys_front) + max(ys_front)) / 2 + 0.5
        smile = mouth < 0.5
        if smile: cy += 2.0
        rx = (3.4 if smile else 1.5 + mouth * 5.0); ry = (2.6 if smile else 0.8 + mouth * mouth * 4.8)
        cx = (xf + 0.8) if smile else (xf - rx * 0.25 + 0.5)    # 평소엔 머리 앞 끝에 걸쳐서 바깥으로 벌어진 입
        for y in range(H):
            for x in range(W):
                if not m[y][x]: continue
                d = ((x + .5 - cx) / rx) ** 2 + ((y + .5 - cy) / ry) ** 2
                if d <= 1 and (not smile or y + .5 >= cy - 0.3): px[x, y] = MOUTH2 if d > 0.7 else MOUTH
        if smile:  # 혀
            for x in (xf - 2, xf - 1):
                y = int(cy + 1.5)
                if 0 <= y < H and m[y][x]: px[x, y] = C('#9a3a6c')
        if mouth >= 0.5:  # 이빨
            n = 2 if mouth < 0.8 else 3
            for i in range(n):
                tx = int(cx - rx * 0.55 + i * (rx * 1.1 / max(1, n - 1)) if n > 1 else cx)
                ty0 = int(cy - ry) + 1; ty1 = int(cy + ry) - 1
                for (x, y) in [(tx, ty0), (tx, ty0 + 1 if mouth > 0.8 else ty0), (tx, ty1), (tx, ty1 - 1 if mouth > 0.8 else ty1)]:
                    if 0 <= x < W and 0 <= y < H and m[y][x]: px[x, y] = TEETH
        if mouth >= 0.8:  # 침
            x, y = int(cx), int(cy + ry)
            for dy in range(0, 3):
                if 0 <= y + dy < H: px[x, y + dy] = DROOL
    elif closed_mouth and ys_front:
        cy = int((min(ys_front) + max(ys_front)) / 2) + 2
        for x in range(xf - 3, xf):
            if m[cy][x]: px[x, cy] = OUTL
        if m[cy - 1][xf - 4]: px[xf - 4, cy - 1] = OUTL
    # 외곽선
    out = im.copy(); op = out.load()
    for y in range(H):
        for x in range(W):
            if m[y][x]: continue
            if any(0 <= x + dx < W and 0 <= y + dy < H and m[y + dy][x + dx] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                if y <= G: op[x, y] = OUTL
    return out

def save(im, name): im.save(os.path.join(OUT, name + '.png'))

# ===================== 앞모습 / 뒷모습 =====================
def blob_mask(rx, ry, cx=14.0, lift=0.0, tail=None):
    """둥근 몸(위 반원 + 넓은 밑판). tail=(dx, dy, r) 꼬리 원 추가"""
    m = [[False] * W for _ in range(H)]
    cy = G - ry - lift
    for y in range(H):
        for x in range(W):
            X, Y = x + .5, y + .5
            ins = ((X - cx) / rx) ** 2 + ((Y - cy) / ry) ** 2 <= 1
            ins |= ((X - cx) / (rx * 1.08)) ** 2 + ((Y - (G - 2.2 - lift)) / 2.6) ** 2 <= 1     # 퍼진 밑판
            if tail:
                tx, ty, tr = tail; ins |= (X - (cx + tx)) ** 2 + (Y - (G - lift + ty)) ** 2 <= tr * tr
            if ins and Y <= G + 0.5: m[y][x] = True
    return m

def shade_round(m, cx, mouth=0.0, back=False):
    im = Image.new('RGBA', (W, H)); px = im.load()
    pts = [(x, y) for y in range(H) for x in range(W) if m[y][x]]
    if not pts: return im
    top = min(y for _, y in pts); bot = max(y for _, y in pts)
    xs = [x for x, _ in pts]; xl, xr = min(xs), max(xs); half = max(1, (xr - xl) / 2)
    for x in range(xl, xr + 1): px[x, G + 1] = SHADOW
    for (x, y) in pts:
        f = (y - top) / max(1, bot - top); side = abs(x + .5 - cx) / half
        c = LIGHT if f < 0.3 else MID if f < 0.7 else DARK
        if side > 0.82 and c == LIGHT: c = MID
        if side > 0.88 and f > 0.35: c = DARK
        # 줄무늬 2줄 (몸을 감싸는 고리 → 가운데가 아래로 휜 곡선)
        bend = (1 - min(1, side) ** 2) * 1.2
        yy = y - bend
        for sy in ((G - 6.2), (G - 3.6)):
            if abs(yy - sy) < 0.6 and f > 0.35: c = STRIPE
        px[x, y] = c
    # 광택 (왼쪽 위)
    for (dx, dy, c) in [(-3, 1, HI), (-2, 1, HI), (-3, 2, HI2), (-4, 2, HI2)]:
        x, y = int(cx) + dx, top + dy
        if 0 <= x < W and 0 <= y < H and m[y][x]: px[x, y] = c
    if not back and mouth > 0:
        smile = mouth < 0.5                      # 평소: 아래로 휜 반달 입 (이빨 없음 → 눈처럼 안 보임)
        mcx, mcy = cx, top + (bot - top) * (0.5 if smile else 0.45)
        rx = (4.0 if smile else 2.2 + mouth * 3.2); ry = (3.0 if smile else 0.7 + mouth * mouth * 3.9)
        for (x, y) in pts:
            d = ((x + .5 - mcx) / rx) ** 2 + ((y + .5 - mcy) / ry) ** 2
            if d <= 1 and (not smile or y + .5 >= mcy): px[x, y] = MOUTH2 if d > 0.65 else MOUTH
        if smile:   # 혀 + 윗잇몸에 매달린 작은 이빨 2개
            ty = int(mcy + ry * 0.6)
            for x in (int(mcx) - 1, int(mcx), int(mcx) + 1): px[x, ty] = C('#9a3a6c')
            for x in (int(mcx - rx * 0.5), int(mcx + rx * 0.5) - 1):
                if m[int(mcy)][x]: px[x, int(mcy)] = TEETH
        if mouth >= 0.5:   # 위아래 이빨
            n = 2 if mouth < 0.7 else 3
            for i in range(n):
                tx = int(mcx - rx * 0.5 + (i * rx / max(1, n - 1) if n > 1 else rx * 0.5))
                for ty in (int(mcy - ry) + 1, int(mcy + ry) - 1 if mouth >= 0.5 else None):
                    if ty is not None and 0 <= ty < H and m[ty][tx]: px[tx, ty] = TEETH
        if mouth >= 0.8:
            x, y = int(mcx) + 1, int(mcy + ry)
            for dy in range(3):
                if 0 <= y + dy < H: px[x, y + dy] = DROOL
    out = im.copy(); op = out.load()
    for y in range(H):
        for x in range(W):
            if m[y][x]: continue
            if any(0 <= x + dx < W and 0 <= y + dy < H and m[y + dy][x + dx] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))) and y <= G:
                op[x, y] = OUTL
    return out

MOUTH_IDLE = 0.38   # 평소에도 입을 이만큼 벌리고 있음

def side(name, L, R, mouth=MOUTH_IDLE, **kw): save(paint(body(L, R, **kw), mouth=mouth), name)
def front(name, rx, ry, mouth=MOUTH_IDLE, lift=0.0, tail=None):   # 앞에선 꼬리가 몸 뒤에 숨어 안 보임
    save(shade_round(blob_mask(rx, ry, lift=lift, tail=tail), 14.0, mouth), name)
def back(name, rx, ry, lift=0.0, tail=(0, 0.5, 2.2)):
    save(shade_round(blob_mask(rx, ry, lift=lift, tail=tail), 14.0, back=True), name)

# --- 옆 (오른쪽을 봄, 왼쪽은 게임이 뒤집음)
side('player_idle_side_0', 21, 6.5); side('player_idle_side_1', 21, 6.5, squash=1.08)
side('player_crawl_side_0', 21, 6.5, x_front=22); side('player_crawl_side_1', 17, 6.6, x_front=21, bump=0.55)
side('player_crawl_side_2', 23, 6.0, x_front=23, squash=0.95); side('player_crawl_side_3', 21, 6.6, x_front=23)
side('player_eat_side_0', 20, 6.8, mouth=0.55); side('player_eat_side_1', 19, 7.4, mouth=0.7, x_front=23)
side('player_eat_side_2', 18, 8.0, mouth=0.88, x_front=24); side('player_eat_side_3', 18, 8.4, mouth=1.0, x_front=25)
# --- 앞 (아래로 갈 때) — 꼬리가 옆으로 살짝 보임
front('player_idle_down_0', 7.2, 6.8); front('player_idle_down_1', 7.0, 7.3)
# 기어가기(앞): 다리가 없으니 좌우로 흔들지 않고, 옆·뒷모습처럼 몸 전체를 오므렸다(납작·넓게) 폈다(길쭉·살짝 뜸) 한다
front('player_crawl_down_0', 7.2, 6.8)
front('player_crawl_down_1', 8.1, 5.9)
front('player_crawl_down_2', 6.6, 7.7, lift=0.5)
front('player_crawl_down_3', 7.0, 7.1)
front('player_eat_down_0', 7.4, 7.2, mouth=0.55); front('player_eat_down_1', 7.8, 7.8, mouth=0.72)
front('player_eat_down_2', 8.2, 8.4, mouth=0.88); front('player_eat_down_3', 8.6, 8.8, mouth=1.0)
# --- 뒤 (위로 갈 때) — 입은 안 보이고 꼬리가 아래로 나와 흔들림
back('player_idle_up_0', 7.2, 6.8); back('player_idle_up_1', 7.0, 7.3)
back('player_crawl_up_0', 7.2, 6.8, tail=(0, 0.5, 2.2)); back('player_crawl_up_1', 8.0, 6.0, tail=(1.5, 0.5, 2.2))
back('player_crawl_up_2', 6.6, 7.6, lift=0.5, tail=(0, 0.8, 2.0)); back('player_crawl_up_3', 7.2, 6.9, tail=(-1.5, 0.5, 2.2))
back('player_eat_up_0', 7.4, 7.2); back('player_eat_up_1', 7.6, 7.9, lift=0.3)
back('player_eat_up_2', 7.8, 8.6, lift=0.6); back('player_eat_up_3', 8.4, 8.0)
# HUD 아이콘·예전 이름 호환
save(paint(body(21, 6.5), mouth=MOUTH_IDLE), 'player_blob')
print('player sprites done')
