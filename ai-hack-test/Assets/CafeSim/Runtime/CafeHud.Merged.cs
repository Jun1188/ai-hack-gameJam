using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace CafeSim
{
    // AutumnCafe integration: Tab stays with the drink reference cards, F stays with the
    // brew station (and serves the finished mug), E opens the customer conversation.
    // Customers are anonymous here: the order has to be inferred from what they say.
    public sealed partial class CafeHud
    {
        public bool mergedCafe;
        public MapleCafe.BaristaView barista;
        public Font artFont;
        public float bubbleSeconds=6f;
        bool promptOpen, subscribed;
        string latestSpeech="", latestKind, deferredSpeech, deferredKind, hint;
        int latestStars=-1, deferredStars=-1;
        CafeVisit speaker, deferredSpeaker;
        float bubbleStart, bubbleUntil, bubbleDuration, ratingBubbleUntil, hintUntil;
        Camera eyes;
        // Menu-card palette: warm paper, dark-brown ink, roasted-orange accent.
        internal static readonly Color paper=new Color32(253,246,228,255), ink=new Color32(66,42,28,255), accent=new Color32(190,104,46,255);
        Texture2D paperTex, tailTex, capTex, buttonTex, buttonHoverTex;
        GUIStyle artText, artSmall, artLabel, artStars, artCap, artButton, paperBox;
        bool inputStyled;
        void Start(){if(mergedCafe&&session!=null){session.DialogueOutput+=OnDialogue;subscribed=true;}}
        void OnDestroy(){foreach(var t in new[]{paperTex,tailTex,capTex,buttonTex,buttonHoverTex})if(t!=null)Destroy(t);DestroyDayTextures();}
        void OnDialogue(CafeVisit visit,string kind,string line)
        {
            if(kind=="USER"||kind=="SERVED")return;
            // Idle chatter never hides the order itself; it stays readable in the E panel.
            if(kind=="CHATTER"&&latestKind=="ORDER"&&Time.unscaledTime<bubbleUntil)return;
            int stars=kind=="RATING"?visit.Reaction.stars:-1;
            string speech=kind=="RATING"?visit.Reaction.reason:line;
            if(kind!="RATING"&&Time.unscaledTime<ratingBubbleUntil){if(kind=="CHATTER"&&deferredKind=="ORDER")return;deferredSpeech=speech;deferredSpeaker=visit;deferredStars=stars;deferredKind=kind;return;}
            ShowSpeech(visit,speech,stars);latestKind=kind;
            if(kind=="RATING")ratingBubbleUntil=bubbleUntil;
        }
        void ShowSpeech(CafeVisit visit,string speech,int stars)
        {
            // Long orders stay up long enough to read; the full history is in the E panel.
            latestSpeech=speech;speaker=visit;latestStars=stars;bubbleStart=Time.unscaledTime;
            bubbleDuration=Mathf.Max(bubbleSeconds,speech.Length*.11f);bubbleUntil=bubbleStart+bubbleDuration;
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
            // Speech and hint timers are real-time; hold them while the day is paused.
            if(Halted){float dt=Time.unscaledDeltaTime;bubbleStart+=dt;bubbleUntil+=dt;ratingBubbleUntil+=dt;hintUntil+=dt;if(promptOpen)SetPrompt(false);}
            if(deferredSpeech!=null&&Time.unscaledTime>=ratingBubbleUntil){ShowSpeech(deferredSpeaker,deferredSpeech,deferredStars);latestKind=deferredKind;deferredSpeech=null;deferredKind=null;}
            // Customers order on admission; hold the line until they have walked up to the counter.
            var walker=session.BodyOf(speaker);
            if(speaker!=null&&speaker.State==VisitState.Active&&walker!=null&&Time.unscaledTime<bubbleUntil&&
                (walker.position-session.servicePoint.position).sqrMagnitude>.04f)
            {bubbleStart=Time.unscaledTime;bubbleUntil=bubbleStart+bubbleDuration;}
            if(draftCustomer!=session.CustomerNumber){draft="";draftCustomer=session.CustomerNumber;chatInput?.SetValueWithoutNotify("");if(promptOpen)SetPrompt(false);}
            if(promptOpen&&!session.CanChat)SetPrompt(false);
            var keys=Keyboard.current;
            if(Application.isFocused&&keys!=null)
            {
                if(keys.eKey.wasPressedThisFrame)HandleOverlayKey(KeyCode.E);
                else if(keys.escapeKey.wasPressedThisFrame)HandleOverlayKey(KeyCode.Escape);
            }
            inputCanEdit=promptOpen&&session.CanChat&&!session.ChatBusy&&!session.ChatClosed;
            if(chatInput!=null)
            {
                StyleChatInput();
                chatInput.style.display=promptOpen?DisplayStyle.Flex:DisplayStyle.None;
                chatInput.SetEnabled(inputCanEdit);
            }
            ApplyOverlayControls();
            UpdateClock();
            cardsWereOpen=barista!=null&&barista.ChoosingMenuCard;
        }
        void StyleChatInput()
        {
            if(inputStyled)return;inputStyled=true;
            var inner=chatInput.Q(className:"unity-base-text-field__input");
            inner.style.borderTopWidth=inner.style.borderBottomWidth=inner.style.borderLeftWidth=inner.style.borderRightWidth=3;
            inner.style.borderTopColor=inner.style.borderBottomColor=inner.style.borderLeftColor=inner.style.borderRightColor=ink;
            inner.style.borderTopLeftRadius=inner.style.borderTopRightRadius=inner.style.borderBottomLeftRadius=inner.style.borderBottomRightRadius=12;
            if(artFont!=null)chatInput.style.unityFont=artFont;
            // Only the rounded inner box is painted; the outer element would show square corners.
            chatInput.style.backgroundColor=Color.clear;
        }
        void ApplyOverlayControls()
        {
            bool overlay=promptOpen||Halted;
            if(barista!=null)barista.ExternalUIActive=overlay;
            if(overlay){UnityEngine.Cursor.lockState=CursorLockMode.None;UnityEngine.Cursor.visible=true;}
        }
        int shortcutFrame=-1;
        void HandleOverlayKey(KeyCode key)
        {
            if(shortcutFrame==Time.frameCount)return;
            if(!string.IsNullOrEmpty(composition)||Time.frameCount-compositionFrame<=1)return;
            // While typing, E is a letter (and the first jamo of ㄷ); only Esc closes the field.
            if(key==KeyCode.E&&promptOpen&&(inputFocused||GUIUtility.keyboardControl!=0))return;
            // The drink cards own the cursor and Esc while they are open.
            if(barista!=null&&barista.ChoosingMenuCard)return;
            if(HandleDayKey(key)){shortcutFrame=Time.frameCount;return;}
            if(key==KeyCode.Escape&&!promptOpen)return;
            shortcutFrame=Time.frameCount;
            if(key==KeyCode.E&&!promptOpen&&!session.CanChat){ShowHint(session.Queue!=null&&session.Queue.Inactive.Count>0?"손님이 주문하러 오는 중이에요":"대화할 손님이 없어요");return;}
            SetPrompt(!promptOpen);
            if(!promptOpen){UnityEngine.Cursor.lockState=CursorLockMode.Locked;UnityEngine.Cursor.visible=false;}
        }
        void ShowHint(string value){hint=value;hintUntil=Time.unscaledTime+2.5f;}

        void ArtStyles()
        {
            if(artText!=null)return;
            var face=artFont!=null?artFont:font;
            paperTex=Rounded(64,22,paper,ink,3);
            capTex=Rounded(32,9,ink,ink,0);
            buttonTex=Rounded(32,10,accent,ink,2.5f);
            buttonHoverTex=Rounded(32,10,Color.Lerp(accent,Color.white,.18f),ink,2.5f);
            tailTex=Tail(40,30,paper,ink,3);
            artText=new GUIStyle{font=face,fontSize=18,wordWrap=true,richText=false,padding=new RectOffset(0,0,0,0)};artText.normal.textColor=ink;
            artSmall=new GUIStyle(artText){fontSize=15};
            artLabel=new GUIStyle(artSmall){fontStyle=FontStyle.Bold,wordWrap=false};artLabel.normal.textColor=accent;
            artStars=new GUIStyle(artText){fontSize=22,wordWrap=false};artStars.normal.textColor=accent;
            artCap=new GUIStyle(artSmall){fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,wordWrap=false,border=new RectOffset(10,10,10,10),padding=new RectOffset(8,8,2,3)};
            artCap.normal.background=capTex;artCap.normal.textColor=paper;
            artButton=new GUIStyle(artCap){fontSize=16,border=new RectOffset(12,12,12,12)};
            artButton.normal.background=buttonTex;artButton.hover.background=artButton.active.background=buttonHoverTex;
            artButton.hover.textColor=artButton.active.textColor=paper;
            paperBox=new GUIStyle{border=new RectOffset(24,24,24,24)};paperBox.normal.background=paperTex;
        }
        void Paper(Rect rect,bool shadow=true)
        {
            if(Event.current.type!=EventType.Repaint)return;
            if(shadow){var old=GUI.color;GUI.color=new Color(.16f,.09f,.05f,.28f);paperBox.Draw(new Rect(rect.x+4,rect.y+5,rect.width,rect.height),false,false,false,false);GUI.color=old;}
            paperBox.Draw(rect,false,false,false,false);
        }
        // Rounded card with an inked outline, drawn as a nine-slice background.
        internal static Texture2D Rounded(int size,float radius,Color fill,Color line,float width)
        {
            var tex=new Texture2D(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color[size*size];float half=size*.5f;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                Vector2 q=new Vector2(Mathf.Abs(x+.5f-half),Mathf.Abs(y+.5f-half))-Vector2.one*(half-1-radius);
                float d=Vector2.Max(q,Vector2.zero).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;
                var c=width>0?Color.Lerp(fill,line,Mathf.Clamp01(d+width+.5f)):fill;
                c.a*=Mathf.Clamp01(.5f-d);pixels[y*size+x]=c;
            }
            tex.SetPixels(pixels);tex.Apply();return tex;
        }
        // Comic tail: open along the top edge so it merges into the bubble above it.
        static Texture2D Tail(int w,int h,Color fill,Color line,float width)
        {
            var tex=new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color[w*h];
            Vector2 a=new Vector2(6,h+2),b=new Vector2(w-4,h+2),tip=new Vector2(3,1.5f);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                Vector2 p=new Vector2(x+.5f,y+.5f);
                float d=-Mathf.Min(Edge(p,tip,a),Edge(p,b,tip));
                var c=Color.Lerp(fill,line,Mathf.Clamp01(d+width+.5f));
                c.a=Mathf.Clamp01(.5f-d);pixels[y*w+x]=c;
            }
            tex.SetPixels(pixels);tex.Apply();return tex;
        }
        // Signed distance to the line through a→b, positive on the triangle's inner side.
        static float Edge(Vector2 p,Vector2 a,Vector2 b){Vector2 n=new Vector2(b.y-a.y,a.x-b.x).normalized;return Vector2.Dot(p-a,n);}

        void DrawMerged()
        {
            ArtStyles();
            var previous=GUI.matrix;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            Vector2 offset=new Vector2((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f);
            GUI.matrix=Matrix4x4.TRS(offset,Quaternion.identity,Vector3.one*scale);
            DayStyles();
            DrawSpeech(scale,offset);
            if(promptOpen)DrawChat(scale);
            if(!Halted)DrawControls();
            DrawDayOverlay();
            GUI.matrix=previous;
        }
        void DrawSpeech(float scale,Vector2 offset)
        {
            if(Time.unscaledTime>=bubbleUntil||string.IsNullOrEmpty(latestSpeech))return;
            var body=session.BodyOf(speaker);
            if(speaker!=null&&speaker.State==VisitState.Active&&body!=null&&(body.position-session.servicePoint.position).sqrMagnitude>.04f)return;
            const float width=330,padX=22,padY=16;
            float starsHeight=latestStars>=0?28:0;
            float textHeight=Mathf.Min(150,artText.CalcHeight(new GUIContent(latestSpeech),width-padX*2));
            float height=textHeight+starsHeight+padY*2;
            // Anchor above the speaker's head; fall back to the top centre when off-screen.
            Rect rect=new Rect(640-width*.5f,24,width,height);float tipX=-1;
            if(eyes==null&&barista!=null)eyes=barista.GetComponentInChildren<Camera>();
            if(body!=null&&eyes!=null)
            {
                Vector3 point=eyes.WorldToScreenPoint(body.position+Vector3.up*1.9f);
                float x=(point.x-offset.x)/scale,y=(Screen.height-point.y-offset.y)/scale;
                if(point.z>.2f&&x>-40&&x<1320&&y>-40&&y<760)
                {
                    rect.x=Mathf.Clamp(x-width*.3f,16,1264-width);rect.y=Mathf.Clamp(y-height-26,16,600-height);
                    if(promptOpen)rect.y=Mathf.Min(rect.y,330-height);
                    tipX=Mathf.Clamp(x,rect.x+24,rect.xMax-48);
                }
            }
            // A short pop when a new line starts, like a panel being slapped on.
            float pop=Mathf.SmoothStep(.82f,1f,(Time.unscaledTime-bubbleStart)/.14f);
            var unscaled=GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one*pop,new Vector2(tipX>=0?tipX:rect.center.x,rect.yMax));
            Paper(rect);
            if(tipX>=0&&Event.current.type==EventType.Repaint)GUI.DrawTexture(new Rect(tipX-6,rect.yMax-3,40,30),tailTex);
            if(latestStars>=0)GUI.Label(new Rect(rect.x+padX,rect.y+padY-2,width-padX*2,28),new string('★',latestStars)+new string('☆',5-latestStars),artStars);
            GUI.Label(new Rect(rect.x+padX,rect.y+padY+starsHeight,width-padX*2,textHeight),latestSpeech,artText);
            GUI.matrix=unscaled;
        }
        void DrawControls()
        {
            if(Time.unscaledTime<hintUntil)
            {
                float w=artSmall.CalcSize(new GUIContent(hint)).x+36;var r=new Rect(1256-w,668,w,38);
                Paper(r);GUI.Label(new Rect(r.x+18,r.y+10,w,24),hint,artSmall);return;
            }
            var keys=promptOpen?new[]{"Enter","전송","Esc","닫기"}:new[]{"E","손님과 대화","Tab","메뉴 카드","F","제조 · 서빙","Esc","일시정지"};
            float total=20;
            for(int i=0;i<keys.Length;i+=2)total+=Mathf.Max(32,artCap.CalcSize(new GUIContent(keys[i])).x)+8+artSmall.CalcSize(new GUIContent(keys[i+1])).x+18;
            Rect panel=new Rect(1256-total,664,total,42);Paper(panel);
            float x=panel.x+14;
            for(int i=0;i<keys.Length;i+=2)
            {
                float cap=Mathf.Max(32,artCap.CalcSize(new GUIContent(keys[i])).x);
                GUI.Label(new Rect(x,panel.y+8,cap,26),keys[i],artCap);x+=cap+8;
                float label=artSmall.CalcSize(new GUIContent(keys[i+1])).x;
                GUI.Label(new Rect(x,panel.y+11,label+4,24),keys[i+1],artSmall);x+=label+18;
            }
        }
        void DrawChat(float scale)
        {
            const float x=24,width=640,labelWidth=72;
            bool notReady=session.model!=null&&!session.model.Ready&&!session.ChatBusy;
            string status=session.ChatBusy?session.ChatStatus:session.ChatError??(session.ChatClosed?"대화를 마쳤어요":notReady?session.model.Status:"손님에게 말을 걸어 주문을 알아내 보세요");
            int first=Mathf.Max(0,session.Chat.Count-5);
            float textWidth=width-36-labelWidth,logHeight=artSmall.CalcHeight(new GUIContent(status),width-36)+8;
            for(int i=first;i<session.Chat.Count;i++)logHeight+=artSmall.CalcHeight(new GUIContent(ChatText(session.Chat[i])),textWidth)+8;
            // Leave a row for "다시 대화" / "응답 중단" only when one of them is shown.
            float top=(session.ChatClosed||session.ChatBusy?578:600)-logHeight-18;
            // IMGUI is composited after UI Toolkit, so the card stops above the real text field.
            Paper(new Rect(x,top,width,618-top));
            float y=top+16;
            for(int i=first;i<session.Chat.Count;i++)
            {
                string body=ChatText(session.Chat[i]);float h=artSmall.CalcHeight(new GUIContent(body),textWidth)+8;
                GUI.Label(new Rect(x+18,y,labelWidth,22),ChatLabel(session.Chat[i]),artLabel);
                GUI.Label(new Rect(x+18+labelWidth,y,textWidth,h),body,artSmall);y+=h;
            }
            var old=artSmall.normal.textColor;artSmall.normal.textColor=accent;
            GUI.Label(new Rect(x+18,y,width-36,40),status,artSmall);artSmall.normal.textColor=old;
            Rect action=new Rect(x+width-136,578,118,30);
            if(session.ChatClosed&&GUI.Button(action,"다시 대화",artButton))session.ReopenChat();
            else if(session.ChatBusy&&GUI.Button(action,"응답 중단",artButton))session.CancelChat();
            if(Event.current.type==EventType.Repaint)PositionChatInput(new Rect(x,624,width-128,42),scale);
            GUI.enabled=inputCanEdit&&string.IsNullOrEmpty(composition);
            if(GUI.Button(new Rect(x+width-118,624,118,42),"전송",artButton)&&session.SendChat(draft)){draft="";chatInput.SetValueWithoutNotify("");}
            GUI.enabled=true;
        }
        static string ChatLabel(string entry){int split=entry.IndexOf('\n');return split<0?"":entry.Substring(0,split)=="손님 답변"?"손님":entry.Substring(0,split);}
        static string ChatText(string entry){int split=entry.IndexOf('\n');return split<0?entry:entry.Substring(split+1);}
    }
}
