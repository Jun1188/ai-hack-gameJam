using UnityEngine;

[DisallowMultipleComponent]
public sealed class GrandfatherGait : MonoBehaviour
{
    private const float PlantTime = 0.27f;
    private const float LeftStepEnd = 0.61f;
    private const float RightStepEnd = 0.96f;
    private const float CycleDistance = 0.42f;
    private const float CaneLength = 0.84f;
    private const float RestTipForward = 0.06f;

    [SerializeField] private Transform leftArm, rightArm, leftThigh, rightThigh, leftShin, rightShin;
    [SerializeField] private Transform hips;
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private Transform cane;

    private Transform[] joints;
    private Quaternion[] restRotations;
    private Transform leftFoot, rightFoot;
    private Vector3 leftFootRest, rightFootRest, hipRest, gripRest;
    private Vector3 leftStartOffset, rightStartOffset;
    private Quaternion leftFootFacing, rightFootFacing;
    private float phase, blend, idleTime, traveled, cycleCarry;
    private bool walking, wrapped;

    private void Awake()
    {
        leftFoot = leftShin != null && leftShin.childCount > 0 ? leftShin.GetChild(0) : null;
        rightFoot = rightShin != null && rightShin.childCount > 0 ? rightShin.GetChild(0) : null;
        joints = new[] { leftArm, rightArm, cane != null ? cane.parent.parent : null,
            leftThigh, rightThigh, leftShin, rightShin, leftFoot, rightFoot };
        restRotations = new Quaternion[joints.Length];
        for (int i = 0; i < joints.Length; i++)
            if (joints[i] != null)
                restRotations[i] = joints[i].localRotation;

        if (hips != null) hipRest = hips.localPosition;
        if (leftFoot != null)
        {
            leftFootRest = transform.InverseTransformPoint(leftFoot.position);
            leftFootFacing = Quaternion.Inverse(transform.rotation) * leftFoot.rotation;
        }
        if (rightFoot != null)
        {
            rightFootRest = transform.InverseTransformPoint(rightFoot.position);
            rightFootFacing = Quaternion.Inverse(transform.rotation) * rightFoot.rotation;
        }
        if (cane != null)
            gripRest = transform.InverseTransformPoint(cane.position) + new Vector3(0.07f, 0f, 0.17f);
    }

    // Advance the pose and the controller from the same cycle so the body waits for the cane.
    public float Advance(bool wantsToWalk, float walkSpeed, float deltaTime)
    {
        blend = Mathf.MoveTowards(blend, wantsToWalk ? 1f : 0f, deltaTime * 7f);
        if (!wantsToWalk)
        {
            walking = false;
            return 0f;
        }

        if (!walking)
        {
            phase = traveled = 0f;
            if (leftFoot != null)
                leftStartOffset = transform.InverseTransformPoint(leftFoot.position) - leftFootRest;
            if (rightFoot != null)
                rightStartOffset = transform.InverseTransformPoint(rightFoot.position) - rightFootRest;
            walking = true;
        }

        float oldProgress = TravelProgress(phase);
        float nextPhase = phase + deltaTime * walkSpeed / CycleDistance * (RightStepEnd - PlantTime);
        wrapped = nextPhase >= 1f;
        phase = Mathf.Repeat(nextPhase, 1f);
        if (wrapped)
            leftStartOffset = rightStartOffset = Vector3.zero;
        cycleCarry = wrapped ? TravelProgress(phase) * CycleDistance : 0f;
        return (TravelProgress(phase) - oldProgress + Mathf.Floor(nextPhase)) * CycleDistance;
    }

    public void RecordTravel(float actualDistance, float requestedDistance)
    {
        float advance = Mathf.Clamp(actualDistance, 0f, requestedDistance);
        traveled = wrapped ? Mathf.Min(cycleCarry, advance) : traveled + advance;
        wrapped = false;
    }

    private static float TravelProgress(float cycle)
    {
        // Two eased weight transfers, one for each foot.
        return 0.5f * (Ease(PlantTime, LeftStepEnd, cycle) + Ease(LeftStepEnd, RightStepEnd, cycle));
    }

