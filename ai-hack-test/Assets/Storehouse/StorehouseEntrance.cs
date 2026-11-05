using UnityEngine;
using UnityEngine.InputSystem;

public sealed class StorehouseEntrance : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Camera view;
    [SerializeField] private Vector3 approachOffset = new(7.15f, 0f, 3.35f);
    [SerializeField] private Vector3 markerOffset = new(7.15f, 4.5f, 4.1f);
    [SerializeField, Min(0.1f)] private float radius = 3.3f;
    private bool nearby;
    private GrandfatherMovement movement;
    private QuarterViewCameraPan cameraPan;

    private void Start()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (view == null)
            view = Camera.main;
        if (player != null)
        {
            movement = player.GetComponent<GrandfatherMovement>();
            StorehouseTransition.RestorePlayer(player.gameObject);
        }
        if (view != null)
            cameraPan = view.GetComponent<QuarterViewCameraPan>();
    }

    private void Update()
    {
        if (player == null || StorehouseTransition.Busy)
            return;

        Vector3 delta = player.position - transform.TransformPoint(approachOffset);
        nearby = Mathf.Abs(delta.y) < 2f && new Vector2(delta.x, delta.z).sqrMagnitude <= radius * radius;
        if (!nearby || Keyboard.current?.fKey.wasPressedThisFrame != true)
            return;

        if (!StorehouseTransition.OpenStage(player))
        {
            Debug.LogError("Storehouse must be enabled in the build scene list.", this);
            return;
        }
        if (movement != null) movement.enabled = false;
        if (cameraPan != null) cameraPan.enabled = false;
    }

    private void OnGUI()
    {
        if (!nearby || view == null || StorehouseTransition.Busy || Event.current.type != EventType.Repaint)
            return;

        Vector3 marker = StorehouseHud.Project(view, transform.TransformPoint(markerOffset));
        if (marker.z <= 0f)
            return;
        marker.y -= Mathf.Sin(Time.unscaledTime * 3f) * 4f;
        var box = new Rect(marker.x - 64f, marker.y - 48f, 128f, 56f);
        StorehouseHud.Fill(box, StorehouseHud.Ink);
        StorehouseHud.Fill(new Rect(box.x, box.y, box.width, 2f), StorehouseHud.Gold);
        Vector2 lens = new(marker.x - 39f, box.y + 21f);
        for (int i = 0; i < 16; i++)
        {
            float from = i * Mathf.PI / 8f;
            float to = (i + 1) * Mathf.PI / 8f;
            StorehouseHud.Line(lens + new Vector2(Mathf.Cos(from), Mathf.Sin(from)) * 8f,
                lens + new Vector2(Mathf.Cos(to), Mathf.Sin(to)) * 8f, StorehouseHud.Gold, 2f);
        }
        StorehouseHud.Line(lens + new Vector2(6f, 6f), lens + new Vector2(13f, 13f), StorehouseHud.Gold, 3f);
        GUI.Label(new Rect(box.x + 43f, box.y + 4f, 82f, 46f), "F  곳간 조사",
            StorehouseHud.Type(15, StorehouseHud.Paper));
    }
}
