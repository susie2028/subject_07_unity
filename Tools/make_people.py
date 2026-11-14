# NPC 픽셀 스프라이트 만들기: 청소부, 연구원(2종), 경비원, 고위 연구원, 군인, 연구소장
# 결과: Assets/Resources/Art/stand_<종류>_<front|back|side>.png  (36x46, 발이 맨 아래 줄)
#  - front: 앞모습, back: 뒷모습, side: 오른쪽을 보는 옆모습 (왼쪽은 게임에서 뒤집음)
#  - 게임에서는 캐릭터 그림 22픽셀 = 1칸 크기로 보인다 (CHAR_PX)
# 실행: python3 Tools/make_people.py   (미리보기: Tools/people_preview.png)
import os
from PIL import Image

W, H = 36, 46
GROUND = 45
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Resources', 'Art')


def hx(s):
    s = s.lstrip('#')
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


OL = hx('#16121c')  # 외곽선


class S:
    def __init__(self):
        self.px = [[None] * W for _ in range(H)]

    def put(self, x, y, c):
        if c is None:
            return
        x, y = int(round(x)), int(round(y))
        if 0 <= x < W and 0 <= y < H:
            self.px[y][x] = hx(c) if isinstance(c, str) else c

    def get(self, x, y):
        if 0 <= x < W and 0 <= y < H:
            return self.px[y][x]
        return None

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, c)

    def hline(self, x0, x1, y, c):
        self.rect(min(x0, x1), y, max(x0, x1), y, c)

    def vline(self, x, y0, y1, c):
        self.rect(x, min(y0, y1), x, max(y0, y1), c)

    def line(self, x0, y0, x1, y1, c, w=1):
        n = max(abs(x1 - x0), abs(y1 - y0), 1)
        for i in range(n + 1):
            x = x0 + (x1 - x0) * i / n
            y = y0 + (y1 - y0) * i / n
            for dx in range(w):
                for dy in range(w):
                    self.put(x + dx, y + dy, c)

    # 빛은 왼쪽 위에서: 오른쪽 열은 그림자, 왼쪽 열은 밝게
    def part(self, x0, y0, x1, y1, ramp, sh=1, hi=True):
        hi_c, base, dark = ramp
        self.rect(x0, y0, x1, y1, base)
        if x1 - x0 >= 2 and hi:
            self.vline(x0, y0, y1, hi_c)
        for k in range(sh):
            if x1 - k > x0 + 1:
                self.vline(x1 - k, y0, y1, dark)

    def ell(self, cx, cy, rx, ry, ramp, light=True):
        hi_c, base, dark = ramp if isinstance(ramp, tuple) else (ramp, ramp, ramp)
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                dx, dy = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
                d = dx * dx + dy * dy
                if d <= 1:
                    c = base
                    if light:
                        if dx + dy * 0.6 > 0.55 or (dy > 0.75):
                            c = dark
                        elif dx + dy * 0.8 < -0.75:
                            c = hi_c
                    self.put(x, y, c)

    def outline(self):
        add = []
        for y in range(H):
            for x in range(W):
                if self.px[y][x] is not None:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    p = self.get(x + dx, y + dy)
                    if p is not None and p != OL:
                        add.append((x, y))
                        break
        for x, y in add:
            self.px[y][x] = OL

    def flip(self):
        self.px = [row[::-1] for row in self.px]

    def image(self):
        im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        for y in range(H):
            for x in range(W):
                if self.px[y][x]:
                    im.putpixel((x, y), self.px[y][x])
        return im


def R(a, b, c):
    return (a, b, c)


SKIN = R('#eebd98', '#d69c78', '#a8705a')
SKIN_T = R('#f2c6a2', '#dca883', '#b07a5c')   # 경비원 (볼 붉은)
SKIN_D = R('#c98f6c', '#a87052', '#7c4e38')   # 연구원2
SKIN_S = R('#d8a47e', '#b47e5c', '#875a40')   # 군인
DARK = '#1c1820'

# ------------------------------------------------------------------ 청소부
OVER = R('#86a2bb', '#61809c', '#435a73')
WOOD = R('#c39a66', '#9a7448', '#6e5132')
MOP = R('#e6dab0', '#c4b685', '#958662')
METAL = R('#c3c9cf', '#949ba3', '#646b74')
SHOE_BR = R('#7a5638', '#5a3d27', '#3d281a')


