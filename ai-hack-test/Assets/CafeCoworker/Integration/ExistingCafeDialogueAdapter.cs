using System;
using System.Collections;
using UnityEngine;
using CafeSim;

namespace CafeCoworker.Integration
{
    // Optional one-way adapter. The coworker assemblies do not reference CafeSim.
    public sealed class ExistingCafeDialogueAdapter : MonoBehaviour, INpcReplyProvider
    {
        public BaristaDialogue staffChannel;
        public GuestDialogue guestChannel;
        public ExistingCafeTransportHost host;
        int epoch;
        public string Status => host != null ? host.Status : "자동 답변 준비";
        [Serializable] sealed class ReplyEnvelope { public string speech; }
        void Awake()
        {
            if(staffChannel!=null)staffChannel.Provider=this;
            if(guestChannel!=null)guestChannel.Provider=this;
            if(host==null)host=GetComponentInParent<ExistingCafeTransportHost>();
        }
        public IEnumerator Reply(string system, string prompt, Action<string> success, Action failure)
        {
            if (host == null) { failure(); yield break; }
            int token=++epoch;
            string result = null;
            yield return host.Reply(this,system,prompt,x=>result=x,()=>token==epoch);
            if(token!=epoch)yield break;
            string speech = null;
            if (!string.IsNullOrWhiteSpace(result))
            {
                try { speech = JsonUtility.FromJson<ReplyEnvelope>(result)?.speech; }
                catch (ArgumentException) { }
            }
            if (string.IsNullOrWhiteSpace(speech) || speech.Length > 1800) failure();
            else success(speech.Trim());
        }
        public void Cancel() { epoch++;host?.Cancel(this); }
        void OnDisable() { Cancel(); }
    }
}
