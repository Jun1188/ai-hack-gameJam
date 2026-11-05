using CafeSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title screen "모두의 카페".
public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameScene = "CafeGame";
    [SerializeField] private Button startButton, settingsButton, quitButton;
    [SerializeField] private Text notice;
    [SerializeField] private Font optionsFont;

    private bool loading, settingsOpen;
    private CafeOptionsGui options;
    private GraphicRaycaster menuRaycaster;

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        if (notice != null) notice.gameObject.SetActive(false);
        if (startButton != null) menuRaycaster = startButton.GetComponentInParent<GraphicRaycaster>();
        BuildMenuLabels();
        // Kept visible from the start for demos, even before the ending has been reached.
        AddReplayButton();
        AddCreditsButton();
    }

    private void BuildMenuLabels()
    {
        if (optionsFont == null || startButton == null || settingsButton == null || quitButton == null)
            return;
        var art = startButton.transform.parent.GetComponent<Image>();
        if (art == null || art.sprite == null) return;

        // Sample a blank section of the same wood panels over the original painted lettering.
        CoverPaintedText(art, new Rect(.042f, .602f, .273f, .121f));
        CoverPaintedText(art, new Rect(.118f, .203f, .062f, .181f));
        var startCover = AddMenuGraphic<Image>(startButton.transform, "Start label backdrop",
            new Rect(.245f, .19f, .515f, .62f));
        startCover.color = new Color32(96, 89, 46, 255);

        AddMenuLabel(art.transform, "모두의 카페", new Rect(.041f, .599f, .274f, .126f), 83);
        AddMenuLabel(startButton.transform, "게임 시작", new Rect(.25f, 0f, .51f, 1f), 48);
        AddMenuLabel(settingsButton.transform, "설정", new Rect(.25f, 0f, .26f, 1f), 48);
        AddMenuLabel(quitButton.transform, "종료", new Rect(.25f, 0f, .26f, 1f), 48);
    }

    private static void CoverPaintedText(Image art, Rect bounds)
    {
        var patch = AddMenuGraphic<RawImage>(art.transform, "Wood behind menu lettering", bounds);
        patch.transform.SetAsFirstSibling();
        patch.texture = art.sprite.texture;
        patch.uvRect = new Rect(bounds.x, .93f, bounds.width, .06f);
    }

    private void AddMenuLabel(Transform parent, string value, Rect bounds, int maxSize)
    {
        var text = AddMenuGraphic<Text>(parent, value, bounds);
        text.font = optionsFont;
        text.text = value;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color32(253, 228, 175, 255);
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 1;
        text.resizeTextMaxSize = maxSize;
    }

    private static T AddMenuGraphic<T>(Transform parent, string name, Rect bounds) where T : Graphic
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(T));
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = bounds.min;
        rect.anchorMax = bounds.max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var graphic = child.GetComponent<T>();
        graphic.raycastTarget = false;
        return graphic;
    }

    private void OnDestroy() => options?.Dispose();

    private void Update()
    {
        if (settingsOpen && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            SetSettingsOpen(false);
        if (creditsOpen && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            creditsOpen = false;
        // The menu buttons stay clickable underneath IMGUI overlays, so they are switched off while one is up.
        if (menuRaycaster != null) menuRaycaster.enabled = !settingsOpen && !creditsOpen && !CafeEnding.Active;
    }

    // ----- Credits -----

    private const string CreditsDate = "2026-11-06";
    private const string CreditsTeam = "한국공학대학교 AI_DIOTS";
    private static readonly string[] CreditsMembers = { "김다니엘", "김선웅", "변준영", "하준호" };
    private bool creditsOpen;
    private GUIStyle creditsTitle, creditsLabel, creditsValue, creditsButton;
    private Font menuFont;

    private Font MenuFont => menuFont != null ? menuFont
        : menuFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕" }, 20) ?? notice?.font;

    // A small "크레딧" pill in the bottom-right corner of the screen.
    private void AddCreditsButton()
    {
        if (menuRaycaster == null) return;
        var go = new GameObject("Credits Button (크레딧)", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(menuRaycaster.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-24, 20);
        rect.sizeDelta = new Vector2(112, 38);
        var image = go.GetComponent<Image>();
        image.color = Color.white;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color(.12f, .07f, .04f, .55f);
        colors.highlightedColor = new Color(.24f, .14f, .08f, .75f);
        colors.pressedColor = new Color(.08f, .04f, .02f, .85f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
        button.onClick.AddListener(() => creditsOpen = true);

        var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
        var labelRect = (RectTransform)label.transform;
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        var text = label.GetComponent<Text>();
        text.text = "크레딧";
        text.font = MenuFont;
        text.fontSize = 18;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color32(246, 234, 210, 255);
        text.raycastTarget = false;
    }

    private void DrawCredits()
    {
        var ink = new Color32(66, 42, 28, 255);
        var accent = new Color32(190, 104, 46, 255);
        if (creditsTitle == null)
        {
            creditsTitle = new GUIStyle { font = MenuFont, fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            creditsTitle.normal.textColor = ink;
            creditsLabel = new GUIStyle(creditsTitle) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            creditsLabel.normal.textColor = accent;
            creditsValue = new GUIStyle(creditsTitle) { fontSize = 19, alignment = TextAnchor.MiddleLeft };
            creditsButton = new GUIStyle(creditsTitle) { fontSize = 16 };
            creditsButton.normal.textColor = creditsButton.hover.textColor = new Color32(253, 246, 228, 255);
        }
        var card = new Rect(640 - 280, 360 - 175, 560, 350);
        if (Event.current.type == EventType.Repaint)
        {
            Fill(new Rect(card.x + 5, card.y + 6, card.width, card.height), new Color(0, 0, 0, .25f));
            Fill(card, ink);
            Fill(new Rect(card.x + 3, card.y + 3, card.width - 6, card.height - 6), new Color32(253, 246, 228, 255));
        }
        GUI.Label(new Rect(card.x, card.y + 26, card.width, 40), "모두의 카페", creditsTitle);
        if (Event.current.type == EventType.Repaint)
            Fill(new Rect(card.x + 48, card.y + 80, card.width - 96, 2), new Color(ink.r / 255f, ink.g / 255f, ink.b / 255f, .3f));
        float y = card.y + 100;
        void Row(string label, string value)
        {
            GUI.Label(new Rect(card.x + 56, y, 80, 30), label, creditsLabel);
            GUI.Label(new Rect(card.x + 136, y, card.width - 176, 30), value, creditsValue);
            y += 44;
        }
        Row("제작일", CreditsDate);
        Row("제작팀", CreditsTeam);
        Row("제작자", string.Join(" · ", CreditsMembers));

        var close = new Rect(640 - 80, card.yMax - 70, 160, 44);
        if (Event.current.type == EventType.Repaint)
            Fill(close, close.Contains(Event.current.mousePosition) ? new Color32(208, 124, 62, 255) : accent);
        if (GUI.Button(close, "닫기", creditsButton)) creditsOpen = false;
        // A click outside the card also closes it.
        if (Event.current.type == EventType.MouseDown && !card.Contains(Event.current.mousePosition)) { creditsOpen = false; Event.current.Use(); }
    }

    private static void Fill(Rect rect, Color color)
    {
        var old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }

    // "엔딩 다시 보기" sits under 종료. The other labels are part of the background art, so this one
    // draws its own text in the same cream colour.
    private void AddReplayButton()
    {
        if (quitButton == null) return;
        var art = (RectTransform)quitButton.transform.parent;
        var go = new GameObject("Replay Ending Button (엔딩 다시 보기)", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(art, false);
        // Same column as the baked-in menu labels (art pixels 98..495 x 770..836 of 1672x941).
        rect.anchorMin = new Vector2(98f / 1672, 1 - 836f / 941);
        rect.anchorMax = new Vector2(495f / 1672, 1 - 770f / 941);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.color = Color.white;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.colors = quitButton.colors;
        button.navigation = quitButton.navigation;
        button.onClick.AddListener(() => CafeEnding.Play(CafeEnding.Mode.Replay));

        var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
        var labelRect = (RectTransform)label.transform;
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(105, 0);   // left edge lines up with 설정 / 종료
        labelRect.offsetMax = Vector2.zero;
        var text = label.GetComponent<Text>();
        text.text = "엔딩 다시 보기";
        // Same lettering as the rebuilt menu labels (BuildMenuLabels) when that font is set.
        text.font = optionsFont != null ? optionsFont : MenuFont;
        text.fontSize = 30;
        text.fontStyle = optionsFont != null ? FontStyle.Normal : FontStyle.Bold;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = new Color32(253, 228, 175, 255);
        text.raycastTarget = false;
    }

    public void StartGame()
    {
        if (loading)
            return;
        if (!Application.CanStreamedLevelBeLoaded(gameScene))
        {
            Debug.LogError($"'{gameScene}' 씬이 Build Profiles(빌드 씬 목록)에 없습니다.", this);
            return;
        }
        loading = true;
        SceneManager.LoadScene(gameScene);
    }

    // Same options card as the in-game pause menu; both edit CafeSettings.
    public void OpenSettings() => SetSettingsOpen(true);

    private void SetSettingsOpen(bool open)
    {
        settingsOpen = open;
        // IMGUI does not block the uGUI buttons underneath, so they stop taking clicks meanwhile.
        if (menuRaycaster != null) menuRaycaster.enabled = !open;
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnGUI()
    {
        if (!settingsOpen && !creditsOpen)
            return;
        if (Event.current.type == EventType.Repaint)
        {
            var old = GUI.color;
            GUI.color = new Color(.12f, .07f, .04f, .55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
        var previous = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) * .5f, (Screen.height - 720 * scale) * .5f, 0), Quaternion.identity, Vector3.one * scale);
        if (creditsOpen)
        {
            DrawCredits();
            GUI.matrix = previous;
            return;
        }
        options ??= new CafeOptionsGui(optionsFont != null ? optionsFont : notice != null ? notice.font : null);
        if (options.Draw(new Rect(640 - CafeOptionsGui.Width * .5f, 360 - CafeOptionsGui.Height * .5f, CafeOptionsGui.Width, CafeOptionsGui.Height)))
            SetSettingsOpen(false);
        GUI.matrix = previous;
    }
}
