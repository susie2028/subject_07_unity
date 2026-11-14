# 참고 시트(Tools/art_src/npc_sheet.png)에서 NPC 스프라이트를 잘라 걷기 애니메이션까지 만든다.
#  시트 배치: 열 = 청소부, 연구원, 경비원, 군인, 소장 / 행 = 앞, 뒤, 옆(오른쪽)
#  - 확대·흐려진 그림을 원래 픽셀 격자(약 9.2px = 1픽셀)로 되돌림
#  - 자홍색 배경 제거, 시트 아래가 잘린 옆모습은 신발 한 줄 보충, 워터마크 지움
#  - 연구원 변형(머리색·피부·넥타이), 고위 연구원(소장의 단정한 흰머리 + 가운 + 보라 넥타이·금 배지)
#  - 걷기 4프레임: 0 서기, 1 왼발 들며 몸 위로, 2 서기, 3 오른발
# 결과: Assets/Resources/Art/stand_<종류>_<front|back|side>.png (서 있는 그림)
#       Assets/Resources/Art/walk_<종류>_<front|back|side>_<0..3>.png (걷기)
# 실행: python3 Tools/make_people_from_sheet.py
import colorsys
import os

import numpy as np
import scipy.ndimage as nd
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, 'art_src', 'npc_sheet.png')
OUT = os.path.join(HERE, '..', 'Assets', 'Resources', 'Art')
P, X0, Y0 = 9.2, 6.25, 7.5          # 원본 픽셀 격자 (크기, 시작점)
CW, CH = 40, 42                     # 결과 캔버스 (발이 맨 아래 줄)
COLS = ['cleaner', 'researcher', 'guard', 'soldier', 'director']
ROWS = ['front', 'back', 'side']
OL = (12, 8, 14, 255)


