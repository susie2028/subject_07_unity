# 기절한 사람 그림: 정면 서 있는 그림(stand_<종류>_front)에서 눈만 X로 바꾼다.
# 게임에서는 이 그림을 바닥에 눕혀서(널브러진 느낌) 그린다.
# python3 Tools/make_stun.py  → Assets/Resources/Art/stun_<종류>.png (+ .meta 없으면 서 있는 그림 meta를 본떠 만든다)
import os, re, uuid, glob
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Resources', 'Art')


def skin(c):
    r, g, b, a = c
    return a > 0 and r > 150 and r > b + 30 and g > 100


def find_eyes(px, W, H):
    """피부 사이에 낀 어두운 1칸짜리 점 = 눈. 위쪽 2/3 안에서 찾는다. → {x: [y...]}"""
    hits = {}
    for y in range(1, H * 2 // 3):
        for x in range(1, W - 1):
            c = px[x, y]
            if c[3] == 0 or sum(c[:3]) > 200:
                continue
            if skin(px[x - 1, y]) and skin(px[x + 1, y]):
                hits.setdefault(x, []).append(y)
    # 좌우 한 쌍만 (같은 높이에 있는 두 열)
    cols = sorted(hits, key=lambda x: -len(hits[x]))[:2]
    return {x: hits[x] for x in cols} if len(cols) == 2 and abs(cols[0] - cols[1]) >= 4 else {}


def draw_x(px, cx, cy, col):
    for dx, dy in ((-1, -1), (1, -1), (0, 0), (-1, 1), (1, 1)):
        px[cx + dx, cy + dy] = col


def save(img, name, meta_src):
    p = os.path.join(ROOT, name + '.png')
    img.save(p)
    m = p + '.meta'
    if not os.path.exists(m) and os.path.exists(meta_src):
        src = open(meta_src, encoding='utf-8').read()
        src = re.sub(r'guid: [0-9a-f]+', 'guid: ' + uuid.uuid4().hex, src, count=1)
        open(m, 'w', encoding='utf-8').write(src)
    print('wrote', name)


def main():
    for f in sorted(glob.glob(os.path.join(ROOT, 'stand_*_front.png'))):
        kind = os.path.basename(f)[len('stand_'):-len('_front.png')]
        im = Image.open(f).convert('RGBA')
        px = im.load(); W, H = im.size
        if kind in ('soldier', 'senior'):
            pass   # 안경·고글 쓴 사람은 눈이 안 보이니 X 없이 그대로 (누워 있는 것만으로 기절 표시)
        else:
            eyes = find_eyes(px, W, H)
            if not eyes:
                print('skip (no eyes found)', kind)
                continue
            for x, ys in eyes.items():
                col = px[x, ys[0]]
                fill = px[x - 1, ys[0]]
                for y in ys:
                    px[x, y] = fill
                cy = ys[-1]   # 아래쪽 칸 기준 (모자챙에 X 윗줄이 묻히지 않게)
                draw_x(px, x, cy, col)
        save(im, 'stun_' + kind, f + '.meta')


if __name__ == '__main__':
    main()
