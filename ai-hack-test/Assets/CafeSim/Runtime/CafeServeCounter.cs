using System;
using UnityEngine;

namespace CafeSim
{
    // Lets the AutumnCafe brew station hand a finished mug to the ordering customer.
    // The target box covers the customer's half of the counter and the customer standing there.
    public sealed class CafeServeCounter : MonoBehaviour, MapleCafe.IDrinkReceiver
    {
        public CafeSession session;
        public MapleCafe.CafeBrewStation station;
        public BoxCollider serveTarget;
        public CharacterController player;

        public Collider Target => serveTarget;

        void Awake()
        {
            if(station!=null)station.Receiver=this;
            if(player!=null&&serveTarget!=null)Physics.IgnoreCollision(player,serveTarget);
        }
        void OnDestroy(){if(station!=null&&ReferenceEquals(station.Receiver,this))station.Receiver=null;}

        CafeVisit Ordering=>session!=null&&session.Selected!=null&&session.Selected.State==VisitState.Active?session.Selected:null;

        public string Describe(string heldDrink,bool iced)
        {
            var c=Ordering;
            if(c==null)return session!=null&&session.Selected!=null&&session.Selected.State==VisitState.Received?"음료를 맛보는 중이에요":"주문한 손님이 없어요";
            if(heldDrink==null)return "완성된 머그잔을 들고 오세요";
            if(heldDrink==MapleCafe.CafeBrewStation.InvalidDrinkName)return "신비한 음료는 드릴 수 없어요  ·  싱크대에서 비워 주세요";
            if(Find(heldDrink)==null)return heldDrink+"는 메뉴에 없어요";
            if (CustomerAtCounter(c) == null) return "손님이 카운터로 오는 중이에요";
            return "F  ·  손님에게 "+CafeQueueSim.Label(heldDrink,iced?"ICE":"HOT")+" 드리기";
        }

        public bool TryReceive(string drink, bool iced, out Transform mugGrip)
        {
            mugGrip = null;
            var item = Find(drink);
            var customer = CustomerAtCounter(Ordering);
            if (item == null || customer == null) return false;
            mugGrip = customer.PrepareMugGrip();
            return mugGrip != null && session.Serve(item.id, iced ? "ICE" : "HOT");
        }

        CafeCustomerAppearance CustomerAtCounter(CafeVisit visit)
        {
            var body = session != null ? session.BodyOf(visit) : null;
            if (body == null || (body.position - session.servicePoint.position).sqrMagnitude > .04f)
                return null;
            return body.GetComponent<CafeCustomerAppearance>();
        }

        MenuItem Find(string drink)=>session?.Menu?.items==null?null:Array.Find(session.Menu.items,m=>m.name==drink);
    }
}
