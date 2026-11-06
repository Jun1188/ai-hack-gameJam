using System;
using System.Collections;
using UnityEngine;
using CafeSim;

namespace CafeCoworker.Integration
{
    // One inference runtime per scene. Channel cancellation only aborts that channel's request.
    public sealed class ExistingCafeTransportHost : MonoBehaviour
    {
        LocalCafeModel model;
        object owner;
        Coroutine boot;
        public string Status=>model!=null?model.Status:"응답 준비 중";
        void Awake()
        {
            model=gameObject.AddComponent<LocalCafeModel>();
            model.ConfigureGemini();boot=StartCoroutine(model.Boot());
        }
        public IEnumerator Reply(object channel,string system,string prompt,Action<string> success,Func<bool> current)
        {
            while(owner!=null) {if(!current())yield break;yield return null;}
            if(!current()||model==null)yield break;
            owner=channel;
            try {yield return model.Generate(prompt,success,x=>{},system,256,prompt);}
            finally {if(ReferenceEquals(owner,channel))owner=null;}
        }
        public void Cancel(object channel)
        {
            if(!ReferenceEquals(owner,channel))return;
            model?.CancelRequest();owner=null;
        }
        public void Reboot()
        {
            if(model==null)return;
            owner=null;model.CancelRequest();
            if(boot!=null)StopCoroutine(boot);
            model.Shutdown();boot=StartCoroutine(model.Boot());
        }
        void OnDisable(){if(boot!=null)StopCoroutine(boot);model?.Shutdown();owner=null;}
    }
}
