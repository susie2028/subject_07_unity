# 군인 대각선 위 영상은 AI가 고글을 자꾸 '불 켜진 랜턴'처럼 빛나게 만든다.
# 빛나기 전 첫 장(walk_soldier_backside_0)의 머리(헬멧·고글·얼굴 옆)를 나머지 프레임에 위치만 맞춰 옮겨 붙여서 불빛을 없앤다.
# 사용: python3 Tools/fix_soldier_glow.py [종류_방향=soldier_backside] [머리 아랫줄=14]
import os, sys
import numpy as np
from PIL import Image

ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', 'Resources', 'Art')
name = sys.argv[1] if len(sys.argv) > 1 else 'soldier_backside'
CUT = int(sys.argv[2]) if len(sys.argv) > 2 else 14

def load(i): return np.array(Image.open(os.path.join(ART, 'walk_%s_%d.png' % (name, i))).convert('RGBA'))

def anchor(a):
    """헬멧 맨 윗줄 y, 그 아래 6줄 사이 헬멧 왼쪽 끝 x (위치 맞추기 기준)"""
    ys, xs = np.nonzero(a[..., 3])
    top = ys.min()
    sel = (ys >= top) & (ys < top + 6)
    return top, xs[sel].min()

ref = load(0); rt, rl = anchor(ref)
head = ref[:rt + CUT].copy()
for i in range(1, 8):
    a = load(i); t, l = anchor(a); dy, dx = t - rt, l - rl
    H, W = a.shape[:2]
    # 머리 줄은 비우고 (번진 불빛 포함) 깨끗한 머리를 옮겨 붙임. 머리 줄 안의 다른 것(팔 등)은 머리 왼쪽 밖이면 남김
    for y in range(0, min(H, rt + CUT + dy)):
        for x in range(W):
            if x >= l - 1:
                a[y, x] = 0
    for y in range(head.shape[0]):
        for x in range(W):
            if head[y, x, 3] == 0: continue
            yy, xx = y + dy, x + dx
            if 0 <= yy < H and 0 <= xx < W: a[yy, xx] = head[y, x]
    Image.fromarray(a, 'RGBA').save(os.path.join(ART, 'walk_%s_%d.png' % (name, i)))
print('ok', name)
