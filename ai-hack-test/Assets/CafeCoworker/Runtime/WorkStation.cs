using UnityEngine;

namespace CafeCoworker
{
    public sealed class WorkStation : MonoBehaviour
    {
        public string stationId, displayName;
        public Transform approach, workSurface;
        public Vector3 Position => approach != null ? approach.position : transform.position;
        public Quaternion Facing => approach != null ? approach.rotation : transform.rotation;
    }
}
