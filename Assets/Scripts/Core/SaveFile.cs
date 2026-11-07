// 이어하기용 저장. 층에 도착할 때마다(체크포인트) 자동 저장된다.
// 저장 내용: 층, 들어온 방법, 카드, 꺼진 함정, 방문한 층, 현재 모습, 기록.
// (먹은 사람·물건 상태는 저장하지 않아서, 게임을 껐다 켜면 그 층 사람들은 다시 돌아와 있다)
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Specimen07
{
    [Serializable]
    public class SaveFile
    {
        const string Key = "specimen07.save";

        public int version = 1;
        public string floorId = "B1", via = "start", form = "body";
        public float timer, elapsed;
        public int alarms, deaths;
        public List<string> cards = new List<string>(), trapsOff = new List<string>(), visited = new List<string>(), seenForm = new List<string>();

        public static bool Exists() { return PlayerPrefs.HasKey(Key); }
        public static void Delete() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }

        public static void Write(GameState s, string via, int deaths)
        {
            var f = new SaveFile
            {
                floorId = s.floorId, via = via, form = s.player.form, timer = s.player.timer, elapsed = s.elapsed, alarms = s.alarms, deaths = deaths,
                cards = new List<string>(s.cards), trapsOff = new List<string>(s.trapsOff), visited = new List<string>(s.visited), seenForm = new List<string>(s.seenForm),
            };
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(f));
            PlayerPrefs.Save();
        }

        public static SaveFile Read()
        {
            if (!Exists()) return null;
            try { return JsonUtility.FromJson<SaveFile>(PlayerPrefs.GetString(Key)); }
            catch (Exception) { return null; }
        }
    }
}
