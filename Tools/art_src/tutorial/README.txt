튜토리얼 카드 영상 만들기 (게임 카메라가 주인공을 따라가므로 화면 가운데를 잘라내면 주인공이 가운데 온다)
ffmpeg -i tut_eat_src.mp4  -vf "crop=600:600:980:400,scale=600:600:flags=lanczos" -an -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -movflags +faststart ../../../Assets/Resources/Tutorial/tut_eat.mp4
ffmpeg -i tut_spit_src.mp4 -vf "crop=760:760:880:320,scale=600:600:flags=lanczos" -an -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -movflags +faststart ../../../Assets/Resources/Tutorial/tut_spit.mp4
(crop=크기:크기:왼쪽:위 — 2560x1440 녹화 기준)