def downsample():
    im = np.array(Image.open(SRC).convert('RGB')).astype(float)
    h = min(im.shape[0], 1101); w = min(im.shape[1], 1830)
    nx = int((w - X0) // P); ny = int((h - Y0) // P) + 1
    out = np.zeros((ny, nx, 3), np.uint8)
    for j in range(ny):
        for i in range(nx):
            xa = X0 + i * P; ya = Y0 + j * P
            blk = im[int(ya + P * 0.3):int(ya + P * 0.7) + 1, int(xa + P * 0.3):int(xa + P * 0.7) + 1].reshape(-1, 3)
            if len(blk):
                out[j, i] = np.median(blk, axis=0)
    return out


def to_rgba(low):
    a = low.astype(int)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    bg = (np.sqrt(((a - [234, 51, 243]) ** 2).sum(-1)) < 110) | ((r > 150) & (b > 150) & (g < 130))
    rgba = np.zeros(low.shape[:2] + (4,), np.uint8)
    rgba[..., :3] = low; rgba[..., 3] = np.where(bg, 0, 255)
    return rgba


def fix_watermark(rgba):
    # 소장 옆모습 가슴께의 회색 별 자국 → 정장색
    y0, y1, x0, x1 = 95, 108, 170, 190
    reg = rgba[y0:y1, x0:x1]
    for y in range(reg.shape[0]):
        for x in range(reg.shape[1]):
            r, g, b, a = [int(v) for v in reg[y, x]]
            if a == 0:
                continue
            if abs(r - g) < 14 and abs(g - b) < 14 and 50 < r < 170:
                reg[y, x, :3] = (34, 34, 36)
            elif r > 225 and g > 190 and b > 160 and r - b < 70:   # 별빛에 밝아진 피부
                reg[y, x, :3] = (232, 184, 150)


def cut(rgba):
    mask = rgba[..., 3] > 0
    lab, n = nd.label(mask, structure=np.ones((3, 3)))
    sizes = nd.sum(mask, lab, range(1, n + 1)); objs = nd.find_objects(lab)
    sprites = {}
    for i, s in enumerate(sizes):
        if s < 150:
            continue
        sl = objs[i]
        row = int(((sl[0].start + sl[0].stop) / 2) // 40); col = int(((sl[1].start + sl[1].stop) / 2) // 40)
        piece = rgba[sl].copy()
        piece[lab[sl] != i + 1] = 0
        cut_bottom = sl[0].stop >= rgba.shape[0]
        sprites[(COLS[col], ROWS[row])] = (piece, cut_bottom)
    return sprites


def outline_fix(a):
    # 반투명 가장자리 정리 + 비어 있는 바깥 테두리 채우기
    a[..., 3] = np.where(a[..., 3] > 0, 255, 0)
    return a


def extend_feet(a):
    # 시트 아래에서 잘린 옆모습: 마지막 줄(신발)을 한 줄 더 + 외곽선
    last = a[-1]
    shoe = last.copy()
    ol = np.zeros_like(last)
    for x in range(a.shape[1]):
        if last[x, 3] > 0:
            ol[x] = OL
    return np.concatenate([a, shoe[None], ol[None]], axis=0)


def place(a):
    # 공통 캔버스에 발을 맨 아래, 가운데 정렬
    h, w = a.shape[:2]
    can = np.zeros((CH, CW, 4), np.uint8)
    ox = (CW - w) // 2; oy = CH - h
    ys0 = max(0, -oy); xs0 = max(0, -ox)
    can[max(0, oy):max(0, oy) + h - ys0, max(0, ox):max(0, ox) + w - xs0] = a[ys0:, xs0:][:CH, :CW]
    return can


# ------------------------------------------------------------------ 색 바꾸기
def hsv(px):
    return colorsys.rgb_to_hsv(px[0] / 255, px[1] / 255, px[2] / 255)


def recolor(a, pick, target, rows=None):
    # pick(h,s,v) 가 참인 픽셀을 target 색조로 (밝기 비율은 유지)
    th, ts, tv = colorsys.rgb_to_hsv(*[c / 255 for c in target])
    out = a.copy()
    picked = []
    for y in range(a.shape[0]):
        if rows and not (rows[0] <= y < rows[1]):
            continue
        for x in range(a.shape[1]):
            if a[y, x, 3] == 0:
                continue
            h, s, v = hsv(a[y, x])
            if pick(h * 360, s, v):
                picked.append((y, x, v))
    if not picked:
        return out
    vmax = max(p[2] for p in picked)
    for y, x, v in picked:
        nv = min(1, tv * (v / vmax) * 1.05)
        r, g, b = colorsys.hsv_to_rgb(th, ts, nv)
        out[y, x, :3] = (int(r * 255), int(g * 255), int(b * 255))
    return out


HAIR = lambda h, s, v: 10 <= h <= 45 and s > 0.35 and v < 0.62          # 갈색 머리
SKIN = lambda h, s, v: 10 <= h <= 40 and 0.15 < s < 0.6 and v >= 0.62   # 피부
TIE = lambda h, s, v: 85 <= h <= 170 and s > 0.25                        # 초록 넥타이


def head_rows(a, body_test):
    # 위에서부터 몸통 색(body_test)이 줄의 40% 이상이 되는 첫 줄 = 목/어깨
    for y in range(int(a.shape[0] * 0.3), a.shape[0]):
        row = [a[y, x] for x in range(a.shape[1]) if a[y, x, 3] > 0]
        if len(row) > 6 and sum(1 for p in row if body_test(p)) >= 0.4 * len(row):
            return y
    return a.shape[0] // 2


def bbox_x(a, y0, y1):
    xs = [x for y in range(y0, y1) for x in range(a.shape[1]) if a[y, x, 3] > 0]
    return (min(xs), max(xs)) if xs else (0, a.shape[1] - 1)


def senior_from(res, dire):
    # 소장 머리(단정한 흰머리, 나이 든 얼굴) + 연구원 가운 몸
    white = lambda p: min(p[:3]) > 175 and max(p[:3]) - min(p[:3]) < 40
    black = lambda p: 20 < max(p[:3]) < 75          # 정장 (외곽선 제외)
    ry = head_rows(res, white); dy = head_rows(dire, black)
    head = dire[:dy]; body = res[ry:]
    hx0, hx1 = bbox_x(dire, 0, dy); bx0, bx1 = bbox_x(res, 0, ry)        # 원래 머리 위치에 맞춤
    shift = int(round((bx0 + bx1) / 2 - (hx0 + hx1) / 2))
    w = max(res.shape[1], dire.shape[1]) + 8
    out = np.zeros((dy + body.shape[0], w, 4), np.uint8)
    pad = 4
    out[dy:, pad:pad + body.shape[1]] = body
    cx0, cx1 = bbox_x(out, dy, dy + 3)
    for y in range(dy):
        for x in range(head.shape[1]):
            if head[y, x, 3] > 0:
                xx = x + pad + shift
                if y >= dy - 5 and not (cx0 - 1 <= xx <= cx1 + 1):
                    continue                            # 소장의 넓은 어깨는 버리고 가운 폭에 맞춤
                if 0 <= xx < w:
                    px = head[y, x].copy()
                    if black(px):                       # 소장 정장 어깨 → 흰 가운 어깨 (금 견장은 그대로 = 고위 느낌)
                        v = int(px[:3].max())
                        px[:3] = (186, 194, 202) if v < 40 else (226, 232, 238)
                    out[y, xx] = px
    out = recolor(out, TIE, (122, 74, 184))                             # 보라 넥타이
    return out


def decorate_senior(a, view):
    # 가슴에 금 배지 (앞·옆)
    if view == 'back':
        return a
    ys, xs = np.nonzero(a[..., 3])
    top = ys.min(); h = ys.max() - top
    y = top + int(h * 0.58)
    row = [x for x in range(a.shape[1]) if a[y, x, 3] > 0]
    if not row:
        return a
    x = row[0] + (len(row) * 2) // 3 if view == 'front' else row[0] + len(row) // 2
    for dx, dy, c in ((0, 0, (243, 207, 98)), (1, 0, (207, 162, 58)), (0, 1, (207, 162, 58)), (1, 1, (147, 112, 31))):
        a[y + dy, x + dx] = (*c, 255)
    return a


# ------------------------------------------------------------------ 걷기
def feet_groups(a):
    h = a.shape[0]
    band = a[h - 4:, :, 3] > 0
    lab, n = nd.label(band)
    groups = []
    for i in range(1, n + 1):
        ys, xs = np.nonzero(lab == i)
        groups.append((xs.min(), xs.max(), len(xs)))
    groups.sort(key=lambda g: -g[2])
    return groups


def shift_up(a, cols_keep_ground):
    # 전체를 1픽셀 위로, cols_keep_ground 열은 맨 아래 픽셀을 복사해 바닥에 붙여 둠
    out = np.zeros_like(a)
    out[:-1] = a[1:]
    for x in cols_keep_ground:
        out[-1, x] = a[-1, x]
    return out


def walk_frames(a, view, static_cols=()):
    frames = [a.copy()]
    h, w = a.shape[:2]
    groups = feet_groups(a)
    main = [g for g in groups[:3] if not set(range(g[0], g[1] + 1)) & set(static_cols)]
    allcols = [x for x in range(w) if a[-1, x, 3] > 0]
    if view in ('front', 'back') and len(main) >= 2:
        L, R = sorted(main[:2], key=lambda g: g[0])
        lift = lambda g: [x for x in allcols if not (g[0] <= x <= g[1])]
        f1 = shift_up(a, lift(L)); f3 = shift_up(a, lift(R))
    else:
        # 옆모습(또는 발이 하나로 붙은 경우): 아래 3줄의 앞뒤를 벌려 보폭
        fx = [x for x in allcols if x not in static_cols]
        if not fx:
            return [a] * 4
        mid = (min(fx) + max(fx)) / 2
        def stride(src, sgn):
            out = src.copy()
            for y in range(h - 3, h):
                row = src[y].copy(); new = np.zeros_like(row)
                for x in range(w):
                    if row[x, 3] == 0:
                        continue
                    if x in static_cols:
                        new[x] = row[x]; continue
                    nx = x + (sgn if x >= mid else -sgn)
                    if 0 <= nx < w:
                        new[nx] = row[x]
                for x in range(w):
                    if new[x, 3] == 0 and row[x, 3] > 0 and x not in static_cols and y < h - 1:
                        new[x] = row[x]          # 다리 사이 빈틈 메움 (발목 위)
                out[y] = new
            return out
        f1 = stride(a, 1)                   # 다리 벌림 (디딤)
        f3 = shift_up(a, allcols)           # 다리 모음 + 몸 위로 (지나감)
    return [frames[0], f1, a.copy(), f3]


def save(img, name):
    Image.fromarray(img, 'RGBA').save(os.path.join(OUT, name + '.png'))


def main():
    low = downsample()
    rgba = to_rgba(low)
    fix_watermark(rgba)
    sp = cut(rgba)
    base = {}
    for (kind, view), (a, cutb) in sp.items():
        a = outline_fix(a)
        if cutb:
            a = extend_feet(a)
        base[(kind, view)] = a
    sets = {}
    for kind in COLS:
        for view in ROWS:
            sets[(kind, view)] = base[(kind, view)]
    for view in ROWS:
        r = base[('researcher', view)]
        sets[('researcher_v2', view)] = recolor(recolor(r, HAIR, (196, 98, 44)), TIE, (64, 112, 200))                       # 주황 머리, 파랑 넥타이
        sets[('researcher_v3', view)] = recolor(recolor(r, HAIR, (226, 196, 110)), TIE, (150, 46, 60))                      # 금발, 자주 넥타이
        sets[('researcher2', view)] = recolor(recolor(recolor(r, HAIR, (44, 38, 52)), SKIN, (176, 118, 84)), TIE, (196, 54, 60))   # 검은 머리, 어두운 피부, 빨강 넥타이
        sets[('researcher2_v2', view)] = recolor(recolor(r, HAIR, (90, 60, 40)), TIE, (220, 160, 40))                       # 짙은 갈색, 노랑 넥타이
        sets[('senior', view)] = decorate_senior(senior_from(r, base[('director', view)]), view)
    preview = []
    for (kind, view), a in sets.items():
        a = place(a)
        save(a, 'stand_%s_%s' % (kind, view))
        static = []
        if kind == 'cleaner':      # 대걸레는 걸을 때 바닥에 그대로
            g = feet_groups(a)
            static = [x for gg in g if gg[0] > CW * 0.62 for x in range(gg[0], gg[1] + 1)]
        fr = walk_frames(a, view, static)
        for i, f in enumerate(fr):
            save(f, 'walk_%s_%s_%d' % (kind, view, i))
        preview.append((kind, view, fr))
    # 미리보기
    kinds = []
    for k, v, _ in preview:
        if k not in kinds:
            kinds.append(k)
    sheet = Image.new('RGBA', (CW * 12 + 40, CH * len(kinds)), (52, 44, 78, 255))
    for k, v, fr in preview:
        r = kinds.index(k); c = ROWS.index(v)
        for i, f in enumerate(fr):
            im = Image.fromarray(f, 'RGBA')
            sheet.paste(im, (c * (CW * 4 + 12) + i * CW, r * CH), im)
    sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(os.path.join(HERE, 'people_preview.png'))
    print('ok', len(sets), 'sets')


if __name__ == '__main__':
    main()
