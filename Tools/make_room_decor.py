# 방 꾸밈용 그림: 소장실 액자·러그, 휴게실 러그, 사무실 러너, 실험실 바닥 경고 표시
# python3 Tools/make_room_decor.py  → Assets/Resources/Art/*.png (+ .meta 없으면 wall_face.png.meta 를 본떠 만든다)
import os, re, uuid, math
from PIL import Image, ImageDraw
ROOT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Resources', 'Art')
def hx(h, a=255): h = h.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)

def save(img, name):
    p = os.path.join(ROOT, name + '.png'); img.save(p)
    m = p + '.meta'
    if not os.path.exists(m):
        src = open(os.path.join(ROOT, 'wall_face.png.meta'), encoding='utf-8').read()
        src = re.sub(r'guid: [0-9a-f]+', 'guid: ' + uuid.uuid4().hex, src, count=1)
        open(m, 'w', encoding='utf-8').write(src)
    print('wrote', name, img.size)

base = Image.open(os.path.join(ROOT, 'wall_face.png')).convert('RGBA')
# 액자 1: 풍경화
a = base.copy(); d = ImageDraw.Draw(a)
d.rectangle([3, 1, 12, 8], fill=hx('#c9a24a')); d.rectangle([4, 2, 11, 7], fill=hx('#6fa0d8'))
d.rectangle([4, 5, 11, 7], fill=hx('#4f8a4a')); d.point([(6, 4), (7, 4)], fill=hx('#4f8a4a')); d.point((10, 3), fill=hx('#ffe08a'))
d.line([3, 9, 12, 9], fill=hx('#1c1e4c', 120))
save(a, 'wall_face_art')
# 액자 2: 초상화 (누군지 모를 실루엣)
a = base.copy(); d = ImageDraw.Draw(a)
d.rectangle([4, 0, 11, 9], fill=hx('#c9a24a')); d.rectangle([5, 1, 10, 8], fill=hx('#5a1a24'))
d.ellipse([6, 2, 9, 5], fill=hx('#e8d2b0')); d.rectangle([6, 6, 9, 8], fill=hx('#1e2329'))
d.line([4, 10, 11, 10], fill=hx('#1c1e4c', 120))
save(a, 'wall_face_art2')
# 액자 3: 연구소 문장 (방패 + 07)
a = base.copy(); d = ImageDraw.Draw(a)
d.polygon([(5, 1), (10, 1), (10, 6), (7, 9), (5, 6)], fill=hx('#c9a24a')); d.polygon([(6, 2), (9, 2), (9, 6), (7, 8), (6, 6)], fill=hx('#2a3466'))
d.point([(7, 4), (8, 4)], fill=hx('#ff7d8c'))
save(a, 'wall_face_emblem')

# 소장실 러그: 짙은 적색 + 금색 테두리 + 가운데 문양
W, H = 64, 40
r = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(r)
d.rectangle([0, 0, W - 1, H - 1], fill=hx('#5e1520')); d.rectangle([2, 2, W - 3, H - 3], outline=hx('#c9a24a'))
d.rectangle([4, 4, W - 5, H - 5], outline=hx('#8a2a34')); d.rectangle([6, 6, W - 7, H - 7], outline=hx('#c9a24a', 160))
cx, cy = W // 2, H // 2
d.polygon([(cx, cy - 9), (cx + 14, cy), (cx, cy + 9), (cx - 14, cy)], fill=hx('#7a2230'), outline=hx('#c9a24a'))
d.polygon([(cx, cy - 4), (cx + 6, cy), (cx, cy + 4), (cx - 6, cy)], fill=hx('#c9a24a'))
for x in range(0, W, 4): d.point([(x, 0), (x + 2, H - 1)], fill=hx('#e8d2b0'))
save(r, 'decal_rug_dir')
# 휴게실 둥근 러그
r = Image.new('RGBA', (40, 40), (0, 0, 0, 0)); d = ImageDraw.Draw(r)
for i, c in enumerate(['#8a4a2a', '#c07a4a', '#8a4a2a', '#e0a070', '#a85e38']): d.ellipse([i * 3, i * 3, 39 - i * 3, 39 - i * 3], fill=hx(c, 220))
save(r, 'decal_rug_rest')
# 사무실 러너 (복도식 긴 카펫)
r = Image.new('RGBA', (64, 14), (0, 0, 0, 0)); d = ImageDraw.Draw(r)
d.rectangle([0, 0, 63, 13], fill=hx('#2a3466', 210)); d.rectangle([1, 1, 62, 12], outline=hx('#7f8fd0', 200))
for x in range(4, 60, 6): d.point([(x, 6), (x + 1, 7)], fill=hx('#5a6ab0', 220))
save(r, 'decal_runner')
# 실험실 바닥 경고 표시 (노란 원 + 세 고리)
r = Image.new('RGBA', (40, 40), (0, 0, 0, 0)); d = ImageDraw.Draw(r)
d.ellipse([1, 1, 38, 38], outline=hx('#e0b83a', 200), width=3)
for k in range(3):
    ang = math.radians(90 + k * 120); x, y = 20 + math.cos(ang) * 7, 20 - math.sin(ang) * 7
    d.ellipse([x - 7, y - 7, x + 7, y + 7], outline=hx('#e0b83a', 210), width=2)
d.ellipse([16, 16, 23, 23], fill=hx('#e0b83a', 210))
save(r, 'decal_hazard')
