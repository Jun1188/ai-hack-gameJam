#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CafeSim
{
    // Opt-in partial preview scene only. Not installed in the game or included in player code.
    [DefaultExecutionOrder(-1000)]
    public sealed class CafeReceiptSunsetPreview : MonoBehaviour
    {
        public enum View { Receipt, Sunset, Status }
        public enum ReceiptCase { Mixed, Empty, Loss, Large }
        public CafeHud hud;
        public CafeSession session;
        public CafeBusinessDay day;
        public CafeDaylight daylight;
        public View view;
        public ReceiptCase receiptCase;
        public float previewHour = 18;
        public int expectedSales, expectedTips, expectedWaste, expectedProfit;
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        Persona[] people;
        Font labelFont;

        [UnityEditor.Callbacks.DidReloadScripts]
        static void RecoverAfterScriptReload()
        {
            if(!UnityEditor.EditorApplication.isPlaying)return;
            UnityEditor.EditorApplication.delayCall+=()=>{
                var preview=FindAnyObjectByType<CafeReceiptSunsetPreview>();
                if(preview!=null&&Application.isPlaying)preview.Start();
            };
        }

        void Awake()
        {
            // Other loaded editor scenes are preserved, but do not run behind this preview.
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var scene=SceneManager.GetSceneAt(i);
                if(scene==gameObject.scene)continue;
                foreach(var root in scene.GetRootGameObjects())root.SetActive(false);
            }
            session.enabled=false;
            if(session.model!=null)session.model.enabled=false;
            session.model=null;
            day.enabled=false;
            day.gameSecondsPerSecond=0;
            foreach(var source in FindObjectsByType<AudioSource>()){source.Stop();source.enabled=false;}
        }

        void Start()
        {
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i)!=gameObject.scene)
                foreach(var root in SceneManager.GetSceneAt(i).GetRootGameObjects())root.SetActive(false);
            people=JsonUtility.FromJson<PersonaTable>(session.personaData.text).items;
            var menu=JsonUtility.FromJson<MenuTable>(session.menuData.text);
            Set(session,"Menu",menu);
            var queue=new CafeQueueSim(people,menu,new CafeKnowledge(),new CafeQueueSettings(),7);
            Set(session,"Queue",queue);
            Set(session,"Conversation",new CafeConversation());
            typeof(CafeHud).GetField("notebookAutoChecked",Fields).SetValue(hud,true);
            var stage=typeof(CafeHud).GetField("tutorialStage",Fields);
            stage.SetValue(hud,Enum.Parse(stage.FieldType,"Done"));
            SeedHistory(queue);
            SetCase(receiptCase);
            SetView(view);
        }

        static void Set(object target,string name,object value)=>target.GetType().GetProperty(name).SetValue(target,value);
        CafeVisit Visit(int id,int person,string menu,string label,float accuracy,int stars)=>new CafeVisit {
            Id=id,Person=people[person%people.Length],VisitNumber=1,State=VisitState.Received,
            Order=new CustomerOrder{menuId=menu,temperature="HOT",speech="따뜻하고 편안한 음료 한 잔 부탁해요."},
            RequestedName=label,ServedName=label,ServedTemperature="HOT",Accuracy=accuracy,
            OrderedAt=10,ResolvedAt=40,ActiveWait=30,Reaction=new CustomerReaction{stars=stars,speech="고마워요.",reason="음료를 천천히 즐길 수 있었어요."},RatingPublished=true
        };

        void SeedHistory(CafeQueueSim queue)
        {
            var past=Visit(1,0,"menu_americano","아메리카노",1,5);
            past.Order.speech="따뜻하고 깔끔한 음료 한 잔 부탁하네.";
            past.History.Add("ORDER\n"+past.Order.speech);past.History.Add("USER\n따뜻하게 준비해 드릴게요.");past.History.Add("REPLY\n고맙네. 느긋하게 마시고 가야겠구먼.");
            var current=Visit(7,1,"menu_latte","카페라떼",1,4);current.State=VisitState.Active;current.RatingPublished=false;
            current.Order.speech="따뜻하고 고소한 음료로 한 잔 부탁해요.";current.History.Add("ORDER\n"+current.Order.speech);
            queue.Active.Add(current);queue.Completed.Add(past);
            Set(session,"Selected",current);
            var archive=(List<CafeVisit>)typeof(CafeSession).GetField("conversationVisits",Fields).GetValue(session);
            archive.Clear();
            archive.Add(past);archive.Add(current);
        }

        public void SetCase(ReceiptCase next)
        {
            RequirePlayMode();
            receiptCase=next;
            var ledger=new CafeDayLedger();
            typeof(CafeBusinessDay).GetField("Ledger").SetValue(day,ledger);
            expectedSales=expectedTips=expectedWaste=expectedProfit=0;
            if(next==ReceiptCase.Mixed)
            {
                ledger.Served(Visit(1,0,"menu_americano","아메리카노",1,5),4500,500,1600);
                ledger.Served(Visit(2,1,"menu_latte","카페라떼",1,4),5000,500,1600);
                ledger.Served(Visit(3,2,"menu_yuja","유자차",1,3),5000,500,1600);
                ledger.Served(Visit(4,3,"menu_condensed","연유라떼",1,5),5500,500,1600);
                ledger.Served(Visit(5,4,"menu_matcha","말차라떼",.2f,1),5000,500,1600);
                var expired=Visit(6,5,"menu_latte","카페라떼",0,0);expired.State=VisitState.TimedOut;ledger.TimedOut(expired);
                expectedSales=20000;expectedTips=2500;expectedWaste=1600;expectedProfit=20900;
            }
            else if(next==ReceiptCase.Loss)
            {
                ledger.Served(Visit(1,0,"menu_latte","카페라떼",.1f,1),5000,500,1600);
                ledger.Served(Visit(2,1,"menu_latte","카페라떼",.1f,1),5000,500,1600);
                expectedWaste=3200;expectedProfit=-3200;
            }
            else if(next==ReceiptCase.Large)
            {
                ledger.Served(Visit(1,0,"menu_americano","단체 주문 A",1,5),1234500,500,1600);
                ledger.Served(Visit(2,1,"menu_latte","단체 주문 B",1,5),2345600,500,1600);
                ledger.Served(Visit(3,2,"menu_latte","카페라떼",0,1),5000,500,1600);
                expectedSales=3580100;expectedTips=2000;expectedWaste=1600;expectedProfit=3580500;
            }
            if(!AmountsMatch)throw new InvalidOperationException("Receipt preview amount mismatch");
        }

        public bool AmountsMatch=>day.Ledger.Sales==expectedSales&&day.Ledger.Tips==expectedTips&&day.Ledger.WasteCost==expectedWaste&&day.Ledger.Profit==expectedProfit;

        public void SetHour(float hour)
        {
            RequirePlayMode();
            previewHour=Mathf.Clamp(hour,9,18);
            Set(day,"Clock",previewHour*3600);
        }

        public void SetView(View next)
        {
            RequirePlayMode();
            view=next;
            typeof(CafeHud).GetMethod("CloseConversations",Fields).Invoke(hud,null);
            Set(day,"Closed",next==View.Receipt);
            Set(day,"Paused",false);
            SetHour(previewHour);
            hud.enabled=next!=View.Sunset;
            var canvas=(RectTransform)typeof(CafeHud).GetField("hudCanvas",Fields).GetValue(hud);
            if(canvas!=null)canvas.gameObject.SetActive(next!=View.Sunset);
            if(next==View.Status)typeof(CafeHud).GetMethod("ToggleConversationList",Fields).Invoke(hud,null);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }

        static void RequirePlayMode()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play Mode in the partial preview scene first.");
        }

        void OnGUI()
        {
            var matrix=GUI.matrix;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)*.5f+950*scale,(Screen.height-720*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
            GUI.depth=-200;
            var style=new GUIStyle(GUI.skin.label){fontSize=14};
            labelFont??=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","맑은 고딕"},14);style.font=labelFont;
            GUI.Box(new Rect(16,548,294,156),GUIContent.none);
            GUI.Label(new Rect(28,558,270,24),"부분 검증 · API / 손님 큐 / 제조 비활성",style);
            if(GUI.Button(new Rect(28,588,80,28),"영수증"))SetView(View.Receipt);
            if(GUI.Button(new Rect(118,588,80,28),"노을"))SetView(View.Sunset);
            if(GUI.Button(new Rect(208,588,80,28),"현황"))SetView(View.Status);
            if(view==View.Receipt)
            {
                int next=GUI.SelectionGrid(new Rect(28,628,260,28),(int)receiptCase,new[]{"혼합","0원","적자","큰 금액"},4);
                if(next!=(int)receiptCase)SetCase((ReceiptCase)next);
                GUI.Label(new Rect(28,668,270,24),"예상 총 수익: "+expectedProfit.ToString("N0")+"원",style);
            }
            else
            {
                if(GUI.Button(new Rect(28,628,120,28),"09:00 아침"))SetHour(9);
                if(GUI.Button(new Rect(158,628,130,28),"18:00 노을"))SetHour(18);
                GUI.Label(new Rect(28,668,270,24),"고정 시각: "+previewHour.ToString("00.00")+"시",style);
            }
            GUI.matrix=matrix;
        }
    }
}
#endif
