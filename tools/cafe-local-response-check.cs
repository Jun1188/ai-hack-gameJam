// Run with Unity CLI script eval_file in Edit Mode. Synthetic inputs only.
var people=UnityEngine.JsonUtility.FromJson<CafeSim.PersonaTable>(System.IO.File.ReadAllText("Assets/CafeSim/Data/personas.json")).items;
var knowledge=new CafeSim.CafeKnowledge();var conversation=new CafeSim.CafeConversation();
var samples=new System.Collections.Generic.List<object>();
int originalChars=0,localChars=0;
foreach(var p in people)
{
 var c=new CafeSim.CafeVisit{Person=p,State=CafeSim.VisitState.Inactive,VisitNumber=1};
 c.LastThought=knowledge.Retrieve(c,"chatter",0);
 var reply=conversation.AutomaticReply(c,"더 자세히 들려주세요",knowledge,null,.6f,out var source,out bool closed);
 if(source!="database"||closed)throw new System.Exception("Grounded opening follow-up "+p.id);
 c.History.Add("REPLY\n"+reply);
 var again=conversation.AutomaticReply(c,"더 자세히 들려주세요",knowledge,null,0,out source,out closed);
 if(CafeSim.CafeReplyComposer.RecentlySaid(c,again))throw new System.Exception("Repeated macro "+p.id);
 // The original reported failure occurred after the opening rows expired.
 c.InactiveWait=70;
 foreach(float threshold in new[]{0f,.6f,1f})
 {
  var followup=conversation.AutomaticReply(c,"더 자세히 들려주세요",knowledge,null,threshold,out source,out closed);
  if(source=="database"||closed)throw new System.Exception("Follow-up selected waiting complaint "+p.id);
  foreach(string input in new[]{"안녕하세요","네","알쏭달쏭한 말"})
  {
   var speech=conversation.AutomaticReply(c,input,knowledge,null,threshold,out source,out closed);
   if(source!="vocabulary"||closed||!CafeSim.CafeReplyComposer.WellFormed(speech))throw new System.Exception("Intent bypass "+p.id);
  }
 }
 string local=conversation.LocalPrompt(c,"더 자세히 들려주세요",knowledge);
 string original=conversation.Prompt(c,"더 자세히 들려주세요",knowledge);
 if(local.Contains("기다린 시간도")||local.Contains("기다림이 꽤"))throw new System.Exception("Unrelated waiting example in local prompt "+p.id);
 if(local.Length>original.Length)throw new System.Exception("Local context larger than API context "+p.id);
 if(local.Contains("허용 답변")||!local.StartsWith(conversation.PersonaWrapper(p)))throw new System.Exception("Restricted or unstable persona wrapper "+p.id);
 originalChars+=original.Length;localChars+=local.Length;
 if(conversation.AcceptLocalReply(c,UnityEngine.JsonUtility.ToJson(new CafeSim.ConversationReply{speech=reply}),new[]{reply}))throw new System.Exception("Repeated local speech accepted");
 if(!conversation.AcceptLocalReply(c,"{\"speech\":\"어떤 부분이 더 궁금하세요?\"}",new[]{"어떤 부분이 더 궁금하세요?"}))throw new System.Exception("Legacy speech-only JSON rejected");
 // A novel sentence need not match any authored candidate or punctuation template.
 if(!conversation.AcceptLocalReply(c,"{\"speech\":\"그 얘길 들으니 오늘은 조금 다른 생각도 드네요…\"}",new[]{"고정 대사예요."}))throw new System.Exception("Free response rejected");
 conversation.AutomaticReply(c,"양자역학 이야기는 어떻게 생각하세요?",knowledge,null,1,out source,out closed);
 if(closed)throw new System.Exception("New topic forced conversation closed");
 int used=c.UsedTerms.Count,history=c.History.Count;
 conversation.Search(c,"이야기");conversation.LocalPrompt(c,"어땠어요?",knowledge);
 if(used!=c.UsedTerms.Count||history!=c.History.Count)throw new System.Exception("Retrieval changed visit");
 samples.Add(new{persona=p.id,first=reply,followup=again});
}
foreach(string bad in new[]{"", "{topic} 이야기예요.","API 연결 오류가 발생했습니다.","죄송합니다. 서버 응답을 받지 못했습니다.","HTTP 503 오류입니다.","Error: 연결이 끊어졌습니다."})
{
 var c=new CafeSim.CafeVisit{Person=people[0]};
 if(conversation.AcceptLocalReply(c,UnityEngine.JsonUtility.ToJson(new CafeSim.ConversationReply{speech=bad}),new[]{bad}))throw new System.Exception("Malformed local speech accepted");
}
if(!CafeSim.CafeConversation.Parse("{\"speech\":\"회사에서 API 오류를 고치느라 늦었어요.\"}",out _))throw new System.Exception("Guest story censored by diagnostic filter");
if(!CafeSim.CafeConversation.Parse("{\"speech\":\"서버 연결 오류 때문에 동료와 함께 밤을 새웠어요.\"}",out _))throw new System.Exception("Technical story opening censored");
foreach(long code in new long[]{0,401,402,403,429,503})
 if(CafeSim.CafeConversation.Parse(UnityEngine.JsonUtility.ToJson(new CafeSim.ConversationReply{speech=CafeSim.LocalCafeModel.ApiFailure(code)}),out _))throw new System.Exception("Provider diagnostic accepted "+code);
if(!CafeSim.CafeConversation.WantsToEnd("대화는 여기까지 할게요.")||CafeSim.CafeConversation.WantsToEnd("다른 이야기를 해볼까요?"))throw new System.Exception("Explicit farewell detection");
var jeju=System.Array.Find(people,p=>p.id=="cus_jeju");
var visit=new CafeSim.CafeVisit{Person=jeju,State=CafeSim.VisitState.Inactive,VisitNumber=1,LastThought="제주에 있는 손주 생각을 하고 이서요."};
visit.History.Add("REPLY\n바다 소리를 들으면 마음이 편안해져요.");
visit.History.Add("REPLY\n안녕하우꽈?");
if(!conversation.CurrentUtterance(visit).StartsWith("바다"))throw new System.Exception("Latest topic lost after greeting");
visit.InactiveWait=70;
if(conversation.Search(visit,"제주 바다에 대해 들려주세요").Exists(h=>h.ContentScore>=.25f))throw new System.Exception("Persona-wide topics treated as waiting-answer evidence");
return new{passed=true,personas=people.Length,playMode=UnityEditor.EditorApplication.isPlaying,originalChars,localChars,samples};
