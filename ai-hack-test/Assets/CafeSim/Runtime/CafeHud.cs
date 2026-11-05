using UnityEngine;
using UnityEngine.InputSystem;

namespace CafeSim
{
    public sealed class CafeHud : MonoBehaviour
    {
        public CafeSession session;
        public CafeFirstPersonController player;
        GUIStyle title, text, small, bubble, button, stars;
        Vector2 chatScroll, menuScroll;
        int previousChatCount;
        int queueTab;
        Vector2 queueScroll;
        Font font;
        string draft="",composition="";
        int draftCustomer;
        GUIStyle inputStyle;
        void OnEnable(){if(Keyboard.current!=null){Keyboard.current.onIMECompositionChange+=OnComposition;Keyboard.current.SetIMEEnabled(true);}}
        void OnDisable(){if(Keyboard.current!=null)Keyboard.current.onIMECompositionChange-=OnComposition;if(player!=null)player.TextInputActive=false;}
        void OnComposition(UnityEngine.InputSystem.LowLevel.IMECompositionString value){composition=value.ToString();}
        readonly Color panel = new Color(0.075f, 0.105f, 0.105f, 0.96f);
        readonly Color cream = new Color(0.95f, 0.92f, 0.82f);
        void Styles()
        {
            if (text != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 18);
            text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 17, wordWrap = true, richText = false };
            text.normal.textColor = cream;
            title = new GUIStyle(text) { fontSize = 25, fontStyle = FontStyle.Bold };
            small = new GUIStyle(text) { fontSize = 14 };
            small.normal.textColor = new Color(0.69f, 0.79f, 0.76f);
            bubble = new GUIStyle(text) { padding = new RectOffset(14, 14, 12, 12) };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 16, fixedHeight = 36, richText = false };
            stars = new GUIStyle(title) { fontSize = 32 }; stars.normal.textColor = new Color(1f, 0.77f, 0.35f);
            inputStyle=new GUIStyle(GUI.skin.textField){font=font,fontSize=17,richText=false,padding=new RectOffset(8,8,7,7)};
        }
        void Panel(Rect rect)
        {
            Color old = GUI.color; GUI.color = panel;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
        string PhaseText()
        {
            switch (session.Phase)
            {
                case CafePhase.Loading: return session.model != null ? session.model.Status : "모델 준비 중";
                case CafePhase.Arriving: return "새 손님이 입장하고 있어요";
                case CafePhase.Ordering: return "접수 전 대기 중 · 서빙 대기 손님을 선택할 수 있어요";
                case CafePhase.Choosing: return "주문을 듣고 오른쪽 메뉴를 제공하세요";
                case CafePhase.Reacting: return "손님이 음료를 맛보고 있어요…";
                case CafePhase.Reaction: return "손님의 반응";
                case CafePhase.Rating: return "손님의 평가 · 잠시 후 다음 손님이 옵니다";
                case CafePhase.Leaving: return "손님이 돌아가고 있어요";
                default: return "응답을 확인해 주세요";
            }
        }
        void OnGUI()
        {
            if (session == null) return;
            Styles();
            var previousMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280f * scale) * .5f, (Screen.height - 720f * scale) * .5f, 0f), Quaternion.identity, Vector3.one * scale);
            Panel(new Rect(20, 20, 420, 598));
            GUILayout.BeginArea(new Rect(36, 34, 388, 568));
            GUILayout.Label("느린 한 잔", title);
            GUILayout.Label("LOCAL CAFÉ  /  손님 주문 테스트", small);
            GUILayout.Space(12);
            if (session.Person != null)
            {
                GUILayout.Label("손님 " + session.CustomerNumber.ToString("00") + "  ·  " + session.Person.name, text);
                GUILayout.Label(string.IsNullOrEmpty(session.Person.ageDescription)?session.Person.age+"세 · "+session.Person.gender:session.Person.ageDescription+" · "+session.Person.group, small);
                GUILayout.Label(session.Person.setting, small);
            }
            GUILayout.Space(10);
            GUILayout.Label(PhaseText(), text);
            GUILayout.Space(8);
            if (previousChatCount != session.ChatRevision) { chatScroll.y = 100000; previousChatCount = session.ChatRevision; }
            chatScroll = GUILayout.BeginScrollView(chatScroll, GUILayout.ExpandHeight(true));
            foreach (var message in session.Chat)
            {
                var background = GUI.backgroundColor; GUI.backgroundColor = new Color(.3f, .44f, .4f);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(message, bubble);
                GUILayout.EndVertical(); GUI.backgroundColor = background;
                GUILayout.Space(8);
            }
            GUILayout.EndScrollView();
            if(draftCustomer!=session.CustomerNumber){draft="";draftCustomer=session.CustomerNumber;GUI.FocusControl(null);}
            bool menuInquiry=session.CanChat&&session.Selected.OffMenu&&session.Selected.State==VisitState.Active;
            GUILayout.Label(session.ChatBusy?session.ChatStatus:menuInquiry?"메뉴 밖 주문 · 원하는 메뉴를 물어보세요":session.ChatClosed?"대화를 마쳤어요":"손님에게 이야기하기 · 한글 입력 / Enter 전송",small);
            if(session.ChatClosed&&session.CanChat&&GUILayout.Button("다시 이야기하기",button))session.ReopenChat();
            GUI.enabled=session.CanChat&&!session.ChatBusy&&(!session.ChatClosed||menuInquiry)&&(player==null||!player.Looking);
            GUI.SetNextControlName("CafeChatInput");
            draft=GUILayout.TextField(draft,300,inputStyle,GUILayout.Height(38));
            bool typing=GUI.GetNameOfFocusedControl()=="CafeChatInput"&&GUI.enabled;
            if(player!=null)player.TextInputActive=typing;
            var evt=Event.current;
            bool enter=typing&&evt.type==EventType.KeyDown&&(evt.keyCode==KeyCode.Return||evt.keyCode==KeyCode.KeypadEnter);
            bool composing=!string.IsNullOrEmpty(composition);
            if(enter){evt.Use();if(!composing&&session.SendChat(draft)){draft="";GUI.FocusControl("CafeChatInput");}}
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("보내기",button)&&!composing&&session.SendChat(draft))draft="";
            GUI.enabled=true;
            if(session.ChatBusy&&GUILayout.Button("응답 중단",button))session.CancelChat();
            GUILayout.EndHorizontal();
            if(!string.IsNullOrEmpty(session.ChatError))GUILayout.Label(session.ChatError,small);
            GUILayout.Label(session.model!=null?session.model.Status:"로컬 모델 준비 전",small);
            if(session.model!=null&&!session.model.Ready&&!session.ChatBusy&&GUILayout.Button("응답 연결 재시작",button))session.RebootChatModel();
            if (session.Phase == CafePhase.Error)
            {
                GUILayout.Label(session.Error, text);
                if (session.Person != null && GUILayout.Button("같은 요청 다시 시도", button)) session.Retry();
                if (GUILayout.Button("테스트 초기화", button)) session.RestartModel();
            }
            GUILayout.EndArea();

            Panel(new Rect(930, 20, 330, 598));
            GUILayout.BeginArea(new Rect(946, 34, 298, 568));
            GUILayout.Label("메뉴 제공", title);
            GUILayout.Label(session.Person == null ? "서빙 대기 손님을 먼저 선택하세요" : "#"+session.CustomerNumber+" "+session.Person.name+"에게 제공", small);
            GUILayout.Space(8);
            menuScroll = GUILayout.BeginScrollView(menuScroll);
            if (session.Menu?.items != null)
            {
                foreach (var item in session.Menu.items)
                {
                    GUILayout.Label(item.name, text);
                    GUILayout.Label(item.description, small);
                    GUILayout.BeginHorizontal();
                    GUI.enabled = session.Phase == CafePhase.Choosing && player != null && !player.Looking;
                    if (item.hot && GUILayout.Button("HOT", button)) session.Serve(item.id, "HOT");
                    if (item.ice && GUILayout.Button("ICE", button)) session.Serve(item.id, "ICE");
                    GUI.enabled = true;
                    GUILayout.EndHorizontal(); GUILayout.Space(10);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if(session.Queue!=null)
            {
                Panel(new Rect(460,20,450,270));
                GUILayout.BeginArea(new Rect(474,30,422,250));
                var q=session.Queue;
                queueTab=GUILayout.Toolbar(queueTab,new[]{"접수 전 "+q.Inactive.Count+"/"+q.Settings.inactiveCapacity,"서빙 대기 "+q.Active.Count,"후기 "+q.Completed.Count});
                GUILayout.Label(q.Inactive.Count>0?"다음 접수까지 약 "+Mathf.CeilToInt(q.ReceptionSecondsRemaining)+"초 · 활성 주문 "+q.Active.Count+"명":"손님을 선택한 뒤 오른쪽 메뉴를 제공하세요",small);
                queueScroll=GUILayout.BeginScrollView(queueScroll);
                var list=queueTab==0?q.Inactive:queueTab==1?q.Active:q.Completed;
                foreach(var c in list)
                {
                    if(queueTab==2&&!c.RatingPublished)continue;
                    string label="#"+c.Id+" "+c.Person.name+" · "+(queueTab==0?c.InactiveWait.ToString("F0")+"초 대기":queueTab==1?
                        c.ActiveWait.ToString("F0")+"초 대기":c.Reaction.stars+"점 · "+(c.State==VisitState.TimedOut?"시간 초과":"수령"));
                    if(GUILayout.Button((session.Selected==c?"▶ ":"")+label,button))session.SelectCustomer(c.Id);
                }
                GUILayout.EndScrollView();GUILayout.EndArea();
            }

            if (session.Phase == CafePhase.Rating && session.Reaction != null)
            {
                Panel(new Rect(460, 430, 450, 188));
                GUI.Label(new Rect(480, 440, 410, 45), session.Reaction.stars+"점  "+new string('★', session.Reaction.stars) + new string('☆', 5 - session.Reaction.stars), stars);
                GUI.Label(new Rect(480, 490, 410, 118), session.Reaction.reason, text);
            }
            Panel(new Rect(20, 636, 1240, 64));
            GUI.Label(new Rect(36, 647, 860, 40), "WASD 이동  ·  마우스 시점  ·  Tab 전환  ·  E / Esc 메뉴 선택\n접수 전 대기 → 주문 접수 → 서빙 대기 → 수령·평가 / 시간 초과 0점", small);
            if (player != null && GUI.Button(new Rect(960, 649, 280, 36), player.Looking ? "메뉴 선택으로 전환 [Tab]" : "시점 조작으로 전환 [Tab]", button)) player.SetLooking(!player.Looking);
            if (player != null && player.Looking) GUI.Label(new Rect(633, 350, 20, 25), "+", text);
            GUI.matrix = previousMatrix;
        }
    }
}
