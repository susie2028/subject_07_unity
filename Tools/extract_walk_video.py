# 걷기 영상(자홍색 배경, 한 캐릭터)에서 걷기 프레임을 뽑아 게임 스프라이트로 저장한다.
# 사용: python3 Tools/extract_walk_video.py <영상.mp4> <종류> <방향 front|back|side> [프레임수=8] [한 걸음 주기 프레임=24]
#   예) python3 Tools/extract_walk_video.py Tools/art_src/walk_soldier_front.mp4 soldier front
# 결과: Assets/Resources/Art/walk_<종류>_<방향>_0..N-1.png (40x42, 발이 맨 아래 줄)
#  - 영상 속 그림을 원래 픽셀 격자(자동 추정)로 되돌리고, 몸 흔들림·위치는 그대로 유지
#  - 프레임이 6장 이상이면 게임은 이 그림을 그대로 재생 (코드로 만든 출렁임을 덧붙이지 않음)
import os
import subprocess
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', 'Assets', 'Resources', 'Art')
CW, CH = 40, 42


def frames_of(path):
    probe = subprocess.run(['ffprobe', '-v', 'error', '-select_streams', 'v:0', '-show_entries', 'stream=width,height', '-of', 'csv=p=0', path], capture_output=True, text=True).stdout.strip().split(',')
    w, h = int(probe[0]), int(probe[1])
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', path, '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'], capture_output=True).stdout
    return np.frombuffer(raw, np.uint8).reshape(-1, h, w, 3).astype(float)


def is_bg(col):
    r, g, b = col
    return np.sqrt(((col - [255, 0, 255]) ** 2).sum()) < 130 or (r > 150 and b > 150 and g < 120) or (r > g + 35 and b > g + 35)


def fit_period(fr):
    best = None
    f = fr[0]
    gx = np.abs(np.diff(f, axis=1)).sum(-1).sum(0)
    for P in np.arange(8, 20, 0.02):
        for x0 in np.arange(0, P, 0.5):
            idx = np.round(np.arange(x0, len(gx) - 1, P)).astype(int)
            sc = gx[idx].mean()
            if best is None or sc > best[0]:
                best = (sc, P)
    return best[1]


def phase(sig, P):
    best = None
    for x0 in np.arange(0, P, 0.25):
        idx = np.round(np.arange(x0, len(sig) - 1, P)).astype(int)
        sc = sig[idx].mean()
        if best is None or sc > best[0]:
            best = (sc, x0)
    return best[1]


def grab(f, P, cx_v, foot_v):
    gx = np.abs(np.diff(f, axis=1)).sum(-1).sum(0); gy = np.abs(np.diff(f, axis=0)).sum(-1).sum(1)
    x0 = phase(gx, P) + 0.5; y0 = phase(gy, P) + 0.5
    H, W = f.shape[:2]
    out = np.zeros((CH, CW, 4), np.uint8)
    for j in range(CH):
        for i in range(CW):
            vx = cx_v + (i - CW / 2 + 0.5) * P; vy = foot_v - (CH - j - 0.5) * P
            kx = round((vx - x0) / P - 0.5); ky = round((vy - y0) / P - 0.5)
            cx = x0 + (kx + 0.5) * P; cy = y0 + (ky + 0.5) * P
            a, b = int(cy - P * 0.2), int(cy + P * 0.2) + 1; c, d = int(cx - P * 0.2), int(cx + P * 0.2) + 1
            if a < 0 or c < 0 or b > H or d > W:
                continue
            col = np.median(f[a:b, c:d].reshape(-1, 3), axis=0)
            if is_bg(col):
                continue
            out[j, i, :3] = col; out[j, i, 3] = 255
    return out


def main():
    path, kind, view = sys.argv[1], sys.argv[2], sys.argv[3]
    n = int(sys.argv[4]) if len(sys.argv) > 4 else 8
    cycle = int(sys.argv[5]) if len(sys.argv) > 5 else 24
    fr = frames_of(path)
    P = fit_period(fr)
    # 캐릭터 기준점: 전체 영상에서 그림의 가로 중심과 발 위치 (평균)
    xs, ys = [], []
    for f in fr[:cycle]:
        m = np.sqrt(((f - [255, 0, 255]) ** 2).sum(-1)) > 120
        yy, xx = np.nonzero(m); xs.append(xx.mean()); ys.append(yy.max())
    cx_v = float(np.mean(xs)); foot_v = float(np.max(ys)) + 1
    picks = [int(round(k * cycle / n)) for k in range(n)]
    for k, i in enumerate(picks):
        Image.fromarray(grab(fr[i], P, cx_v, foot_v), 'RGBA').save(os.path.join(OUT, 'walk_%s_%s_%d.png' % (kind, view, k)))
    # 예전 4장짜리가 남아 있으면 번호가 겹치지 않게 지움
    k = n
    while os.path.exists(os.path.join(OUT, 'walk_%s_%s_%d.png' % (kind, view, k))):
        os.remove(os.path.join(OUT, 'walk_%s_%s_%d.png' % (kind, view, k)))
        meta = os.path.join(OUT, 'walk_%s_%s_%d.png.meta' % (kind, view, k))
        if os.path.exists(meta):
            os.remove(meta)
        k += 1
    print('ok', kind, view, n, 'frames (pixel %.2f)' % P)


if __name__ == '__main__':
    main()
