// 효과음. 프로토타입처럼 코드로 소리를 합성한다.
// Assets/Resources/Sounds/이름.wav(또는 .ogg/.mp3)가 있으면 그 파일을 대신 쓴다.
// 이름: glass gulp morph melt sting huh door deny card lift siren calm beep shot grab zap heli win blip step heart tick, 배경 웅웅거림: hum
using System.Collections.Generic;
using UnityEngine;

namespace Specimen07
{
    public class Sfx : MonoBehaviour
    {
        const int SR = 22050;
        const float MASTER = 0.55f;
        public bool muted;
        AudioSource oneShot, hum, spatial;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        bool inited;

        void Awake()
        {
            oneShot = gameObject.AddComponent<AudioSource>(); oneShot.playOnAwake = false;
            hum = gameObject.AddComponent<AudioSource>(); hum.playOnAwake = false; hum.loop = true;
            spatial = gameObject.AddComponent<AudioSource>(); spatial.playOnAwake = false;
        }

        public void Init()
        {
            if (inited) return;
            inited = true;
            hum.clip = Get("hum");
            hum.volume = muted ? 0 : 1;
            hum.Play();
        }

        public void SetMuted(bool m) { muted = m; oneShot.volume = m ? 0 : 1; hum.volume = m ? 0 : 1; }

        public void Play(string name)
        {
            if (muted) return;
            var c = Get(name);
            if (c != null) oneShot.PlayOneShot(c);
        }

        // 좌우 위치가 있는 소리 (pan: -1 왼쪽 ~ 1 오른쪽)
        public void PlayAt(string name, float volume, float pan)
        {
            if (muted || volume <= 0.01f) return;
            var c = Get(name);
            if (c == null) return;
            spatial.panStereo = pan;
            spatial.PlayOneShot(c, volume);
        }

        public void SetHum(float level, float detuneCents)
        {
            if (!inited) return;
            hum.volume = muted ? 0 : level;
            hum.pitch = Mathf.Pow(2, detuneCents / 1200f);
        }

        AudioClip Get(string name)
        {
            AudioClip c;
            if (clips.TryGetValue(name, out c)) return c;
            c = Resources.Load<AudioClip>("Sounds/" + name);
            if (c == null) c = Synth(name);
            clips[name] = c;
            return c;
        }

