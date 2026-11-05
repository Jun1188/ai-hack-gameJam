using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title screen "모두의 카페". The background art already shows the title and menu labels,
// so the buttons are invisible hit areas laid over them with a soft hover highlight.
public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameScene = "test-loop-merge02";
    [SerializeField] private Button startButton, settingsButton, quitButton;
    [SerializeField] private Text notice;

    private bool loading;

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        if (notice != null) notice.gameObject.SetActive(false);
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

    // Settings are not designed yet; acknowledge the click instead of doing nothing.
    public void OpenSettings()
    {
        if (notice == null)
            return;
        StopAllCoroutines();
        StartCoroutine(ShowNotice("설정은 준비 중이에요"));
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator ShowNotice(string message)
    {
        notice.text = message;
        notice.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(1.6f);
        notice.gameObject.SetActive(false);
    }
}