def mop(s, x0, x1, top, hx_):
    # 위로 든 대걸레 머리: 가닥이 아래로 늘어지고 끝이 들쭉날쭉
    n = x1 - x0 + 1; mid = (x0 + x1) / 2
    for i in range(n):
        x = x0 + i
        c = MOP[0] if i % 3 == 0 else MOP[1] if i % 3 == 1 else MOP[2]
        t = top + (1 if i in (0, n - 1) else 0)
        ln = 9 + (i * 7 + 3) % 4
        for y in range(t, t + ln):
            off = 0
            if y > t + 6:      # 아래쪽은 바깥으로 살짝 벌어짐
                off = -1 if x < mid - 1 else (1 if x > mid + 1 else 0)
            s.put(x + off, y, c)
    s.rect(int(mid) - 2, top - 1, int(mid) + 2, top, MOP[2])   # 묶은 부분
    s.rect(int(mid) - 1, top + 9, int(mid) + 2, top + 10, METAL[2])


def bucket(s, x0, y0):
    s.part(x0, y0, x0 + 6, y0 + 7, METAL, 1)
    s.hline(x0 - 1, x0 + 7, y0, METAL[0]); s.rect(x0 + 1, y0 + 3, x0 + 5, y0 + 3, METAL[2])
    s.hline(x0 + 1, x0 + 5, y0 - 2, METAL[2]); s.put(x0, y0 - 1, METAL[2]); s.put(x0 + 6, y0 - 1, METAL[2])


def cleaner(s, view):
    side = view == 'side'
    back = view == 'back'
    if side:
        # 옆모습: 등이 굽은 키다리, 앞에 대걸레
        s.part(15, 43, 20, 45, SHOE_BR, 1)            # 앞 신발
        s.part(12, 43, 16, 45, SHOE_BR, 1)
        s.part(14, 28, 17, 42, OVER, 1)              # 뒷다리
        s.part(16, 28, 19, 42, OVER, 1)              # 앞다리
        s.part(13, 17, 19, 30, OVER, 1)              # 몸통 (멜빵바지)
        s.rect(13, 17, 14, 19, SKIN[1])              # 어깨 맨살
        s.put(18, 21, '#d9b456')                     # 단추
        s.line(15, 15, 18, 17, SKIN[1], 2)           # 앞으로 나온 목
        s.ell(19, 10, 4.2, 5, SKIN)                  # 머리
        s.put(23, 10, SKIN[1]); s.put(23, 11, SKIN[2])  # 코
        s.put(21, 9, DARK)                           # 눈
        s.put(20, 8, SKIN[2]); s.put(21, 8, SKIN[2])  # 처진 눈썹
        s.hline(20, 22, 13, SKIN[2])                 # 입
        s.put(17, 10, SKIN[2])                       # 귀
        s.rect(15, 15, 18, 16, '#2a2d33')            # 목에 건 헤드폰
        s.rect(14, 16, 15, 17, '#1b1d21')
        s.vline(28, 12, 42, WOOD[1]); s.vline(29, 12, 42, WOOD[2])   # 걸레 자루
        mop(s, 25, 32, 2, None)
        s.line(18, 19, 26, 27, SKIN[1], 2)           # 팔
        s.rect(26, 26, 29, 28, SKIN[0])              # 손
        bucket(s, 22, 38)
        return
    # 앞/뒤
    s.part(12, 43, 16, 45, SHOE_BR, 1); s.part(18, 43, 22, 45, SHOE_BR, 1)
    s.part(13, 28, 16, 42, OVER, 1); s.part(18, 28, 21, 42, OVER, 1)
    s.part(13, 26, 21, 31, OVER, 1)                  # 엉덩이
    s.vline(17, 31, 42, None)
    s.part(13, 19, 21, 27, OVER, 1)                  # 가슴판
    s.rect(12, 17, 22, 19, SKIN[1]); s.rect(12, 17, 12, 19, SKIN[0]); s.rect(22, 17, 22, 19, SKIN[2])  # 어깨
    s.part(14, 17, 15, 19, OVER, 0, False); s.part(19, 17, 20, 19, OVER, 0, False)  # 멜빵
    if not back:
        s.put(14, 19, '#d9b456'); s.put(20, 19, '#d9b456')
        s.rect(16, 21, 18, 23, OVER[2]); s.hline(16, 18, 21, OVER[0])  # 주머니
    else:
        s.line(14, 19, 19, 25, OVER[2]); s.line(20, 19, 15, 25, OVER[2])  # 뒤 X 멜빵
    s.part(10, 18, 11, 31, SKIN, 0)                  # 왼팔 (늘어뜨림)
    s.rect(9, 31, 11, 33, SKIN[1]); s.put(9, 33, SKIN[2])
    s.rect(16, 14, 18, 17, SKIN[1]); s.put(18, 15, SKIN[2])  # 긴 목
    s.ell(17, 9, 4.3, 5.2, SKIN)                     # 대머리
    s.put(12, 9, SKIN[1]); s.put(12, 10, SKIN[2]); s.put(22, 9, SKIN[2]); s.put(22, 10, SKIN[2])  # 귀
    if not back:
        s.put(15, 8, DARK); s.put(19, 8, DARK)       # 눈
        s.put(14, 7, SKIN[2]); s.put(15, 7, SKIN[2]); s.put(19, 7, SKIN[2]); s.put(20, 7, SKIN[2])
        s.put(17, 9, SKIN[2]); s.put(17, 10, SKIN[2])
        s.hline(16, 18, 12, '#7a4a3a'); s.put(15, 13, '#7a4a3a')   # 처진 입
        s.put(14, 11, SKIN[2]); s.put(20, 11, SKIN[2])
    else:
        s.put(15, 5, SKIN[0]); s.put(16, 5, SKIN[0])
        s.hline(15, 19, 12, SKIN[2])
    s.rect(14, 15, 20, 16, '#2a2d33')                # 목에 건 헤드폰
    s.rect(13, 16, 14, 18, '#1b1d21'); s.rect(20, 16, 21, 18, '#1b1d21')
    # 대걸레 (오른손)
    s.vline(26, 12, 42, WOOD[1]); s.vline(27, 12, 42, WOOD[2])
    mop(s, 22, 31, 2, None)
    s.line(22, 18, 24, 26, SKIN[1], 2)               # 오른팔
    s.rect(24, 26, 28, 28, SKIN[0]); s.put(28, 28, SKIN[2])
    bucket(s, 28, 38)


