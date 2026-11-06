using UnityEngine;

namespace CafeSim
{
    // Swings the glass entry door inward while a customer walks through it. The shop bells
    // above the door jingle as it opens and again when it falls shut.
    public sealed class CafeEntryDoor : MonoBehaviour
    {
        public CafeSession session;
        // Pivot on the hinge jamb; the door leaf (glass, rails, pull, sign) is parented under it.
        public Transform hinge;
        public Transform bells;
        public AudioSource sound;
        public AudioClip openJingle, closeJingle;
        public float leafWidth=1.15f, openAngle=-95, openRadius=1.35f, swingSeconds=.55f;
        float swing, ring;
        bool open;
        Vector3 center;

        void Awake(){if(hinge!=null)center=hinge.position+hinge.right*leafWidth*.5f;}

        void Update()
        {
            if(hinge==null)return;
            bool wanted=CustomerNear();
            if(wanted&&!open&&swing<=0){Play(openJingle);ring=1;}
            open=wanted;
            float before=swing;
            swing=Mathf.MoveTowards(swing,open?1:0,Time.deltaTime/swingSeconds);
            if(before>0&&swing<=0){Play(closeJingle);ring=.6f;}
            hinge.localRotation=Quaternion.Euler(0,openAngle*Mathf.SmoothStep(0,1,swing),0);
            if(bells!=null)
            {
                ring=Mathf.MoveTowards(ring,0,Time.deltaTime*.9f);
                float t=Time.time*17;
                bells.localRotation=Quaternion.Euler(Mathf.Sin(t)*28*ring,0,Mathf.Sin(t*1.3f+1)*18*ring);
            }
        }
        bool CustomerNear()
        {
            if(session==null)return false;
            foreach(var body in session.Bodies)
            {
                if(body==null)continue;
                Vector3 d=body.position-center;d.y=0;
                if(d.sqrMagnitude<openRadius*openRadius)return true;
            }
            return false;
        }
        void Play(AudioClip clip){if(sound!=null&&clip!=null)sound.PlayOneShot(clip);}
    }
}
