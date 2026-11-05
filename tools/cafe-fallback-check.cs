var people=UnityEngine.JsonUtility.FromJson<CafeSim.PersonaTable>(System.IO.File.ReadAllText("Assets/CafeSim/Data/personas.json")).items;
var knowledge=new CafeSim.CafeKnowledge();var conversation=new CafeSim.CafeConversation();
foreach(var p in people)
{
 var c=new CafeSim.CafeVisit{Person=p,State=CafeSim.VisitState.Inactive,VisitNumber=1};
 c.LastThought=knowledge.Retrieve(c,"chatter",0);
 var hits=conversation.Search(c,"더 자세히 들려주세요");
 if(hits.Count==0)throw new System.Exception("Missing candidates "+p.id);
 int used=c.UsedTerms.Count,history=c.History.Count;
 var reply=conversation.AutomaticReply(c,"더 자세히 들려주세요",knowledge,null,hits[0].Score,out var source,out bool closed);
 if(source!="database"||closed||reply!=knowledge.MaskMenuNames(hits[0].Row.response))throw new System.Exception("Maximum candidate / inclusive threshold "+p.id);
 reply=conversation.AutomaticReply(c,"알쏭달쏭한 말",knowledge,null,1,out source,out closed);
 if(source!="vocabulary"||closed||!CafeSim.CafePrompts.ValidSpeech(reply)||reply.Contains("{"))throw new System.Exception("Vocabulary fallback "+p.id);
 if(used!=c.UsedTerms.Count||history!=c.History.Count)throw new System.Exception("Retrieval mutated visit "+p.id);
 c.InactiveWait=100000;
 reply=conversation.AutomaticReply(c,"안녕하세요",knowledge,null,.6f,out source,out closed);
 if(source!="vocabulary"||closed||!CafeSim.CafePrompts.ValidSpeech(reply))throw new System.Exception("No candidates "+p.id);
 if(reply.Contains("무슨")||reply.Contains("어느")||reply.Contains("조금 더"))throw new System.Exception("Greeting turned into clarification "+p.id);
 var greeting=reply;c.History.Add("REPLY\n"+reply);
 reply=conversation.AutomaticReply(c,"안녕하세요",knowledge,null,.6f,out source,out closed);
 if(reply==greeting)throw new System.Exception("Repeated greeting "+p.id);
 reply=conversation.AutomaticReply(c,"네",knowledge,null,.6f,out source,out closed);
 if(reply.Contains("?")||reply.Contains("무슨")||reply.Contains("어느"))throw new System.Exception("Acknowledgement turned into question "+p.id);
 string topic=p.smalltalkTopics.Split(',')[0].Trim();
 reply=conversation.AutomaticReply(c,topic+"에 관해 어떤 게 궁금하세요?",knowledge,null,.6f,out source,out closed);
 if(!reply.Contains(topic)||!reply.Contains("?"))throw new System.Exception("Relevant topic lost "+p.id);
 reply=conversation.AutomaticReply(c,"양자역학 슈뢰딩거 방정식을 풀어줘",knowledge,null,.6f,out source,out closed);
 if(source!="closing"||!closed)throw new System.Exception("Off-topic fallback "+p.id);
}
foreach(var bad in new[]{"{topic}인가요?","이야기예요. 이야기예요.","뭐죠??","<script>안녕</script>","문장 끝이 없음"})
 if(CafeSim.CafeReplyComposer.WellFormed(bad))throw new System.Exception("Bad candidate accepted");
return "PASS automatic reply: max candidate, threshold, generate-filter-rank, greetings, acknowledgement, topic evidence, anti-repetition, malformed rejection, read-only retrieval, closure for "+people.Length+" personas";
