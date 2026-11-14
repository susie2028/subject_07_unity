# 청소부 뒷모습(서 있기 + 걷기)에 대걸레를 그려 넣는다.
# 앞모습 그림의 대걸레를 좌우로 뒤집어, 프레임마다 왼쪽(뒤에서 본 오른손) 장갑 위치에 맞춰 바닥에 세운다.
# 대걸레는 몸 뒤쪽(몸 그림이 위에 덮임). 시트·영상에서 그림을 다시 뽑은 뒤에 실행.
# 실행: python3 Tools/add_cleaner_back_mop.py
import glob
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(HERE, '..', 'Assets', 'Resources', 'Art')


def mop_from_front():
    a = np.array(Image.open(os.path.join(ART, 'stand_cleaner_front.png')).convert('RGBA'))
    h, w = a.shape[:2]
    m = np.zeros((h, w), bool)
    yellow = (a[..., 0] > 200) & (a[..., 1] > 150) & (a[..., 2] < 90)
    for y in range(h):
        for x in range(w):
            if a[y, x, 3] == 0:
                continue
            if 13 <= y <= 25 and x >= 29: m[y, x] = True
            elif 26 <= y <= 31 and 29 <= x <= 31 and not yellow[y, x]: m[y, x] = True
            elif y >= 32 and x >= 27: m[y, x] = True
    mop = np.zeros_like(a); mop[m] = a[m]
    # 장갑에 가려 있던 자루 부분 채우기 (외곽선-나무-외곽선)
    for y in range(26, 32):
        mop[y, 29] = (12, 8, 14, 255); mop[y, 30] = a[20, 30]; mop[y, 31] = (12, 8, 14, 255)
    mop = mop[:, ::-1]           # 좌우 뒤집기 (뒤에서 보면 반대쪽 손)
    ys, xs = np.nonzero(mop[..., 3])
    stick_x = w - 1 - 30         # 뒤집힌 자루 가운데 열
    return mop, stick_x, ys.max()


def apply(path, mop, stick_x, mop_bot):
    a = np.array(Image.open(path).convert('RGBA'))
    h, w = a.shape[:2]
    yellow = (a[..., 0] > 200) & (a[..., 1] > 150) & (a[..., 2] < 90) & (a[..., 3] > 0)
    ys, xs = np.nonzero(yellow & (np.arange(w)[None, :] < w // 2))
    op = np.nonzero(a[..., 3])
    ground = op[0].max()
    gx = int(round(xs.mean())) if len(xs) else stick_x
    dx = gx - 1 - stick_x        # 장갑 바깥쪽에 자루
    dy = ground - mop_bot
    mx0 = np.nonzero(mop[..., 3])[1].min()
    dx = max(dx, -mx0)           # 걸레 머리가 그림 밖으로 잘리지 않게
    out = np.zeros_like(a)
    my, mx = np.nonzero(mop[..., 3])
    for y, x in zip(my, mx):
        ny, nx = y + dy, x + dx
        if 0 <= ny < h and 0 <= nx < w:
            out[ny, nx] = mop[y, x]
    keep = a[..., 3] > 0
    out[keep] = a[keep]           # 몸이 위에
    Image.fromarray(out, 'RGBA').save(path)


def main():
    mop, sx, mb = mop_from_front()
    files = [os.path.join(ART, 'stand_cleaner_back.png')] + sorted(glob.glob(os.path.join(ART, 'walk_cleaner_back_*.png')))
    for f in files:
        apply(f, mop, sx, mb)
    print('ok', len(files))


if __name__ == '__main__':
    main()
