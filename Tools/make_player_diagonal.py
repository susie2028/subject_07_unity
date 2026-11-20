# 주인공 대각선(아래-옆 / 위-옆) 프레임 만들기.
# 정면(down)·뒷면(up) 프레임의 몸통에 옆모습(side)의 짧은 꼬리를 붙이고, 얼굴을 바라보는 쪽으로 옮긴다.
# 오른쪽을 보는 그림으로 만들고, 왼쪽은 게임에서 뒤집어 쓴다.
#   python3 Tools/make_player_diagonal.py [--preview out.png]
# → Assets/Resources/Art/player_<idle|crawl|eat>_<downside|upside>_<n>.png (+ .meta)
import os, re, sys, uuid
from PIL import Image

ART = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Resources', 'Art')
OUTLINE = (0x24, 0x12, 0x3a)
SHADOW_A = 70
FACE = {(0x2c, 0x12, 0x40), (0x17, 0x0a, 0x24), (0xf1, 0xec, 0xf9), (0x9a, 0x3a, 0x6c), (122, 182, 239)}   # 눈·입·이빨·혀·침

def load(n): return Image.open(os.path.join(ART, n + '.png')).convert('RGBA')
def rgb(p): return p[:3]
def is_shadow(p): return p[3] > 0 and p[3] < 200 and rgb(p) == (0, 0, 0)
def is_face(p): return p[3] == 255 and rgb(p) in FACE

def body_rows(im):
    w, h = im.size
    rows = {}
    for y in range(h):
        xs = [x for x in range(w) if im.getpixel((x, y))[3] == 255]
        if xs: rows[y] = (min(xs), max(xs))
    return rows

def diag(base, side, front):
    w, h = base.size
    SH = 0      # 몸통은 정면 그림 자리 그대로 (방향을 바꿔도 몸이 튀지 않게). 꼬리는 왼쪽 빈 곳에
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    # 1) 몸통 (그림자 줄 제외) 오른쪽으로
    face_px = []
    for y in range(h):
        for x in range(w):
            p = base.getpixel((x, y))
            if p[3] == 0 or is_shadow(p): continue
            if front and is_face(p): face_px.append((x, y, p)); continue
            if x + SH < w: out.putpixel((x + SH, y), p)
    # 얼굴 자리를 주변 몸 색으로 메우기
    if front:
        for (x, y, p) in face_px:
            # 같은 줄에서 얼굴이 아닌 가장 가까운 몸 색
            c = None
            for d in range(1, 8):
                for xx in (x - d, x + d):
                    if 0 <= xx < w:
                        q = base.getpixel((xx, y))
                        if q[3] == 255 and not is_face(q) and rgb(q) != OUTLINE: c = q; break
                if c: break
            if c and x + SH < w: out.putpixel((x + SH, y), c)
    # 2) 꼬리: 옆모습의 꼬리를 끝 2픽셀만 잘라 몸 왼쪽 아래에 붙인다 (옆모습과 같은 프레임이라 꼬리가 같이 꿈틀거림)
    #    옆모습에서 꼬리가 몸에 붙는 열(JOIN)을 몸통 왼쪽 테두리 바로 안쪽에 맞춘다
    brow = body_rows(out)
    bottom = max(brow)
    JOIN, CUT = 7, 2
    ref = brow.get(bottom - 2, brow[bottom])[0]          # 몸통 아래쪽 왼쪽 테두리
    off = ref + 1 - JOIN
    for y in range(bottom - 6, bottom + 1):
        for sx in range(CUT, JOIN + 1):
            p = side.getpixel((sx, y))
            if p[3] != 255: continue
            tx = sx + off
            if not (0 <= tx < w): continue
            q = out.getpixel((tx, y))
            if q[3] == 0 or rgb(q) == OUTLINE: out.putpixel((tx, y), p)
    # 잘라 낸 꼬리 끝 테두리 닫기: 꼬리 근처에서 몸 안쪽 색이 바깥(투명)에 맞닿으면 그 바깥 칸을 테두리로
    for y in range(bottom - 6, bottom + 1):
        for x in range(0, ref + 2):
            if out.getpixel((x, y))[3] != 0: continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h:
                    q = out.getpixel((nx, ny))
                    if q[3] == 255 and rgb(q) != OUTLINE: out.putpixel((x, y), OUTLINE + (255,)); break
    # 3) 얼굴: 바라보는 쪽(오른쪽)으로 더 옮기고 가로로 조금 좁힌다 (3/4 시점)
    if front and face_px:
        fx0 = min(x for x, _, _ in face_px); fx1 = max(x for x, _, _ in face_px)
        cx = (fx0 + fx1) / 2
        for (x, y, p) in face_px:
            nx = int(round(cx + (x - cx) * 0.8)) + SH + 2
            if 0 <= nx < w and out.getpixel((nx, y))[3] == 255 and rgb(out.getpixel((nx, y))) != OUTLINE: out.putpixel((nx, y), p)
    # 4) 그림자: 몸 전체 폭에 맞춰
    sy = max(y for y in range(h) for x in range(w) if is_shadow(base.getpixel((x, y))))
    rows = body_rows(out)
    xs = [r[0] for r in rows.values()] + [r[1] for r in rows.values()]
    for x in range(min(xs), max(xs) + 1): out.putpixel((x, sy), (0, 0, 0, SHADOW_A))
    return out

def save(img, name):
    p = os.path.join(ART, name + '.png'); img.save(p)
    if not os.path.exists(p + '.meta'):
        src = open(os.path.join(ART, 'player_idle_down_0.png.meta'), encoding='utf-8').read()
        open(p + '.meta', 'w', encoding='utf-8').write(re.sub(r'guid: [0-9a-f]+', 'guid: ' + uuid.uuid4().hex, src, count=1))

def main():
    made = []
    for anim, n in (('idle', 2), ('crawl', 4), ('eat', 4)):
        for i in range(n):
            side = load('player_%s_side_%d' % (anim, i))
            for view, base, front in (('downside', 'down', True), ('upside', 'up', False)):
                img = diag(load('player_%s_%s_%d' % (anim, base, i)), side, front)
                name = 'player_%s_%s_%d' % (anim, view, i)
                save(img, name); made.append((anim, view, i, img))
    if '--preview' in sys.argv:
        out = sys.argv[sys.argv.index('--preview') + 1]
        S = 6; cols = 4
        order = [(a, v) for a in ('idle', 'crawl', 'eat') for v in ('down', 'downside', 'side', 'upside', 'up')]
        sheet = Image.new('RGBA', (cols * 30 * S, len(order) * 22 * S), (40, 44, 80, 255))
        for r, (a, v) in enumerate(order):
            for i in range(4):
                fn = os.path.join(ART, 'player_%s_%s_%d.png' % (a, v, i))
                if not os.path.exists(fn): continue
                im = Image.open(fn).convert('RGBA').resize((28 * S, 20 * S), Image.NEAREST)
                sheet.paste(im, (i * 30 * S, r * 22 * S), im)
        sheet.save(out)
    print('frames:', len(made))

main()
