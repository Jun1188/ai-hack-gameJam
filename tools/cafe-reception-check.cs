var menu=UnityEngine.JsonUtility.FromJson<CafeSim.MenuTable>(System.IO.File.ReadAllText("Assets/CafeSim/Data/menu.json"));
var people=UnityEngine.JsonUtility.FromJson<CafeSim.PersonaTable>(System.IO.File.ReadAllText("Assets/CafeSim/Data/personas.json"));
var kb=new CafeSim.CafeKnowledge();var talk=new CafeSim.CafeConversation();
void Check(bool value,string reason){if(!value)throw new System.Exception(reason);}
var q=new CafeSim.CafeQueueSim(people.items,menu,kb,new CafeSim.CafeQueueSettings{arrivalSeconds=10000,orderSeconds=10,activeTimeoutSeconds=10000},17);
q.Tick(0);Check(q.Active.Count==1&&q.Inactive.Count==0,"Empty active accepts on arrival");
var second=q.Admit(false);q.Tick(9.75f);Check(second.State==CafeSim.VisitState.Inactive,"One active waits base interval");
q.Tick(.25f);Check(second.State==CafeSim.VisitState.Active,"One active accepts at 10 seconds");
var third=q.Admit(false);q.Tick(19.75f);Check(third.State==CafeSim.VisitState.Inactive,"Two active waits twice base interval");
q.Tick(.25f);Check(third.State==CafeSim.VisitState.Active&&third.InactiveWait==20,"Two active accepts at 20 seconds");
var fourth=q.Admit(false);
foreach(var old in new System.Collections.Generic.List<CafeSim.CafeVisit>(q.Active))q.Serve(old.Id,"menu_latte","HOT");
Check(fourth.State==CafeSim.VisitState.Active&&fourth.InactiveWait==0,"Last active served promotes immediately");
var expiry=new CafeSim.CafeQueueSim(people.items,menu,kb,new CafeSim.CafeQueueSettings{arrivalSeconds=10000,orderSeconds=10000,activeTimeoutSeconds=1},4);
expiry.Tick(0);expiry.Active[0].Situation=null;var waiting=expiry.Admit(false);expiry.Tick(1);
Check(waiting.State==CafeSim.VisitState.Active,"Timeout also promotes without empty-queue delay");
int answers=0;
foreach(var person in people.items)foreach(var request in kb.Requests)
{
 var c=new CafeSim.CafeVisit{Person=person,State=CafeSim.VisitState.Active,OffMenu=true,RequestedName=request.name,Order=new CafeSim.CustomerOrder{menuId=request.id,temperature=request.temperature}};
 foreach(var input in new[]{"무슨 메뉴를 원하세요?","그 음료는 없어요","대신 카페라떼는 어떠세요?"})
 {
  Check(talk.TryNewMenuReply(c,input,menu,out var reply)&&reply.Contains(request.name)&&reply.Length<65,"Wanted-menu short answer from table "+person.id);
 }
 Check(!talk.TryNewMenuReply(c,"양자역학 방정식을 풀어줘",menu,out _),"Unrelated topic is not a menu query");
 c.State=CafeSim.VisitState.Received;Check(!talk.TryNewMenuReply(c,"뭐 드실래요?",menu,out _),"Completed order cannot answer as pending");answers++;
}
foreach(var person in people.items)
{
 var c=new CafeSim.CafeVisit{Person=person,State=CafeSim.VisitState.Inactive};c.LastThought=kb.Retrieve(c,"chatter",0);
 foreach(var input in new[]{"아 그러셨군요", "그래요?", "그때는 어땠어요?", "재밌었겠네요", "좀 더 말해줘", "그게 무슨 뜻이에요?"})
  Check(!talk.IsOffTopic(c,input),"Natural conversational turn falsely closed: "+person.id);
}
Check(CafeSim.CafeConversation.Parse("{\"speech\":\"이만 쉴게요.\",\"endConversation\":true}",out _,out bool close)&&close,"Semantic close contract");
Check(CafeSim.CafeConversation.Parse("{\"speech\":\"네, 그랬어요.\"}",out _,out close)&&!close,"Old reply format defaults open");
return "PASS immediate reception, 1x/2x active-count timing, service/timeout promotion; "+answers+" persona-menu mappings; 96 natural follow-ups; semantic close compatibility";
