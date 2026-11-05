using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
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
        [SerializeField] Font customerSpeechFont;
        public float bubbleSeconds=6f;
        const float ExtraOrderBubbleSeconds = 3f;
        const float BubbleDurationReduction = 2f;
        bool promptOpen, subscribed;
        string latestSpeech="", latestKind, deferredSpeech, deferredKind, hint;
        int latestStars=-1, deferredStars=-1;
        CafeVisit speaker, deferredSpeaker;
        float bubbleStart, bubbleUntil, bubbleDuration, ratingBubbleUntil, hintUntil;
        Camera eyes;
        // Menu-card palette: warm paper, dark-brown ink, roasted-orange accent.
        internal static readonly Color paper=new Color32(253,246,228,255), ink=new Color32(66,42,28,255), accent=new Color32(190,104,46,255);
        Texture2D paperTex, tailTex, capTex, buttonTex, buttonHoverTex;
        GUIStyle artText, artSmall, artLabel, artCap, artButton, paperBox;
        RectTransform speechRoot, speechShadow, speechPaper, speechTail;
        Text speechLabel, speechStars;
        float speechWidth, speechTextHeight, speechTextScale;
        static readonly Regex SentenceBreak = new Regex(
            @"([.!?。！？…]+[""'”’」』）)]*)(?:[ \t]+(?=\S)|(?=[가-힣]))");
        static readonly Regex SpeechLineStart = new Regex(@"^[ \t]*(?=\S)", RegexOptions.Multiline);
        const string SpeechIndent = "\u00A0\u00A0";
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
            ShowSpeech(visit,speech,stars,kind);
            if(kind=="RATING")ratingBubbleUntil=bubbleUntil;
        }
        void ShowSpeech(CafeVisit visit,string speech,int stars,string kind)
        {
            // Long orders stay up long enough to read; the full history is in the E panel.
            string sentences = SentenceBreak.Replace(speech, "$1\n");
            latestSpeech = SpeechLineStart.Replace(sentences, SpeechIndent);
            speaker=visit;latestStars=stars;latestKind=kind;bubbleStart=Time.unscaledTime;
            float readingTime = Mathf.Max(bubbleSeconds, speech.Length * .11f);
            float orderBonus = kind == "ORDER" ? ExtraOrderBubbleSeconds : 0f;
            bubbleDuration = Mathf.Max(.1f, readingTime + orderBonus - BubbleDurationReduction);
            bubbleUntil = bubbleStart + bubbleDuration;
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
            if(deferredSpeech!=null&&Time.unscaledTime>=ratingBubbleUntil){ShowSpeech(deferredSpeaker,deferredSpeech,deferredStars,deferredKind);deferredSpeech=null;deferredKind=null;}
            // Customers order on admission; hold the line until they have walked up to the counter.
            var walker=session.BodyOf(speaker);
            if(speaker!=null&&speaker.State==VisitState.Active&&walker!=null&&Time.unscaledTime<bubbleUntil&&
                (walker.position-session.servicePoint.position).sqrMagnitude>.04f)
            {bubbleStart=Time.unscaledTime;bubbleUntil=bubbleStart+bubbleDuration;}
            if(draftCustomer!=session.CustomerNumber){draft="";draftCustomer=session.CustomerNumber;chatInput?.SetValueWithoutNotify("");if(promptOpen)SetPrompt(false);}
            if(promptOpen&&!session.CanChat)SetPrompt(false);
            var keys=Keyboard.current;
            if(Application.isFocused&&keys!=null&&!CafeStartIris.BlocksInput)
            {
                bool enterPressed = keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame;
                // The tutorial owns Enter/Esc while it is open; otherwise E talks, Q opens the 영업 수첩, Esc pauses.
                if (TutorialTabRequired)
                {
                    if (keys.tabKey.wasPressedThisFrame) AdvanceTutorialTabPrompt();
                }
                else if (TutorialOpen)
                {
                    if (enterPressed || keys.escapeKey.wasPressedThisFrame) CloseTutorial(true);
                }
                else if (enterPressed && TryAdvanceFromReceipt()) return;
                else if(keys.eKey.wasPressedThisFrame)HandleOverlayKey(KeyCode.E);
                else if(keys.qKey.wasPressedThisFrame)HandleOverlayKey(KeyCode.Q);
                else if(keys.escapeKey.wasPressedThisFrame)HandleOverlayKey(KeyCode.Escape);
            }
            inputCanEdit=promptOpen&&session.CanChat&&!session.ChatBusy&&!session.ChatClosed&&!CafeStartIris.BlocksInput;
            if(chatInput!=null)
            {
                StyleChatInput();
                chatInput.style.display=promptOpen?DisplayStyle.Flex:DisplayStyle.None;
                chatInput.SetEnabled(inputCanEdit);
            }
            ApplyOverlayControls();
            UpdateClock();
            CheckNotebookNews();
            cardsWereOpen=barista!=null&&barista.ChoosingMenuCard;
        }
        void LateUpdate()
        {
            if (!mergedCafe || session == null || CafeStartIris.BlocksInput) return;

            var body = session.BodyOf(speaker);
            bool inView = TryGetSpeechAnchor(body, out Vector3 anchor);
            // Looking away pauses reading time while the speaker is still in the cafe.
            if (!Halted && body != null && !inView &&
                Time.unscaledTime - Time.unscaledDeltaTime < bubbleUntil)
                bubbleUntil += Time.unscaledDeltaTime;

            bool visible = inView && Time.unscaledTime < bubbleUntil && !string.IsNullOrEmpty(latestSpeech) &&
                (speaker.State != VisitState.Active || (body.position - session.servicePoint.position).sqrMagnitude <= .04f);
            if (visible) UpdateSpeech(anchor);
            else if (speechRoot != null) speechRoot.gameObject.SetActive(false);
            UpdateTutorial();
            if (tutorialMenu != null)
                tutorialMenu.SetTutorialCardFocus(tutorialStage == TutorialStage.ChooseCard &&
                    !Halted && !promptOpen && !session.ChatBusy);
        }

        bool TryGetSpeechAnchor(Transform body, out Vector3 anchor)
        {
            anchor = default;
            if (eyes == null && barista != null) eyes = barista.GetComponentInChildren<Camera>();
            if (body == null || !body.gameObject.activeInHierarchy || eyes == null || !eyes.isActiveAndEnabled)
                return false;

            Vector3 viewportAnchor = eyes.WorldToViewportPoint(body.position + Vector3.up * 1.9f);
            if (viewportAnchor.z <= eyes.nearClipPlane || viewportAnchor.z >= eyes.farClipPlane ||
                viewportAnchor.x < 0f || viewportAnchor.x > 1f ||
                viewportAnchor.y < 0f || viewportAnchor.y > 1f) return false;

            anchor = eyes.ViewportToScreenPoint(viewportAnchor);
            return true;
        }

        void StyleChatInput()
        {
            if(inputStyled)return;inputStyled=true;
            var inner=chatInput.Q(className:"unity-base-text-field__input");
            inner.style.borderTopWidth=inner.style.borderBottomWidth=inner.style.borderLeftWidth=inner.style.borderRightWidth=3;
            inner.style.borderTopColor=inner.style.borderBottomColor=inner.style.borderLeftColor=inner.style.borderRightColor=ink;
            inner.style.borderTopLeftRadius=inner.style.borderTopRightRadius=inner.style.borderBottomLeftRadius=inner.style.borderBottomRightRadius=12;
            // Only the rounded inner box is painted; the outer element would show square corners.
            chatInput.style.backgroundColor=Color.clear;
        }
        void ApplyOverlayControls()
        {
            bool overlay=promptOpen||Halted;
            if(barista!=null)barista.ExternalUIActive=overlay;
            if (TutorialTabRequired) UnityEngine.Cursor.visible = false;
            else if(overlay){UnityEngine.Cursor.lockState=CursorLockMode.None;UnityEngine.Cursor.visible=true;}
        }
        int shortcutFrame=-1;
        void HandleOverlayKey(KeyCode key)
        {
            if(shortcutFrame==Time.frameCount||CafeEnding.Active)return;
            if(!string.IsNullOrEmpty(composition)||Time.frameCount-compositionFrame<=1)return;
            // While typing, E is a letter (and the first jamo of ㄷ); only Esc closes the field.
            if((key==KeyCode.E||key==KeyCode.Q)&&promptOpen&&(inputFocused||GUIUtility.keyboardControl!=0))return;
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
            if(promptOpen)DrawChat(scale);
            if(!Halted)DrawControls();
            if (TutorialTabRequired) DrawTutorialTabPrompt(scale, offset);
            else if (TutorialOpen) DrawTutorial(scale, offset);
            else
            {
                DrawTutorialHint();
                DrawDayOverlay();
            }
            GUI.matrix=previous;
        }
        void BuildSpeech()
        {
            var canvas = EnsureHudCanvas();
            speechRoot = (RectTransform)new GameObject("Customer speech", typeof(RectTransform)).transform;
            speechRoot.SetParent(canvas, false);
            speechRoot.anchorMin = speechRoot.anchorMax = new Vector2(.5f, .5f);
            var topLeft = new Vector2(0f, 1f);
            speechShadow = Box("Shadow", speechRoot, paperSprite, new Color(.16f,.09f,.05f,.28f),
                topLeft, new Vector2(4f, -5f), Vector2.zero);
            speechPaper = Box("Paper", speechRoot, paperSprite, Color.white, topLeft, Vector2.zero, Vector2.zero);
            var tail = AddSpeechGraphic<RawImage>("Tail");
            tail.texture = tailTex;
            tail.rectTransform.sizeDelta = new Vector2(40f, 30f);
            speechTail = tail.rectTransform;
            speechStars = AddSpeechGraphic<Text>("Rating");
            speechStars.fontSize = 22;
            speechStars.color = accent;
            speechStars.horizontalOverflow = HorizontalWrapMode.Overflow;
            speechLabel = AddSpeechGraphic<Text>("Dialogue");
            speechLabel.fontSize = 20;
            speechLabel.color = ink;
            foreach (var label in new[] { speechStars, speechLabel })
            {
                label.font = customerSpeechFont != null ? customerSpeechFont : artText.font;
                label.alignment = TextAnchor.UpperLeft;
                label.supportRichText = false;
                label.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        T AddSpeechGraphic<T>(string name) where T : Graphic
        {
            var graphic = new GameObject(name, typeof(RectTransform), typeof(T)).GetComponent<T>();
            var rect = graphic.rectTransform;
            rect.SetParent(speechRoot, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            graphic.raycastTarget = false;
            return graphic;
        }

        float MeasureSpeechHeight(string speech, float width)
        {
            var settings = speechLabel.GetGenerationSettings(new Vector2(width, 0f));
            return speechLabel.cachedTextGeneratorForLayout.GetPreferredHeight(speech, settings) / settings.scaleFactor;
        }

        float MeasureSpeechWidth(string speech, float padding)
        {
            const float baseWidth = 330f, maxWidth = 390f, widthStep = 4f;
            float width = baseWidth;
            foreach (string line in speech.Split('\n'))
            {
                string sentence = line.TrimEnd();
                int[] glyphStarts = StringInfo.ParseCombiningCharacters(sentence);
                int last = glyphStarts.Length - 1;
                while (last > 0 && char.IsPunctuation(sentence, glyphStarts[last]))
                    last--;
                if (last <= 0) continue;

                string prefix = sentence.Substring(0, glyphStarts[last]).TrimEnd();
                if (prefix.Length == 0) continue;

                // If dropping the final glyph and its punctuation removes a line, try to fit them back in.
                float targetHeight = MeasureSpeechHeight(prefix, width - padding);
                for (float candidate = width; candidate <= maxWidth; candidate += widthStep)
                {
                    if (MeasureSpeechHeight(sentence, candidate - padding) > targetHeight) continue;
                    width = candidate;
                    break;
                }
            }
            return width;
        }

        void UpdateSpeech(Vector3 anchor)
        {
            if (speechRoot == null) BuildSpeech();
            speechRoot.gameObject.SetActive(true);
            const float padX = 22f, padY = 16f;
            float starsHeight = latestStars >= 0 ? 28f : 0f;
            float textScale = speechLabel.pixelsPerUnit;
            if (speechLabel.text != latestSpeech || speechTextScale != textScale)
            {
                speechLabel.text = latestSpeech;
                speechTextScale = textScale;
                speechWidth = MeasureSpeechWidth(latestSpeech, padX * 2);
                speechTextHeight = MeasureSpeechHeight(latestSpeech, speechWidth - padX * 2);
            }
            float width = speechWidth;
            float height = speechTextHeight + starsHeight + padY * 2;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Vector2 offset = (new Vector2(Screen.width, Screen.height) - new Vector2(1280f, 720f) * scale) * .5f;
            float x = (anchor.x - offset.x) / scale;
            float y = (Screen.height - anchor.y - offset.y) / scale;
            var rect = new Rect(Mathf.Clamp(x - width * .3f, 16, 1264 - width),
                Mathf.Clamp(y - height - 26, 16, 600 - height), width, height);
            if (promptOpen) rect.y = Mathf.Clamp(330 - height, 16, rect.y);
            float tipX = Mathf.Clamp(x, rect.x + 24, rect.xMax - 48);
            speechRoot.pivot = new Vector2((tipX - rect.x) / width, 0f);
            speechRoot.anchoredPosition = new Vector2(tipX - 640f, 360f - rect.yMax);
            speechRoot.sizeDelta = speechShadow.sizeDelta = speechPaper.sizeDelta = rect.size;
            speechRoot.localScale = Vector3.one * Mathf.SmoothStep(.82f, 1f, (Time.unscaledTime - bubbleStart) / .14f);
            speechTail.anchoredPosition = new Vector2(tipX - rect.x - 6f, 3f - height);
            speechLabel.rectTransform.anchoredPosition = new Vector2(padX, -padY - starsHeight);
            speechLabel.rectTransform.sizeDelta = new Vector2(width - padX * 2, speechTextHeight);
            speechStars.enabled = latestStars >= 0;
            if (speechStars.enabled)
            {
                speechStars.text = new string('★', latestStars) + new string('☆', 5 - latestStars);
                speechStars.rectTransform.anchoredPosition = new Vector2(padX, 2f - padY);
                speechStars.rectTransform.sizeDelta = new Vector2(width - padX * 2, starsHeight);
            }
        }
        void DrawControls()
        {
            if(Time.unscaledTime<hintUntil)
            {
                float w=artSmall.CalcSize(new GUIContent(hint)).x+36;var r=new Rect(1256-w,668,w,38);
                Paper(r);GUI.Label(new Rect(r.x+18,r.y+10,w,24),hint,artSmall);return;
            }
            var keys=promptOpen?new[]{"Enter","전송","Esc","닫기"}:new[]{"E","손님과 대화","Tab","메뉴 카드","F","제조 · 서빙","Q","영업 수첩","Esc","일시정지"};
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
