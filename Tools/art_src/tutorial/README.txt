튜토리얼 카드 움짤 만들기 (게임 카메라가 주인공을 따라가므로 화면 가운데를 잘라내면 주인공이 가운데 온다)
카드는 Assets/Resources/Art/UI/tut_<이름>_0.png, _1.png … 를 초당 12장으로 반복해서 보여 준다 (HudTutorial.cs FRAME_FPS).
1) 잘라서 정사각형 영상으로 (crop=크기:크기:왼쪽:위 — 2560x1440 녹화 기준)
ffmpeg -i tut_eat_src.mp4  -vf "crop=600:600:980:400"  -an tut_eat.mp4
ffmpeg -i tut_spit_src.mp4 -vf "crop=760:760:880:320" -an tut_spit.mp4
2) 그림으로 뽑기 (예전 그림은 먼저 지우기)
ffmpeg -i tut_eat.mp4 -vf "fps=12,scale=380:380:flags=lanczos" -start_number 0 ../../../Assets/Resources/Art/UI/tut_eat_%d.png
3) 시야 카드
ffmpeg -i tut_sight_src.mp4 -vf "crop=1000:1000:'1020+min(t,0.8)*120':150,fps=12,scale=380:380:flags=lanczos" -start_number 0 ../../../Assets/Resources/Art/UI/tut_sight_%d.png
(영상 파일을 Assets/StreamingAssets/Tutorial/<이름>.mp4 에 두면 그림이 없을 때 영상 재생기로도 시도한다)
