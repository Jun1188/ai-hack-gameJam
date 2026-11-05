using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class StorehouseAssembly : MonoBehaviour
{
    private const float HeaderHeight = 88f;
    private const float TrayHeight = 118f;
    private const int TopPlateStep = 6;
    private static readonly Bounds PartBox = new(Vector3.zero, Vector3.one);
    private static readonly string[] Phases =
        { "기초", "바닥", "바닥", "바닥", "벽", "벽", "벽", "지붕", "지붕", "마감" };
    private static readonly string[] Tasks =
    {
        "밀려난 기초 블록 2개를 제자리로 밀기",
        "기초 위에 바닥 틀 올리기",
        "장선 4개를 60.96cm 간격으로 놓기",
        "장선 위에 바닥판 덮기",
        "뒷벽, 창문 벽 순으로 세우기",
        "옆벽, 문 벽 순으로 세우기",
        "벽 위에 윗틀 두 개 놓기",
        "양 끝부터 서까래 5쌍 세우기",
        "지붕판 두 장 덮기",
        "마당 쪽에 문짝 달기"
    };
    private static readonly string[] Hints =
    {
        "할아버지   기억을 더듬어 보자. 빈자리 두 곳이 보이지?",
        "할아버지   돌 여섯 개 위에 틀을 올리면 되겠구나.",
        "할아버지   두 뼘 간격으로 고르게. G를 누르면 넷을 한 번에 놓는다.",
        "할아버지   장선이 다 깔렸으니 이제 바닥을 덮자.",
        "할아버지   창은 바람 들어오는 쪽이어야 곡식이 안 썩는다.",
        "할아버지   문은 마당 쪽이다. R로 패널을 돌려 보렴.",
        "할아버지   작업대의 윗틀을 집어서 벽 위 초록색 자리에 놓으렴.",
        "할아버지   양 끝부터 세워야 가운데가 맞는다.",
        "할아버지   서까래 위에 지붕판을 한 장씩 얹어 보자.",
        "할아버지   이제 문짝 하나만 달면 되겠구나."
    };

    [SerializeField] private Camera view;
    [SerializeField] private Material[] wood;
    [SerializeField] private Material stone, roof, metal, outline;
    [SerializeField] private Light completionLamp;

    private List<StorehouseParts.Piece> pieces;
    private readonly List<int> available = new(5);
    private readonly List<Transform> previews = new(5);
    private Transform previewRoot;
    private int step, selected = -1, installed;
    private int dragged = -1, hoveredSocket = -1;
    private Vector2 grabOffset;
    private bool animating, blueprint = true;
    private string feedback;
    private float feedbackUntil;

    public int CurrentStep => Mathf.Min(step + 1, Tasks.Length);
    public bool Complete => step >= Tasks.Length;
    public bool IsDragging => dragged >= 0;
    private bool InstallingTopPlates => step == TopPlateStep;

    private int NextPart()
    {
        foreach (int index in available)
            if (!pieces[index].Installed)
                return index;
        return -1;
    }

    private void Start()
    {
        var build = StorehouseParts.Group(transform, "Storehouse assembly", new Vector3(.92f, 0f, .12f));
        var parts = new StorehouseParts(build, wood, stone, roof, metal);
        parts.Build();
        pieces = parts.All;
        previewRoot = StorehouseParts.Group(transform, "Recall outlines", Vector3.zero);
        foreach (var piece in pieces)
        {
            piece.Model.position = piece.Socket;
            piece.Model.gameObject.SetActive(piece.Installed);
            if (piece.Installed)
                installed++;
        }
        PrepareStep();
    }

    private void PrepareStep()
    {
        selected = -1;
        available.Clear();
        foreach (Transform preview in previews)
            Destroy(preview.gameObject);
        previews.Clear();
        if (Complete)
            return;

        for (int i = 0; i < pieces.Count; i++)
            if (pieces[i].Stage == step && !pieces[i].Installed)
                available.Add(i);

        for (int index = 0; index < available.Count; index++)
        {
            var piece = pieces[available[index]];
            Transform ghost = Instantiate(piece.Model, previewRoot);
            ghost.name = piece.Name + " | recall";
            ghost.SetPositionAndRotation(piece.Socket, Quaternion.identity);
            ghost.localScale = Vector3.one;
            foreach (Renderer mesh in ghost.GetComponentsInChildren<Renderer>(true))
            {
                mesh.sharedMaterial = outline;
                mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            ghost.gameObject.SetActive(blueprint);
            previews.Add(ghost);

            if (step != 0)
                piece.Supply = new Vector3(-3.55f, .94f + (step == 4 || step == 5 ? .32f : .08f),
                    InstallingTopPlates ? 0f : (index - (available.Count - 1) / 2f) * 1.16f);
            piece.Model.localScale = Vector3.one * (step == 0 ? 1f : .36f);
            piece.Model.SetPositionAndRotation(piece.Supply, Quaternion.Euler(0f, piece.Turns * 90f, 0f));
            piece.Model.gameObject.SetActive(true);
        }
        if (InstallingTopPlates)
            ShowNextTopPlate();
    }

    private void ShowNextTopPlate()
    {
        int next = NextPart();
        for (int i = 0; i < available.Count; i++)
        {
            int index = available[i];
            previews[i].gameObject.SetActive(index == next);
            pieces[index].Model.gameObject.SetActive(pieces[index].Installed || index == next);
        }
    }

    // Kept separate from mouse and keyboard input so a helper can issue the same commands later.
    public bool SelectPart(int part)
    {
        if (animating || IsDragging || StorehouseTransition.Busy || !available.Contains(part) || pieces[part].Installed
            || (InstallingTopPlates && part != NextPart()))
            return false;
        selected = part;
        feedbackUntil = 0f;
        return true;
    }

    public void RotateSelection()
    {
        if (selected < 0 || animating || step < 4 || step >= TopPlateStep)
            return;
        var piece = pieces[selected];
        piece.Turns = (piece.Turns + 1) % piece.TurnPeriod;
        piece.Model.rotation = Quaternion.Euler(0f, piece.Turns * 90f, 0f);
        feedbackUntil = 0f;
    }

    public bool TryPlace(int socket) => TryPlace(socket, false);

    private bool TryPlace(int socket, bool fromDrag)
    {
        if (animating || IsDragging || Complete || StorehouseTransition.Busy
            || !available.Contains(socket) || pieces[socket].Installed)
            return false;

        string issue = PlacementIssue(socket);
        if (issue != null)
            return Reject(issue);

        StartCoroutine(InstallSelection(socket, fromDrag));
        return true;
    }

    private string PlacementIssue(int socket)
    {
        if (selected < 0)
            return "먼저 옮길 부품을 집으세요.";
        if (step >= 4 && step <= TopPlateStep)
        {
            int next = NextPart();
            if (socket != next)
                return $"{pieces[next].Name}부터 놓으세요.";
        }
        if (step == 7 && socket != available[0] && socket != available[^1]
            && (!pieces[available[0]].Installed || !pieces[available[^1]].Installed))
            return "가운데부터 세우면 간격이 어긋나요. 양 끝을 먼저 세우세요.";

        if (selected != socket && step != 2 && step != 7)
            return step == 0 ? "이 돌은 저 자리가 아닌데. 반대편 빈자리를 봐." : "이 부품이 들어갈 자리가 아니에요.";

        var piece = pieces[selected];
        if (piece.Turns != 0)
        {
            string rotate = $"R {piece.TurnPeriod - piece.Turns}회";
            if (step == 4 && piece.TurnPeriod == 4)
                return $"{rotate}로 창이 바람 쪽을 향하게 하세요.";
            if (step == 5 && piece.TurnPeriod == 4)
                return $"{rotate}로 문이 마당을 향하게 하세요.";
            return $"{rotate}로 벽을 자리와 나란히 맞추세요.";
        }
        return null;
    }

    private bool Reject(string message)
    {
        ShowFeedback(message);
        return false;
    }

    private void ShowFeedback(string message, float seconds = 3.5f)
    {
        feedback = message;
        feedbackUntil = Time.unscaledTime + seconds;
    }

    private IEnumerator InstallSelection(int socket, bool fromDrag)
    {
        animating = true;
        if (socket != selected)
        {
            // Joists and rafters are interchangeable. The remaining model moves to the unused supply slot.
            var source = pieces[selected];
            var destination = pieces[socket];
            (source.Model, destination.Model) = (destination.Model, source.Model);
            (source.Supply, destination.Supply) = (destination.Supply, source.Supply);
        }
        yield return Install(socket, fromDrag);
        FinishMove();
    }

    public bool PlaceJoists()
    {
        if (step != 2 || animating || IsDragging || StorehouseTransition.Busy)
            return false;
        StartCoroutine(InstallJoists());
        return true;
    }

    private IEnumerator InstallJoists()
    {
        animating = true;
        selected = -1;
        foreach (int index in available)
            if (!pieces[index].Installed)
                yield return Install(index);
        FinishMove();
        ShowFeedback("장선 4개를 60.96cm 간격으로 배치했습니다.");
    }

    private IEnumerator Install(int index, bool fromDrag = false)
    {
        var piece = pieces[index];
        Vector3 start = piece.Model.position;
        Vector3 size = piece.Model.localScale;
        float duration = fromDrag ? .16f : step == 0 ? .44f : .42f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            float lift = fromDrag || step == 0 ? 0f : Mathf.Sin(Mathf.PI * progress) * .72f;
            piece.Model.position = Vector3.Lerp(start, piece.Socket, progress) + Vector3.up * lift;
            piece.Model.localScale = Vector3.Lerp(size, Vector3.one, progress);
            yield return null;
        }
        piece.Model.position = piece.Socket;
        piece.Model.localScale = Vector3.one;
        piece.Installed = true;
        previews[available.IndexOf(index)].gameObject.SetActive(false);
        installed++;
    }

    private void FinishMove()
    {
        selected = -1;
        animating = false;
        feedbackUntil = 0f;
        if (NextPart() >= 0)
        {
            if (InstallingTopPlates)
                ShowNextTopPlate();
            return;
        }
        step++;
        PrepareStep();
        if (!Complete)
            return;

        if (completionLamp != null)
            completionLamp.enabled = true;
        if (view != null && view.TryGetComponent(out StorehouseCamera stageCamera))
            stageCamera.FrameCompletedStorehouse();
    }

    private void Update()
    {
        if (pieces == null || StorehouseTransition.Busy || animating)
            return;
        Keyboard keys = Keyboard.current;
        if (keys?.escapeKey.wasPressedThisFrame == true && IsDragging)
        {
            EndDrag(-1);
            return;
        }
        if (keys?.escapeKey.wasPressedThisFrame == true || (Complete && keys?.fKey.wasPressedThisFrame == true))
        {
            StorehouseTransition.ReturnToCottage();
            return;
        }
        if (Complete)
            return;

        if (keys?.hKey.wasPressedThisFrame == true)
        {
            blueprint = !blueprint;
            if (!InstallingTopPlates)
                for (int i = 0; i < previews.Count; i++)
                    previews[i].gameObject.SetActive(blueprint && !pieces[available[i]].Installed);
        }
        if (keys?.rKey.wasPressedThisFrame == true) RotateSelection();
        if (keys?.gKey.wasPressedThisFrame == true && PlaceJoists()) return;
    }

    private void HandleDragEvent()
    {
        var input = Event.current;
        int control = GUIUtility.GetControlID(FocusType.Passive);
        if (!IsDragging && GUIUtility.hotControl == control)
            GUIUtility.hotControl = 0;
        if (Complete || animating || input.button != 0)
            return;

        EventType type = input.GetTypeForControl(control);
        bool pressing = type == EventType.MouseDown;
        bool moving = type == EventType.MouseDrag;
        bool releasing = type == EventType.MouseUp;
        if ((!pressing && !IsDragging) || (!pressing && !moving && !releasing))
            return;

        bool wasDragging = IsDragging;
        Vector2 pointer = input.mousePosition;
        pointer.y = Screen.height - pointer.y;
        UpdateDrag(pointer, pressing, !releasing);
        if (IsDragging)
            GUIUtility.hotControl = control;
        else if (wasDragging)
            GUIUtility.hotControl = 0;
        if (wasDragging || IsDragging)
            input.Use();
    }

    private void UpdateDrag(Vector2 pointer, bool pressed, bool held)
    {
        bool overWorkArea = pointer.y > TrayHeight && pointer.y < Screen.height - HeaderHeight
            && QuarterViewCameraPan.VisibleViewport().Contains(pointer);
        if (!IsDragging && overWorkArea && pressed)
        {
            int hit = FindLoosePart(view.ScreenPointToRay(pointer));
            if (hit >= 0 && SelectPart(hit))
            {
                dragged = hit;
                grabOffset = (Vector2)view.WorldToScreenPoint(DragAnchor(pieces[dragged])) - pointer;
            }
        }
        if (!IsDragging)
            return;

        var piece = pieces[dragged];
        Vector2 anchor = pointer + grabOffset;
        if (overWorkArea)
        {
            float depth = Mathf.Max(view.nearClipPlane + 1f,
                Vector3.Dot(transform.position - view.transform.position, view.transform.forward) - 5f);
            Vector3 carriedAnchor = view.ScreenToWorldPoint(new Vector3(anchor.x, anchor.y, depth));
            piece.Model.position += carriedAnchor - DragAnchor(piece);
        }
        hoveredSocket = overWorkArea ? FindSocket(anchor, pointer) : -1;
        if (!held)
            EndDrag(hoveredSocket);
    }

    private static Vector3 DragAnchor(StorehouseParts.Piece piece)
        => piece.Model.position + (piece.Pin - piece.Socket) * piece.Model.localScale.x;

    private int FindLoosePart(Ray ray)
    {
        int best = -1;
        float nearest = float.PositiveInfinity;
        foreach (int index in available)
        {
            var piece = pieces[index];
            if (piece.Installed)
                continue;
            foreach (Renderer surface in piece.Model.GetComponentsInChildren<Renderer>())
            {
                Transform box = surface.transform;
                var localRay = new Ray(box.InverseTransformPoint(ray.origin), box.InverseTransformVector(ray.direction));
                if (!PartBox.IntersectRay(localRay, out float distance))
                    continue;
                float worldDistance = (box.TransformPoint(localRay.GetPoint(distance)) - ray.origin).sqrMagnitude;
                if (worldDistance < nearest)
                {
                    nearest = worldDistance;
                    best = index;
                }
            }
        }
        return best;
    }

    private float PixelsPerUnit => view.pixelHeight / (2f * view.orthographicSize);
    private float SnapRadiusPixels => (InstallingTopPlates ? .68f : step == 2 || step == 7 ? .32f : .48f)
        * PixelsPerUnit;

    private int FindSocket(Vector2 anchor, Vector2 pointer)
    {
        int closest = -1;
        float bestDistance = SnapRadiusPixels * SnapRadiusPixels;
        foreach (int index in available)
        {
            var piece = pieces[index];
            if (piece.Installed || (InstallingTopPlates && index != dragged))
                continue;
            Vector3 target = view.WorldToScreenPoint(piece.Pin);
            float distance = ((Vector2)target - anchor).sqrMagnitude;
            if (target.z > 0f && distance <= bestDistance)
            {
                closest = index;
                bestDistance = distance;
            }
        }
        if (closest < 0 && InstallingTopPlates && dragged == NextPart()
            && NearTopPlate(pointer, previews[available.IndexOf(dragged)]))
            return dragged;
        return closest;
    }

    private bool NearTopPlate(Vector2 pointer, Transform preview)
    {
        float radius = .35f * PixelsPerUnit;
        foreach (Transform beam in preview)
        {
            Vector3 axis = beam.localScale.x > beam.localScale.z ? Vector3.right : Vector3.forward;
            Vector2 start = view.WorldToScreenPoint(beam.TransformPoint(-axis * .5f));
            Vector2 end = view.WorldToScreenPoint(beam.TransformPoint(axis * .5f));
            Vector2 line = end - start;
            float along = Mathf.Clamp01(Vector2.Dot(pointer - start, line) / Mathf.Max(1f, line.sqrMagnitude));
            if ((pointer - start - line * along).sqrMagnitude <= radius * radius)
                return true;
        }
        return false;
    }

    private void EndDrag(int socket)
    {
        int index = dragged;
        dragged = hoveredSocket = -1;
        if (socket >= 0 && TryPlace(socket, true))
            return;
        if (socket < 0)
            ShowFeedback("부품을 표시된 자리 가까이 가져가 놓으세요.");
        StartCoroutine(ReturnPart(pieces[index]));
    }

    private IEnumerator ReturnPart(StorehouseParts.Piece piece)
    {
        animating = true;
        Vector3 start = piece.Model.position;
        const float duration = .19f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            piece.Model.position = Vector3.Lerp(start, piece.Supply, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        piece.Model.position = piece.Supply;
        animating = false;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused || !IsDragging)
            return;
        var piece = pieces[dragged];
        piece.Model.position = piece.Supply;
        dragged = hoveredSocket = -1;
    }

    private void OnGUI()
    {
        if (pieces == null || view == null || StorehouseTransition.Busy)
            return;
        HandleDragEvent();
        float width = Screen.width;
        float height = Screen.height;
        DrawHeader(width);
        DrawTray(width, height);
        if (!Complete && !animating)
            DrawSockets(width, height);
    }

    private void DrawHeader(float width)
    {
        StorehouseHud.Fill(new Rect(0f, 0f, width, HeaderHeight), StorehouseHud.Ink);
        StorehouseHud.Fill(new Rect(0f, 87f, width, 1f), StorehouseHud.Gold);
        GUI.Label(new Rect(22f, 8f, width - 44f, 24f),
            Complete ? "01  /  조립 완료" : $"01  /  곳간 조립      {step + 1:00} / 10   {Phases[step]}",
            StorehouseHud.Type(14, StorehouseHud.Gold));
        GUI.Label(new Rect(22f, 30f, width - 44f, 37f), Complete ? "새 곳간이 완성됐습니다" : Tasks[step],
            StorehouseHud.Type(24, StorehouseHud.Paper));
        float bar = Mathf.Min(170f, width * .24f);
        GUI.Label(new Rect(width - bar - 22f, 8f, bar, 24f), $"{installed:00} / 26",
            StorehouseHud.Type(14, StorehouseHud.Mint, TextAnchor.MiddleRight));
        StorehouseHud.Fill(new Rect(width - bar - 22f, 35f, bar, 3f), new Color(.21f, .29f, .25f));
        StorehouseHud.Fill(new Rect(width - bar - 22f, 35f, bar * installed / 26f, 3f), StorehouseHud.Mint);
    }

    private void DrawTray(float width, float height)
    {
        float y = height - TrayHeight;
        StorehouseHud.Fill(new Rect(0f, y, width, TrayHeight), StorehouseHud.Ink);
        string message = Complete ? "할아버지   이제 곡식을 비바람에서 지킬 수 있겠구나."
            : Time.unscaledTime < feedbackUntil ? feedback : Hints[step];
        GUI.Label(new Rect(22f, y + 3f, width - 44f, 29f), message, StorehouseHud.Type(15, StorehouseHud.Mint));
        if (Complete)
        {
            if (StorehouseHud.Button(new Rect(22f, y + 38f, 180f, 34f), "F  마당으로 돌아가기", true))
                StorehouseTransition.ReturnToCottage();
        }
        else
        {
            int columns = available.Count + (step == 2 ? 1 : 0);
            float cell = Mathf.Min(165f, (width - 44f) / Mathf.Max(1, columns));
            for (int i = 0; i < available.Count; i++)
            {
                int index = available[i];
                var piece = pieces[index];
                string label = piece.Installed ? $"완료  {piece.Name}"
                    : InstallingTopPlates && !piece.Model.gameObject.activeSelf ? $"다음  {piece.Name}" : $"{i + 1}  {piece.Name}";
                if (selected == index && step >= 4 && step < TopPlateStep)
                    label += $"  {piece.Turns * 90}°";
                var card = new Rect(22f + i * cell, y + 37f, cell - 5f, 33f);
                StorehouseHud.Fill(card, selected == index ? StorehouseHud.Gold : new Color(.11f, .15f, .13f));
                GUI.Label(card, label, StorehouseHud.Type(14, selected == index ? StorehouseHud.Ink : StorehouseHud.Paper,
                    TextAnchor.MiddleCenter));
            }
            if (step == 2 && StorehouseHud.Button(
                new Rect(22f + available.Count * cell, y + 37f, cell - 5f, 33f), "G  장선 일괄 배치"))
                PlaceJoists();
        }
        GUI.Label(new Rect(22f, y + 77f, width - 44f, 28f),
            "부품을 왼쪽 버튼으로 끌어 표시된 자리 가까이 놓기     R 회전     H 기억 보기     방향키·화면 가장자리 이동     휠 확대",
            StorehouseHud.Type(13, StorehouseHud.Paper));
    }

    private void DrawSockets(float width, float height)
    {
        if (InstallingTopPlates)
        {
            if (Event.current.type == EventType.Repaint)
                DrawTopPlateGuide();
            return;
        }
        for (int i = 0; i < available.Count; i++)
        {
            int index = available[i];
            var piece = pieces[index];
            if (piece.Installed)
                continue;
            Vector3 pin = StorehouseHud.Project(view, piece.Pin + Vector3.up * .13f);
            if (pin.z <= 0f)
                continue;
            float x = Mathf.Clamp(pin.x, 26f, width - 26f);
            float y = Mathf.Clamp(pin.y, 109f, height - 140f);
            if (Event.current.type == EventType.Repaint)
                StorehouseHud.Line(new Vector2(x, y), new Vector2(pin.x, pin.y + 7f), StorehouseHud.Mint, 1f);
            bool near = hoveredSocket == index;
            string issue = near ? PlacementIssue(index) : null;
            Color color = issue == null ? StorehouseHud.Mint : StorehouseHud.Gold;
            if (near && Event.current.type == EventType.Repaint)
            {
                Vector3 socket = StorehouseHud.Project(view, piece.Pin);
                StorehouseHud.Ring(new Vector2(socket.x, socket.y), SnapRadiusPixels, color);
                DrawDropHint(new Vector2(x, y), issue ?? "놓으면 배치됩니다", color);
            }
            var badge = new Rect(x - 16f, y - 13f, 32f, 26f);
            StorehouseHud.Fill(badge, near ? color : StorehouseHud.Ink);
            GUI.Label(badge, $"{i + 1}", StorehouseHud.Type(14, near ? StorehouseHud.Ink : StorehouseHud.Paper,
                TextAnchor.MiddleCenter));
        }
    }

    private void DrawTopPlateGuide()
    {
        int index = NextPart();
        if (index < 0)
            return;
        var piece = pieces[index];
        Vector3 goal = StorehouseHud.Project(view, piece.Pin);
        if (goal.z > 0f)
        {
            StorehouseHud.Ring(goal, SnapRadiusPixels, StorehouseHud.Mint);
            string text = hoveredSocket == index ? "지금 놓으세요!" : $"▼  여기에 {piece.Name} 놓기";
            DrawDropHint(goal, text, StorehouseHud.Mint, 19);
        }
        if (!IsDragging)
        {
            Vector3 source = StorehouseHud.Project(view, piece.Supply);
            if (source.z > 0f)
                DrawDropHint(source, $"▼  {piece.Name} 집기", StorehouseHud.Gold, 19);
        }
    }

    private static void DrawDropHint(Vector2 badge, string message, Color color, int fontSize = 14)
    {
        Rect viewport = QuarterViewCameraPan.VisibleViewport();
        var style = StorehouseHud.Type(fontSize, color, TextAnchor.MiddleCenter);
        float width = Mathf.Min(style.CalcSize(new GUIContent(message)).x + 20f, viewport.width);
        float x = Mathf.Clamp(badge.x - width * .5f, viewport.xMin, viewport.xMax - width);
        float y = Mathf.Clamp(badge.y - 50f, Screen.height - viewport.yMax, Screen.height - viewport.yMin - 28f);
        var hint = new Rect(x, y, width, 28f);
        StorehouseHud.Fill(hint, StorehouseHud.Ink);
        GUI.Label(hint, message, style);
    }
}
