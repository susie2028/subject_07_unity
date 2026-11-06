# 표본 07 (SPECIMEN 07) — Unity

먹은 것의 모습으로 변신해 탈출하는 탑다운 잠입 퍼즐.
웹 프로토타입(`Prototype/index.html`)을 Unity로 그대로 옮긴 프로젝트입니다. 규칙, 수치, 6개 층 맵, NPC 순찰 경로가 모두 프로토타입과 같습니다.

---

## 처음 여는 법

1. **GitHub Desktop**에서 이 저장소를 Clone 합니다.
2. **Unity Hub**에서 `Add` → `Add project from disk` → 받은 폴더를 선택합니다.
   - Unity 6 (6000.x)이면 됩니다. "버전이 설치되어 있지 않다"는 창이 뜨면 설치된 Unity 6 버전을 골라 여세요.
3. 처음 열면 `Assets/Scenes/Main.unity`가 자동으로 만들어지고 열립니다.
4. **▶ Play**를 누르면 타이틀 화면부터 시작합니다.

> 씬이 안 보이면 상단 메뉴 **표본07 → 메인 씬 만들기**를 누르세요.
> 사실 어떤 씬에서 Play를 눌러도 게임은 자동으로 시작됩니다.

### ⚠️ 두 명이 같이 쓸 때 꼭 지킬 것

- **처음 한 번은 한 사람만** 프로젝트를 열고, 생긴 파일(`.meta` 파일들, `ProjectSettings/`, `Packages/packages-lock.json`, `Assets/Scenes/`)을 전부 Commit → Push 하세요. 다른 사람은 그다음에 Pull 해서 엽니다.
  두 사람이 각자 처음 열면 같은 파일에 서로 다른 ID(.meta)가 생겨서 충돌합니다.
- **같은 씬(.unity)이나 프리팹을 동시에 고치지 마세요.** 합칠 때 깨집니다. 누가 만지는지 미리 말로 정하세요.
- 작업 전에 **Pull**, 끝나면 **Commit → Push**.
- `.meta` 파일은 항상 같이 올려야 합니다. (GitHub Desktop은 자동으로 같이 잡힙니다)

---

## 조작

| 키 | 동작 |
|---|---|
| WASD / 방향키 | 이동 |
| E / Space | 먹기 · 문 열기 · 콘솔 조작 · 엘리베이터 |
| M | 미니맵 켜기/끄기 |
| R | 층 도착 시점부터 다시 |
| 1~5, Esc | 엘리베이터 층 선택, 닫기 |

개발용: Play 중에 메뉴 **표본07 → 개발: 3F/4F/5F에서 바로 시작** (필요한 카드가 지급됨)

---

## 🎨 아트 바꾸기 (스프라이트 교체)

**`Assets/Resources/Art/` 폴더에 정해진 이름의 PNG를 넣으면 끝입니다.** 코드를 고칠 필요가 없습니다.

- 이름 목록: [`Assets/Resources/Art/_스프라이트_이름표.txt`](Assets/Resources/Art/_스프라이트_이름표.txt)
- 1칸 = **32×32 픽셀**, 위에서 내려다본 시점
- PNG를 넣으면 자동으로 픽셀아트 설정(Sprite, Point 필터, 압축 없음, PPU 32)이 적용됩니다.
- 그림이 없는 것은 코드가 프로토타입과 비슷한 임시 그림을 그려서 씁니다. → **하나씩 바꿔 나가도 됩니다.**

예시:
```
Assets/Resources/Art/player_blob.png      ← 주인공 본모습
Assets/Resources/Art/char_guard.png       ← 경비원 (오른쪽을 보는 그림 한 장 → 자동 회전)
Assets/Resources/Art/tile_wall.png        ← 벽
```

캐릭터를 4방향으로 그리고 싶으면 `char_guard_right/_down/_left/_up.png` 네 장을 넣으면 회전 대신 방향 그림을 씁니다.

소리도 같은 방식입니다: `Assets/Resources/Sounds/` ([이름표](Assets/Resources/Sounds/_소리_이름표.txt))
글꼴: `Assets/Resources/Fonts/Main.ttf`

---

## 폴더 구조

```
Assets/
  Scripts/
    Data/GameData.cs     ← 수치(시야, 속도, 변신 시간, 경보 시간…)와 대사. 밸런스 조정은 여기서
    Data/FloorDefs.cs    ← 6개 층 맵 데이터 (프로토타입에서 자동 변환)
    Core/Game.cs         ← 게임 규칙 전부 (발각, 먹기, 변신, 카드, 문, 경보, 추격, 함정, 헬기)
    Core/FloorGeo.cs     ← 맵 데이터 → 칸 격자
    Core/State.cs        ← 진행 상태 / 체크포인트
    View/Art.cs          ← 스프라이트 불러오기 + 임시 그림
    View/WorldView.cs    ← 게임 화면 그리기 (타일, 캐릭터, 시야 부채꼴, 어둠)
    View/Hud.cs          ← UI 전부 (HUD, 미니맵, 말풍선, 메뉴 화면)
    Audio/Sfx.cs         ← 효과음
  Editor/                ← 에디터 도우미 (PNG 자동 설정, 씬 자동 생성)
  Resources/Art|Sounds|Fonts  ← 여기에 아트/소리/글꼴을 넣으면 교체됨
Prototype/index.html     ← 원본 웹 프로토타입 (브라우저로 열면 바로 플레이)
Tools/                   ← 프로토타입 맵 → FloorDefs.cs 변환 스크립트
```

### 맵을 고치고 싶다면
- 간단한 수정: `FloorDefs.cs`의 숫자를 바로 고치면 됩니다. (rooms = 방 사각형, fill = 가구, doors = 문, npcs.route = 순찰 경로 `[x, y, 대기초, 바라보는 각도]`)
- 프로토타입 HTML을 고친 뒤 다시 변환하려면:
  ```
  node Tools/extract.js Prototype/index.html   # floors.json 생성
  python3 Tools/gen.py Assets/Scripts/Data/FloorDefs.cs
  ```

---

## 프로토타입과 다른 점
- 터치 조작(모바일 스틱)은 뺐습니다 (기획 플랫폼이 PC).
- 조명 깜빡임·색조는 비슷하게만 재현했습니다.
- 그 외 규칙·수치·맵·대사는 동일합니다.