# ------------------------------------------------------------------ 연구원
COAT = R('#fbfcfd', '#d9e0e6', '#a7b2bc')
PANTS_BR = R('#6a5242', '#4e3a2e', '#33251d')
HAIR_BR = R('#9a6a40', '#704a2a', '#4a2e18')
HAIR_BK = R('#3a3440', '#24202a', '#141118')
HAIR_GR = R('#d8d8dc', '#aeaeb6', '#7c7c86')
GLASS = '#9fd2ee'
LENS = '#eaf7ff'


def researcher(s, view, hair=HAIR_BR, skin=SKIN, messy=True, trim=None, cup=True, papers=True):
    side = view == 'side'
    back = view == 'back'
    tr = trim or COAT[2]
    if side:
        s.part(16, 44, 20, 45, R('#3a3434', '#2a2626', '#1a1818'), 0)
        s.part(13, 44, 16, 45, R('#3a3434', '#2a2626', '#1a1818'), 0)
        s.part(15, 34, 18, 43, PANTS_BR, 1)
        for y in range(26, 43):                      # 뒤로 퍼지는 가운
            w = (y - 26) // 4
            s.rect(13 - w, y, 20, y, COAT[1])
            s.put(13 - w, y, COAT[0]); s.put(20, y, COAT[2])
        s.vline(20, 30, 42, tr)
        s.rect(15, 36, 18, 42, None); s.part(15, 36, 18, 43, PANTS_BR, 1)
        for y in range(26, 36):                      # 가운 다시 (앞)
            w = (y - 26) // 4
            s.rect(13 - w, y, 20, y, COAT[1]); s.put(13 - w, y, COAT[0])
        s.rect(16, 25, 18, 26, skin[1])
        s.ell(17.5, 21, 4.6, 4.6, skin)
        s.put(22, 21, skin[1])                       # 코
        s.rect(13, 16, 20, 19, hair[1]); s.rect(13, 19, 15, 23, hair[1]); s.rect(13, 16, 14, 23, hair[2])
        if messy:
            for x, y in ((14, 15), (17, 14), (20, 15), (12, 17), (12, 20), (21, 16)):
                s.put(x, y, hair[0] if x < 17 else hair[1])
        s.hline(18, 22, 21, GLASS); s.put(21, 22, LENS); s.put(20, 22, GLASS)
        s.put(19, 24, skin[2])
        if papers:
            s.rect(19, 28, 23, 33, '#f6f6f6')
            for y in (29, 31, 33):
                s.hline(19, 23, y, '#b8c0c8')
            s.rect(20, 32, 22, 33, skin[0])
        s.line(17, 27, 20, 31, COAT[2])
        return
    # 앞/뒤
    shoe = R('#3a3434', '#2a2626', '#1a1818')
    s.part(13, 44, 16, 45, shoe, 0); s.part(18, 44, 21, 45, shoe, 0)
    s.part(14, 35, 16, 43, PANTS_BR, 1); s.part(18, 35, 20, 43, PANTS_BR, 1)
    for y in range(26, 43):                          # 가운 (아래로 퍼짐)
        w = (y - 26) // 4
        x0, x1 = 12 - w, 22 + w
        s.rect(x0, y, x1, y, COAT[1]); s.put(x0, y, COAT[0]); s.put(x1, y, COAT[2]); s.put(x1 - 1, y, COAT[2])
        if not back and y >= 34:                     # 앞이 열려 바지가 보임
            g = (y - 34) // 3
            s.rect(17 - g, y, 17 + g, y, None if y < 35 else None)
    if not back:
        for y in range(34, 43):
            g = (y - 33) // 3
            for x in range(17 - g, 18 + g):
                s.put(x, y, PANTS_BR[1] if x != 17 + g else PANTS_BR[2])
        s.line(15, 26, 17, 31, tr); s.line(19, 26, 17, 31, tr)   # 옷깃
        s.vline(17, 31, 33, tr)
    else:
        s.vline(17, 34, 42, COAT[2])                  # 뒤 트임
        s.hline(13, 21, 30, COAT[2])                  # 허리 벨트
    if trim and not back:
        s.vline(12, 30, 38, trim)
    # 팔
    s.part(10, 27, 12, 34, COAT, 1)
    s.part(22, 27, 24, 33, COAT, 1)
    s.rect(16, 25, 18, 26, skin[1])
    s.ell(17, 21, 4.6, 4.8, skin)
    s.put(12, 21, skin[1]); s.put(22, 21, skin[2])
    if not back:
        s.rect(12, 15, 22, 18, hair[1]); s.rect(12, 18, 13, 21, hair[1]); s.rect(21, 18, 22, 21, hair[2])
        s.hline(13, 21, 18, hair[1]); s.put(15, 19, hair[1]); s.put(19, 19, hair[2])
        s.hline(13, 16, 21, GLASS); s.hline(18, 21, 21, GLASS); s.hline(13, 16, 22, GLASS); s.hline(18, 21, 22, GLASS)
        s.put(14, 21, LENS); s.put(15, 21, LENS); s.put(19, 21, LENS); s.put(20, 21, LENS)
        s.put(14, 22, DARK); s.put(19, 22, DARK); s.put(17, 21, GLASS)
        s.hline(16, 17, 24, skin[2])
    else:
        s.rect(12, 15, 22, 23, hair[1]); s.rect(20, 16, 22, 23, hair[2]); s.rect(12, 16, 13, 22, hair[0])
        s.hline(14, 20, 24, skin[2])
    if messy:
        for x, y, c in ((12, 14, 0), (15, 13, 0), (16, 14, 1), (19, 13, 1), (22, 14, 2), (23, 16, 2), (11, 17, 0), (23, 19, 2), (11, 20, 1)):
            s.put(x, y, hair[c])
    else:
        s.hline(13, 21, 14, hair[1])
    if back:
        s.rect(9, 33, 11, 34, skin[1]); s.rect(23, 33, 25, 34, skin[1])
        return
    if papers:
        s.rect(12, 28, 19, 33, '#f8f8f8'); s.vline(19, 28, 33, '#d0d6dc')
        for y in (29, 31, 33):
            s.hline(12, 18, y, '#b4bcc6')
        s.rect(11, 30, 12, 32, skin[0]); s.rect(19, 32, 20, 33, skin[1])
    else:
        s.rect(10, 34, 12, 35, skin[1])
    if cup:
        s.part(23, 29, 25, 32, R('#8a5c3a', '#6a4428', '#4a2e1a'), 1)
        s.put(26, 30, '#6a4428'); s.put(23, 29, '#e9e2d6'); s.put(24, 29, '#e9e2d6')
        s.rect(23, 33, 25, 33, skin[1])
        for x, y in ((24, 27), (25, 26), (24, 25)):
            s.put(x, y, (220, 220, 230, 150))
    else:
        s.rect(22, 34, 24, 35, skin[1])


