# 사람 그림(stand_<종류>_<방향>)의 실제 키(투명 아닌 줄 수)를 재서 Assets/Scripts/View/SpriteHeights.cs 로 적는다.
# 게임은 이 표로 방향마다 키를 맞춘다 (돌아설 때 캐릭터가 커졌다 작아졌다 하지 않게). 사람 그림을 새로 넣으면 다시 실행.
import os, glob, re
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(HERE, '..', 'Assets', 'Resources', 'Art')
OUT = os.path.join(HERE, '..', 'Assets', 'Scripts', 'View', 'SpriteHeights.cs')
rows = []
for p in sorted(glob.glob(os.path.join(ART, 'stand_*.png'))):
    name = os.path.basename(p)[:-4]
    a = np.asarray(Image.open(p).convert('RGBA'))[..., 3]
    ys = np.nonzero(a.any(1))[0]
    if len(ys): rows.append((name, int(ys.max() - ys.min() + 1)))
with open(OUT, 'w', encoding='utf-8') as f:
    f.write('// 자동 생성: python3 Tools/gen_sprite_heights.py — 손으로 고치지 말 것\n')
    f.write('using System.Collections.Generic;\n\nnamespace Specimen07\n{\n    public static class SpriteHeights\n    {\n')
    f.write('        public static readonly Dictionary<string, int> H = new Dictionary<string, int>\n        {\n')
    for n, h in rows: f.write('            { "%s", %d },\n' % (n, h))
    f.write('        };\n    }\n}\n')
print('ok', len(rows))
