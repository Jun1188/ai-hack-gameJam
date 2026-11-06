using System.Collections;
using UnityEngine;

namespace CafeSim
{
    public sealed partial class CafeHud
    {
        [Range(0, 1)] public float customerVoiceVolume = .16f;
        AudioSource customerVoice;
        AudioClip[] voiceSyllables;
        Coroutine voiceRoutine;

        void StartSpeechVoice(CafeVisit visit, string speech)
        {
            StopSpeechVoice();
            if (!Application.isPlaying || string.IsNullOrWhiteSpace(speech)) return;
            if (customerVoice == null)
            {
                customerVoice = gameObject.AddComponent<AudioSource>();
                customerVoice.playOnAwake = false;
                customerVoice.loop = false;
                customerVoice.spatialBlend = 0;
                // Uses the regular listener: mute, volume, pause and the ending fade apply.
                voiceSyllables = new AudioClip[4];
                for (int i = 0; i < voiceSyllables.Length; i++)
                {
                    float[] samples = CafeSpeechSound.Samples(i);
                    var clip = AudioClip.Create("Cafe voice syllable " + i, samples.Length, 1, CafeSpeechSound.Rate, false);
                    clip.SetData(samples, 0);
                    voiceSyllables[i] = clip;
                }
            }
            customerVoice.volume = Mathf.Clamp01(customerVoiceVolume);
            int voice = 0;
            foreach (char c in visit.Person.id) voice = (voice * 31 + c) & 0x7fffffff;
            voiceRoutine = StartCoroutine(SpeechSounds(speech, .88f + voice % 5 * .065f));
        }

        IEnumerator SpeechSounds(string speech, float pitch)
        {
            // Let LateUpdate place the bubble before the first syllable is heard.
            yield return null;
            int spoken = 0;
            foreach (char c in speech)
            {
                if (spoken >= 36) break;
                if (!char.IsLetterOrDigit(c)) continue;
                while (Halted) yield return null;
                bool visible = speechRoot != null && speechRoot.gameObject.activeInHierarchy;
                bool inChat = promptOpen && !conversationListOpen && ViewingLiveConversation;
                if (!CafeEnding.Active && !CafeStartIris.Active && !RecipeCardsOpen && (visible || inChat))
                {
                    customerVoice.pitch = pitch + (c % 5 - 2) * .025f;
                    customerVoice.PlayOneShot(voiceSyllables[c % voiceSyllables.Length]);
                }
                spoken++;
                float until = Time.unscaledTime + .065f + c % 3 * .008f;
                while (Time.unscaledTime < until) yield return null;
            }
            voiceRoutine = null;
        }

        void StopSpeechVoice()
        {
            if (voiceRoutine != null) StopCoroutine(voiceRoutine);
            voiceRoutine = null;
            if (customerVoice != null) customerVoice.Stop();
        }

        void DestroySpeechVoice()
        {
            StopSpeechVoice();
            if (voiceSyllables != null) foreach (var clip in voiceSyllables) if (clip != null) Destroy(clip);
        }
    }

    // Original synthesized, rounded vowel-like blips. No recorded speech or game samples.
    internal static class CafeSpeechSound
    {
        internal const int Rate = 22050;
        internal static float[] Samples(int vowel)
        {
            int length = Mathf.RoundToInt(Rate * .065f);
            var samples = new float[length];
            float fundamental = 175 + vowel * 23;
            float formant = 650 + vowel * 180;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)Rate;
                float phase = 2 * Mathf.PI * fundamental * t + .12f * Mathf.Sin(2 * Mathf.PI * 23 * t);
                float sample = .55f * Mathf.Sin(phase) + .24f * Mathf.Sin(phase * 2);
                for (int harmonic = 3; harmonic <= 9; harmonic++)
                {
                    float distance = (fundamental * harmonic - formant) / 420;
                    sample += .18f * Mathf.Exp(-distance * distance) * Mathf.Sin(phase * harmonic);
                }
                float envelope = Mathf.Sin(Mathf.PI * i / (length - 1));
                samples[i] = Mathf.Clamp(sample * .62f, -.8f, .8f) * envelope * envelope;
            }
            return samples;
        }
    }
}
