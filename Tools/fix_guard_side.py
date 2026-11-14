# 경비원 옆모습 걷기에서 AI가 덧그린 '두 번째 팔'(배 앞에서 흔들리는 손과 소매 선)을 지운다.
# 손전등을 든 팔(앞쪽, 노란 불빛 주변)만 남기고, 배 부분 안쪽의 검은 선·피부색 손을 몸통 색으로 메운다.
# 바깥 외곽선은 그대로 두고, 끝나면 끊긴 외곽선을 다시 닫는다.
# 실행: python3 Tools/fix_guard_side.py   (extract_walk_video.py 로 guard side 를 뽑은 다음)
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


def fix(a):
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
    # 손전등: 몸 앞쪽(오른쪽) 끝에서 튀어나온 부분. 노란 불빛이 있으면 그 주변, 없으면 오른쪽 끝 6칸
    r_, g_, b_ = a[..., 0].astype(int), a[..., 1].astype(int), a[..., 2].astype(int)
    yellow = (r_ > 160) & (g_ > 140) & (b_ < 100) & (g_ > r_ * 0.78) & op & (np.arange(h)[:, None] > face_bot)   # 피부색과 구분되는 노란 불빛
    keep = np.zeros_like(op)
    fy, fx = np.nonzero(yellow)
    if len(fx):
        keep[max(0, fy.min() - 3):fy.max() + 4, max(0, fx.min() - 6):] = True
    else:
        xs = np.nonzero(op[int(face_bot) + 2:int(top + (bot - top) * 0.8)].any(0))[0]
        keep[:, xs.max() - 6:] = True
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
    return close_outline(a)


def main():
    files = sorted(glob.glob(os.path.join(ART, 'walk_guard_side_*.png')))
    for f in files:
        a = np.array(Image.open(f).convert('RGBA'))
        Image.fromarray(fix(a), 'RGBA').save(f)
    print('ok', len(files))


if __name__ == '__main__':
    main()
