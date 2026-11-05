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
        if (!settingsOpen)
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
        options ??= new CafeOptionsGui(optionsFont != null ? optionsFont : notice != null ? notice.font : null);
        if (options.Draw(new Rect(640 - CafeOptionsGui.Width * .5f, 360 - CafeOptionsGui.Height * .5f, CafeOptionsGui.Width, CafeOptionsGui.Height)))
            SetSettingsOpen(false);
        GUI.matrix = previous;
    }
}
