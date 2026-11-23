# 연구원 그림(영상에서 뽑은 걷기 walk_researcher_* + 서 있기 stand_researcher_*)으로 연구원들을 만든다.
#  - 일반 연구원들: 안경 벗기고 점눈, 머리색·넥타이를 사람마다 다르게 (머리 모양은 원래 그대로)
#      researcher 갈색 / _v2 주황 / _v3 금발 / researcher2 검은 머리·어두운 피부 / researcher2_v2 짙은 갈색
#  - 고위 연구원(senior): 안경 그대로, 보라 넥타이, 금 배지, 머리는 art_src/senior_heads 의 대머리+옆머리로 갈아 끼움
#  (머리 깎기·늘이기 함수 trim_spikes / cut_short / add_long / add_bun 은 남겨 둠. 지금은 안 씀)
# 원본(안경 쓴 연구원)은 Assets/Resources/Art 의 _src_researcher_* 로 따로 보관해 두고 거기서 다시 만든다.
# 실행: python3 Tools/make_variant_walks.py   (영상에서 연구원 걷기를 다시 뽑았다면: --fresh)
# 대각선 그림을 새로 뽑았으면: walk_researcher_<frontside|backside>_N.png / stand_… 를 art_src/researcher_frames 에 복사한 뒤 실행
import colorsys
import glob
import os
import shutil
import sys

import numpy as np
import scipy.ndimage as nd
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import make_people_from_sheet as mp  # noqa: E402
from extract_walk_video import close_outline  # noqa: E402

ART = os.path.join(HERE, '..', 'Assets', 'Resources', 'Art')
SRC = os.path.join(HERE, 'art_src', 'researcher_frames')   # 안경 쓴 원본 보관
EYE = (26, 18, 24, 255)


def cls(p):
    if p[3] == 0:
        return '.'
    h, s, v = colorsys.rgb_to_hsv(*(p[:3] / 255)); h *= 360
    if v < 0.2: return 'k'
    if 10 <= h <= 45 and s > 0.35 and v < 0.62: return 'H'
    if 10 <= h <= 40 and 0.15 < s < 0.6 and v >= 0.62: return 'S'
    if 85 <= h <= 170 and s > 0.25: return 'T'
    if s < 0.2: return 'W'
    return '?'


def classes(a):
    h, w = a.shape[:2]
    return np.array([[cls(a[y, x].astype(float)) for x in range(w)] for y in range(h)])


# ------------------------------------------------------------------ 안경 벗기기 → 점눈
def remove_glasses(a, view):
    if view == 'back':
        return a
    a = a.copy(); C = classes(a)
    # 얼굴 = 맨 위 피부 덩어리에서 아래로 10줄 (안경이 얼굴을 위아래로 나눠도 함께 잡히게, 손은 제외)
    S = C == 'S'
    lab, n = nd.label(S, structure=np.ones((3, 3)))
    comps = []
    for i in range(1, n + 1):
        yy, xx = np.nonzero(lab == i)
        if len(yy) >= 4:
            comps.append((yy.min(), i))
    if not comps:
        return a
    comps.sort()
    yy, xx = np.nonzero(lab == comps[0][1])
    fy0 = yy.min(); fy1 = fy0 + 10
    fx0, fx1 = xx.min() - 1, xx.max() + 1
    sel = S.copy(); sel[:fy0] = False; sel[fy1 + 1:] = False; sel[:, :max(0, fx0)] = False; sel[:, fx1 + 1:] = False
    ys, xs = np.nonzero(sel)
    if len(ys) == 0:
        return a
    fx0, fx1 = xs.min(), xs.max()
    skin = np.median(a[ys, xs, :3], axis=0).astype(np.uint8)
    fy1 = min(fy1, ys.max())
    # 렌즈: 얼굴 범위 안의 흰/회색 픽셀
    lens = np.zeros(C.shape, bool)
    for y in range(fy0 + 2, min(fy1, fy0 + 7) + 1):          # 눈높이 (얼굴 위에서 2~7줄)
        for x in range(fx0 + 1, fx1):
            if C[y, x] == 'W':
                lens[y, x] = True
    ly = np.nonzero(lens.any(1))[0]
    if len(ly) == 0:
        return a
    ly0, ly1 = ly.min(), ly.max()
    # 렌즈 줄 ±1 의 렌즈·안경테(검정)를 피부로 (머리카락은 그대로)
    for y in range(max(fy0, ly0 - 1), min(fy0 + 8, ly1 + 1) + 1):
        for x in range(fx0, fx1 + 1):
            if C[y, x] in ('W', 'k'):
                l = a[y, x - 1, 3] if x > 0 else 0
                r = a[y, x + 1, 3] if x + 1 < a.shape[1] else 0
                if l and r:
                    a[y, x, :3] = skin; a[y, x, 3] = 255
    # 점눈: 렌즈 덩어리 가운데에 세로 2픽셀
    lab, n = nd.label(lens)
    for i in range(1, n + 1):
        yy, xx = np.nonzero(lab == i)
        if len(yy) < 2:
            continue
        cx = int(round(xx.mean())); cy = int(round(yy.min()))
        a[cy, cx] = EYE; a[min(cy + 1, a.shape[0] - 1), cx] = EYE
    return a


