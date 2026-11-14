# 경비원 옆모습 걷기에서 AI가 덧그린 '두 번째 팔'(배 앞에서 흔들리는 손과 소매 선)을 지운다.
# 손전등을 든 팔(앞쪽, 노란 불빛 주변)만 남기고, 배 부분 안쪽의 검은 선·피부색 손을 몸통 색으로 메운다.
# 바깥 외곽선은 그대로 두고, 끝나면 끊긴 외곽선을 다시 닫는다.
# 그 다음 손전등 든 앞쪽 팔(어깨 견장 → 손전등 뒤 손)을 외곽선 있게 다시 그려 실루엣이 잘 보이게 한다.
# 실행: python3 Tools/fix_guard_side.py   (extract_walk_video.py 로 guard side 를 뽑은 다음. 끝나면 walk_guard_side_0 을 stand_guard_side 로 복사)
import glob
import os
import sys

import numpy as np
import scipy.ndimage as nd
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from extract_walk_video import close_outline  # noqa: E402
import make_variant_walks as mv  # noqa: E402

ART = os.path.join(HERE, '..', 'Assets', 'Resources', 'Art')


def find_flashlight(a, face_bot, bot):
    # 손전등 = 배 높이에서 몸 앞쪽(오른쪽)에 있는 올리브색 몸통 + 주황/노란 불빛
    h, w = a.shape[:2]
    op = a[..., 3] > 0
    r_, g_, b_ = a[..., 0].astype(int), a[..., 1].astype(int), a[..., 2].astype(int)
    olive = (b_ < 55) & (r_ > 40) & (g_ > 40) & (abs(r_ - g_) < 30)
    lens = (r_ > 170) & (g_ > 110) & (b_ < 140) & (g_ < r_)
    xs = np.nonzero(op.any(0))[0]
    cx = (xs.min() + xs.max()) / 2
    m = op & (olive | lens)
    m[:int(face_bot) + 2] = False; m[int(face_bot + (bot - face_bot) * 0.75):] = False; m[:, :int(cx) + 2] = False
    lab, n = nd.label(m, structure=np.ones((3, 3)))
    if n == 0:
        return None
    sizes = nd.sum(m, lab, range(1, n + 1))
    i = int(np.argmax(sizes)) + 1
    yy, xx = np.nonzero(lab == i)
    return yy.min(), yy.max(), xx.min(), xx.max()


def fix(a, fl_override=None):
    a = a.copy()
    C = mv.classes(a)
    op = a[..., 3] > 0
    h, w = op.shape
    ys = np.nonzero(op)[0]; top, bot = ys.min(), ys.max()
    # 얼굴(맨 위 피부 덩어리) 아래부터
    lab, n = nd.label(C == 'S', structure=np.ones((3, 3)))
    face_bot = top + (bot - top) * 0.45
    best = None
    for i in range(1, n + 1):
        yy, xx = np.nonzero(lab == i)
        if len(yy) >= 8 and (best is None or yy.min() < best[0]):
            best = (yy.min(), yy.max())
    if best:
        face_bot = best[1]
    # 손전등 주변은 남김
    keep = np.zeros_like(op)
    fl = fl_override if fl_override is not None else find_flashlight(a, face_bot, bot)
    if fl:
        y0, y1, x0, x1 = fl
        keep[max(0, y0 - 1):y1 + 2, max(0, x0 - 1):] = True
    #   → 벨트 = 배 아래쪽에서 검은 선이 가로로 길게(8칸 이상) 이어지는 첫 줄
    belt = int(top + (bot - top) * 0.8)
    for y in range(int(face_bot) + 4, bot):
        run = best_run = 0
        for x in range(w):
            run = run + 1 if C[y, x] == 'k' else 0
            best_run = max(best_run, run)
        if best_run >= 8:
            belt = y; break
    inner = nd.binary_erosion(op, iterations=2)          # 바깥 외곽선에서 2픽셀 안쪽만
    region = np.zeros_like(op)
    region[int(face_bot) + 2:belt, :] = True
    navy_mask = (C == '?') & region & inner
    if not navy_mask.any():
        return a
    navy = np.median(a[navy_mask][:, :3], axis=0).astype(np.uint8)
    # 지울 것: 배 안쪽의 검은 선 + 피부색(손)
    kill = region & inner & ~keep & ((C == 'k') | (C == 'S'))
    # 벨트 줄 바로 위의 손도 (벨트 줄 자체는 남김)
    hand = (C == 'S') & ~keep & (np.arange(h)[:, None] > face_bot + 1) & (np.arange(h)[:, None] <= belt + 1)
    hand = nd.binary_dilation(hand, iterations=1) & ((C == 'S') | ((C == 'k') & inner)) & ~keep & (np.arange(h)[:, None] > face_bot + 1)
    kill |= hand
    a[kill, :3] = navy
    a = close_outline(a)
    return draw_arm(a, face_bot, belt, navy, fl)


