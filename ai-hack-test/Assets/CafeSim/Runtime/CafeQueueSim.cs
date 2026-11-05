using System;
using System.Collections.Generic;
using UnityEngine;

namespace CafeSim
{
    public enum VisitState { Inactive, Active, Received, TimedOut }
    [Serializable] public sealed class CafeQueueSettings
    {
        public int inactiveCapacity=4;
        public float arrivalSeconds=60, orderSeconds=60, activeTimeoutSeconds=180, chatterSeconds=30;
        public float inactivePenaltyPerSecond=.005f,activePenaltyPerSecond=.008f,offMenuChance=.05f;
        public void Validate()
        {
            foreach(float x in new[]{arrivalSeconds,orderSeconds,activeTimeoutSeconds,chatterSeconds,inactivePenaltyPerSecond,activePenaltyPerSecond,offMenuChance})
                if(float.IsNaN(x)||float.IsInfinity(x))throw new InvalidOperationException("Non-finite cafe queue setting");
            if(inactiveCapacity<1||arrivalSeconds<=0||orderSeconds<=0||activeTimeoutSeconds<=0||chatterSeconds<=0||
                inactivePenaltyPerSecond<0||activePenaltyPerSecond<0||offMenuChance<0||offMenuChance>1)
                throw new InvalidOperationException("Invalid cafe queue settings");
        }
    }
    public sealed class CafeVisit
    {
        public int Id,VisitNumber,InactiveEntries=1,ActiveEntries,UtteranceCount;
        public Persona Person; public CustomerOrder Order; public CustomerReaction Reaction;
        public VisitSituation Situation;
        public VisitState State; public float ArrivedAt,OrderedAt,ResolvedAt,InactiveWait,ActiveWait,Accuracy;
        public float LastChatterAt; public bool OffMenu,RatingPublished; public string RequestedName,ServedName,ServedTemperature,LastLine="",LastThought="";
        public readonly HashSet<string> UsedTerms=new HashSet<string>();
        public readonly HashSet<string> UsedPhrases=new HashSet<string>();
        public readonly List<string> History=new List<string>();
        public HashSet<string> Parameters()
        {
            var flags=new HashSet<string>();
            if(InactiveEntries>0)flags.Add("visited_inactive"); if(ActiveEntries>0)flags.Add("visited_active");
            if(VisitNumber>1)flags.Add("repeat");
            if(State==VisitState.Inactive)flags.Add("inactive"); if(State==VisitState.Active)flags.Add("active");
            if(State==VisitState.Received){flags.Add("received");flags.Add(Accuracy>=.999f?"accurate":Accuracy>=.7f?"similar":"different");}
            if(State==VisitState.TimedOut)flags.Add("timeout");
            if(OffMenu)flags.Add("off_menu"); if(InactiveWait>=15||ActiveWait>=15)flags.Add("delayed");
            if(Situation!=null&&Situation.effect.Contains("말수가 적"))flags.Add("quiet");
            return flags;
        }
    }
    // Time is supplied by the caller. No scene/UI dependencies; deadlines are evaluated before service.
    public sealed class CafeQueueSim
    {
        public readonly List<CafeVisit> Inactive=new List<CafeVisit>(),Active=new List<CafeVisit>();
        public readonly List<CafeVisit> Completed=new List<CafeVisit>();
        public readonly CafeQueueSettings Settings;
        public readonly CafeKnowledge Knowledge;
        public float Now {get;private set;} public int RejectedArrivals {get;private set;}
        public event Action<CafeVisit,string,string> Output;
        readonly Persona[] people;readonly MenuTable menu;readonly System.Random random;
        readonly Dictionary<string,int> visits=new Dictionary<string,int>();
        int nextId;float nextArrival;
        public CafeQueueSim(Persona[] people,MenuTable menu,CafeKnowledge knowledge,CafeQueueSettings settings,int seed)
        {
            settings.Validate();if(people==null||people.Length==0||menu?.items==null||menu.items.Length==0)throw new InvalidOperationException("Empty cafe data");
            foreach(var m in menu.items)knowledge.Similarity(m.id,"HOT",m.id,"HOT");
            this.people=people;this.menu=menu;Knowledge=knowledge;Settings=settings;random=new System.Random(seed);
            nextArrival=0;
        }
        public void Tick(float dt)
        {
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0)throw new ArgumentException("Invalid elapsed time");
            // Substeps keep admission and timeout ordering stable after a slow frame.
            float end=Now+dt;
            do {float step=Mathf.Min(.25f,end-Now);Now+=step;UpdateWaits();Expire();
                if(Now>=nextArrival){Admit();nextArrival=Now+Settings.arrivalSeconds;}
                AcceptReadyOrder();
                Chatter();
            }while(Now<end-.00001f);
        }
        public CafeVisit Admit(bool? offMenu=null)
        {
            if(Inactive.Count>=Settings.inactiveCapacity){RejectedArrivals++;return null;}
            var p=people[random.Next(people.Length)];int visit=visits.TryGetValue(p.id,out int previous)?previous+1:1;visits[p.id]=visit;
            var c=new CafeVisit{Id=++nextId,Person=p,VisitNumber=visit,ArrivedAt=Now,LastChatterAt=Now,State=VisitState.Inactive,OffMenu=offMenu??random.NextDouble()<Settings.offMenuChance};
            var available=CafePrompts.OrderMenu(p,menu).items;
            if(available.Length==0)throw new InvalidOperationException("No order candidates for "+p.id);
            var situations=Array.FindAll(Knowledge.Situations,s=>s.personaId==p.id);
            c.Situation=situations.Length==0?null:situations[random.Next(situations.Length)];
            float sum=0;foreach(var m in available)sum+=Knowledge.Weight(c,m.id);
            if(sum<=0)throw new InvalidOperationException("No positive order weights for "+p.id);
            float roll=(float)random.NextDouble()*sum;var selected=available[available.Length-1];
            foreach(var m in available){roll-=Knowledge.Weight(c,m.id);if(roll<0){selected=m;break;}}
            float hotChance=Mathf.Clamp01(p.hotPreference+(c.Situation?.hotDelta??0));
            string temp=selected.hot&&(!selected.ice||random.NextDouble()<hotChance)?"HOT":"ICE";
            c.RequestedName=selected.name;c.Order=new CustomerOrder{menuId=selected.id,temperature=temp};
            if(c.OffMenu){var options=Knowledge.SimilarRequests(selected.id,temp);if(options.Length==0)options=Array.FindAll(Knowledge.Requests,r=>r.values[5]==0);
                var special=options[random.Next(options.Length)];c.RequestedName=special.name;c.Order.menuId=special.id;c.Order.temperature=special.temperature;}
            Inactive.Add(c);Emit(c,"ARRIVING",c.Situation?.context??p.setting);return c;
        }
        public CafeVisit AcceptOrder()
        {
            if(Inactive.Count==0)return null;
            var c=Inactive[0];Inactive.RemoveAt(0);c.InactiveWait=Now-c.ArrivedAt;c.OrderedAt=Now;c.ActiveEntries++;c.State=VisitState.Active;c.LastChatterAt=Now;
            string opener=Knowledge.Retrieve(c,"opener",0),ending=Knowledge.Retrieve(c,"ending",0);
            if(c.VisitNumber>1)opener=Knowledge.Retrieve(c,"repeat",0)+" "+opener;
            c.Order.speech=Knowledge.Compose("order",new Dictionary<string,string>{{"opener",opener},{"requested",Knowledge.Describe(c.Order.menuId,c.RequestedName)+"를 "+(c.Order.temperature=="HOT"?"따뜻하게":"차갑게")+" 한 잔"},{"ending",ending}});
            if(c.Situation!=null)c.Order.speech=c.Situation.spokenContext+" "+c.Order.speech;
            Active.Add(c);Emit(c,"ORDER",c.Order.speech);
            return c;
        }
        public float ReceptionSecondsRemaining => Inactive.Count==0?0:Mathf.Max(0,Settings.orderSeconds*Active.Count-(Now-Inactive[0].ArrivedAt));
        void AcceptReadyOrder()
        {
            if(Inactive.Count>0&&ReceptionSecondsRemaining<=0)AcceptOrder();
        }
        void UpdateWaits()
        {
            foreach(var c in Inactive)c.InactiveWait=Now-c.ArrivedAt;
            foreach(var c in Active)c.ActiveWait=Now-c.OrderedAt;
        }
        public float WaitingSatisfaction(CafeVisit c)=>Mathf.Clamp01(1-(c.InactiveWait*Settings.inactivePenaltyPerSecond+c.ActiveWait*Settings.activePenaltyPerSecond)/4f);
        void Chatter()
        {
            foreach(var queue in new[]{Inactive,Active})foreach(var c in queue)
            {
                if(Now-c.LastChatterAt<Settings.chatterSeconds)continue;c.LastChatterAt=Now;
                float wait=c.State==VisitState.Inactive?c.InactiveWait:c.ActiveWait;
                string line=Knowledge.Retrieve(c,"chatter",wait,true);
                if(string.IsNullOrEmpty(line))continue;
                // Remember the actual prior thought; do not ask for or wait for player input.
                c.LastThought=line;Emit(c,"CHATTER",line);
            }
        }
        void Expire()
        {
            for(int i=Active.Count-1;i>=0;i--)
            {
                var c=Active[i];if(c.ActiveWait<Settings.activeTimeoutSeconds*(c.Situation?.patienceScale??1))continue;
                Active.RemoveAt(i);c.State=VisitState.TimedOut;c.ResolvedAt=Now;
                string line=Expand(Knowledge.Retrieve(c,"timeout",c.ActiveWait),c);
                c.Reaction=new CustomerReaction{speech=line,reason=line,stars=0};c.RatingPublished=true;Finish(c);Emit(c,"RATING", "0/5 | "+line);
            }
        }
        public bool Serve(int id,string menuId,string temp)
        {
            UpdateWaits();Expire();var c=Active.Find(v=>v.Id==id);var served=Array.Find(menu.items,m=>m.id==menuId);
            if(c==null||served==null||!served.Supports(temp)){AcceptReadyOrder();return false;}
            Active.Remove(c);c.State=VisitState.Received;c.ResolvedAt=Now;c.ServedName=served.name;c.ServedTemperature=temp;
            c.Accuracy=Knowledge.Similarity(c.Order.menuId,c.Order.temperature,menuId,temp);
            string fact="";
            if(c.Order.menuId!=menuId||c.Order.temperature!=temp)
                fact=Expand(Knowledge.Retrieve(c,"contrast",c.ActiveWait),c);
            string response=Knowledge.Retrieve(c,c.Accuracy>=.999f?"match":c.OffMenu?(c.Accuracy>=.7f?"similar":"different"):"wrong",c.ActiveWait);
            response=Expand(response,c);
            int stars=Mathf.Clamp(Mathf.RoundToInt(1+4*c.Accuracy-c.InactiveWait*Settings.inactivePenaltyPerSecond-c.ActiveWait*Settings.activePenaltyPerSecond),1,5);
            if(c.Accuracy<.999f)stars=Mathf.Min(4,stars);
            string opinion=Knowledge.Retrieve(c,c.Accuracy>=.999f&&stars>=4?"good":c.Accuracy>=.7f&&stars>=3?"partialGood":"bad",c.ActiveWait);
            string waiting=Expand(Knowledge.Retrieve(c,"waitReview",c.ActiveWait),c);
            c.Reaction=new CustomerReaction{speech=Knowledge.Compose("reaction",new Dictionary<string,string>{{"fact",fact},{"response",response}}),stars=stars,
                reason=Knowledge.Compose("review",new Dictionary<string,string>{{"opinion",opinion},{"wait",waiting}})};
            Emit(c,"SERVED",Label(served.name,temp)+" | similarity="+c.Accuracy.ToString("F2"));Emit(c,"REACTION",c.Reaction.speech);Finish(c);AcceptReadyOrder();return true;
        }
        void Finish(CafeVisit c){Completed.Add(c);if(Completed.Count>64)Completed.RemoveAt(0);}
        public void PublishRating(CafeVisit c){if(c.State==VisitState.Received&&!c.RatingPublished){c.RatingPublished=true;Emit(c,"RATING",c.Reaction.stars+"/5 | "+c.Reaction.reason);}}
        string Expand(string text,CafeVisit c)=>text.Replace("{requested}",Knowledge.Describe(c.Order.menuId,c.RequestedName)).Replace("{served}",c.ServedName==null?"":Knowledge.Describe(Array.Find(menu.items,m=>m.name==c.ServedName)?.id??"",c.ServedName)).Replace("{requestedTemp}",c.Order.temperature=="HOT"?"따뜻한":"차가운").Replace("{servedTemp}",c.ServedTemperature=="HOT"?"따뜻한":"차가운").Replace("{inactiveSeconds}",Mathf.FloorToInt(c.InactiveWait).ToString()).Replace("{activeSeconds}",Mathf.FloorToInt(c.ActiveWait).ToString()).Replace("{lastThought}",string.IsNullOrEmpty(c.LastThought)?c.Order.speech:c.LastThought);
        void Emit(CafeVisit c,string kind,string text)
        {
            if(kind!="SERVED"&&kind!="ARRIVING")text=Knowledge.MaskMenuNames(text);
            if(kind=="ORDER")c.Order.speech=text;
            if(kind=="REACTION")c.Reaction.speech=text;
            if(kind=="RATING")c.Reaction.reason=Knowledge.MaskMenuNames(c.Reaction.reason);
            c.LastLine=text;c.UtteranceCount++;c.History.Add(kind+"\n"+text);if(c.History.Count>16)c.History.RemoveAt(0);Output?.Invoke(c,kind,text);
        }
        public static string Label(string name,string temp)=>(temp=="HOT"?"따뜻한 ":"아이스 ")+name;
    }
}
