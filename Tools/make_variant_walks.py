# 연구원 걷기(영상에서 뽑은 walk_researcher_*)와 서 있는 그림을 색만 바꿔 변형들을 만든다.
#  researcher_v2(주황 머리·파랑 넥타이), researcher_v3(금발·자주), researcher2(검은 머리·어두운 피부·빨강), researcher2_v2(짙은 갈색·노랑)
# 실행: python3 Tools/make_variant_walks.py   (영상에서 연구원 걷기를 다시 뽑은 뒤)
import glob
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import make_people_from_sheet as mp  # noqa: E402

ART = os.path.join(HERE, '..', 'Assets', 'Resources', 'Art')
VARIANTS = {
    'researcher_v2': lambda r: mp.recolor(mp.recolor(r, mp.HAIR, (222, 112, 48)), mp.TIE, (64, 112, 200)),
    'researcher_v3': lambda r: mp.recolor(mp.recolor(r, mp.HAIR, (252, 222, 120)), mp.TIE, (150, 46, 60)),
    'researcher2': lambda r: mp.recolor(mp.recolor(mp.recolor(r, mp.HAIR, (44, 38, 52)), mp.SKIN, (176, 118, 84)), mp.TIE, (196, 54, 60)),
    'researcher2_v2': lambda r: mp.recolor(mp.recolor(r, mp.HAIR, (90, 60, 40)), mp.TIE, (220, 160, 40)),
}


def set_vref(paths):
    # 모든 프레임에서 같은 기준 밝기를 쓰도록 (프레임마다 색이 달라지지 않게)
    import colorsys
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
    n = 0
    set_vref(sorted(glob.glob(os.path.join(ART, 'walk_researcher_front_[0-9].png'))) + [os.path.join(ART, 'stand_researcher_front.png')])
    for path in sorted(glob.glob(os.path.join(ART, 'walk_researcher_*_[0-9].png'))):
        name = os.path.basename(path)[len('walk_researcher_'):]          # 예: front_3.png
        if name.split('_')[0] not in ('front', 'back', 'side'):
            continue
        a = np.array(Image.open(path).convert('RGBA'))
        for kind, fn in VARIANTS.items():
            Image.fromarray(fn(a), 'RGBA').save(os.path.join(ART, 'walk_%s_%s' % (kind, name)))
            n += 1
    # 서 있는 그림도 같은 기준으로 (걷다 멈출 때 색이 바뀌지 않게)
    for view in ('front', 'back', 'side'):
        a = np.array(Image.open(os.path.join(ART, 'stand_researcher_%s.png' % view)).convert('RGBA'))
        for kind, fn in VARIANTS.items():
            Image.fromarray(fn(a), 'RGBA').save(os.path.join(ART, 'stand_%s_%s.png' % (kind, view)))
            n += 1
    print('ok', n)


if __name__ == '__main__':
    main()
