#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace CafeSim
{
    // Explicit command-line opt-in only. Never attached to a scene or run in normal play.
    public sealed class CafeChatSmoke : MonoBehaviour
    {
        [Serializable] sealed class Evidence
        {
            public bool passed,modelReady,databaseReply,generatedReply,contextReply,offTopicClosed,cacheIsolated,cancelled,selectionIsolated,served;
            public float databaseSeconds,generatedSeconds,contextSeconds;
            public string error,payloadPath,backend;
        }
        Evidence result=new Evidence();string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--cafe-chat-smoke");
            if(index<0)return;
            var test=new GameObject("Explicit cafe chat smoke").AddComponent<CafeChatSmoke>();
            test.output=index+1<args.Length?args[index+1]:Path.Combine(Application.persistentDataPath,"cafe-chat-smoke.json");
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;
            yield return null;
            var s=FindFirstObjectByType<CafeSession>();
            if(s==null){Finish("Cafe session missing");yield break;}
            s.queueSettings.activeTimeoutSeconds=1800;s.queueSettings.arrivalSeconds=10000;s.queueSettings.orderSeconds=10000;
            float deadline=Time.realtimeSinceStartup+240;
            while(!s.model.Ready&&s.model.Error==null&&Time.realtimeSinceStartup<deadline)yield return null;
            result.modelReady=s.model.Ready;result.payloadPath=LocalCafeModel.PayloadPath;
            result.backend=s.model.UseApi?"api":"local";string expectedSource=s.model.UseApi?"api":"model";
            if(!result.modelReady){Finish(s.model.Error??"Model boot timeout");yield break;}
            s.Queue.Tick(31);var c=s.Selected;
            if(c==null){Finish("Customer missing");yield break;}
            float retrievalStart=Time.realtimeSinceStartup;
            string automatic=s.Conversation.AutomaticReply(c,"더 자세히 들려주세요",s.Queue.Knowledge,s.Menu,s.automaticReplyThreshold,out var automaticSource,out _);
            result.databaseReply=automaticSource=="database"&&CafePrompts.ValidSpeech(automatic);result.databaseSeconds=Time.realtimeSinceStartup-retrievalStart;
            string topic=c.Person.smalltalkTopics.Split(',')[0];
            string prompt=s.Conversation.Prompt(c,topic+" 이야기",s.Queue.Knowledge);
            s.Conversation.Remember(prompt,"테스트 답변이에요.");
            result.cacheIsolated=s.Conversation.TryCached(prompt,out _)&&!s.Conversation.TryCached(prompt+"다른 문맥",out _);
            if(!s.SendChat(topic+" 이야기를 지금 기분과 연결해서 짧게 들려주세요.")){Finish(s.ChatError??"Live input rejected");yield break;}
            deadline=Time.realtimeSinceStartup+310;while(s.ChatBusy&&Time.realtimeSinceStartup<deadline)yield return null;
            result.generatedReply=s.LastReplySource==expectedSource;result.generatedSeconds=s.LastReplySeconds;
            if(!result.generatedReply){Finish(s.ChatError??"Live generation missing");yield break;}
            if(!s.SendChat(topic+"에 대해 방금 제게 하신 답변을 이어서 한 문장 더 말해주세요.")){Finish("Context input rejected");yield break;}
            deadline=Time.realtimeSinceStartup+310;while(s.ChatBusy&&Time.realtimeSinceStartup<deadline)yield return null;
            result.contextReply=s.LastReplySource==expectedSource;result.contextSeconds=s.LastReplySeconds;
            // Exercise in-flight cancellation and switching; no old reply may leak to a new selection.
            s.SendChat(topic+"에 대해 조금 더 말씀해 주세요.");s.CancelChat();result.cancelled=!s.ChatBusy;
            int oldHistory=c.History.Count;var other=s.Queue.Admit(false);
            if(other!=null){s.SelectCustomer(other.Id);result.selectionIsolated=s.Chat.TrueForAll(x=>!x.StartsWith("나\n",StringComparison.Ordinal));s.SelectCustomer(c.Id);}
            s.SendChat("양자역학 슈뢰딩거 방정식을 풀어줘");
            deadline=Time.realtimeSinceStartup+310;while(s.ChatBusy&&Time.realtimeSinceStartup<deadline)yield return null;
            result.offTopicClosed=s.ChatClosed&&s.LastReplySource=="closing"&&!s.ChatBusy&&c.History.Count>oldHistory;
            s.ReopenChat();if(c.State==VisitState.Inactive)s.Queue.AcceptOrder();
            if(c.State==VisitState.Active){var m=Array.Find(s.Menu.items,x=>x.Supports("HOT")||x.Supports("ICE"));s.Serve(m.id,m.Supports("HOT")?"HOT":"ICE");result.served=c.State==VisitState.Received;}
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.ChangeExtension(output,"png"));yield return null;
            result.passed=result.modelReady&&result.databaseReply&&result.generatedReply&&result.contextReply&&result.offTopicClosed&&result.cacheIsolated&&result.cancelled&&result.selectionIsolated&&result.served;
            Finish(result.passed?null:"One or more acceptance checks failed");
        }
        void Finish(string error)
        {
            result.error=error;File.WriteAllText(output,JsonUtility.ToJson(result,true));
            Debug.Log("[CafeChatSmoke] "+(result.passed?"PASS":"FAIL")+" (no dialogue text logged)");
            Application.Quit(result.passed?0:1);
        }
    }
}
#endif
