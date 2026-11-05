using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CafeSim
{
    // 전원 단골 엔딩 (ending_spec.md). One persistent overlay that plays the timetable of spec section 3,
    // then returns to the main menu. Drawn with IMGUI above every other layer.
    //  Full:      night fade (0.8 + 0.5) → "다음 날 아침" (1.5) → art (1.5) → look (2.0) → title (1.2)
    //             → "클릭하여 계속" after 1.5 s, wait for one input → fade out (2.0) → main menu fade-in (1.0)
    //  Morning:   same, but starts on black (used when the game is started with an ending still pending)
    //  Replay:    from the main menu; skips the night and morning steps
    public sealed class CafeEnding : MonoBehaviour
    {
        public enum Mode { Full, Morning, Replay }

        public const string MenuScene = "MainMenu";
        const float TitleLeft = 410f / 1672, TitleTop = 55f / 941, TitleWidth = 890f / 1672, TitleHeight = 170f / 941;

        static CafeEnding current;
        public static bool Active => current != null;

        Mode mode;
        int morningDay;
        float black, morningAlpha, artAlpha, titleAlpha, titleRise, hintStart = -1;
        bool acceptInput, inputTaken;
        Texture2D art, title;
        GUIStyle morningStyle, hintStyle;
        AudioSource music, ambience, murmur;
        float listenerVolume = 1;

        public static void Play(Mode mode, int morningDay = 0)
        {
            if (current != null) return;
            current = new GameObject("Cafe Ending").AddComponent<CafeEnding>();
            DontDestroyOnLoad(current.gameObject);
            current.mode = mode;
            current.morningDay = morningDay;
            current.StartCoroutine(current.Run());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatic() => current = null;

        void Awake()
        {
            art = Resources.Load<Texture2D>("CafeEnding/ending_bg");
            title = Resources.Load<Texture2D>("CafeEnding/ending_title");
            // Optional sound (spec 6). Drop clips with these names into Resources/CafeEnding to enable them.
            music = Source("CafeEnding/ending_bgm", true);
            ambience = Source("CafeEnding/morning_ambience", true);
            murmur = Source("CafeEnding/crowd_murmur", true);
            listenerVolume = AudioListener.volume;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        AudioSource Source(string path, bool loop)
        {
            var clip = Resources.Load<AudioClip>(path);
            if (clip == null) return null;
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.volume = 0;
            source.ignoreListenerPause = true;   // the business day pauses the listener when it closes
            source.ignoreListenerVolume = true;  // the business BGM fades through the listener volume
            return source;
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }

        IEnumerator Run()
        {
            if (mode == Mode.Full)
            {
                // 1. 하룻밤 전환: fade to black with the business sounds, then hold.
                yield return Tween(.8f, k => { black = k; AudioListener.volume = listenerVolume * (1 - k); });
                yield return Wait(.5f);
            }
            else
            {
                if (mode == Mode.Morning) { black = 1; AudioListener.volume = 0; }
                else yield return Tween(.5f, k => black = k);
            }

            if (mode != Mode.Replay)
            {
                // 2. 아침 문구 with birds and the neighbourhood waking up.
                StartSound(ambience);
                yield return Tween(.4f, k => { morningAlpha = k; Volume(ambience, .6f * k); });
                yield return Wait(.7f);
                yield return Tween(.4f, k => morningAlpha = 1 - k);
            }

            // 3. 일러스트 + ending music and a quiet murmur of voices.
            StartSound(music);
            StartSound(murmur);
            yield return Tween(1.5f, k => { artAlpha = k; Volume(music, .55f * k); Volume(murmur, .18f * k); });
            // 4. 감상 시간
            yield return Wait(2f);
            // 5. Thank You For Playing: fades in while rising 10 px (at 1080p).
            yield return Tween(1.2f, k => { titleAlpha = k; titleRise = 1 - k; });
            // 6. 입력 대기: the hint appears 1.5 s after the title; only one input is taken.
            // Input opens with the hint, and a button already held (or a click buffered earlier) is ignored:
            // only a press that starts after this point continues.
            hintStart = Time.unscaledTime + 1.5f;
            yield return Wait(1.5f);
            yield return null;
            acceptInput = true;
            while (!inputTaken) yield return null;

            // 7. Everything fades out together.
            float m0 = music != null ? music.volume : 0, a0 = ambience != null ? ambience.volume : 0, c0 = murmur != null ? murmur.volume : 0;
            yield return Tween(2f, k =>
            {
                artAlpha = titleAlpha = 1 - k;
                Volume(music, m0 * (1 - k)); Volume(ambience, a0 * (1 - k)); Volume(murmur, c0 * (1 - k));
            });
            hintStart = -1;

            if (mode != Mode.Replay)
            {
                var collection = CafeCollection.Instance;
                collection.MarkEndingSeen();
                // Continuing afterwards is free play from the morning of the ending.
                CafeBusinessDay.ContinueFrom(morningDay > 0 ? morningDay : collection.EndingDay);
                AudioListener.volume = CafeSettings.Volume;
                yield return SceneManager.LoadSceneAsync(MenuScene);
                yield return null;
            }
            else AudioListener.volume = listenerVolume;

            // 8. 메인 화면 fade-in.
            yield return Tween(1f, k => black = 1 - k);
            Destroy(gameObject);
        }

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        static IEnumerator Tween(float seconds, System.Action<float> apply)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                apply(Mathf.SmoothStep(0, 1, t / seconds));
                yield return null;
            }
            apply(1);
        }

        static void StartSound(AudioSource source) { if (source != null && !source.isPlaying) { source.volume = 0; source.Play(); } }
        static void Volume(AudioSource source, float volume) { if (source != null) source.volume = volume; }

        void Update()
        {
            if (!acceptInput || inputTaken)
            {
                // Before input opens, any press is consumed so it cannot count later.
                return;
            }
            // Any pointer (every mouse, pen or touch screen) or any key on any keyboard.
            bool pressed = false;
            foreach (var device in InputSystem.devices)
            {
                if (device is Pointer pointer && pointer.press.wasPressedThisFrame) pressed = true;
                else if (device is Mouse mouse && mouse.rightButton.wasPressedThisFrame) pressed = true;
                else if (device is Keyboard keyboard && keyboard.anyKey.wasPressedThisFrame) pressed = true;
            }
            if (!pressed) return;
            inputTaken = true;     // later presses are ignored, so mashing cannot skip twice
            acceptInput = false;
        }

        void OnGUI()
        {
            GUI.depth = -1000;
            // Swallow clicks and keys so nothing underneath reacts while the ending plays.
            var e = Event.current;
            if (e.type != EventType.Repaint && e.type != EventType.Layout) { e.Use(); return; }
            if (e.type != EventType.Repaint) return;

            var screen = new Rect(0, 0, Screen.width, Screen.height);
            Fill(screen, new Color(0, 0, 0, black));

            if (morningAlpha > 0)
            {
                morningStyle ??= Style(Mathf.RoundToInt(Screen.height * .04f), FontStyle.Normal);
                morningStyle.fontSize = Mathf.RoundToInt(Screen.height * .04f);
                morningStyle.normal.textColor = new Color(.98f, .94f, .86f, morningAlpha);
                GUI.Label(screen, "다음 날 아침", morningStyle);
            }

            if (artAlpha > 0 && art != null)
            {
                // Cover the screen like the 1920x1080 art; other aspect ratios crop the edges.
                float scale = Mathf.Max(Screen.width / (float)art.width, Screen.height / (float)art.height);
                var artRect = new Rect((Screen.width - art.width * scale) * .5f, (Screen.height - art.height * scale) * .5f,
                    art.width * scale, art.height * scale);
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, artAlpha);
                GUI.DrawTexture(artRect, art, ScaleMode.StretchToFill);
                if (title != null && titleAlpha > 0)
                {
                    GUI.color = new Color(1, 1, 1, Mathf.Min(artAlpha, titleAlpha));
                    float rise = titleRise * 10f * artRect.height / 1080f;
                    GUI.DrawTexture(new Rect(artRect.x + TitleLeft * artRect.width, artRect.y + TitleTop * artRect.height + rise,
                        TitleWidth * artRect.width, TitleHeight * artRect.height), title, ScaleMode.StretchToFill);
                }
                GUI.color = old;

                if (hintStart > 0 && Time.unscaledTime >= hintStart)
                {
                    // Gentle 1 s blink that never fully disappears.
                    float pulse = .725f + .275f * Mathf.Cos((Time.unscaledTime - hintStart) * Mathf.PI * 2f);
                    hintStyle ??= Style(16, FontStyle.Bold);
                    hintStyle.fontSize = Mathf.RoundToInt(Screen.height * .026f);
                    var line = new Rect(0, artRect.y + (TitleTop + TitleHeight) * artRect.height - Screen.height * .005f,
                        Screen.width, Screen.height * .045f);
                    // Ink-blue lettering with a soft light outline so it reads on the pale sky.
                    float o = Mathf.Max(1f, Screen.height / 540f);
                    hintStyle.normal.textColor = new Color(1f, .98f, .93f, .75f * pulse * artAlpha);
                    foreach (var d in new[] { new Vector2(-o, 0), new Vector2(o, 0), new Vector2(0, -o), new Vector2(0, o) })
                        GUI.Label(new Rect(line.position + d, line.size), "클릭하여 계속", hintStyle);
                    hintStyle.normal.textColor = new Color(.11f, .23f, .36f, pulse * artAlpha);
                    GUI.Label(line, "클릭하여 계속", hintStyle);
                }
            }
        }

        static GUIStyle Style(int size, FontStyle weight)
        {
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR" }, size);
            return new GUIStyle { font = font, fontSize = size, fontStyle = weight, alignment = TextAnchor.MiddleCenter };
        }

        static void Fill(Rect rect, Color color)
        {
            if (color.a <= 0) return;
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
