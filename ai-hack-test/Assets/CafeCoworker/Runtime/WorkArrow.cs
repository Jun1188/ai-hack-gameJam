using UnityEngine;

namespace CafeCoworker
{
    // A scene-local world marker. Its bottom tip remains over the work surface as size changes.
    public sealed class WorkArrow : MonoBehaviour
    {
        public CoopSession session;
        public float size = 1.6f, tipGap = .22f, bobAmplitude = .055f;
        public Transform arrow;
        WorkItem previous;
        void LateUpdate()
        {
            var task = session.Scheduler?.Next(Worker.Player);
            bool visible = task != null && !session.player.MenuOpen;
            arrow.gameObject.SetActive(visible);
            if (!visible) return;
            var station = session.Station(task.Spec.station);
            // The generated mesh tip is local y=0. Scaling grows upward rather than into the target.
            arrow.localScale = Vector3.one * size;
            arrow.position = station.workSurface.position + Vector3.up *
                (tipGap + bobAmplitude + Mathf.Sin(Time.time * 3) * bobAmplitude);
            arrow.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(
                session.player.eyes.transform.position - arrow.position, Vector3.up).normalized);
            previous = task;
        }
    }
}
