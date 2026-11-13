# 주인공(표본 07) 스프라이트 생성기 — 눈 없는 보라색 지렁이 괴물, 옆모습(오른쪽을 봄)
# 사용: python3 Tools/make_player.py Assets/Resources/Art
# 만드는 그림:
#   player_idle_0..1   가만히 있을 때 (숨쉬기)
#   player_crawl_0..3  기어갈 때 (자벌레처럼 오므렸다 폈다)
#   player_eat_0..3    먹을 때 (입이 점점 크게 벌어짐)
# 게임은 왼쪽으로 갈 때 좌우 반전해서 쓴다. 직접 그린 그림으로 바꿀 때도 같은 이름·오른쪽을 보는 방향으로 그리면 된다.
import os, sys, math
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/Resources/Art"
W, H, G = 28, 20, 17  # 캔버스, 바닥선(y)

def C(h, a=255):
    h = h.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)
OUTL = C('#24123a'); DARK = C('#5b2f8f'); MID = C('#7a48bd'); LIGHT = C('#9a6bd9'); HI = C('#e2d4f7'); HI2 = C('#b996e8')
MOUTH = C('#170a24'); MOUTH2 = C('#2c1240'); TEETH = C('#f1ecf9'); DROOL = C('#7ab6ef'); SHADOW = C('#000000', 70)

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
                th = R * squash * (0.35 + 0.75 * u ** 0.9) + bump * R * math.exp(-((u - 0.5) / 0.22) ** 2)
                bottom = G - lift - (2.2 * (1 - u / 0.18) ** 1.4 if u < 0.18 else 0)        # 꼬리 끝이 들림
                if bottom - th <= Y <= bottom: inside = True
            if inside and Y <= G + 0.5: m[y][x] = True
    return m

def paint(m, mouth=0.0, closed_mouth=True, gloss=True):
    im = Image.new('RGBA', (W, H)); px = im.load()
    cols = [x for x in range(W) if any(m[y][x] for y in range(H))]
    if not cols: return im
    xf = max(cols); xb = min(cols)
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
            if x < xf - 11 and (abs(f - 0.55) < 0.07 or abs(f - 0.8) < 0.06): c = DARK
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
    ys_front = [y for y in range(H) if m[y][xf - 1]]
    if mouth > 0 and ys_front:
        cy = (min(ys_front) + max(ys_front)) / 2 + 0.5
        rx = 1.5 + mouth * 5.0; ry = 1.0 + mouth * 4.6; cx = xf - rx * 0.55 + 0.5
        for y in range(H):
            for x in range(W):
                if not m[y][x]: continue
                d = ((x + .5 - cx) / rx) ** 2 + ((y + .5 - cy) / ry) ** 2
                if d <= 1: px[x, y] = MOUTH2 if d > 0.7 else MOUTH
        if mouth >= 0.45:  # 이빨
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

# 가만히: 숨쉬기
save(paint(body(21, 6.5)), 'player_idle_0')
save(paint(body(21, 6.5, squash=1.08)), 'player_idle_1')
# 기어가기: 폈다 → 오므림(혹) → 쭉 뻗음 → 보통
save(paint(body(21, 6.5, x_front=22)), 'player_crawl_0')
save(paint(body(17, 6.6, x_front=21, bump=0.55)), 'player_crawl_1')
save(paint(body(23, 6.0, x_front=23, squash=0.95)), 'player_crawl_2')
save(paint(body(21, 6.6, x_front=23)), 'player_crawl_3')
# 먹기: 머리를 들며 입이 커진다
save(paint(body(20, 6.8), mouth=0.25), 'player_eat_0')
save(paint(body(19, 7.4, x_front=23), mouth=0.55), 'player_eat_1')
save(paint(body(18, 8.0, x_front=24), mouth=0.85), 'player_eat_2')
save(paint(body(18, 8.4, x_front=25), mouth=1.0), 'player_eat_3')
# HUD 아이콘·예전 이름 호환
save(paint(body(21, 6.5)), 'player_blob')
print('player sprites done')
