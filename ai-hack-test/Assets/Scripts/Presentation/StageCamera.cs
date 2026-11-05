using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandpaRobot.Presentation
{
    public sealed class StageCamera : MonoBehaviour
    {
        public Camera view;
        public float width=24,depth=20,azimuth=45,zoom=.98f;
        public float orbitDegreesPerSecond=60;
        public bool inputBlocked;
        public float focusHeight=.35f;
        public void ResetStage(float w,float d,float height=.35f){width=w;depth=d;focusHeight=height;azimuth=45;zoom=.98f;}
        void LateUpdate()
        {
            var keyboard=Keyboard.current;
            if(!inputBlocked&&keyboard!=null)
            {
                if(keyboard.rKey.isPressed)azimuth=Mathf.Repeat(azimuth+(keyboard.shiftKey.isPressed?-1:1)*orbitDegreesPerSecond*Time.deltaTime,360);
                if(keyboard.equalsKey.isPressed||keyboard.numpadPlusKey.isPressed)zoom-=Time.deltaTime*.3f;
                if(keyboard.minusKey.isPressed||keyboard.numpadMinusKey.isPressed)zoom+=Time.deltaTime*.3f;
            }
            zoom=Mathf.Clamp(zoom,.78f,1);Apply();
        }
        public static float OrbitZoomLimit(float w,float d,float aspect,float height)
        {
            // Every viewport corner must remain within the inscribed circle at ANY yaw.
            // Solving the ray/ground footprint once removes angle-dependent zoom pumping.
            float sine=14f/Mathf.Sqrt(14*14+18*18);
            float offset=height*18f/14f;
            float radius=Mathf.Max(.5f,Mathf.Min(w,d)/2-.3f);
            float a=aspect*aspect+1/(sine*sine),b=2*offset/sine,c=offset*offset-radius*radius;
            return Mathf.Max(.1f,(-b+Mathf.Sqrt(Mathf.Max(0,b*b-4*a*c)))/(2*a));
        }
        public void Apply()
        {
            if(!view)view=GetComponent<Camera>();
            var center=new Vector3(0,focusHeight,0);
            transform.position=center+Quaternion.Euler(0,azimuth,0)*new Vector3(0,14,-18);
            transform.LookAt(center);view.orthographic=true;
            view.orthographicSize=OrbitZoomLimit(width,depth,Mathf.Max(.1f,view.aspect),focusHeight)*Mathf.Clamp(zoom,.78f,1);
            view.nearClipPlane=.1f;view.farClipPlane=100;
        }
    }
}
