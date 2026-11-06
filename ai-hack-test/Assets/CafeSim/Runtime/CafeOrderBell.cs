using UnityEngine;

namespace CafeSim
{
    // The customer taps the brass counter bell once on reaching the counter, as the order bubble appears.
    public sealed class CafeOrderBell : MonoBehaviour
    {
        public CafeSession session;
        public AudioSource sound;
        public AudioClip ding;
        public Transform button;
        int rungFor;
        float press;
        Vector3 buttonRest;

        void Awake(){if(button!=null)buttonRest=button.position;}

        void Update()
        {
            if(session==null)return;
            var c=session.Selected;var body=session.BodyOf(c);
            if(!session.Paused&&c!=null&&c.State==VisitState.Active&&c.Id!=rungFor&&body!=null&&
                (body.position-session.servicePoint.position).sqrMagnitude<.04f)
            {
                rungFor=c.Id;press=1;
                if(sound!=null&&ding!=null)sound.PlayOneShot(ding);
            }
            if(button==null)return;
            press=Mathf.MoveTowards(press,0,Time.deltaTime*5);
            button.position=buttonRest-Vector3.up*.006f*Mathf.Sin(press*Mathf.PI);
        }
    }
}