def draw_arm(a, face_bot, belt, navy, fl):
    # 손전등 든 팔(앞쪽 팔)을 실루엣이 잘 보이게 다시 그림: 어깨(금색 견장) → 손전등 뒤쪽 손
    if fl is None:
        return a
    a = a.copy()
    h, w = a.shape[:2]
    op = a[..., 3] > 0
    r_, g_, b_ = a[..., 0].astype(int), a[..., 1].astype(int), a[..., 2].astype(int)
    gold = (r_ > 140) & (g_ > 100) & (b_ < 90) & (r_ - b_ > 70) & op
    rows = np.arange(h)[:, None]
    gy, gx = np.nonzero(gold & (rows > face_bot) & (rows < belt))
    if len(gy) == 0:
        return a
    # 견장 = 금색이 2개 이상 있는 줄 (없으면 가장 아래 금색)
    cand = [y for y in set(gy.tolist()) if (gy == y).sum() >= 2]
    sy = min(cand) if cand else gy.max()
    sx = gx[gy == sy].mean()
    y0, y1, x0, x1 = fl
    fy = (y0 + y1) / 2
    S = np.array([sx + 0.5, sy + 2.5]); G = np.array([x0 - 0.8, fy + 0.5])
    fl_keep = np.zeros_like(op)
    fl_keep[max(0, y0 - 1):y1 + 2, x0:] = op[max(0, y0 - 1):y1 + 2, x0:]
    yy, xx = np.mgrid[0:h, 0:w]
    P = np.stack([xx + 0.5, yy + 0.5], -1)
    d = G - S; L = (d ** 2).sum()
    t = np.clip(((P - S) @ d) / L, 0, 1)
    dist = np.sqrt(((P - (S + t[..., None] * d)) ** 2).sum(-1))
    arm = dist <= 1.35
    handm = np.sqrt(((P - G) ** 2).sum(-1)) <= 1.25
    both = arm | handm
    ring = nd.binary_dilation(both, structure=np.ones((3, 3))) & ~both
    sleeve = np.clip(navy.astype(int) + 14, 0, 255).astype(np.uint8)
    out = a.copy()
    out[ring & ~fl_keep] = (12, 8, 14, 255)
    out[arm & ~handm] = (*sleeve, 255)
    out[arm & ~handm & (yy < (S[1] + t * d[1]) - 0.4)] = (*np.clip(sleeve.astype(int) + 16, 0, 255).astype(np.uint8), 255)   # 소매 윗면 밝게
    out[handm] = (226, 168, 128, 255)
    out[fl_keep] = a[fl_keep]       # 손전등은 손 위에
    return out


def face_and_bottom(a):
    C = mv.classes(a)
    op = a[..., 3] > 0
    ys = np.nonzero(op)[0]; top, bot = ys.min(), ys.max()
    lab, n = nd.label(C == 'S', structure=np.ones((3, 3)))
    best = None
    for i in range(1, n + 1):
        yy, xx = np.nonzero(lab == i)
        if len(yy) >= 8 and (best is None or yy.min() < best[0]):
            best = (yy.min(), yy.max())
    return (best[1] if best else top + (bot - top) * 0.45), bot


def main():
    files = sorted(glob.glob(os.path.join(ART, 'walk_guard_side_*.png')))
    imgs = [np.array(Image.open(f).convert('RGBA')) for f in files]
    # 프레임마다 손전등 위치를 찾고, 튀는 프레임은 다른 프레임들의 중간값으로 (손전등은 거의 같은 자리)
    fls = [find_flashlight(a, *face_and_bottom(a)) for a in imgs]
    good = [f for f in fls if f]
    med = tuple(int(round(np.median([f[i] for f in good]))) for i in range(4)) if good else None
    for k, f in enumerate(fls):
        if med and (f is None or abs((f[0] + f[1]) / 2 - (med[0] + med[1]) / 2) > 1.5 or abs(f[2] - med[2]) > 2):
            fls[k] = med
    for f, a, fl in zip(files, imgs, fls):
        Image.fromarray(fix(a, fl), 'RGBA').save(f)
    print('ok', len(files))


if __name__ == '__main__':
    main()
