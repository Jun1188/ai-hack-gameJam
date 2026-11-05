using System.Collections.Generic;
using UnityEngine;

namespace CafeSim
{
    public sealed partial class CafeHud
    {
        enum StatusPage { Overview, Orders, Conversations, Settlement, OrderDetail, LedgerDetail }
        StatusPage statusPage, historyReturnPage;
        CafeVisit statusOrder;
        int statusLedgerIndex;
        Vector2 statusScroll;

        void OpenStatusPage(StatusPage page)
        {
            statusPage = page;
            conversationListOpen = true;
            conversationFromList = false;
            statusScroll = Vector2.zero;
            chatInput?.Blur();
            inputFocused = false;
            SetPrompt(true);
        }

        void BackFromStatus()
        {
            switch (statusPage)
            {
                case StatusPage.Overview: CloseConversations(); break;
                case StatusPage.OrderDetail: OpenStatusPage(StatusPage.Orders); break;
                case StatusPage.LedgerDetail: OpenStatusPage(StatusPage.Settlement); break;
                default: OpenStatusPage(StatusPage.Overview); break;
            }
        }

        void DrawStatusPanel()
        {
            const float x = 180, y = 156, width = 720;
            string title = statusPage switch {
                StatusPage.Orders => "주문 현황", StatusPage.Conversations => "이전 대화 목록",
                StatusPage.Settlement => "영업 · 정산 현황", StatusPage.OrderDetail => "주문 확인",
                StatusPage.LedgerDetail => "정산 항목 확인", _ => "현황" };
            GUI.Label(new Rect(x,y,width-160,28),title,artText);
            if(GUI.Button(new Rect(x+width-132,y,132,30),statusPage==StatusPage.Overview?"닫기 · Esc":"뒤로 · Esc",artButton))BackFromStatus();
            var area = new Rect(x, y+66, width, 352);
            switch(statusPage)
            {
                case StatusPage.Overview:
                    GUI.Label(new Rect(x,y+36,width,24),"확인할 항목을 선택하세요. 현재 손님과의 대화는 E로 열어요.",artSmall);
                    var visits=StatusVisits();
                    if(GUI.Button(new Rect(area.x,area.y,width,68),"주문 현황   ·   "+visits.Count+"건",artButton))OpenStatusPage(StatusPage.Orders);
                    if(GUI.Button(new Rect(area.x,area.y+84,width,68),"이전 대화 목록   ·   손님별 기록",artButton))OpenConversationList();
                    int settled=businessDay!=null?businessDay.Ledger.Orders.Count:0;
                    if(GUI.Button(new Rect(area.x,area.y+168,width,68),"영업 · 정산 현황   ·   "+settled+"건 정산",artButton))OpenStatusPage(StatusPage.Settlement);
                    break;
                case StatusPage.Conversations: DrawConversationList(area); break;
                case StatusPage.Orders: DrawStatusOrders(area); break;
                case StatusPage.OrderDetail: DrawStatusOrder(area); break;
                case StatusPage.Settlement: DrawStatusSettlement(area); break;
                case StatusPage.LedgerDetail: DrawStatusLedger(area); break;
            }
        }

        List<CafeVisit> StatusVisits()
        {
            var visits=new List<CafeVisit>();
            foreach(var c in session.ConversationVisits)if(c.Order!=null&&!visits.Contains(c))visits.Add(c);
            if(session.Queue!=null)
            {
                foreach(var c in session.Queue.Active)if(!visits.Contains(c))visits.Add(c);
                foreach(var c in session.Queue.Inactive)if(!visits.Contains(c))visits.Add(c);
            }
            visits.Sort((a,b)=>a.State==VisitState.Active&&b.State!=VisitState.Active?-1:b.State==VisitState.Active&&a.State!=VisitState.Active?1:b.Id.CompareTo(a.Id));
            return visits;
        }

        static string OrderState(CafeVisit visit)=>visit.State switch {
            VisitState.Inactive=>"주문 대기",VisitState.Active=>"서빙 대기",VisitState.Received=>"제공 완료",VisitState.TimedOut=>"시간 초과",_=>"이전 주문"};