    private static float Ease(float start, float end, float value)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, value));
    }

    private void LateUpdate()
    {
        idleTime += Time.deltaTime;
        for (int i = 0; i < joints.Length; i++)
            if (joints[i] != null)
                joints[i].localRotation = restRotations[i];

        if (idleClip != null && idleClip.length > 0f)
            idleClip.SampleAnimation(gameObject, Mathf.Repeat(idleTime, idleClip.length));

        if (hips != null)
            hips.localPosition = hipRest + hips.parent.InverseTransformVector(Vector3.down * (0.048f * blend));

        if (blend > 0f)
        {
            if (leftArm != null)
                leftArm.rotation = Quaternion.AngleAxis(8f * Mathf.Sin(phase * 2f * Mathf.PI) * blend,
                    transform.right) * leftArm.rotation;

            StepFoot(leftThigh, leftShin, leftFoot, leftFootRest, leftStartOffset, leftFootFacing, PlantTime, LeftStepEnd);
            StepFoot(rightThigh, rightShin, rightFoot, rightFootRest, rightStartOffset, rightFootFacing, LeftStepEnd, RightStepEnd);
        }

        if (cane != null)
            PoseCane();
    }

    private void StepFoot(Transform thigh, Transform shin, Transform foot, Vector3 rest, Vector3 startOffset, Quaternion restFacing, float start, float end)
    {
        if (thigh == null || shin == null || foot == null)
            return;

        float swing = Mathf.InverseLerp(start, end, phase);
        float swingProgress = Ease(start, end, phase);
        float stride = CycleDistance * swingProgress - traveled;
        rest += startOffset * (1f - swingProgress);
        Vector3 target = transform.TransformPoint(rest + new Vector3(0f, Lift(swing, 0.075f), stride));
        Vector3 idleFoot = foot.position;
        Quaternion idleRotation = foot.rotation;
        Vector3 kneePole = transform.forward;
        SolveLimb(thigh, shin, foot, Vector3.Lerp(idleFoot, target, walking ? 1f : blend), kneePole);
        foot.rotation = Quaternion.Slerp(idleRotation,
            transform.rotation * Quaternion.Euler(-12f * Lift(swing, 1f), 0f, 0f) * restFacing, blend);
    }

    private void PoseCane()
    {
        float swing = Mathf.InverseLerp(0f, PlantTime, phase);
        float plantForward = RestTipForward + CycleDistance * Ease(0f, PlantTime, phase) - traveled;
        Vector3 tip = transform.TransformPoint(new Vector3(gripRest.x + 0.035f,
            0f,
            Mathf.Lerp(RestTipForward, plantForward, blend)));

        if (Physics.Raycast(tip + Vector3.up * 0.25f, Vector3.down, out RaycastHit ground,
            0.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && ground.normal.y > 0.65f)
            tip.y = ground.point.y;
        tip.y += 0.006f + Lift(swing, 0.09f) * blend;

        // Aim the shaft from the planted tip toward the hand's comfortable reach.
        Vector3 grip = transform.TransformPoint(gripRest);
        Vector3 handle = tip + (grip - tip).normalized * CaneLength;
        Transform hand = cane.parent;
        SolveLimb(rightArm, hand.parent, hand, handle, transform.right - transform.forward);
        cane.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
        cane.rotation = Quaternion.FromToRotation(Vector3.up, cane.position - tip) * cane.rotation;
    }

    private static float Lift(float progress, float height)
    {
        float arc = Mathf.Sin(Mathf.PI * progress);
        return arc * arc * height;
    }

    private static void SolveLimb(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
    {
        if (upper == null || lower == null)
            return;

        Vector3 origin = upper.position;
        Vector3 direction = (target - origin).normalized;
        if (direction == Vector3.zero)
            return;

        float upperLength = Vector3.Distance(origin, lower.position);
        float lowerLength = Vector3.Distance(lower.position, end.position);
        float reach = Mathf.Clamp(Vector3.Distance(origin, target),
            Mathf.Abs(upperLength - lowerLength) + 0.001f, upperLength + lowerLength - 0.001f);
        float along = (upperLength * upperLength - lowerLength * lowerLength + reach * reach) / (2f * reach);
        float bend = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        Vector3 joint = origin + direction * along + Vector3.ProjectOnPlane(pole, direction).normalized * bend;
        upper.rotation = Quaternion.FromToRotation(lower.position - origin, joint - origin) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(end.position - lower.position,
            origin + direction * reach - lower.position) * lower.rotation;
    }
}


