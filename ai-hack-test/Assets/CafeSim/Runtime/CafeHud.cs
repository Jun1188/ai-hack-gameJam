using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace CafeSim
{
    [DefaultExecutionOrder(-90)]
    public sealed partial class CafeHud : MonoBehaviour
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
        GameObject inputRoot;
        TextField chatInput;
        bool inputFocused;
        Keyboard imeKeyboard;
        int compositionFrame=-10;
        bool inputCanEdit;
        Rect inputSlot;
        void OnEnable(){BindKeyboard();}
        void BindKeyboard()
        {
            if(imeKeyboard==Keyboard.current)return;
            if(imeKeyboard!=null)imeKeyboard.onIMECompositionChange-=OnComposition;
            imeKeyboard=Keyboard.current;
            if(imeKeyboard!=null)imeKeyboard.onIMECompositionChange+=OnComposition;
        }
        void Update()
        {
            BindKeyboard();
            if(mergedCafe){UpdateMerged();return;}
            if(CafeStartIris.BlocksInput)return;
            // UI Toolkit control state must not depend on whether IMGUI received a
            // Repaint. Game-view focus, cursor mode and selection can change between them.
            if(chatInput!=null)
            {
                inputCanEdit=session!=null&&session.CanChat&&!session.ChatBusy&&
                    (!session.ChatClosed||(session.Selected.OffMenu&&session.Selected.State==VisitState.Active))&&
                    (player==null||!player.Looking);
                chatInput.SetEnabled(inputCanEdit);
                if(!inputCanEdit){chatInput.Blur();inputFocused=false;}
            }
            if(player!=null)player.TextInputActive=inputFocused;
        }
        void OnDisable()
        {
            StopSpeechVoice();
            if (tutorialMenu != null) tutorialMenu.SetTutorialCardFocus(false);
            if (TutorialTabRequired)
            {
                tutorialStage = TutorialStage.Done;
                EndTutorialTabPrompt();
            }
            CloseTutorial(false);
            if(speechRoot!=null)speechRoot.gameObject.SetActive(false);
            if(imeKeyboard!=null)imeKeyboard.onIMECompositionChange-=OnComposition;imeKeyboard=null;
            if(inputRoot!=null)Destroy(inputRoot);inputRoot=null;chatInput=null;inputFocused=false;inputStyled=false;composition="";
            if(player!=null)player.TextInputActive=false;
            if(barista!=null)barista.ExternalUIActive=false;
            if(tutorialMenu!=null)tutorialMenu.AllowConversationToggle=false;
            if(session!=null)session.DialogueOutput-=OnDialogue;subscribed=false;
        }
        void EnsureChatInput()
        {
            if(chatInput!=null)return;
            inputRoot=new GameObject("Cafe Korean chat input");inputRoot.transform.SetParent(transform,false);
            var document=inputRoot.AddComponent<UIDocument>();
            document.panelSettings=Resources.Load<PanelSettings>("CafeChatPanelSettings");
            if(document.panelSettings==null){Debug.LogError("Missing CafeChatPanelSettings");return;}
            document.sortingOrder=100;
            var root=document.rootVisualElement;root.pickingMode=PickingMode.Ignore;
            chatInput=new TextField{ name="CafeChatInput", maxLength=300, multiline=false, isDelayed=false, selectAllOnFocus=false, selectAllOnMouseUp=false };
            chatInput.AddToClassList("cafe-chat-input");
            chatInput.style.position=Position.Absolute;
            chatInput.style.marginTop=0;chatInput.style.marginBottom=0;chatInput.style.marginLeft=0;chatInput.style.marginRight=0;
            chatInput.style.paddingTop=0;chatInput.style.paddingBottom=0;chatInput.style.paddingLeft=0;chatInput.style.paddingRight=0;
            var koreanFont=Resources.Load<UnityEngine.TextCore.Text.FontAsset>("CafeChatKoreanFont");
            if (artFont != null || koreanFont != null)
            {
                var inputFont = artFont != null ? FontDefinition.FromFont(artFont) : FontDefinition.FromSDFFont(koreanFont);
                chatInput.style.unityFontDefinition = inputFont;
                chatInput.Query<TextElement>().ForEach(element => element.style.unityFontDefinition = inputFont);
            }
            chatInput.style.color=InputText;chatInput.style.backgroundColor=InputBack;
            chatInput.RegisterValueChangedCallback(e=>draft=e.newValue);
            chatInput.RegisterCallback<FocusInEvent>(_=>{inputFocused=true;if(player!=null)player.TextInputActive=true;});
            chatInput.RegisterCallback<FocusOutEvent>(_=>{inputFocused=false;ResetComposition();if(player!=null)player.TextInputActive=false;});
            chatInput.RegisterCallback<KeyDownEvent>(e=>
            {
                if(CafeStartIris.BlocksInput)return;
                if(!string.IsNullOrEmpty(composition)||Time.frameCount-compositionFrame<=1)return;
                // Global overlay shortcuts are processed once in Update, even while this field is focused.
                // Esc is handled once in Update; Tab must not move focus out of the field.
                if(mergedCafe&&(e.keyCode==KeyCode.Tab||e.keyCode==KeyCode.Escape)){e.StopPropagation();e.PreventDefault();return;}
                if(e.keyCode==KeyCode.Escape){if(mergedCafe)SetPrompt(false);chatInput.Blur();e.StopPropagation();return;}
                if(e.keyCode!=KeyCode.Return&&e.keyCode!=KeyCode.KeypadEnter)return;
                e.StopPropagation();
                if(string.IsNullOrEmpty(composition)&&(!mergedCafe||CanSubmitConversation)&&session.SendChat(draft)){draft="";chatInput.SetValueWithoutNotify("");}
            },TrickleDown.TrickleDown);
            root.Add(chatInput);
        }
        void ResetComposition(){composition="";compositionFrame=-10;}
        void OnComposition(UnityEngine.InputSystem.LowLevel.IMECompositionString value)
        {
            if(!inputFocused||(mergedCafe&&!promptOpen)){ResetComposition();return;}
            composition=value.ToString();compositionFrame=Time.frameCount;
        }
        void OnApplicationFocus(bool focused)
        {if(!focused){ResetComposition();chatInput?.Blur();inputFocused=false;}}
        readonly Color panel = new Color(0.075f, 0.105f, 0.105f, 0.96f);
        readonly Color cream = new Color(0.95f, 0.92f, 0.82f);
        Color InputText=>mergedCafe?ink:cream;
        Color InputBack=>mergedCafe?paper:panel;
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
        }
        void Panel(Rect rect)
        {
            Color old = GUI.color; GUI.color = panel;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
        void ChatPanel()
        {
            var bounds=new Rect(20,20,420,598);
            if(inputSlot.height<=0){Panel(bounds);return;}
            // IMGUI is composited after UI Toolkit. Leave the real text field uncovered.
            Panel(new Rect(bounds.x,bounds.y,bounds.width,inputSlot.yMin-bounds.yMin));
            Panel(new Rect(bounds.x,inputSlot.yMax,bounds.width,bounds.yMax-inputSlot.yMax));
            Panel(new Rect(bounds.x,inputSlot.y,bounds.xMax>=inputSlot.x?inputSlot.x-bounds.x:0,inputSlot.height));
            Panel(new Rect(inputSlot.xMax,inputSlot.y,bounds.xMax-inputSlot.xMax,inputSlot.height));
        }
        void PositionChatInput(Rect slot,float scale)
        {
            inputSlot=slot;
            Vector3 position=GUI.matrix.MultiplyPoint3x4(new Vector3(slot.x,slot.y,0));
            Vector2 start=new Vector2(position.x,position.y),size=new Vector2(slot.width*scale,slot.height*scale);
            if(chatInput.panel!=null)
            {
                Vector2 end=RuntimePanelUtils.ScreenToPanel(chatInput.panel,start+size);
                start=RuntimePanelUtils.ScreenToPanel(chatInput.panel,start);
                size=end-start;
            }
            float inputScale=size.y/slot.height;
            chatInput.style.left=start.x;chatInput.style.top=start.y;
            chatInput.style.width=size.x;chatInput.style.height=size.y;
            chatInput.style.fontSize=17*inputScale;
            var inner=chatInput.Q(className:"unity-base-text-field__input");
            inner.style.minHeight=0;inner.style.height=Length.Percent(100);
            inner.style.paddingTop=3*inputScale;inner.style.paddingBottom=3*inputScale;
            inner.style.paddingLeft=14*inputScale;inner.style.paddingRight=14*inputScale;
            inner.style.minWidth=0;
            inner.style.marginTop=0;inner.style.marginBottom=0;inner.style.marginLeft=0;inner.style.marginRight=0;
            inner.style.color=InputText;inner.style.backgroundColor=InputBack;
            chatInput.Query<TextElement>().ForEach(element=>
            {
                element.style.color=InputText;element.style.minHeight=17*inputScale;element.style.flexGrow=1;
                element.style.marginTop=0;element.style.marginBottom=0;
                element.style.paddingTop=0;element.style.paddingBottom=0;
            });
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
            if (session == null || CafeStartIris.BlocksInput) return;
            Styles();
            EnsureChatInput();
            if(mergedCafe){DrawMerged();return;}
            var previousMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280f * scale) * .5f, (Screen.height - 720f * scale) * .5f, 0f), Quaternion.identity, Vector3.one * scale);
            ChatPanel();
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
            if(draftCustomer!=session.CustomerNumber){draft="";draftCustomer=session.CustomerNumber;chatInput?.SetValueWithoutNotify("");chatInput?.Blur();}
            bool menuInquiry=session.CanChat&&session.Selected.OffMenu&&session.Selected.State==VisitState.Active;
            GUILayout.Label(!session.CanChat?"후기를 확인한 뒤 다음 손님과 대화할 수 있어요":player!=null&&player.Looking?"E / Esc로 커서를 해제한 뒤 입력창을 누르세요":session.ChatBusy?session.ChatStatus:menuInquiry?"메뉴 밖 주문 · 원하는 메뉴를 물어보세요":session.ChatClosed?"대화를 마쳤어요":"손님에게 이야기하기 · 한글 입력 / Enter 전송",small);
            if(session.ChatClosed&&session.CanChat&&GUILayout.Button("다시 이야기하기",button))session.ReopenChat();
            GUI.enabled=session.CanChat&&!session.ChatBusy&&(!session.ChatClosed||menuInquiry)&&(player==null||!player.Looking);
            Rect inputRect=GUILayoutUtility.GetRect(0,38,GUILayout.ExpandWidth(true));
            if(chatInput!=null&&Event.current.type==EventType.Repaint)
            {
                PositionChatInput(new Rect(36+inputRect.x,34+inputRect.y,inputRect.width,38),scale);
            }
            bool composing=!string.IsNullOrEmpty(composition);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("보내기",button)&&!composing&&session.SendChat(draft)){draft="";chatInput?.SetValueWithoutNotify("");}
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
