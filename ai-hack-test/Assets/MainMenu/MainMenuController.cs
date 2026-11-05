using CafeSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title screen "모두의 카페". The background art already shows the title and menu labels,
// so the buttons are invisible hit areas laid over them with a soft hover highlight.
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