        void DrawStatusOrders(Rect area)
        {
            var visits=StatusVisits();
            if(visits.Count==0){GUI.Label(area,"아직 주문 내역이 없어요.",artSmall);return;}
            statusScroll=GUI.BeginScrollView(area,statusScroll,new Rect(0,0,area.width-22,visits.Count*62),false,true);
            CafeVisit chosen=null;
            for(int i=0;i<visits.Count;i++)if(GUI.Button(new Rect(0,i*62,area.width-24,52),visits[i].Person.name+"   /   "+OrderState(visits[i]),artButton))chosen=visits[i];
            GUI.EndScrollView();
            if(chosen!=null){statusOrder=chosen;OpenStatusPage(StatusPage.OrderDetail);}
        }

        void DrawStatusOrder(Rect area)
        {
            if(statusOrder==null){GUI.Label(area,"선택한 주문이 없어요.",artSmall);return;}
            var c=statusOrder;
            GUI.Label(new Rect(area.x,area.y,area.width,28),c.Person.name+" · "+OrderState(c),artLabel);
            string body=c.State==VisitState.Inactive?"손님이 아직 주문하지 않았어요.":c.Order?.speech??"주문 기록이 없어요.";
            // Live orders retain the customer's description, without revealing their hidden menu ID.
            if(c.State==VisitState.Received)body+="\n\n제공한 음료: "+c.ServedName+" · "+c.ServedTemperature;
            if(c.RatingPublished)body+="\n\n후기: "+(c.Reaction?.stars??0)+"점 · "+c.Reaction?.reason;
            GUI.Label(new Rect(area.x,area.y+42,area.width,230),body,artText);
            if(GUI.Button(new Rect(area.x,area.y+294,230,40),"이 손님의 대화 기록",artButton))ViewConversation(c.Person.id,true);
        }

        void DrawStatusSettlement(Rect area)
        {
            if(businessDay==null){GUI.Label(area,"정산 정보가 없어요.",artSmall);return;}
            var ledger=businessDay.Ledger;
            GUI.Label(new Rect(area.x,area.y,area.width,26),businessDay.Closed?"오늘 마감 정산":"현재 영업 집계 · 마감 전",artLabel);
            GUI.Label(new Rect(area.x,area.y+34,area.width,48),"매출 "+Won(ledger.Sales)+"   + 팁 "+Won(ledger.Tips)+"   - 손실 "+Won(ledger.WasteCost)+"\n총 수익 "+Won(ledger.Profit),artText);
            var list=new Rect(area.x,area.y+96,area.width,area.height-96);
            if(ledger.Orders.Count==0){GUI.Label(list,"아직 정산된 주문이 없어요.",artSmall);return;}
            statusScroll=GUI.BeginScrollView(list,statusScroll,new Rect(0,0,list.width-22,ledger.Orders.Count*58),false,true);
            int chosen=-1;
            for(int i=0;i<ledger.Orders.Count;i++)
            {
                var o=ledger.Orders[i];
                if(GUI.Button(new Rect(0,i*58,list.width-24,48),o.personaName+" · "+(o.timedOut?"시간 초과":o.success?"제공 성공":"제공 실패")+"   /   "+Won(o.paid),artButton))chosen=i;
            }
            GUI.EndScrollView();
            if(chosen>=0){statusLedgerIndex=chosen;OpenStatusPage(StatusPage.LedgerDetail);}
        }

        void DrawStatusLedger(Rect area)
        {
            var entries=businessDay?.Ledger.Orders;
            if(entries==null||statusLedgerIndex<0||statusLedgerIndex>=entries.Count){GUI.Label(area,"선택한 정산 항목이 없어요.",artSmall);return;}
            var o=entries[statusLedgerIndex];
            string detail=o.personaName+"\n\n"+o.label+"\n결제 금액: "+Won(o.paid)+"\n팁: "+Won(o.tip)+"\n"+
                (o.timedOut?"음료를 받기 전에 떠난 손님이에요.":o.success?"음료를 제공한 주문이에요.":"제공에 실패해 재료 손실로 집계된 주문이에요.");
            GUI.Label(area,detail,artText);
        }
    }
}