# ------------------------------------------------------------------ 머리 모양
def hair_mask(a):
    C = classes(a)
    op = np.nonzero(a[..., 3])[0]
    upper = np.arange(a.shape[0])[:, None] < op.min() + (op.max() - op.min()) * 0.55   # 위쪽만 (갈색 바지 제외)
    H = (C == 'H') & upper
    # 머리카락 속 검은 선도 머리로 (머리 덩어리 안쪽)
    k = C == 'k'
    filled = nd.binary_closing(H, structure=np.ones((3, 3)))
    return H | (k & filled)


def hair_color(a):
    ys, xs = np.nonzero(hair_mask(a) & (classes(a) == 'H'))
    return a[ys, xs].copy()


def trim_spikes(a, strength=1):
    # 삐죽삐죽한 머리끝을 깎아 단정하게 (열림 연산으로 튀어나온 부분 제거)
    a = a.copy()
    op = a[..., 3] > 0
    top = np.nonzero(op)[0].min(); bottom = np.nonzero(op)[0].max()
    C = classes(a)
    edge = (C == 'k') & nd.binary_dilation(~op)              # 바깥 외곽선만 (눈·안쪽 선은 건드리지 않음)
    m = ((C == 'H') | edge) & (np.arange(a.shape[0])[:, None] < top + (bottom - top) * 0.5)
    m &= op
    opened = nd.binary_opening(m, structure=np.ones((2 + strength, 2 + strength)))
    cut = m & ~opened
    cut &= np.arange(a.shape[0])[:, None] < top + (bottom - top) * 0.45
    a[cut] = 0
    return close_outline(a)


def cut_short(a, rows=3):
    # 머리 윗부분을 rows 줄 깎아 아주 짧게
    a = trim_spikes(a, 2)
    op = a[..., 3] > 0
    top = np.nonzero(op)[0].min()
    H = hair_mask(a)
    for y in range(top, top + rows):
        for x in range(a.shape[1]):
            if a[y, x, 3]:
                a[y, x] = 0
    # 깎인 자리 아래 줄을 머리색으로 채워 둥글게
    return close_outline(a)


