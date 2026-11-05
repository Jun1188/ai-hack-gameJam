using UnityEngine;

namespace GrandpaRobot.Presentation
{
    // Generic Mixamo clips contain no cane grip pose. Keep the prop upright while the
    // hand follows those clips; a dedicated cane gait can replace this presentation aid.
    public sealed class WalkingCane : MonoBehaviour
    {
        public Transform groundRoot;
        public Transform grip;
        public float referenceLength=1;
        Vector3 originalScale;
        Renderer[] renderers;
        void Awake(){originalScale=transform.localScale;renderers=GetComponentsInChildren<Renderer>();}
        void LateUpdate()
        {
            if(grip)transform.position=grip.position;
            transform.rotation=Quaternion.Euler(0,groundRoot?groundRoot.eulerAngles.y:0,0);
            if(groundRoot)
                transform.localScale=originalScale*(Mathf.Clamp(transform.position.y-groundRoot.position.y,.6f,1.3f)/referenceLength);
            if(grip&&renderers.Length>0&&transform.childCount>0)
            {
                var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                // FBX pivot is at the model origin, not the handle. Anchor the actual
                // handle bounds, so the prop does not hover next to the wrist.
                var handle=new Vector3(bounds.center.x,bounds.max.y-.025f,bounds.center.z);
                transform.GetChild(0).position+=grip.position-handle;
            }
        }
    }
}