# ------------------------------------------------------------------ 경비원
NAVY = R('#45578f', '#30406f', '#1f2a4c')
CAP = R('#33416f', '#222d52', '#161d38')
GOLD = R('#f3cf62', '#cfa23a', '#93701f')


def guard(s, view):
    side = view == 'side'
    back = view == 'back'
    sk = SKIN_T
    shoe = R('#2a2a30', '#18181c', '#0c0c10')
    if side:
        s.part(14, 44, 21, 45, shoe, 0)
        s.part(14, 37, 19, 43, NAVY, 1)
        s.ell(18, 30, 10.5, 9, NAVY)                 # 앞으로 나온 배
        s.hline(9, 25, 37, '#151824'); s.put(25, 37, METAL[1])
        s.part(11, 22, 19, 27, NAVY, 1)
        s.put(13, 24, GOLD[1]); s.put(14, 24, GOLD[1])
        s.ell(17, 18, 5.2, 5, sk)                    # 머리
        s.put(22, 18, sk[1]); s.put(22, 19, sk[2])
        s.put(20, 17, DARK); s.hline(20, 21, 20, sk[2])
        s.ell(17, 21.5, 4, 1.6, sk)                  # 두 턱
        s.rect(12, 11, 20, 14, CAP[1]); s.rect(12, 11, 13, 14, CAP[0])
        s.rect(18, 15, 24, 15, '#121828'); s.rect(13, 15, 19, 15, CAP[2])
        s.rect(19, 12, 20, 13, GOLD[1])
        s.line(18, 25, 23, 31, NAVY[1], 2)           # 손전등 팔
        s.rect(23, 31, 29, 32, '#3a3d44'); s.rect(29, 31, 30, 32, '#fff2a0'); s.rect(22, 31, 24, 33, sk[1])
        s.line(15, 25, 25, 23, NAVY[1], 2)           # 도넛 팔
        s.rect(25, 22, 27, 24, sk[1])
        s.ell(28.5, 21, 2.6, 2.4, R('#f8a2bc', '#ec7c9e', '#c5577a')); s.put(28, 21, '#6a3a2a')
        return
    s.part(10, 44, 16, 45, shoe, 0); s.part(19, 44, 25, 45, shoe, 0)
    s.part(11, 38, 16, 43, NAVY, 1); s.part(19, 38, 24, 43, NAVY, 1)
    s.part(10, 23, 25, 27, NAVY, 2)                  # 어깨
    s.ell(17.5, 31, 12.2, 9.4, NAVY)                 # 배
    s.hline(9, 26, 38, '#151824')
    if not back:
        s.rect(17, 38, 18, 38, METAL[1])
        s.vline(17, 24, 31, NAVY[2])                 # 단추 줄
        for y in (25, 28, 31):
            s.put(18, y, GOLD[1])
        for x in range(10, 26):                      # 배 둘레 선
            y = 33 + int(((x - 17.5) / 9) ** 2 * 3)
            s.put(x, y, NAVY[2])
        s.put(12, 26, GOLD[0]); s.put(13, 26, GOLD[1]); s.put(22, 26, GOLD[1]); s.put(23, 26, GOLD[2])   # 가슴 배지
    else:
        s.vline(17, 24, 36, NAVY[2])
    s.rect(9, 23, 10, 24, GOLD[1]); s.rect(25, 23, 26, 24, GOLD[2])   # 견장
    s.part(4, 25, 6, 33, NAVY, 1)                    # 왼팔
    s.part(29, 25, 31, 29, NAVY, 1)                  # 오른팔
    s.ell(17.5, 17.5, 6.2, 5.2, sk)                  # 둥근 얼굴
    s.put(11, 17, sk[1]); s.put(24, 17, sk[2])
    if not back:
        s.put(15, 17, DARK); s.put(20, 17, DARK)
        s.hline(14, 15, 16, sk[2]); s.hline(20, 21, 16, sk[2])
        s.rect(17, 19, 18, 20, '#8a4a44'); s.put(17, 19, '#5a2a28')
        s.put(13, 19, '#f0a090'); s.put(22, 19, '#f0a090')
        s.hline(14, 21, 22, sk[2])
    else:
        s.hline(12, 23, 16, '#3a2a24')
    s.rect(11, 10, 24, 14, CAP[1]); s.hline(11, 24, 10, CAP[0]); s.vline(24, 11, 14, CAP[2])
    s.hline(10, 25, 15, '#121828')
    if not back:
        s.rect(17, 11, 18, 13, GOLD[1]); s.put(17, 11, GOLD[0])
        # 손전등 (왼손, 배 앞으로)
        s.rect(5, 33, 8, 36, sk[1])
        s.rect(8, 34, 13, 36, '#3a3d44'); s.hline(8, 13, 34, '#5a5e66')
        s.rect(13, 33, 14, 37, '#4a4e56'); s.rect(14, 34, 14, 36, '#fff2a0')
        # 도넛 (오른손, 들어 올림)
        s.rect(29, 23, 31, 25, NAVY[1])
        s.rect(29, 21, 31, 23, sk[1])
        s.ell(31, 19, 3, 2.6, R('#f8a2bc', '#ec7c9e', '#c5577a'))
        s.put(31, 19, '#6a3a2a'); s.put(30, 19, '#6a3a2a')
        s.put(29, 18, '#ffffff'); s.put(32, 20, '#7fd0ff'); s.put(31, 17, '#ffe07a')
    else:
        s.rect(4, 33, 6, 35, sk[1]); s.rect(29, 29, 31, 30, sk[1])