def add_long(a, view, extra=4):
    # 얼굴 옆(앞모습)·뒤통수(뒷모습·옆모습)로 머리를 어깨까지 늘어뜨림
    a = a.copy()
    cols = hair_color(a)
    if len(cols) == 0:
        return a
    dark = np.median(cols[:, :3], axis=0).astype(np.uint8)
    H = hair_mask(a)
    ys, xs = np.nonzero(H)
    hy1 = ys.max()
    if view == 'front':
        C = classes(a)
        sy, sx = np.nonzero(C == 'S')
        if len(sy) == 0:
            return a
        top_face = sy.min()
        sel = sy < top_face + 9
        sy, sx = sy[sel], sx[sel]
        fx0, fx1 = sx.min(), sx.max()
        for y in range(sy.min(), sy.max() + extra):
            for x in list(range(fx0 - 2, fx0)) + list(range(fx1 + 1, fx1 + 3)):
                if 0 <= x < a.shape[1] and 0 <= y < a.shape[0]:
                    a[y, x, :3] = dark; a[y, x, 3] = 255
    else:
        # 머리 덩어리 아래쪽 행을 extra 줄 아래로 복사 (뒤통수/옆 뒤쪽)
        row = hy1
        span = [x for x in range(a.shape[1]) if H[row, x]]
        if view == 'side':
            span = span[:max(3, len(span) * 2 // 3)]         # 뒤쪽(왼쪽)만
        for k in range(1, extra + 1):
            y = row + k
            if y >= a.shape[0]:
                break
            shrink = k // 2
            for x in span[shrink:len(span) - (shrink if view == 'back' else 0)]:
                a[y, x, :3] = dark; a[y, x, 3] = 255
    return close_outline(a)


def add_bun(a, view, r=3.2):
    # 정수리에 동그란 올림머리
    a = a.copy()
    cols = hair_color(a)
    if len(cols) == 0:
        return a
    col = np.median(cols[:, :3], axis=0).astype(np.uint8)
    hi = np.clip(col.astype(int) + 30, 0, 255).astype(np.uint8)
    op = a[..., 3] > 0
    ys, xs = np.nonzero(hair_mask(a))
    top = ys.min()
    cx = xs.mean() if view != 'side' else xs.min() + (xs.max() - xs.min()) * 0.35
    cy = top - r * 0.4
    shift = int(np.ceil(r * 1.2))
    if top - shift < 1:     # 위에 공간이 없으면 그림 전체를 내릴 수 없으니 반지름 줄임
        r = max(2.0, top / 1.4); cy = top - r * 0.4
    for y in range(a.shape[0]):
        for x in range(a.shape[1]):
            d = ((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2) ** 0.5
            if d <= r and y <= top + 1:
                a[y, x, :3] = hi if (x < cx - 0.5 and y < cy) else col; a[y, x, 3] = 255
    return close_outline(a)


# ------------------------------------------------------------------ 고위 연구원 장식
def senior_badge(a, view):
    if view == 'back':
        return a
    a = a.copy()
    C = classes(a)
    ys, xs = np.nonzero(C == 'W')
    op = np.nonzero(a[..., 3])[0]
    top, bottom = op.min(), op.max()
    sel = ys > top + (bottom - top) * 0.62
    if not sel.any():
        return a
    ys, xs = ys[sel], xs[sel]
    y = int(np.percentile(ys, 25))
    row = xs[ys == y]
    if len(row) == 0:
        return a
    x = int(row.max() - 2) if view == 'front' else int(np.median(row))
    for dx, dy, c in ((0, 0, (243, 207, 98)), (1, 0, (207, 162, 58)), (0, 1, (207, 162, 58)), (1, 1, (147, 112, 31))):
        if 0 <= y + dy < a.shape[0] and 0 <= x + dx < a.shape[1] and a[y + dy, x + dx, 3]:
            a[y + dy, x + dx, :3] = c
    return a


# ------------------------------------------------------------------ 고위 연구원 머리 갈아 끼우기
# art_src/senior_heads/head_<방향>.png : 서 있는 그림(stand)과 같은 자리에 그린 새 머리 (대머리 + 옆머리).
# 걷기 프레임마다 머리 위치(위아래 출렁임)를 원래 머리 실루엣으로 찾아서 그 자리에 붙인다.
HEADS = os.path.join(HERE, 'art_src', 'senior_heads')
NECK = {'front': 24, 'back': 24, 'side': 20}      # 이 줄 위가 머리 (서 있는 그림 기준)
HEAD_DROP = {'front': 1, 'back': 2, 'side': 3}    # 머리를 몇 픽셀 내려 붙일지 (목 짧게 — 대각선 아래 영상 그림에 맞춤)
_ref = {}


def new_head(a, view):
    hp = os.path.join(HEADS, 'head_%s.png' % view)
    if not os.path.exists(hp):
        return a
    if view not in _ref:
        _ref[view] = np.array(Image.open(os.path.join(SRC, 'stand_researcher_%s.png' % view)).convert('RGBA'))
    head = np.array(Image.open(hp).convert('RGBA'))
    ny = NECK[view]
    m_ref = _ref[view][:ny, :, 3] > 0
    best = None
    for dy in range(-3, 4):
        for dx in range(-3, 4):
            sh = np.roll(np.roll(a[..., 3] > 0, -dy, 0), -dx, 1)[:ny]
            sc = (sh & m_ref).sum() - 0.5 * (sh ^ m_ref).sum()
            if best is None or sc > best[0]:
                best = (sc, dx, dy)
    _, dx, dy = best
    o = a.copy()
    o[:max(0, ny + dy)] = 0
    drop = HEAD_DROP.get(view, 0)
    for y, x in zip(*np.nonzero(head[..., 3] > 0)):
        Y, X = y + dy + drop, x + dx
        if 0 <= Y < o.shape[0] and 0 <= X < o.shape[1]:
            o[Y, X] = head[y, x]
    if view == 'back':   # 목덜미에 남은 원래 갈색 머리 → 새 머리색
        for y in range(max(0, ny + dy), min(o.shape[0], ny + dy + 4)):
            for x in range(o.shape[1]):
                r, g, b_, al = [int(t) for t in o[y, x]]
                if al and r > b_ + 20 and r >= g and max(r, g, b_) < 150:
                    o[y, x, :3] = (144, 141, 157) if max(r, g, b_) > 60 else (112, 106, 137)
        o = hair_outline(soften_hair(clean_collar(o)))
    return close_outline(o)


def clean_collar(o, rows=5):
    """뒷모습에서 머리를 내려 붙이면 옆머리 밑에 원래 가운 테두리(검은 줄)·회색 음영 띠가 남는다 → 가운 흰색으로.
    열마다 머리(회보라)의 맨 아랫줄을 찾고, 그 아래 rows줄 안에서 양옆이 칠해진 안쪽 픽셀만 고친다 (바깥 테두리는 그대로)"""
    o = o.copy(); H, W = o.shape[:2]
    WHITE = np.array([232, 232, 231], np.uint8)
    HAIR = {(0x90, 0x8d, 0x9d), (0xb2, 0xb0, 0xbb), (0x70, 0x6a, 0x89), (0x65, 0x5d, 0x63)}   # 고위 연구원 옆머리 색 (senior_heads)
    def hairish(c): return (int(c[0]), int(c[1]), int(c[2])) in HAIR
    for x in range(1, W - 1):
        hb = None
        for y in range(H):
            if o[y, x, 3] and hairish(o[y, x]): hb = y
        if hb is None: continue
        for y in range(hb + 1, min(H - 1, hb + 1 + rows)):
            c = o[y, x]
            if not c[3] or not (o[y, x - 1, 3] and o[y, x + 1, 3]): continue
            r, g, b = int(c[0]), int(c[1]), int(c[2])
            gray = max(r, g, b) - min(r, g, b) < 18
            if (max(r, g, b) < 70) or (gray and 90 < max(r, g, b) < 215):
                # 바로 아래가 비어 있으면 몸 바깥 테두리 → 그대로
                if not o[y + 1, x, 3]: continue
                o[y, x, :3] = WHITE
    return o


def soften_hair(o):
    """뒷모습 옆머리의 진한 음영(구불구불한 짙은 보라) → 중간 회보라로. 바깥 테두리(검정)는 그대로"""
    o = o.copy(); H, W = o.shape[:2]
    MID = np.array([0x90, 0x8d, 0x9d], np.uint8)
    for y in range(1, H - 1):
        for x in range(1, W - 1):
            c = o[y, x]
            if not c[3]: continue
            r, g, b = int(c[0]), int(c[1]), int(c[2])
            if not (b >= r - 4 and b > g and 70 <= max(r, g, b) < 0x8c and max(r, g, b) - min(r, g, b) < 40): continue
            if all(o[y + dy, x + dx, 3] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                o[y, x, :3] = MID
    return o


def hair_outline(o):
    """뒷모습: 옆머리와 흰 가운 사이에 검은 픽셀 테두리 (다른 부분 테두리와 같은 색)"""
    o = o.copy(); H, W = o.shape[:2]
    OUT = np.array([0x0c, 0x08, 0x0e], np.uint8)
    def hairish(c):
        r, g, b = int(c[0]), int(c[1]), int(c[2])
        return b >= r - 4 and b > g and 70 <= max(r, g, b) < 200 and max(r, g, b) - min(r, g, b) < 40
    for x in range(W):
        for y in range(H - 1):
            if o[y, x, 3] and hairish(o[y, x]):
                c = o[y + 1, x]
                if c[3] and min(int(c[0]), int(c[1]), int(c[2])) > 190:   # 바로 아래가 흰 가운
                    o[y + 1, x, :3] = OUT
    return o


def rec(a, hair=None, tie=None, skin=None):
    if hair: a = mp.recolor(a, mp.HAIR, hair)
    if skin: a = mp.recolor(a, mp.SKIN, skin)
    if tie: a = mp.recolor(a, mp.TIE, tie)
    return a


LOOKS = {
    # 머리 모양은 원래 그대로(깎거나 늘이면 깨져 보여서), 색만 다르게. 일반 연구원은 안경 벗기고 점눈
    'researcher':     lambda a, v: remove_glasses(a, v),
    'researcher_v2':  lambda a, v: rec(remove_glasses(a, v), hair=(222, 112, 48), tie=(64, 112, 200)),
    'researcher_v3':  lambda a, v: rec(remove_glasses(a, v), hair=(252, 222, 120), tie=(150, 46, 60)),
    'researcher2':    lambda a, v: rec(remove_glasses(a, v), hair=(44, 38, 52), skin=(176, 118, 84), tie=(196, 54, 60)),
    'researcher2_v2': lambda a, v: rec(remove_glasses(a, v), hair=(90, 60, 40), tie=(220, 160, 40)),
    # 고위 연구원만 안경 + 회색 머리 + 보라 넥타이 + 금 배지
    'senior':         lambda a, v: senior_badge(new_head(rec(a, hair=(196, 196, 204), tie=(122, 74, 184)), v), v),   # 머리는 새로 그린 대머리로
}


def set_vref(paths):
    for fn in (mp.HAIR, mp.SKIN, mp.TIE):
        vmax = 0
        for p in paths:
            a = np.array(Image.open(p).convert('RGBA'))
            for y, x in zip(*np.nonzero(a[..., 3])):
                h, s_, v = colorsys.rgb_to_hsv(*(a[y, x, :3] / 255))
                if fn(h * 360, s_, v):
                    vmax = max(vmax, v)
        mp.VREF[fn.__name__] = vmax or None


def main():
    os.makedirs(SRC, exist_ok=True)
    fresh = '--fresh' in sys.argv or not os.listdir(SRC)
    if fresh:   # 지금 Art에 있는 (안경 쓴) 연구원 그림을 원본으로 보관
        for p in glob.glob(os.path.join(ART, 'walk_researcher_*_[0-9].png')) + glob.glob(os.path.join(ART, 'stand_researcher_*.png')):
            name = os.path.basename(p)
            if name.split('_')[2].split('.')[0] in ('front', 'back', 'side', 'frontside', 'backside'):
                shutil.copy(p, os.path.join(SRC, name))
    srcs = sorted(glob.glob(os.path.join(SRC, '*.png')))
    set_vref([p for p in srcs if '_front_' in p or p.endswith('_front.png')])   # 정면만 (frontside 제외 — 색 기준이 바뀌지 않게)
    n = 0
    for p in srcs:
        name = os.path.basename(p)                       # walk_researcher_front_3.png / stand_researcher_side.png
        prefix, rest = name.split('_researcher_')
        view = rest.split('_')[0].split('.')[0]
        a = np.array(Image.open(p).convert('RGBA'))
        # 대각선(frontside/backside)은 안경 없는 연구원으로 새로 뽑은 그림 → 안경 벗기기·배지 없이 색만 바꾼다.
        # 고위 연구원은 대각선용 대머리 그림이 없어서 건너뜀 (게임은 대각선 그림이 없으면 정면/옆/뒤를 씀)
        diag = view in ('frontside', 'backside')
        for kind, fn in LOOKS.items():
            if diag and kind == 'senior':
                continue
            out = fn(a, 'back' if diag else view)
            Image.fromarray(out, 'RGBA').save(os.path.join(ART, '%s_%s_%s' % (prefix, kind, rest)))
            n += 1
    print('ok', n)


if __name__ == '__main__':
    main()
