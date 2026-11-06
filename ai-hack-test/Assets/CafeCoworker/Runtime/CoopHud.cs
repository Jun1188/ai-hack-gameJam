using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace CafeCoworker
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class CoopHud : MonoBehaviour
    {
        public CoopSession session;
        public Font font;
        VisualElement root, modal, threadPanel, historyPanel, pausePanel;
        VisualElement workCard;
        bool workCardVisible;
        Label work, notice, hint, threadTitle, provider;
        Button talk, send;
        ScrollView thread, history;
        TextField input;
        enum View { None, StaffLive, StaffHistory, CustomerLive, CustomerHistory, History, Pause }
        View view;
        int lastRevision = -1, lastLines = -1;
        string lastWork;
        bool uiBound;
        float refreshAt;
        bool imeActive;
        int imeFrame = -10;
        GuestRecord historyGuest;
        Label speechBubble;
        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            workCard=root.Q("taskCard");workCardVisible=false;workCard.style.display=DisplayStyle.None;
            speechBubble=root.Q<Label>("speechBubble");
            if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);
            work = root.Q<Label>("work"); notice = root.Q<Label>("notice"); hint = root.Q<Label>("hint");
            modal = root.Q("modal"); threadPanel = root.Q("threadPanel"); historyPanel = root.Q("historyPanel"); pausePanel = root.Q("pausePanel");
            thread = root.Q<ScrollView>("thread"); history = root.Q<ScrollView>("history");
            threadTitle = root.Q<Label>("threadTitle"); provider = root.Q<Label>("provider");
            input = root.Q<TextField>("input"); talk = root.Q<Button>("talk"); send = root.Q<Button>("send");
            talk.clicked += OpenStaff;
            root.Q<Button>("currentCustomer").clicked += () => Open(View.CustomerLive);
            root.Q<Button>("records").clicked += () => Open(View.History);
            root.Q<Button>("pause").clicked += () => Open(View.Pause);
            root.Q<Button>("close").clicked += Close;
            root.Q<Button>("back").clicked += () => Open(View.History);
            root.Q<Button>("newOrder").clicked += () => session.NewOrder();
            root.Q<Button>("cancelOrder").clicked += () => session.CancelOrder();
            root.Q<Button>("resume").clicked += Close;
            root.Q<Button>("restart").clicked += () => { session.ResetDay(); Close(); };
            root.Q<Button>("cancelReply").clicked += () => session.dialogue.Cancel();
            send.clicked += Submit;
            input.RegisterCallback<KeyDownEvent>(evt =>
            {
                if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && !Composing())
                { Submit(); evt.StopPropagation(); }
                // E/F remain text while focused; Escape is handled once by this UI.
                if (evt.keyCode == KeyCode.Escape) { Close(); evt.StopPropagation(); }
            });
            root.RegisterCallback<GeometryChangedEvent>(evt => root.EnableInClassList("compact", evt.newRect.width < 1050));
            session.dialogue.Changed += OnDialogue;
            session.guests.Changed += OnGuestDialogue;
            uiBound = true; Open(View.None);
            if (Keyboard.current != null) Keyboard.current.onIMECompositionChange += CompositionChanged;
        }
        void OnDisable()
        {
            if (uiBound && session != null && session.dialogue != null) session.dialogue.Changed -= OnDialogue;
            if (uiBound && session != null && session.guests != null) session.guests.Changed -= OnGuestDialogue;
            uiBound = false;
            if (Keyboard.current != null) Keyboard.current.onIMECompositionChange -= CompositionChanged;
            if (session != null) { session.Paused = false; session.player.MenuOpen = false; }
        }
        void CompositionChanged(UnityEngine.InputSystem.LowLevel.IMECompositionString value)
        { imeActive = value.Count > 0; imeFrame = Time.frameCount; }
        bool Composing() => imeActive || Time.frameCount <= imeFrame + 1;
        void Update()
        {
            if (!uiBound || session.Scheduler == null) return;
            var key = Keyboard.current;
            if (key != null&&Application.isFocused)
            {
                if (view == View.None)
                {
                    if (key.eKey.wasPressedThisFrame) { if (session.CanTalk) OpenStaff(); else Open(View.CustomerLive); }
                    else if (key.f2Key.wasPressedThisFrame) Open(View.History);
                    else if (key.escapeKey.wasPressedThisFrame) Open(View.Pause);
                    else if (key.bKey.wasPressedThisFrame) session.NewOrder();
                    else if (key.hKey.wasPressedThisFrame) {workCardVisible=!workCardVisible;workCard.style.display=workCardVisible?DisplayStyle.Flex:DisplayStyle.None;}
                }
                else if (key.escapeKey.wasPressedThisFrame) Close();
            }
            talk.SetEnabled(session.CanTalk && view == View.None);
            UpdateBubble();
            talk.style.display = session.CanTalk && view == View.None ? DisplayStyle.Flex : DisplayStyle.None;
            hint.text = view != View.None ? "" : session.TalkDiscovery ? "E · 가까운 동료와 이야기할 수 있어요" :
                session.CanWork ? session.WorkHint : "WASD 이동 · 마우스 시점 · H 할 일 · B 주문 벨 · F 작업 유지 · F2 기록 · Esc 메뉴";
            if (Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + .12f;
            string summary = session.DescribeWork();
            var current = session.Scheduler.Orders.LastOrDefault(o => !o.Cancelled);
            work.text = (current == null ? "다음 주문 대기" : "주문 " + current.Id + " · " + current.Recipe.name) + "\n\n" + summary;
            var p = session.Scheduler.Next(Worker.Player);
            if (p != null && p.State == WorkState.Working) work.text += "\n진행 " + Mathf.RoundToInt(p.Progress / p.Spec.seconds * 100) + "%";
            notice.text = session.Notice;
            if (lastRevision != session.Revision || lastWork != summary)
            { lastRevision = session.Revision; lastWork = summary; if (view == View.History) DrawHistory(); }
            bool staffView=view==View.StaffLive;
            provider.text = (staffView?session.dialogue.Busy:session.guests.Busy) ? "답변을 생각 중…" :
                (view==View.CustomerLive&&!session.guests.Active?"현재 대화할 손님이 없어요. 기록만 볼 수 있어요.":"Enter 전송 · Esc 닫기");
            bool editable = staffView ? !session.dialogue.Busy : view==View.CustomerLive&&session.guests.Active&&!session.guests.Busy;
            input.SetEnabled(editable); send.SetEnabled(editable);
            if (view == View.CustomerLive || view == View.CustomerHistory)
                if (lastLines != (view==View.CustomerHistory?historyGuest:session.guests.Current)?.Lines.Count) DrawThread();
        }
        public void OpenStaff()
        { if (session.CanTalk && view == View.None) Open(View.StaffLive); }
        void Open(View next)
        {
            view = next; lastLines = -1;
            session.player.MenuOpen = view != View.None;
            session.Paused = view == View.Pause;
            session.barista.enabled = !session.Paused;
            workCard.style.display=view==View.None&&workCardVisible?DisplayStyle.Flex:DisplayStyle.None;
            modal.style.display = view == View.None ? DisplayStyle.None : DisplayStyle.Flex;
            bool isThread = view == View.StaffLive || view == View.StaffHistory || view == View.CustomerLive || view == View.CustomerHistory;
            threadPanel.style.display = isThread ? DisplayStyle.Flex : DisplayStyle.None;
            historyPanel.style.display = view == View.History ? DisplayStyle.Flex : DisplayStyle.None;
            pausePanel.style.display = view == View.Pause ? DisplayStyle.Flex : DisplayStyle.None;
            input.style.display = send.style.display = view == View.StaffLive||view==View.CustomerLive ? DisplayStyle.Flex : DisplayStyle.None;
            root.Q<Button>("cancelReply").style.display = view == View.StaffLive ? DisplayStyle.Flex : DisplayStyle.None;
            root.Q<Button>("back").style.display = view == View.StaffHistory || view == View.CustomerHistory ? DisplayStyle.Flex : DisplayStyle.None;
            if (isThread) DrawThread();
            if (view == View.History) DrawHistory();
            if (view == View.StaffLive||view==View.CustomerLive) input.schedule.Execute(() => input.Focus());
        }
        void Close()
        {
            if (view == View.StaffLive) session.dialogue.Cancel();
            if (view == View.CustomerLive) session.guests.Cancel();
            input.Blur(); Open(View.None);
            session.barista.enabled = true;
        }
        void Submit()
        {
            if (Composing()) return;
            bool sent=view==View.StaffLive?session.dialogue.Send(input.value):view==View.CustomerLive&&session.guests.Send(input.value);
            if (sent) { input.SetValueWithoutNotify(""); input.Focus(); }
        }
        void OnDialogue()
        {
            if (view == View.StaffLive || view == View.StaffHistory) DrawThread();
            if (view == View.History) DrawHistory();
        }
        void DrawThread()
        {
            bool staff = view == View.StaffLive || view == View.StaffHistory;
            var guest=view==View.CustomerHistory?historyGuest:session.guests.Current;
            var lines = staff ? session.dialogue.History : guest?.Lines ?? new List<ConversationLine>();
            threadTitle.text = staff ? session.Tables.profile.displayName + (view == View.StaffLive ? " · 동료와 대화" : " · 대화 기록") :
                view == View.CustomerLive ? "진행 중 대화 · "+(guest?.Profile.name??"손님") : (guest?.Profile.name??"손님")+" · 이전 대화 기록";
            thread.Clear();
            foreach (var line in lines)
            {
                var item = new VisualElement(); item.AddToClassList("message");
                var who = new Label(line.Speaker); who.AddToClassList("speaker");
                var text = new Label(line.Text); text.AddToClassList("speech");
                item.Add(who); item.Add(text); thread.Add(item);
            }
            if (lines.Count == 0) thread.Add(new Label(staff ? "아직 나눈 대화가 없어요." : "아직 손님 주문이 없어요."));
            lastLines = lines.Count;
            if (view == View.StaffLive || view == View.CustomerLive)
                thread.schedule.Execute(() => thread.scrollOffset = new Vector2(0, float.MaxValue));
        }
        void DrawHistory()
        {
            history.Clear();
            var staff = new Button(() => Open(View.StaffHistory)) { text = session.Tables.profile.displayName + " · 동료 대화 기록" };
            staff.AddToClassList("record"); history.Add(staff);
            if (session.guests.Records.Count > 0)
            {
                foreach(var record in session.guests.Records.AsEnumerable().Reverse())
                {
                    var selected=record;
                    var customer=new Button(()=>{historyGuest=selected;Open(View.CustomerHistory);}){text=record.Profile.name+" · 손님 대화 기록"};
                    customer.AddToClassList("record");history.Add(customer);
                }
            }
            else history.Add(new Label("손님을 만나면 아래에 기록이 쌓여요."));
        }
        void OnGuestDialogue()
        {
            if(view==View.CustomerLive||view==View.CustomerHistory)DrawThread();
            if(view==View.History)DrawHistory();
        }
        void UpdateBubble()
        {
            bool visible=view==View.None&&session.CanSeeStaff&&!string.IsNullOrEmpty(session.BubbleText);
            speechBubble.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
            if(!visible)return;
            speechBubble.text=session.Tables.profile.displayName+"\n"+session.BubbleText;
            Vector3 screen=session.player.eyes.WorldToScreenPoint(session.barista.transform.position+Vector3.up*1.93f);
            Vector2 panel=RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(screen.x,Screen.height-screen.y));
            float height=Mathf.Max(80,speechBubble.resolvedStyle.height);
            speechBubble.style.left=Mathf.Clamp(panel.x-160,12,Mathf.Max(12,root.resolvedStyle.width-332));
            speechBubble.style.top=Mathf.Max(12,panel.y-height-12);
        }
    }
}