# ------------------------------------------------------------------ 군인
ARMY = R('#7c9a5c', '#5a7842', '#3e562d')
ARMY_D = R('#4e6a3a', '#3a5230', '#283a20')
BOOT = R('#4a5434', '#323a22', '#1e2414')
EYE_G = '#8dff8a'


def soldier(s, view):
    side = view == 'side'
    back = view == 'back'
    sk = SKIN_S
    if side:
        s.part(14, 42, 22, 45, BOOT, 1)
        s.part(14, 34, 20, 42, ARMY, 1)
        s.rect(15, 38, 20, 39, ARMY_D[1])
        s.part(12, 32, 21, 34, ARMY_D, 1); s.rect(12, 33, 14, 36, ARMY_D[2])
        s.part(11, 15, 22, 32, ARMY, 2)              # 두꺼운 몸
        s.ell(16, 19, 6.5, 5.5, ARMY)                # 어깨
        s.ell(21, 24, 5.5, 3.6, ARMY)                # 팔짱 (앞)
        s.ell(24, 22, 2.4, 2.4, ARMY_D)
        s.rect(16, 12, 21, 15, sk[1]); s.put(22, 13, sk[1]); s.hline(19, 21, 14, sk[2])
        s.ell(17, 7.5, 5.5, 5, ARMY)                 # 헬멧
        s.hline(12, 22, 11, ARMY_D[2])
        s.rect(19, 9, 23, 11, '#1c2418'); s.rect(21, 9, 23, 10, EYE_G); s.put(23, 9, '#e6ffe0')
        s.rect(15, 2, 16, 4, '#2a3a20')
        return
    s.part(10, 42, 16, 45, BOOT, 1); s.part(19, 42, 25, 45, BOOT, 1)
    s.part(11, 34, 16, 42, ARMY, 1); s.part(19, 34, 24, 42, ARMY, 1)
    s.rect(11, 38, 16, 39, ARMY_D[1]); s.rect(19, 38, 24, 39, ARMY_D[1]); s.hline(12, 15, 38, ARMY_D[0]); s.hline(20, 23, 38, ARMY_D[0])
    s.part(11, 32, 24, 34, ARMY_D, 1)                # 벨트
    s.rect(9, 33, 11, 37, ARMY_D[2]); s.rect(24, 33, 26, 37, ARMY_D[2])   # 주머니·권총집
    s.part(11, 15, 24, 32, ARMY, 2)                  # 몸통
    s.ell(8.5, 20, 6, 5.5, ARMY); s.ell(26.5, 20, 6, 5.5, ARMY)   # 거대한 어깨
    if not back:
        for y in (28, 30):
            s.hline(13, 22, y, ARMY[2])
        s.vline(17, 26, 31, ARMY[2])
        # 팔짱
        s.ell(17.5, 22.5, 9.5, 2.3, ARMY); s.ell(17.5, 25.5, 9.5, 2.3, ARMY)
        s.hline(9, 26, 24, ARMY[2])
        s.ell(7, 24, 2.4, 3, ARMY_D); s.ell(28, 24, 2.4, 3, ARMY_D)   # 팔꿈치
        s.rect(24, 21, 26, 23, ARMY_D[1]); s.rect(9, 25, 11, 27, ARMY_D[1])  # 장갑 낀 주먹
    else:
        s.part(4, 22, 7, 33, ARMY, 1); s.part(28, 22, 31, 33, ARMY, 1)   # 뒤에서 보면 팔이 양옆
        s.vline(17, 16, 31, ARMY[2]); s.hline(12, 23, 22, ARMY[2])
        s.rect(13, 17, 22, 21, ARMY_D[1])            # 등 배낭 판
    s.rect(14, 12, 21, 15, sk[1]); s.vline(21, 12, 15, sk[2])
    if not back:
        s.hline(15, 20, 14, sk[2]); s.put(17, 13, sk[2])
        s.vline(13, 10, 14, '#2a3a20'); s.vline(22, 10, 14, '#2a3a20')   # 턱끈
    s.ell(17.5, 7.5, 6.5, 5.2, ARMY)                 # 헬멧
    s.hline(11, 24, 11, ARMY_D[2])
    s.rect(17, 2, 18, 5, '#2a3a20')                  # 고글 거치대
    if not back:
        s.rect(12, 9, 23, 11, '#1c2418')
        s.rect(13, 9, 16, 10, EYE_G); s.rect(19, 9, 22, 10, EYE_G); s.put(13, 9, '#e6ffe0'); s.put(19, 9, '#e6ffe0')
        s.rect(13, 11, 16, 11, '#3ccc5a'); s.rect(19, 11, 22, 11, '#3ccc5a')
    else:
        s.hline(11, 24, 10, '#1c2418')


