# UI 판넬·이름표 9-slice 그림 만들기
# 원본: Tools/art_src/ui/panel_src.jpg, tab_src.jpg (AI로 뽑은 큰 그림, 마젠타 배경)
# 원본은 픽셀 크기가 들쭉날쭉해서(1칸 ≈ 14.6px / 22.5px), 원본에서 색과 구조(테두리 두께, 리벳, 줄무늬)를 재서
# 진짜 1픽셀 크기로 깔끔하게 다시 찍는다. 네 모서리는 대칭.
#  → Assets/Resources/Art/UI/frame_panel.png (20×20, 테두리 8px, 가운데 스캔라인 — Image Type: Tiled 권장)
#  → Assets/Resources/Art/UI/frame_tab.png   (14×16, 왼쪽 8 / 오른쪽 4 / 위아래 3px — Sliced)
# 테두리(9-slice) 값은 Assets/Editor/ArtImporter.cs가 자동으로 넣는다.
import os
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Resources', 'Art', 'UI')

def hexc(h):
    h = h.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)

def paint(rows, pal):
    H = len(rows); W = len(rows[0])
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    for y, r in enumerate(rows):
        assert len(r) == W, (y, r)
        for x, ch in enumerate(r):
            if ch != '.': im.putpixel((x, y), pal[ch])
    return im

# ---------- 판넬 (원본에서 잰 색) ----------
PANEL_PAL = {
    'K': hexc('0a0710'),   # 바깥 검은 테두리
    'F': hexc('2b2644'),   # 금속 틀
    'h': hexc('5a5670'),   # 리벳 반짝임
    'd': hexc('1c1a26'),   # 리벳
    's': hexc('050407'),   # 리벳 그림자
    'L': hexc('a79ae2'),   # 안쪽 연보라 선
    'N': hexc('0d0c22'),   # 안쪽 남색
    'n': hexc('12112b'),   # 스캔라인
}
CORNER = [            # 왼쪽 위 8×8 (나머지 모서리는 뒤집어서)
    '..KKKKKK',
    '.KFFFFFF',
    'KFhdFFFF',
    'KFdsFLLL',
    'KFFFNNNN',
    'KFFLNNNN',
    'KFFLNNNN',
    'KFFLNNNN',
]
C = 4   # 가운데 크기

def panel():
    B = len(CORNER); W = B * 2 + C
    rows = [[None] * W for _ in range(W)]
    def put(x, y, ch): rows[y][x] = ch
    for y in range(W):
        for x in range(W):
            # 어느 모서리/변에 속하는지 → 왼쪽 위 좌표로 접어서 찾는다
            fx = x if x < B else (W - 1 - x if x >= W - B else None)
            fy = y if y < B else (W - 1 - y if y >= W - B else None)
            if fx is not None and fy is not None: ch = CORNER[fy][fx]
            elif fy is not None: ch = CORNER[fy][B - 1]      # 위·아래 변
            elif fx is not None: ch = CORNER[B - 1][fx]      # 왼쪽·오른쪽 변
            else: ch = 'N'
            if ch == 'N' and y % 2 == 1: ch = 'n'            # 스캔라인 (짝수 크기라 타일해도 이어짐)
            # 아래·오른쪽 리벳은 반짝임 위치를 그대로 왼쪽 위로 (빛 방향 통일)
            if ch in 'hds' and fx is not None and fy is not None:
                lx = x if x < B else x - (W - B) + 0   # 모서리 안 좌표
                ly = y if y < B else y - (W - B)
                ox = 2 if x < B else (W - B) + (B - 4)
                oy = 2 if y < B else (W - B) + (B - 4)
                ch = {(0, 0): 'h', (1, 0): 'd', (0, 1): 'd', (1, 1): 's'}.get((x - ox, y - oy), ch)
            put(x, y, ch)
    return paint([''.join(r) for r in rows], PANEL_PAL)

# ---------- 이름표 (원본 64×16 을 재서 정리) ----------
TAB_PAL = {
    'K': hexc('0a0710'),
    'L': hexc('b195e2'),   # 연보라 테두리
    'M': hexc('8161bb'),   # 모서리 경사
    'D': hexc('311a53'),   # 안쪽 그림자 띠
    'F': hexc('3c2456'),   # 안쪽
    'Y': hexc('fbcd25'),   # 노랑 줄무늬
}
def tab():
    stripes = ['KKYY', 'KYYK', 'YYKK', 'YKKY']   # 대각선, 4줄 주기
    left = ['..KKKKKK', '.KKLLLLL', 'KKLMDDDD']
    for i in range(10):
        r = 3 + i
        edge = 'M' if r in (3, 12) else 'D'
        left.append('KL' + edge + stripes[i % 4] + 'D')
    left += ['KKLMDDDD', '.KKLLLLL', '..KKKKKK']
    center = ['KK', 'LL', 'DD'] + ['FF'] * 10 + ['DD', 'LL', 'KK']
    right = []   # 왼쪽 끝을 뒤집되 줄무늬 없이
    for r in range(16):
        l = left[r][:4]
        if 3 <= r <= 12: l = l[:3] + 'F'
        right.append(l[::-1])
    rows = [left[r] + center[r] + right[r] for r in range(16)]
    return paint(rows, TAB_PAL)

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    panel().save(os.path.join(OUT, 'frame_panel.png'))
    tab().save(os.path.join(OUT, 'frame_tab.png'))
    print('wrote frame_panel.png, frame_tab.png')


# ---------- 버튼 (aicron에서 뽑은 알약 모양 버튼을 재서 1픽셀로 다시 찍음, 20px 높이) ----------
BTN_PAL = {
    'K': hexc('060408'),   # 바깥 검은 테두리
    'L': hexc('b0b0b4'),   # 위쪽 밝은 금속
    'M': hexc('646569'),   # 위쪽 중간 금속
    'S': hexc('1d1426'),   # 안쪽 그림자 (위)
    'F': hexc('34203f'),   # 안쪽 보라
    'f': hexc('2e1c39'),   # 안쪽 보라 (아래쪽 살짝 어둡게)
    'T': hexc('1c1924'),   # 안쪽 그림자 (아래)
    'G': hexc('55535e'),   # 아래쪽 금속
}
def button():
    H, W = 20, 30                    # 높이, 폭
    CAP = [4, 2, 1, 1]               # 위·아래 줄마다 안쪽으로 들어간 칸 수 (둥근 끝)
    inset = CAP + [0] * (H - 2 * len(CAP)) + CAP[::-1]
    inside = lambda x, y: 0 <= y < H and inset[y] <= x < W - inset[y]
    def dist(x, y):   # 바깥에서 몇 칸 안쪽인지 (체비셰프)
        for d in range(1, 12):
            if any(not inside(x + dx, y + dy) for dx in range(-d, d + 1) for dy in range(-d, d + 1)): return d
        return 12
    rows = []
    for y in range(H):
        r = ''
        for x in range(W):
            if not inside(x, y): r += '.'; continue
            d = dist(x, y); top = y < H / 2
            if d == 1: r += 'K'
            elif d == 2: r += 'L' if top else 'G'
            elif d == 3: r += 'M' if top else 'G'
            elif d == 4: r += 'S' if top else 'T'
            else: r += 'F' if y < H * 0.6 else 'f'
        rows.append(r)
    return paint(rows, BTN_PAL)


if __name__ == '__main__':
    button().save(os.path.join(OUT, 'frame_button.png'))
    print('wrote frame_button.png')