        /* ================= 합성 ================= */
        float[] buf;
        void Tone(float freq, float dur, string type = "sine", float gain = 0.2f, float to = 0, float delay = 0)
        {
            int s0 = (int)(delay * SR), n = (int)(dur * SR);
            float phase = 0;
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float t = i / (float)SR;
                float f = to > 0 ? freq * Mathf.Pow(to / freq, t / dur) : freq;
                phase += f / SR; phase -= Mathf.Floor(phase);
                float w;
                switch (type)
                {
                    case "square": w = phase < 0.5f ? 1 : -1; break;
                    case "sawtooth": w = phase * 2 - 1; break;
                    case "triangle": w = 1 - 4 * Mathf.Abs(phase - 0.5f); break;
                    default: w = Mathf.Sin(phase * Mathf.PI * 2); break;
                }
                float env = t < 0.01f ? Mathf.Lerp(0.0001f, gain, t / 0.01f) : gain * Mathf.Pow(0.0001f / gain, (t - 0.01f) / Mathf.Max(0.001f, dur - 0.01f));
                buf[s0 + i] += w * env;
            }
        }
        // 필터 (state variable): lowpass / bandpass / highpass
        void Noise(float dur, float gain = 0.3f, float freq = 3000, float q = 0.8f, string type = "bandpass", float delay = 0)
        {
            int s0 = (int)(delay * SR), n = (int)(dur * SR);
            float low = 0, band = 0, f = 2 * Mathf.Sin(Mathf.PI * Mathf.Min(freq, SR / 4f) / SR), damp = 1 / Mathf.Max(0.5f, q);
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float x = (Random.value * 2 - 1) * Mathf.Pow(1 - i / (float)n, 2);
                low += f * band; float high = x - low - damp * band; band += f * high;
                float y = type == "lowpass" ? low : type == "highpass" ? high : band;
                buf[s0 + i] += y * gain;
            }
        }

        AudioClip Synth(string name)
        {
            float len = 1.3f;
            if (name == "hum") len = 1f;
            buf = new float[(int)(len * SR)];
            switch (name)
            {
                case "glass": Noise(0.7f, 0.9f, 4200, 0.6f, "highpass"); { float[] fs = { 2900, 3700, 4600, 5300 }; for (int i = 0; i < 4; i++) Tone(fs[i], 0.25f, "sine", 0.06f, 0, 0.03f * i + Random.value * 0.05f); } Tone(90, 0.4f, "triangle", 0.25f, 50); break;
                case "gulp": Tone(260, 0.18f, "sine", 0.25f, 90); Tone(180, 0.22f, "sine", 0.22f, 70, 0.32f); Noise(0.25f, 0.12f, 500, 0.8f, "bandpass", 0.1f); break;
                case "morph": Tone(140, 0.35f, "triangle", 0.2f, 420); Noise(0.3f, 0.15f, 900, 2); break;
                case "melt": Tone(400, 0.6f, "triangle", 0.18f, 80); Noise(0.5f, 0.1f, 700, 3); break;
                case "sting": Tone(220, 0.9f, "sawtooth", 0.14f); Tone(233, 0.9f, "sawtooth", 0.14f); Tone(330, 0.9f, "square", 0.05f, 300); break;
                case "huh": Tone(520, 0.12f, "square", 0.05f); Tone(700, 0.14f, "square", 0.05f, 0, 0.12f); break;
                case "door": Tone(660, 0.1f, "square", 0.06f); Tone(990, 0.15f, "square", 0.06f, 0, 0.1f); Noise(0.5f, 0.12f, 300, 0.8f, "lowpass"); break;
                case "deny": Tone(160, 0.2f, "square", 0.07f); break;
                case "card": Tone(880, 0.08f, "square", 0.05f); Tone(1320, 0.18f, "triangle", 0.08f, 0, 0.08f); break;
                case "lift": Tone(523, 0.3f, "triangle", 0.1f); Tone(659, 0.5f, "triangle", 0.1f, 0, 0.25f); Noise(0.8f, 0.08f, 200, 0.8f, "lowpass", 0.1f); break;
                case "siren": Tone(740, 0.45f, "sawtooth", 0.05f, 980); Tone(980, 0.45f, "sawtooth", 0.05f, 740, 0.45f); break;
                case "calm": Tone(660, 0.2f, "triangle", 0.07f); Tone(440, 0.35f, "triangle", 0.07f, 0, 0.18f); break;
                case "beep": Tone(1500, 0.05f, "square", 0.03f); break;
                case "shot": Noise(0.35f, 1.0f, 1200, 0.5f, "lowpass"); Tone(120, 0.25f, "square", 0.2f, 40); break;
                case "grab": Noise(0.3f, 0.4f, 600, 1); Tone(200, 0.3f, "square", 0.1f, 90); break;
                case "zap": Noise(0.4f, 0.6f, 5000, 2); Tone(1800, 0.3f, "sawtooth", 0.08f, 300); break;
                case "heli": Noise(1.2f, 0.3f, 160, 3); break;
                case "win": { float[] fs = { 523, 659, 784, 1046 }; for (int i = 0; i < 4; i++) Tone(fs[i], 0.35f, "triangle", 0.12f, 0, i * 0.12f); } break;
                case "blip": Tone(900, 0.04f, "square", 0.02f); break;
                case "step": Noise(0.07f, 0.35f, 220, 1f, "lowpass"); Tone(75, 0.06f, "sine", 0.18f, 50); break;                 // 안 보이는 NPC 발소리
                case "heart": Tone(58, 0.13f, "sine", 0.55f, 42); Tone(52, 0.14f, "sine", 0.4f, 38, 0.2f); break;               // 변신이 곧 풀릴 때 심장 소리
                case "tick": Tone(1250, 0.03f, "square", 0.04f); break;
                case "hum":
                    {   // 118Hz 톱니파 + 저역 통과 (1초에 정확히 118번 → 이음새 없이 반복)
                        float low = 0, f = 2 * Mathf.Sin(Mathf.PI * 380f / SR);
                        for (int pass = 0; pass < 2; pass++)
                            for (int i = 0; i < buf.Length; i++)
                            {
                                float ph = (i * 118f / SR) % 1f, x = ph * 2 - 1;
                                low += f * (x - low);
                                if (pass == 1) buf[i] = low * 0.018f;
                            }
                        break;
                    }
                default: Debug.LogWarning("[Sfx] 없는 소리: " + name); break;
            }
            // 끝의 무음 잘라내기
            int end = buf.Length;
            if (name != "hum") { while (end > 1 && Mathf.Abs(buf[end - 1]) < 0.0005f) end--; end = Mathf.Min(buf.Length, end + 200); }
            var data = new float[end];
            for (int i = 0; i < end; i++) data[i] = Mathf.Clamp(buf[i] * MASTER, -1, 1);
            var clip = AudioClip.Create(name, end, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
