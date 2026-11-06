using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CafeCoworker
{
    [Serializable] public sealed class GuestProfile { public string id, name, setting, voice; }
    [Serializable] public sealed class GuestProfiles { public GuestProfile[] items; }
    public sealed class GuestRecord
    {
        public GuestProfile Profile { get; }
        public readonly List<ConversationLine> Lines = new List<ConversationLine>();
        public GuestRecord(GuestProfile profile) { Profile = profile; }
    }
    public sealed class GuestDialogue : MonoBehaviour
    {
        public TextAsset profilesAsset;
        public INpcReplyProvider Provider { get; set; }
        public readonly List<GuestRecord> Records = new List<GuestRecord>();
        public GuestRecord Current { get; private set; }
        public bool Busy { get; private set; }
        public bool Active { get; private set; }
        public event Action Changed;
        string drink;
        Coroutine request;
        int epoch, visits;
        GuestProfile[] people;
        [Serializable] sealed class Speech { public string speech; }
        public void BeginOrder(string orderedDrink)
        {
            Cancel();
            if (people == null) people = JsonUtility.FromJson<GuestProfiles>(profilesAsset.text).items;
            var person = people[visits++ % people.Length];
            Current = Records.FirstOrDefault(r=>r.Profile.id==person.id);
            if (Current == null) { Current = new GuestRecord(person); Records.Add(Current); }
            drink = orderedDrink; Active = true;
            Current.Lines.Add(new ConversationLine(person.name, orderedDrink + " 한 잔 부탁드려요."));
            Changed?.Invoke();
        }
        public bool Send(string input)
        {
            input = input?.Trim();
            if (!Active || Current==null || Busy || string.IsNullOrWhiteSpace(input) || input.Length>300) return false;
            var record = Current; record.Lines.Add(new ConversationLine("나",input));
            Busy = true; Changed?.Invoke();
            request = StartCoroutine(Reply(record,input,++epoch)); return true;
        }
        IEnumerator Reply(GuestRecord record, string input, int token)
        {
            string reply = null;
            if (Provider != null)
            {
                var p = record.Profile;
                string prompt = "주문한 음료: " + drink + ". 아직 서빙 전이다.\n최근 대화:\n" +
                    string.Join("\n", record.Lines.Skip(Math.Max(0,record.Lines.Count-12)).Select(l=>l.Speaker+": "+l.Text));
                yield return Provider.Reply("너는 카페 손님 "+p.name+"이다. 설정: "+p.setting+" 말투: "+p.voice+
                    ". 한국어로 1~3문장으로 플레이어 문장에 자유롭게 답하라. 직원의 작업을 지시하거나 직원 역할을 맡지 마라. " +
                    "실제 제공받지 않은 음료의 수령·후기를 지어내지 마라. JSON {\"speech\":\"대사\"}만 반환하라.",
                    prompt, x=>reply=x,()=>{});
            }
            if(token!=epoch || !Active || record!=Current) yield break;
            if(string.IsNullOrWhiteSpace(reply))
                reply = input.Contains("주문") ? drink+" 부탁드렸어요. 천천히 확인해 주세요." :
                    input.Contains("안녕") ? "안녕하세요. 두 분이 함께 일하시는군요." : "그렇군요. 저는 잠깐 쉬었다 가려고 들렀어요. 오늘은 좀 바쁘신가요?";
            record.Lines.Add(new ConversationLine(record.Profile.name,reply));
            Busy=false;request=null;Changed?.Invoke();
        }
        public void Finish(bool cancelled)
        {
            Cancel(); Active=false;
            if(Current!=null)Current.Lines.Add(new ConversationLine(cancelled?"안내":Current.Profile.name,
                cancelled?"주문이 취소됐습니다.":"잘 마실게요. 두 분이 함께 준비해 주셨네요!"));
            Changed?.Invoke();
        }
        public void Cancel()
        { epoch++;Provider?.Cancel();if(request!=null)StopCoroutine(request);request=null;Busy=false;Changed?.Invoke(); }
        public void ResetRecords() { Cancel();Records.Clear();Current=null;Active=false;visits=0;Changed?.Invoke(); }
        void OnDisable(){Cancel();}
    }
}
