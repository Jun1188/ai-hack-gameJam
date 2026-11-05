using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class StorehouseTransition : MonoBehaviour
{
    public const string CottageScene = "Assets/Scenes/Cottage.unity";
    public const string StageScene = "Assets/Scenes/Storehouse.unity";
    private static StorehouseTransition current;
    private static bool restorePlayer;
    private static Vector3 returnPosition;
    private static Quaternion returnRotation;
    private float coverage;

    public static bool Busy => current != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        current = null;
        restorePlayer = false;
    }

    public static bool OpenStage(Transform player)
    {
        if (Busy || !Application.CanStreamedLevelBeLoaded(StageScene))
            return false;
        returnPosition = player.position;
        returnRotation = player.rotation;
        restorePlayer = true;
        Go(StageScene);
        return true;
    }

    public static void ReturnToCottage()
    {
        if (!Busy && Application.CanStreamedLevelBeLoaded(CottageScene))
            Go(CottageScene);
    }

    public static void RestorePlayer(GameObject player)
    {
        if (!restorePlayer)
            return;
        restorePlayer = false;
        var controller = player.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;
        player.transform.SetPositionAndRotation(returnPosition, returnRotation);
        if (wasEnabled) controller.enabled = true;
    }

    private static void Go(string path)
    {
        current = new GameObject("Storehouse Scene Transition").AddComponent<StorehouseTransition>();
        DontDestroyOnLoad(current.gameObject);
        current.StartCoroutine(current.SwitchScene(path));
    }

    private IEnumerator SwitchScene(string path)
    {
        yield return Animate(0f, 1f, 0.42f);
        yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
        yield return null;
        yield return Animate(1f, 0f, 0.48f);
        current = null;
        Destroy(gameObject);
    }

    private IEnumerator Animate(float from, float to, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            coverage = Mathf.SmoothStep(from, to, elapsed / duration);
            yield return null;
        }
        coverage = to;
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint)
            return;

        GUI.depth = -100;
        float width = Screen.width * coverage * 0.5f;
        var dark = new Color(0.07f, 0.095f, 0.085f);
        StorehouseHud.Fill(new Rect(0f, 0f, width, Screen.height), dark);
        StorehouseHud.Fill(new Rect(Screen.width - width, 0f, width, Screen.height), dark);
        if (coverage <= 0f)
            return;
        StorehouseHud.Fill(new Rect(width - 2f, 0f, 2f, Screen.height), StorehouseHud.Gold);
        StorehouseHud.Fill(new Rect(Screen.width - width, 0f, 2f, Screen.height), StorehouseHud.Gold);
        if (coverage > 0.98f)
            GUI.Label(new Rect(0f, Screen.height * 0.5f - 24f, Screen.width, 48f), "곳간",
                StorehouseHud.Type(24, StorehouseHud.Paper, TextAnchor.MiddleCenter));
    }
}
