using System;
using System.Collections.Generic;
using UnityEngine;

namespace CafeSim
{
    [Serializable] public sealed class KnowledgeRows<T> { public T[] items; }
    [Serializable] public sealed class PhraseTerm { public string id,text; }
    [Serializable] public sealed class PhraseBinding { public string personaId,termId,slot; public string[] requires; public float minWait,maxWait; }
    [Serializable] public sealed class PhrasePattern { public string id,text; }
    [Serializable] public sealed class PersonaProfile { public string personaId,lexiconId; }
    [Serializable] public sealed class DrinkFeatures { public string id,name,temperature; public float[] values; }
    [Serializable] public sealed class MenuFact { public string id,coffee,milk,color,carbonation,temperature,flavor,spokenDescription; public int sweetness; }
    [Serializable] public sealed class MenuWeight { public string personaId,menuId; public float weight; }
    [Serializable] public sealed class VisitSituation { public string id,personaId,context,spokenContext,effect,weightRule; public float hotDelta,patienceScale=1; }
    [Serializable] public sealed class ForbiddenForm { public string menuId,language,word; }

    // Indexed local retrieval -> fact slots -> constrained sentence generation. No live inference wait.
    public sealed class CafeKnowledge
    {
        readonly Dictionary<string,string> terms=new Dictionary<string,string>();
        readonly Dictionary<string,string> patterns=new Dictionary<string,string>();
        readonly Dictionary<string,string> profiles=new Dictionary<string,string>();
        readonly Dictionary<string,List<PhraseBinding>> index=new Dictionary<string,List<PhraseBinding>>();
        readonly Dictionary<string,DrinkFeatures> drinks=new Dictionary<string,DrinkFeatures>();
        public readonly DrinkFeatures[] Requests;
        public readonly MenuWeight[] Weights;
        public readonly VisitSituation[] Situations;
        readonly MenuFact[] facts;
        readonly ForbiddenForm[] forbidden;
        public CafeKnowledge()
        {
            foreach(var t in Load<PhraseTerm>("terms")) terms.Add(t.id,t.text);
            foreach(var p in Load<PhrasePattern>("patterns")) patterns.Add(p.id,p.text);
            foreach(var p in Load<PersonaProfile>("profiles")) profiles.Add(p.personaId,p.lexiconId);
            foreach(var b in Load<PhraseBinding>("bindings"))
            {
                if(!terms.ContainsKey(b.termId)||!profiles.ContainsValue(b.personaId)||b.maxWait<=b.minWait) throw new InvalidOperationException("Invalid phrase binding");
                string key=b.personaId+":"+b.slot;
                if(!index.TryGetValue(key,out var list))index[key]=list=new List<PhraseBinding>();
                list.Add(b);
            }
            foreach(var d in Load<DrinkFeatures>("menuFeatures")){ValidateFeatures(d);drinks.Add(d.id,d);}
            Requests=Load<DrinkFeatures>("requests"); foreach(var d in Requests)ValidateFeatures(d);
            Weights=Load<MenuWeight>("orderWeights");Situations=Load<VisitSituation>("situations");facts=Load<MenuFact>("menuFacts");forbidden=Load<ForbiddenForm>("forbiddenWords");
            foreach(var w in Weights)if(!profiles.ContainsKey(w.personaId)||!drinks.ContainsKey(w.menuId)||w.weight<0)throw new InvalidOperationException("Invalid order weight foreign key");
            foreach(var s in Situations)if(!profiles.ContainsKey(s.personaId)||s.patienceScale<=0)throw new InvalidOperationException("Invalid situation foreign key");
        }
        static void ValidateFeatures(DrinkFeatures d)
        {
            if(d.values==null||d.values.Length!=6||Array.Exists(d.values,v=>float.IsNaN(v)||v<0||v>1))throw new InvalidOperationException("Invalid drink features: "+d.id);
        }
        static T[] Load<T>(string table)
        {
            var asset=Resources.Load<TextAsset>("CafeKnowledge/"+table);
            if(asset==null)throw new InvalidOperationException("Missing knowledge table: "+table);
            var rows=JsonUtility.FromJson<KnowledgeRows<T>>(asset.text);
            if(rows?.items==null)throw new InvalidOperationException("Invalid knowledge table: "+table);
            return rows.items;
        }
        public string Retrieve(CafeVisit c,string slot,float wait,bool noRepeat=false)
        {
            if(c.Parameters().Contains("quiet")&&slot=="chatter")return "";
            if(!profiles.TryGetValue(c.Person.id,out string profile)||!index.TryGetValue(profile+":"+slot,out var candidates))return "";
            var flags=c.Parameters(); var valid=new List<PhraseBinding>();
            foreach(var b in candidates)
                if(wait>=b.minWait&&wait<b.maxWait&&(b.requires==null||Array.TrueForAll(b.requires,flags.Contains))&&(!noRepeat||(!c.UsedTerms.Contains(b.termId)&&!c.UsedPhrases.Contains(terms[b.termId]))))valid.Add(b);
            if(valid.Count==0)return "";
            int specificity=0;foreach(var b in valid)specificity=Math.Max(specificity,b.requires?.Length??0);
            valid=valid.FindAll(b=>(b.requires?.Length??0)==specificity);
            // Prefer unused retrieved text. Seed depends on this persona's visit, not demographics.
            var fresh=valid.FindAll(b=>!c.UsedTerms.Contains(b.termId)); if(fresh.Count>0)valid=fresh;
            var selected=valid[(c.VisitNumber+c.UtteranceCount)%valid.Count];
            c.UsedTerms.Add(selected.termId);
            c.UsedPhrases.Add(terms[selected.termId]);
            return terms[selected.termId];
        }
        public string Compose(string pattern,Dictionary<string,string> slots)
        {
            string line=patterns[pattern];foreach(var pair in slots)line=line.Replace("{"+pair.Key+"}",pair.Value??"");
            if(line.Contains("{")||line.Contains("}"))throw new InvalidOperationException("Unresolved sentence slot");
            return line.Trim();
        }
        // Read-only retrieval for live dialogue: never consumes autonomous chatter terms.
        public string ConversationContext(CafeVisit c)
        {
            if(!profiles.TryGetValue(c.Person.id,out var profile)||!index.TryGetValue(profile+":chatter",out var rows))return "";
            var lines=new List<string>();var flags=c.Parameters();
            float wait=c.State==VisitState.Inactive?c.InactiveWait:c.ActiveWait;
            foreach(var b in rows)
                if(wait>=b.minWait&&wait<b.maxWait&&(b.requires==null||Array.TrueForAll(b.requires,flags.Contains))&&!lines.Contains(terms[b.termId]))
                    lines.Add(terms[b.termId]);
            return string.Join("\n",lines);
        }
        public float Similarity(string requested,string requestedTemp,string served,string servedTemp)
        {
            if(requested==served)return requestedTemp==servedTemp?1f:.8f;
            DrinkFeatures wanted=drinks.TryGetValue(requested,out var normal)?normal:Array.Find(Requests,r=>r.id==requested);
            if(wanted==null||!drinks.TryGetValue(served,out var actual))throw new InvalidOperationException("Missing drink similarity features");
            float[] weights={1f,1f,1.5f,1.2f,1f,1.5f};float distance=0,total=0;
            for(int i=0;i<weights.Length;i++){distance+=weights[i]*Mathf.Abs(wanted.values[i]-actual.values[i]);total+=weights[i];}
            // A different item can be similar, but never represented as an exact match.
            return Mathf.Min(.95f,.8f*(1-distance/total)+(requestedTemp==servedTemp?.2f:0));
        }
        public string Describe(string id,string fallback)
        {
            var f=Array.Find(facts,x=>x.id==id);
            return f==null?fallback:f.spokenDescription;
        }
        public string MaskMenuNames(string text)
        {
            var ordered=(ForbiddenForm[])forbidden.Clone();Array.Sort(ordered,(a,b)=>b.word.Length.CompareTo(a.word.Length));
            foreach(var f in ordered)text=System.Text.RegularExpressions.Regex.Replace(text,System.Text.RegularExpressions.Regex.Escape(f.word),"그 음료",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            return text;
        }
        public bool ContainsForbidden(string text)=>Array.Exists(forbidden,f=>text.IndexOf(f.word,StringComparison.OrdinalIgnoreCase)>=0);
        public DrinkFeatures[] SimilarRequests(string menuId,string temperature)
        {
            var basis=drinks[menuId];
            return Array.FindAll(Requests,r=>(basis.values[5]>0||r.values[5]==0)&&Similarity(r.id,r.temperature,menuId,temperature)>=.7f);
        }
        public float Weight(CafeVisit c,string id)
        {
            float value=Array.Find(Weights,w=>w.personaId==c.Person.id&&w.menuId==id)?.weight??0;
            // Zero-weight exclusions remain exclusions. Prototype supports only declared numeric modifiers.
            if(value<=0)return 0;
            var fact=Array.Find(facts,f=>f.id==id);string rule=c.Situation?.weightRule??"";
            bool sweet=fact!=null&&fact.sweetness>=2,coffee=fact!=null&&fact.coffee.Contains("있음");
            if(sweet&&rule.Contains("단 메뉴"))value+=rule.Contains("+2")?2:rule.Contains("-1")?-1:rule.Contains("+1")?1:0;
            if(coffee&&rule.Contains("커피 메뉴"))value+=rule.Contains("-1")?-1:rule.Contains("+1")?1:0;
            if(rule.Contains("제주 특산")&&(id=="menu_yuja"||id=="menu_matcha"))value++;
            if(rule.Contains("에스프레소·아메리카노")&&(id=="menu_espresso"||id=="menu_americano"))value++;
            if(rule.Contains("평소와 다른 메뉴")&&value==1)value++;
            return Mathf.Max(0,value);
        }
    }
}
