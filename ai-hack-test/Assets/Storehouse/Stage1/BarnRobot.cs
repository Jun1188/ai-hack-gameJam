using System.Collections;
using UnityEngine;

// The child robot. It only knows how to move, carry, brace and swing a hammer;
// BarnStage decides what each command means and whether it is allowed.
public sealed class BarnRobot : MonoBehaviour
{
    public const float CarryScale = .34f;
    private const float WalkSpeed = 3.1f;
    private const float TurnSpeed = 540f;

    private Transform body, head, leftArm, rightArm, hammer, carryPoint;
    private Transform[] legs;
    private float walkPhase, swing, speechUntil;
    private bool bracing;

    public BarnItem Carrying { get; private set; }
    public string Speech { get; private set; } = "";
    public bool Speaking => Time.unscaledTime < speechUntil;
    public Vector3 HeadTop => transform.position + Vector3.up * 1.45f;

    public static BarnRobot Create(Transform parent, Vector3 position, Material shell, Material trim, Material glow)
    {
        var robot = new GameObject("Robot NPC | 아이 로봇").AddComponent<BarnRobot>();
        robot.transform.SetParent(parent, false);
        robot.transform.position = position;
        robot.BuildBody(shell, trim, glow);
        return robot;
    }

    private void BuildBody(Material shell, Material trim, Material glow)
    {
        body = StorehouseParts.Group(transform, "Body", new Vector3(0f, .58f, 0f));
        StorehouseParts.Box(body, "Torso", Vector3.zero, new Vector3(.46f, .46f, .34f), shell);
        StorehouseParts.Box(body, "Belly plate", new Vector3(0f, -.02f, .175f), new Vector3(.3f, .24f, .02f), trim);
        StorehouseParts.Box(body, "Belly light", new Vector3(0f, .06f, .19f), new Vector3(.07f, .05f, .02f), glow);

        head = StorehouseParts.Group(body, "Head", new Vector3(0f, .43f, 0f));
        StorehouseParts.Box(head, "Head shell", Vector3.zero, new Vector3(.52f, .38f, .42f), shell);
        StorehouseParts.Box(head, "Face plate", new Vector3(0f, -.01f, .215f), new Vector3(.42f, .26f, .02f), trim);
        foreach (int side in new[] { -1, 1 })
        {
            StorehouseParts.Box(head, "Eye", new Vector3(side * .1f, .02f, .23f), new Vector3(.08f, .1f, .02f), glow);
            StorehouseParts.Box(head, "Ear bolt", new Vector3(side * .28f, 0f, 0f), new Vector3(.05f, .12f, .12f), trim);
        }
        StorehouseParts.Box(head, "Antenna", new Vector3(0f, .27f, 0f), new Vector3(.025f, .16f, .025f), trim);
        StorehouseParts.Box(head, "Antenna bulb", new Vector3(0f, .37f, 0f), new Vector3(.07f, .07f, .07f), glow);

        leftArm = Arm("Left arm", -1, shell, trim);
        rightArm = Arm("Right arm", 1, shell, trim);
        hammer = StorehouseParts.Group(rightArm, "Hammer", new Vector3(0f, -.36f, .06f));
        StorehouseParts.Box(hammer, "Handle", new Vector3(0f, 0f, .12f), new Vector3(.035f, .035f, .26f), trim);
        StorehouseParts.Box(hammer, "Head", new Vector3(0f, 0f, .26f), new Vector3(.08f, .14f, .07f), trim);
        hammer.gameObject.SetActive(false);

        legs = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            legs[i] = StorehouseParts.Group(transform, "Leg", new Vector3(i == 0 ? -.12f : .12f, .36f, 0f));
            StorehouseParts.Box(legs[i], "Leg", new Vector3(0f, -.17f, 0f), new Vector3(.12f, .34f, .14f), trim);
            StorehouseParts.Box(legs[i], "Foot", new Vector3(0f, -.33f, .04f), new Vector3(.16f, .06f, .22f), shell);
        }
        carryPoint = StorehouseParts.Group(transform, "Carry point", new Vector3(0f, 1.62f, 0f));
    }

    private Transform Arm(string name, int side, Material shell, Material trim)
    {
        Transform arm = StorehouseParts.Group(body, name, new Vector3(side * .3f, .16f, 0f));
        StorehouseParts.Box(arm, "Upper arm", new Vector3(0f, -.15f, 0f), new Vector3(.1f, .3f, .1f), trim);
        StorehouseParts.Box(arm, "Hand", new Vector3(0f, -.33f, 0f), new Vector3(.13f, .1f, .13f), shell);
        return arm;
    }

    public void Say(string text, float seconds = 0f)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        Speech = text.Trim();
        speechUntil = Time.unscaledTime + (seconds > 0f ? seconds : Mathf.Clamp(2.2f + Speech.Length * .09f, 2.5f, 7f));
    }

    public IEnumerator MoveTo(Vector3 destination, float stopDistance = .05f)
    {
        destination.y = transform.position.y;
        while (true)
        {
            Vector3 delta = destination - transform.position;
            delta.y = 0f;
            if (delta.magnitude <= stopDistance)
                break;
            Face(delta, Time.deltaTime);
            transform.position += Vector3.ClampMagnitude(delta, WalkSpeed * Time.deltaTime);
            walkPhase += Time.deltaTime * 11f;
            yield return null;
        }
        walkPhase = 0f;
    }

    public IEnumerator FaceTowards(Vector3 point)
    {
        Vector3 delta = point - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude < .0001f)
            yield break;
        Quaternion goal = Quaternion.LookRotation(delta);
        while (Quaternion.Angle(transform.rotation, goal) > 2f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, TurnSpeed * Time.deltaTime);
            yield return null;
        }
    }

    // Hops along an arc, used to climb onto the roof for hammering and to come back down.
    public IEnumerator Hop(Vector3 destination, float height = 1.2f, float duration = .55f)
    {
        Vector3 start = transform.position;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            transform.position = Vector3.Lerp(start, destination, k) + Vector3.up * (Mathf.Sin(Mathf.PI * k) * height);
            yield return null;
        }
        transform.position = destination;
    }

    private void Face(Vector3 direction, float deltaTime)
    {
        if (direction.sqrMagnitude < .0001f)
            return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction),
            TurnSpeed * deltaTime);
    }

    public void PickUp(BarnItem item)
    {
        Carrying = item;
        item.Model.SetParent(carryPoint, true);
    }

    public BarnItem Release(Transform newParent)
    {
        BarnItem item = Carrying;
        Carrying = null;
        if (item != null)
            item.Model.SetParent(newParent, true);
        return item;
    }

    public void SetBracing(bool on) => bracing = on;

    public void ShowHammer(bool on) => hammer.gameObject.SetActive(on);

    public void Strike() => swing = 1f;

    public Vector3 CarryPosition => carryPoint.position;

    private void LateUpdate()
    {
        float bob = walkPhase > 0f ? Mathf.Abs(Mathf.Sin(walkPhase)) * .05f : Mathf.Sin(Time.time * 2.2f) * .012f;
        body.localPosition = new Vector3(0f, .58f + bob, 0f);
        for (int i = 0; i < legs.Length; i++)
            legs[i].localRotation = Quaternion.Euler(walkPhase > 0f ? Mathf.Sin(walkPhase + i * Mathf.PI) * 28f : 0f, 0f, 0f);
        head.localRotation = Quaternion.Euler(0f, Speaking ? Mathf.Sin(Time.time * 7f) * 6f : 0f, 0f);

        swing = Mathf.MoveTowards(swing, 0f, Time.deltaTime * 4.5f);
        float carryLift = Carrying != null ? -165f : 0f;
        float brace = bracing ? -95f : 0f;
        float hammerArm = hammer.gameObject.activeSelf ? -150f + Mathf.Sin(swing * Mathf.PI) * 120f : 0f;
        float stride = walkPhase > 0f && Carrying == null ? Mathf.Sin(walkPhase) * 22f : 0f;
        leftArm.localRotation = Quaternion.Euler((carryLift != 0f ? carryLift : brace) - stride, 0f, 0f);
        rightArm.localRotation = Quaternion.Euler(hammerArm != 0f ? hammerArm : (carryLift != 0f ? carryLift : brace) + stride, 0f, 0f);
    }
}
