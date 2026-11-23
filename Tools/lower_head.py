# 걷기 프레임에서 머리만 몇 픽셀 내려 붙인다 (목이 길어 보일 때). 머리 = 맨 위부터 흰 가운이 시작되는 줄 위까지.
# 사용: python3 Tools/lower_head.py <종류_방향 예: senior_backside> <내릴 픽셀=3>
import os, sys
import numpy as np
from PIL import Image

ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', 'Resources', 'Art')
name = sys.argv[1]; drop = int(sys.argv[2]) if len(sys.argv) > 2 else 3

def coat_row(a):
    for y in range(a.shape[0]):
        row = a[y]
        white = ((row[:, 3] > 0) & (row[:, :3].min(-1) > 200)).sum()
        if white >= 4: return y
    return None

files = sorted(f for f in os.listdir(ART) if (f.startswith('walk_%s_' % name) or f == 'stand_%s.png' % name) and f.endswith('.png'))
for f in files:
    p = os.path.join(ART, f)
    a = np.array(Image.open(p).convert('RGBA'))
    cy = coat_row(a)
    if cy is None: print('skip', f); continue
    head = a[:cy].copy()
    o = a.copy(); o[:cy] = 0
    # 원래 가운 윗부분은 그대로 두고, 그 위에 내린 머리를 덮어 그린다
    o[cy:cy + drop] = a[cy:cy + drop]
    for y, x in zip(*np.nonzero(head[..., 3] > 0)):
        if y + drop < o.shape[0]: o[y + drop, x] = head[y, x]
    Image.fromarray(o, 'RGBA').save(p)
print('ok', name, len(files))