# ------------------------------------------------------------------ 연구소장
SUIT = R('#44444f', '#2a2a33', '#17171c')


def director(s, view):
    side = view == 'side'
    back = view == 'back'
    sk = SKIN
    shoe = R('#3a3a40', '#121214', '#060608')
    if side:
        s.part(14, 44, 21, 45, shoe, 0)
        s.part(14, 37, 19, 43, SUIT, 1)
        s.ell(16.5, 29, 6.8, 8.6, SUIT)              # 배
        s.vline(22, 23, 36, GOLD[1])                 # 앞 금색 선
        s.rect(15, 18, 19, 20, '#f2f2ee'); s.put(19, 19, '#2a1a1a')
        s.ell(17, 13, 4.6, 5, sk)
        s.put(22, 13, sk[1]); s.put(22, 14, sk[2])
        s.put(20, 12, DARK); s.rect(19, 15, 22, 15, '#1a1414'); s.put(23, 14, '#1a1414')
        s.rect(13, 11, 14, 15, '#7a5c44')
        s.line(16, 22, 19, 33, SUIT[1], 2)           # 팔
        s.hline(18, 21, 33, GOLD[1])
        s.rect(19, 34, 21, 35, sk[1])
        s.part(17, 36, 25, 43, R('#3e3e4c', '#282834', '#16161e'), 1)   # 서류가방
        s.rect(19, 35, 22, 35, '#3e3e4c'); s.put(21, 38, GOLD[1])
        return
    s.part(11, 44, 16, 45, shoe, 0); s.part(19, 44, 24, 45, shoe, 0)
    s.put(12, 44, '#5a5a66'); s.put(20, 44, '#5a5a66')
    s.part(12, 37, 16, 43, SUIT, 1); s.part(19, 37, 23, 43, SUIT, 1)
    s.part(10, 19, 25, 37, SUIT, 2)
    s.ell(17.5, 30, 8.6, 7.5, SUIT)                  # 배
    s.hline(10, 25, 37, GOLD[2])
    s.part(7, 20, 9, 34, SUIT, 1); s.part(26, 20, 28, 33, SUIT, 1)   # 팔
    s.hline(7, 9, 34, GOLD[1]); s.hline(26, 28, 33, GOLD[1])
    if not back:
        s.rect(15, 19, 20, 23, '#f2f2ee'); s.vline(17, 20, 25, '#1a1414'); s.vline(18, 20, 25, '#2a2020'); s.put(17, 22, GOLD[0])
        s.line(15, 19, 13, 31, GOLD[1]); s.line(20, 19, 22, 31, GOLD[1])   # 금색 옷깃
        for y in (28, 31):
            s.put(15, y, GOLD[1]); s.put(20, y, GOLD[1])
        s.hline(21, 23, 26, GOLD[2])                 # 가슴 주머니
        s.rect(7, 35, 9, 36, sk[1])
        s.vline(8, 37, 39, GOLD[1]); s.put(7, 39, GOLD[1])   # 열쇠
        s.rect(26, 34, 28, 35, sk[1])
        s.part(25, 36, 33, 43, R('#3e3e4c', '#282834', '#16161e'), 1)
        s.rect(27, 35, 30, 35, '#3e3e4c'); s.put(29, 38, GOLD[1]); s.put(30, 38, GOLD[2])
    else:
        s.vline(17, 30, 37, SUIT[2]); s.hline(12, 23, 21, SUIT[0])
        s.rect(7, 35, 9, 36, sk[1]); s.rect(26, 34, 28, 35, sk[1])
    s.rect(16, 17, 19, 18, sk[1])
    s.ell(17.5, 12.5, 5, 5.2, sk)
    s.put(12, 12, sk[1]); s.put(23, 12, sk[2])
    s.rect(12, 11, 13, 15, '#7a5c44'); s.rect(22, 11, 23, 15, '#5e4430')   # 옆머리
    if not back:
        s.put(15, 12, DARK); s.put(20, 12, DARK)
        s.hline(14, 15, 11, sk[2]); s.hline(20, 21, 11, sk[2])
        s.put(17, 13, sk[2]); s.put(18, 14, sk[2])
        s.rect(15, 15, 20, 15, '#1a1414'); s.put(14, 16, '#1a1414'); s.put(21, 16, '#1a1414')   # 콧수염
        s.put(15, 9, sk[0]); s.put(16, 8, sk[0])
    else:
        s.hline(13, 22, 15, '#6a4e38'); s.hline(14, 21, 16, '#5e4430')


