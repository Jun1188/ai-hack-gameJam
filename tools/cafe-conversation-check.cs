var people=UnityEngine.JsonUtility.FromJson<CafeSim.PersonaTable>(System.IO.File.ReadAllText("Assets/CafeSim/Data/personas.json")).items;
var knowledge=new CafeSim.CafeKnowledge();var conversation=new CafeSim.CafeConversation();int count=0;
foreach(var p in people)
{
 var c=new CafeSim.CafeVisit{Person=p,State=CafeSim.VisitState.Inactive,VisitNumber=1};
 c.LastThought=knowledge.Retrieve(c,"chatter",0);
 if(string.IsNullOrEmpty(c.LastThought))throw new System.Exception("Missing opening "+p.id);
 if(!conversation.TryDirect(c,"더 자세히 들려주세요",out var reply)||!CafeSim.CafePrompts.ValidSpeech(reply))throw new System.Exception("Missing mapping "+p.id);
 foreach(var q in new[]{"왜요?","정말요?","안녕하세요",p.smalltalkTopics.Split(',')[0]+" 이야기 들려주세요"})
  if(conversation.IsOffTopic(c,q))throw new System.Exception("False topic rejection "+p.id+" "+q);
 foreach(var q in new[]{"양자역학 슈뢰딩거 방정식을 풀어줘", "비트코인 주식 시세를 분석해줘", "우주 로켓 설계도를 작성해줘"})
  if(!conversation.IsOffTopic(c,q))throw new System.Exception("Unrelated topic accepted "+p.id+" "+q+" score="+conversation.TopicSimilarity(c,q));
 var before=c.UsedTerms.Count;conversation.Search(c,"예전 추억 이야기");if(before!=c.UsedTerms.Count)throw new System.Exception("Retrieval consumed chatter");
 c.History.Add("REPLY\n"+reply);if(conversation.TryDirect(c,"더 자세히 들려주세요",out _))throw new System.Exception("Repeated opening reply");
 count++;
}
if(CafeSim.CafeConversation.Parse("{\"speech\":\"\"}",out _))throw new System.Exception("Empty reply accepted");
if(!CafeSim.CafeConversation.Parse("{\"speech\":\"반가워요.\"}",out _))throw new System.Exception("Korean reply rejected");
if(!CafeSim.LocalCafeModel.ValidApiEndpoint("https://example.com/v1/chat/completions")||!CafeSim.LocalCafeModel.ValidApiEndpoint("http://127.0.0.1:18766/v1/chat/completions")||CafeSim.LocalCafeModel.ValidApiEndpoint("http://example.com/v1/chat/completions")||CafeSim.LocalCafeModel.ValidApiEndpoint("https://key@example.com/v1/chat/completions"))throw new System.Exception("API endpoint validation");
conversation.Remember("context-A","반가워요.");if(!conversation.TryCached("context-A",out _)||conversation.TryCached("context-B",out _))throw new System.Exception("Context cache isolation");
return "PASS "+count+" personas: opening mappings, Korean follow-ups, 3 unrelated-topic rejections each, read-only retrieval, no opening repetition, JSON validation, context cache isolation";
