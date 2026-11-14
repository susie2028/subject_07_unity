# 경비원 옆모습 걷기에서 AI가 덧그린 '두 번째 팔'(배 앞에서 흔들리는 손과 소매 선)을 지운다.
# 손전등을 든 앞쪽 팔(몸 앞 실루엣)은 그대로 두고, 배 위에 따로 붙은 피부색 손과 그 손가락 선만 몸통 색으로 메운다.
# 바깥 외곽선은 그대로 두고, 끝나면 끊긴 외곽선을 다시 닫는다.
# (지금은 안 씀: 경비원 옆모습은 영상에서 뽑은 그대로 사용)
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
        keep[max(0, y0 - 5):y1 + 4, max(0, x0 - 7):] = True     # 손전등 + 그걸 든 앞쪽 팔 실루엣
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
    # 지울 것: 배 앞쪽(손전등 든 팔 쪽)이 아닌 곳의 피부색 손 + 그 손을 감싼 손가락·손목 선 (안쪽 검은 선만)
    rows = np.arange(h)[:, None]
    hand = (C == 'S') & ~keep & (rows > face_bot + 1) & (rows <= belt + 1)
    lab2, n2 = nd.label(hand, structure=np.ones((3, 3)))
    kill = np.zeros_like(op)
    for i in range(1, n2 + 1):
        part = lab2 == i
        if part.sum() < 2:
            continue
        ring = nd.binary_dilation(part, structure=np.ones((3, 3)), iterations=1)
        brownish = (a[..., 0].astype(int) > a[..., 2].astype(int) + 10) & op      # 손 가장자리의 섞인 갈색
        kill |= part | (ring & ((C == 'k') | brownish) & inner & ~keep)
    a[kill, :3] = navy
    return close_outline(a)


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