# ------------------------------------------------------------------ 고위 연구원 / 연구원2 (연구원 변형)
def senior(s, view):
    researcher(s, view, hair=HAIR_GR, messy=False, trim='#b48cf0', cup=False, papers=True)


def researcher2(s, view):
    researcher(s, view, hair=HAIR_BK, skin=SKIN_D, messy=False, cup=True, papers=False)


CHARS = {
    'cleaner': cleaner, 'researcher': researcher, 'researcher2': researcher2, 'guard': guard,
    'senior': senior, 'soldier': soldier, 'director': director,
}


def make(name, view):
    s = S()
    fn = CHARS[name]
    fn(s, view)
    s.outline()
    if view == 'back':
        s.flip()   # 뒤에서 보면 들고 있는 물건이 반대쪽
    return s.image()


if __name__ == '__main__':
    views = ('front', 'side', 'back')
    sheet = Image.new('RGBA', (W * len(CHARS) * 3 + 20, H * 3 + 10), (40, 34, 60, 255))
    for ci, name in enumerate(CHARS):
        for vi, v in enumerate(views):
            im = make(name, v)
            im.save(os.path.join(OUT, 'stand_%s_%s.png' % (name, v)))
            sheet.paste(im.resize((W * 3 // 3, H * 3 // 3)), (ci * W * 3 + vi * W, 0), im)
    # 크게 보기
    big = Image.new('RGBA', (W * 3 * len(CHARS), H), (60, 50, 90, 255))
    for ci, name in enumerate(CHARS):
        for vi, v in enumerate(views):
            im = make(name, v)
            big.paste(im, (ci * W * 3 + vi * W, 0), im)
    big = big.resize((big.width * 5, big.height * 5), Image.NEAREST)
    big.save(os.path.join(os.path.dirname(__file__), 'people_preview.png'))
    print('ok', len(CHARS) * 3, 'sprites')
