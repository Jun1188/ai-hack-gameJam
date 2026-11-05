using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace CafeSim
{
    public sealed partial class CafeHud
    {
        public bool mergedCafe;
        public MapleCafe.BaristaView barista;
        public float bubbleSeconds=6f;
        bool detailsOpen, promptOpen, subscribed;
        string latestSpeech="", keyDraft="";
        float bubbleUntil;
        readonly Vector2[] detailScroll=new Vector2[4];
        int detailPage;
        GUIStyle sectionTitle;
        float ratingBubbleUntil;
        string deferredSpeech;
        void Start(){if(mergedCafe&&session!=null){session.DialogueOutput+=OnDialogue;subscribed=true;}}
        int logCount;
        void OnDialogue(CafeVisit visit,string kind,string line)
        {
            if(kind=="USER")return;
            if(kind=="SERVED"){int i=line.IndexOf(" | similarity=");if(i>=0)line=line.Substring(0,i);}
            string speech=visit.Person.name+" · "+(kind=="RATING"?visit.Reaction.stars+" / 5":"손님")+"\n"+line;
            if(kind!="RATING"&&Time.unscaledTime<ratingBubbleUntil){deferredSpeech=speech;return;}
            latestSpeech=speech; bubbleUntil=Time.unscaledTime+bubbleSeconds;
            if(kind=="RATING")ratingBubbleUntil=bubbleUntil;
        }
        void SetPrompt(bool value)
        {
            promptOpen=value;
            if(!value){chatInput?.Blur();inputFocused=false;}
            ApplyOverlayControls();
            if(value&&chatInput!=null)chatInput.schedule.Execute(()=>{if(promptOpen)chatInput.Focus();}).ExecuteLater(80);
        }
        void UpdateMerged()
        {
            if(!subscribed&&session!=null){session.DialogueOutput+=OnDialogue;subscribed=true;}
            EnsureChatInput();
            if(deferredSpeech!=null&&Time.unscaledTime>=ratingBubbleUntil){latestSpeech=deferredSpeech;deferredSpeech=null;bubbleUntil=Time.unscaledTime+bubbleSeconds;}
            if(draftCustomer!=session.CustomerNumber){draft="";draftCustomer=session.CustomerNumber;chatInput?.SetValueWithoutNotify("");SetPrompt(false);}
            var keys=Keyboard.current;
            if(Application.isFocused&&keys!=null)
            {
                if(keys.tabKey.wasPressedThisFrame)HandleOverlayKey(KeyCode.Tab);
                else if(keys.eKey.wasPressedThisFrame)HandleOverlayKey(KeyCode.E);
                else if(keys.escapeKey.wasPressedThisFrame)HandleOverlayKey(KeyCode.Escape);
            }
            inputCanEdit=promptOpen&&session.CanChat&&!session.ChatBusy&&!session.ChatClosed;
            if(chatInput!=null)
            {
                chatInput.style.display=promptOpen?DisplayStyle.Flex:DisplayStyle.None;
                chatInput.SetEnabled(inputCanEdit);
            }
            ApplyOverlayControls();
        }
        void ApplyOverlayControls()
        {
            bool open=promptOpen||detailsOpen;
            if(barista!=null)barista.ExternalUIActive=open;
            if(open){UnityEngine.Cursor.lockState=CursorLockMode.None;UnityEngine.Cursor.visible=true;}
        }
        int shortcutFrame=-1;
        void HandleOverlayKey(KeyCode key)
        {
            if(shortcutFrame==Time.frameCount)return;
            if(key!=KeyCode.Tab&&key!=KeyCode.E&&key!=KeyCode.Escape)return;
            if(key==KeyCode.E&&detailsOpen&&GUIUtility.keyboardControl!=0)return;
            if(!string.IsNullOrEmpty(composition)||Time.frameCount-compositionFrame<=1)return;
            shortcutFrame=Time.frameCount;
            if(key==KeyCode.Tab){detailsOpen=!detailsOpen;SetPrompt(false);}
            else if(key==KeyCode.E)SetPrompt(!promptOpen&&session.CanChat);
            else {detailsOpen=false;SetPrompt(false);}
            ApplyOverlayControls();
            if(!promptOpen&&!detailsOpen)
            {
                bool resumeLook=key!=KeyCode.Escape;
                UnityEngine.Cursor.lockState=resumeLook?CursorLockMode.Locked:CursorLockMode.None;
                UnityEngine.Cursor.visible=!resumeLook;
            }
        }
        void DrawMerged()
        {
            var previous=GUI.matrix;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f),Quaternion.identity,Vector3.one*scale);
            if(detailsOpen)DrawDetails();
            if(Time.unscaledTime<bubbleUntil&&!string.IsNullOrEmpty(latestSpeech))
            {
                float height=Mathf.Min(230,bubble.CalcHeight(new GUIContent(latestSpeech),448));
                Panel(new Rect(24,24,448,height+8));GUI.Label(new Rect(24,24,448,height),latestSpeech,bubble);
                GUI.Label(new Rect(50,height+24,30,25),"▼",text);
            }
            if(promptOpen)
            {
                Panel(new Rect(24,573,830,47));
                GUI.Label(new Rect(40,581,798,32),session.ChatBusy?session.ChatStatus:session.ChatError??"Enter 전송 · E / Esc 닫기",small);
                if(Event.current.type==EventType.Repaint)PositionChatInput(new Rect(24,624,700,42),scale);
                GUI.enabled=inputCanEdit&&string.IsNullOrEmpty(composition);
                if(GUI.Button(new Rect(736,624,118,42),"전송",button)&&session.SendChat(draft)){draft="";chatInput.SetValueWithoutNotify("");}
                GUI.enabled=true;
            }
            Panel(new Rect(940,674,316,32));
            GUI.Label(new Rect(950,678,300,27),promptOpen?"E 입력 닫기 · Tab 기록 / 상태":"E 손님과 대화 · Tab 기록 / 상태",small);
            if(!detailsOpen&&session.Selected?.State==VisitState.Active)
            {
                Panel(new Rect(730,24,526,34));
                GUI.Label(new Rect(742,29,500,28),session.Person.name+" · 주문 제한 "+Mathf.CeilToInt(Mathf.Max(0,session.queueSettings.activeTimeoutSeconds-session.Selected.ActiveWait))+"초",small);
            }
            GUI.matrix=previous;
        }
        void DrawDetails()
        {
            if(sectionTitle==null)sectionTitle=new GUIStyle(title){fontSize=20};
            // One narrow panel and one scroll region per page keep the cafe visible.
            Panel(new Rect(886,20,370,640));
            GUI.Label(new Rect(902,32,280,30),"카페 기록 / 관리",sectionTitle);
            if(GUI.Button(new Rect(1210,30,30,28),"×")){detailsOpen=false;SetPrompt(false);}
            GUILayout.BeginArea(new Rect(902,72,338,572));
            int nextPage=GUILayout.Toolbar(detailPage,new[]{"주문","메뉴","대화","설정"},GUILayout.Height(32));
            if(nextPage!=detailPage){detailPage=nextPage;GUI.FocusControl(null);}
            GUILayout.Space(10);
            if(detailPage==2&&logCount!=session.DialogueLog.Count)
            {detailScroll[2].y=100000;logCount=session.DialogueLog.Count;}
            detailScroll[detailPage]=GUILayout.BeginScrollView(detailScroll[detailPage],false,false);
            if(detailPage==0)DrawOrderDetails();
            else if(detailPage==1)DrawServeDetails();
            else if(detailPage==2)
            {
                GUILayout.Label("전체 대화 기록",sectionTitle);
                if(session.DialogueLog.Count==0)GUILayout.Label("손님과 대화를 시작하면 기록이 남아요.",small);
                foreach(string entry in session.DialogueLog)
                {GUILayout.Space(8);GUILayout.Label(entry,bubble);}
            }
            else DrawConnectionDetails();
            GUILayout.Space(12);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        void DrawOrderDetails()
        {
            GUILayout.Label("현재 주문",sectionTitle);
            GUILayout.Label(PhaseText(),small);
            if(session.Person!=null)
            {
                GUILayout.Space(8);
                GUILayout.Label("#"+session.CustomerNumber+" · "+session.Person.name,text);
                GUILayout.Label(session.Person.ageDescription,small);
                GUILayout.Label(session.Person.setting,small);
                if(session.Selected.State==VisitState.Active)
                {
                    GUILayout.Label("남은 시간 "+Mathf.CeilToInt(Mathf.Max(0,session.queueSettings.activeTimeoutSeconds-session.Selected.ActiveWait))+"초",text);
                    GUILayout.Label(session.Order.speech,bubble);
                    if(GUILayout.Button("메뉴 제공하기",button))detailPage=1;
                }
            }
            if(session.Queue==null)return;
            GUILayout.Space(18);GUILayout.Label("줄서기 · "+session.Queue.Inactive.Count+"명",sectionTitle);
            foreach(var c in session.Queue.Inactive)
                GUILayout.Label("#"+c.Id+" "+c.Person.name+" · "+c.InactiveWait.ToString("F0")+"초 대기",small);
            GUILayout.Space(18);GUILayout.Label("손님 후기",sectionTitle);
            bool hasReview=false;
            foreach(var c in session.Queue.Completed)if(c.RatingPublished)
            {
                hasReview=true;GUILayout.Space(8);
                GUILayout.Label("#"+c.Id+" "+c.Person.name+" · "+c.Reaction.stars+" / 5",text);
                GUILayout.Label(c.Reaction.reason,small);
            }
            if(!hasReview)GUILayout.Label("아직 등록된 후기가 없어요.",small);
        }
        void DrawServeDetails()
        {
            GUILayout.Label("메뉴 제공",sectionTitle);
            GUILayout.Label(session.CanChat?session.Person.name+" 손님에게 제공":"주문 중인 손님이 없어요.",small);
            GUILayout.Label("M 메뉴 카드 · E 대화 입력",small);
            if(session.Menu?.items==null)return;
            foreach(var item in session.Menu.items)
            {
                GUILayout.Space(14);GUILayout.Label(item.name,text);GUILayout.Label(item.description,small);
                GUILayout.BeginHorizontal();GUI.enabled=session.CanChat&&session.Selected.State==VisitState.Active;
                if(item.hot&&GUILayout.Button("HOT",button))session.Serve(item.id,"HOT");
                if(item.ice&&GUILayout.Button("ICE",button))session.Serve(item.id,"ICE");
                GUI.enabled=true;GUILayout.EndHorizontal();
            }
        }
        void DrawConnectionDetails()
        {
            GUILayout.Label("응답 연결",sectionTitle);
            GUILayout.Label(session.model.Status,small);
            GUILayout.Label("응답 출처: "+(session.LastReplySource??"대기 중")+" · "+session.LastReplySeconds.ToString("F1")+"초",small);
            if(session.ChatError!=null)GUILayout.Label(session.ChatError,small);
            if(session.Error!=null)GUILayout.Label(session.Error,small);
            if(session.ChatBusy&&GUILayout.Button("응답 중단",button))session.CancelChat();
            if(session.ChatClosed&&session.CanChat&&GUILayout.Button("다시 대화",button))session.ReopenChat();
            GUILayout.Space(18);GUILayout.Label("API 설정",sectionTitle);
            if(GUILayout.Button("공급자: Gemini",button)){session.CancelChat();session.model.ConfigureGemini();session.RebootChatModel();}
            GUILayout.Label("모델",small);
            session.model.apiModel=GUILayout.TextField(session.model.apiModel,GUILayout.Height(30));
            GUILayout.Label("API 키 · 이번 실행에만 적용",small);
            keyDraft=GUILayout.PasswordField(keyDraft,'●',GUILayout.Height(30));
            if(GUILayout.Button("설정 적용 / 연결 재시작",button))
            {session.CancelChat();session.model.runtimeApiKey=keyDraft.Trim();keyDraft="";session.RebootChatModel();}
            GUILayout.Space(18);GUILayout.Label("세션 관리",sectionTitle);
            if(GUILayout.Button("스테이지 초기화",button))session.RestartModel();
            GUILayout.Label("Tab 닫기 · 마우스 휠로 전체 내용 보기",small);
        }
    }
}
