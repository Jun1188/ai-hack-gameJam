using System;
using System.Collections.Generic;
using UnityEngine;

namespace CafeCoworker
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class BezierWorkMotor : MonoBehaviour
    {
        public Bounds walkBounds = new Bounds(Vector3.zero, new Vector3(12, 3, 12));
        public float speed = 1.35f, clearance = .34f, cellSize = .55f, acceleration = 2.6f;
        public Transform visual;
        public Animator animator;
        public bool Arrived { get; private set; } = true;
        public string Status { get; private set; } = "";
        public float ActualSpeed { get; private set; }
        public Vector3 Destination { get; private set; }
        public bool Active => active;
        CharacterController body;
        readonly List<Vector3> route = new List<Vector3>();
        readonly RaycastHit[] hits = new RaycastHit[64];
        int point;
        float velocity, nextPlan;
        bool active;
        Quaternion destinationFacing;
        Vector3 previous;
        void Awake()
        {
            body = GetComponent<CharacterController>(); previous = transform.position;
            InitializeAnimation();
        }
        public void InitializeAnimation()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.SetBool("Walking", false);
            animator.Play("Idle", 0, 0);
            animator.Update(0);
            foreach(var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.updateWhenOffscreen=true;
                skin.forceMatrixRecalculationPerRender=true;
            }
        }
        public void SetDestination(Vector3 destination, Quaternion facing)
        {
            Destination = destination; destinationFacing = facing;
            Arrived = false; active = true; route.Clear(); velocity = 0; nextPlan = 0;
        }
        public void Stop()
        { active = false; route.Clear(); velocity = 0; ActualSpeed = 0; Status = ""; SetAnimation(0); }
        public bool Clear(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from; delta.y = 0;
            Vector3 low = from + Vector3.up * (clearance + .06f), high = from + Vector3.up * (1.75f - clearance);
            int n = Physics.CapsuleCastNonAlloc(low, high, clearance, delta.normalized, hits,
                delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (hits[i].collider != null && !hits[i].collider.transform.IsChildOf(transform)) return false;
            // CapsuleCast does not report all starting overlaps.
            foreach (var c in Physics.OverlapCapsule(low, high, clearance, ~0, QueryTriggerInteraction.Ignore))
                if (!c.transform.IsChildOf(transform)) return false;
            return n < hits.Length;
        }
        void Update()
        {
            if (body == null) return;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (!active) { SetAnimation(0); return; }
            var flat = Destination - transform.position; flat.y = 0;
            if (flat.magnitude < .12f)
            {
                velocity = Mathf.MoveTowards(velocity, 0, acceleration * dt);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, destinationFacing, 210 * dt);
                Arrived = Quaternion.Angle(transform.rotation, destinationFacing) < 4;
                Status = "";
                if (Arrived) active = false;
                ActualSpeed = 0; SetAnimation(0); return;
            }
            if ((route.Count == 0 || point >= route.Count) && Time.time >= nextPlan)
            {
                route.Clear(); route.AddRange(Plan(transform.position, Destination)); point = 0; nextPlan = Time.time + .75f;
            }
            while (point < route.Count && FlatDistance(transform.position, route[point]) < .09f) point++;
            Vector3 target = point < route.Count ? route[point] : transform.position;
            var direction = target - transform.position; direction.y = 0;
            bool clear = direction.magnitude > .001f && Clear(transform.position, target);
            if (!clear)
            {
                velocity = Mathf.MoveTowards(velocity, 0, acceleration * 2 * dt);
                Status = "통로가 열리기를 기다리고 있어요";
                if (Time.time >= nextPlan) { route.Clear(); nextPlan = Time.time + .6f; }
                SetAnimation(0); ActualSpeed = 0; return;
            }
            Status = "";
            float desired = Mathf.Min(speed, Mathf.Sqrt(2 * acceleration * flat.magnitude));
            float turn = Vector3.Dot(transform.forward, direction.normalized);
            desired *= Mathf.Lerp(.35f, 1, Mathf.InverseLerp(-.2f, .95f, turn));
            velocity = Mathf.MoveTowards(velocity, desired, acceleration * dt);
            Vector3 step = direction.normalized * Mathf.Min(direction.magnitude, velocity * dt);
            previous = transform.position;
            // Validate the actual step too: a player can enter the old route between replans.
            if (Clear(previous, previous + step)) body.Move(step + Vector3.down * 1.5f * dt);
            var travel = transform.position - previous; travel.y = 0;
            ActualSpeed = travel.magnitude / Mathf.Max(.001f, dt);
            if (travel.sqrMagnitude > .000001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(travel), 1 - Mathf.Exp(-8 * dt));
            SetAnimation(ActualSpeed);
        }
        void SetAnimation(float value)
        {
            if (animator == null) return;
            animator.SetBool("Walking", value > .04f);
            animator.speed = value > .04f ? Mathf.Clamp(value / 1.35f, .4f, 1.3f) : 1;
        }
        public List<Vector3> Plan(Vector3 start, Vector3 end)
        {
            if (!walkBounds.Contains(new Vector3(end.x, walkBounds.center.y, end.z))) return new List<Vector3>();
            if (Clear(start, end)) return SampleBezier(start, start + (end - start) / 3, end - (end - start) / 3, end);
            float y = start.y;
            int nx = Mathf.CeilToInt(walkBounds.size.x / cellSize) + 1;
            int nz = Mathf.CeilToInt(walkBounds.size.z / cellSize) + 1;
            if (nx * nz > 6000) return new List<Vector3>();
            Vector3 min = walkBounds.min;
            Vector3 Pos(int id) => new Vector3(min.x + (id % nx) * cellSize, y, min.z + (id / nx) * cellSize);
            int Id(Vector3 p) => Mathf.Clamp(Mathf.RoundToInt((p.z - min.z) / cellSize), 0, nz - 1) * nx +
                Mathf.Clamp(Mathf.RoundToInt((p.x - min.x) / cellSize), 0, nx - 1);
            int first = Id(start), last = Id(end);
            var open = new List<int> { first };
            var came = new Dictionary<int, int>();
            var cost = new Dictionary<int, float> { [first] = 0 };
            var closed = new HashSet<int>();
            while (open.Count > 0 && closed.Count < nx * nz)
            {
                int current = open[0]; float best = float.MaxValue;
                foreach (int id in open)
                {
                    float score = cost[id] + FlatDistance(Pos(id), end);
                    if (score < best) { best = score; current = id; }
                }
                open.Remove(current);
                if (current == last)
                {
                    var raw = new List<Vector3> { end };
                    while (came.TryGetValue(current, out int prev)) { raw.Add(Pos(current)); current = prev; }
                    raw.Add(start); raw.Reverse();
                    // Connect the exact start/end, not just the quantized grid cells.
                    for (int i = 1; i < raw.Count; i++) if (!Clear(raw[i - 1], raw[i])) return new List<Vector3>();
                    return Smooth(raw);
                }
                closed.Add(current);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int x = current % nx + dx, z = current / nx + dz;
                        if (x < 0 || x >= nx || z < 0 || z >= nz) continue;
                        int next = z * nx + x;
                        if (closed.Contains(next)) continue;
                        Vector3 a = current == first ? start : Pos(current), b = next == last ? end : Pos(next);
                        if (!Clear(a, b)) continue;
                        float nc = cost[current] + FlatDistance(a, b);
                        if (cost.TryGetValue(next, out float old) && nc >= old) continue;
                        came[next] = current; cost[next] = nc;
                        if (!open.Contains(next)) open.Add(next);
                    }
            }
            return new List<Vector3>();
        }
        List<Vector3> Smooth(List<Vector3> raw)
        {
            var shortPath = new List<Vector3> { raw[0] };
            for (int i = 0; i < raw.Count - 1;)
            {
                int next = raw.Count - 1;
                while (next > i + 1 && !Clear(raw[i], raw[next])) next--;
                shortPath.Add(raw[next]); i = next;
            }
            var result = new List<Vector3>();
            for (int i = 0; i < shortPath.Count - 1; i++)
            {
                Vector3 a = shortPath[i], b = shortPath[i + 1];
                Vector3 before = i > 0 ? shortPath[i - 1] : a;
                Vector3 after = i + 2 < shortPath.Count ? shortPath[i + 2] : b;
                float handle = Mathf.Min(.65f, Vector3.Distance(a, b) / 3);
                var segment = SampleBezier(a, a + (b - before).normalized * handle,
                    b - (after - a).normalized * handle, b);
                bool safe = true; Vector3 prior = a;
                foreach (var p in segment) { if (!Clear(prior, p)) { safe = false; break; } prior = p; }
                result.AddRange(safe ? segment : SampleBezier(a, Vector3.Lerp(a, b, .33f), Vector3.Lerp(a, b, .67f), b));
            }
            return result;
        }
        public static List<Vector3> SampleBezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int samples = Mathf.Max(8, Mathf.CeilToInt((Vector3.Distance(a,b)+Vector3.Distance(b,c)+Vector3.Distance(c,d)) / .09f));
            var points = new List<Vector3>(samples);
            for (int i = 1; i <= samples; i++)
            {
                float t = (float)i / samples, u = 1 - t;
                points.Add(u*u*u*a + 3*u*u*t*b + 3*u*t*t*c + t*t*t*d);
            }
            return points;
        }
        static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a,b); }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.3f, .8f, .7f);
            for (int i = 1; i < route.Count; i++) Gizmos.DrawLine(route[i - 1] + Vector3.up * .1f, route[i] + Vector3.up * .1f);
            Gizmos.DrawWireCube(walkBounds.center, walkBounds.size);
        }
    }
}
