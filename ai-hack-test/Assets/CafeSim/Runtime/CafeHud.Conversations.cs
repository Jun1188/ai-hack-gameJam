using System.Collections.Generic;
using UnityEngine;

namespace CafeSim
{
    public sealed partial class CafeHud
    {
        bool conversationListOpen, conversationFromList;
        bool currentTaskVisible;
        string conversationPersona;
        Vector2 conversationListScroll, conversationScroll;
        int threadEntryCount = -1;
        float threadHeight;
        bool RecipeCardsOpen => barista != null && barista.ChoosingMenuCard;
        bool ViewingLiveConversation => !conversationFromList && session != null && session.CanChat &&
            session.Selected.Person.id == conversationPersona;
        bool CanSubmitConversation => promptOpen && !conversationListOpen && !RecipeCardsOpen &&
            ViewingLiveConversation && !session.ChatBusy && !session.ChatClosed && !Halted && !CafeStartIris.BlocksInput;

        void OpenLiveConversation()
        {
            if (!session.CanChat) return;
            ViewConversation(session.Selected.Person.id, false);
        }

        void ViewConversation(string persona, bool fromList)
        {
            if (conversationPersona != persona)
            {
                draft = "";
                chatInput?.SetValueWithoutNotify("");
            }
            conversationPersona = persona;
            if(fromList)historyReturnPage=statusPage;
            conversationFromList = fromList;
            conversationListOpen = false;
            threadEntryCount = -1;
            conversationScroll = Vector2.zero;
            SetPrompt(true);
        }

        void OpenConversationList()
        {
            OpenStatusPage(StatusPage.Conversations);
        }

        void ToggleConversationList()
        {
            if (promptOpen && conversationListOpen) CloseConversations();
            else OpenStatusPage(StatusPage.Overview);
        }

        void CloseConversations()
        {
            conversationListOpen = conversationFromList = false;
            SetPrompt(false);
            if (!Halted && !RecipeCardsOpen)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
        }

        void DrawCurrentTask()
        {
            const float x = 160, width = 760;
            Paper(new Rect(x, 28, width, 76));
            GUI.Label(new Rect(x + 18, 40, 410, 24), "현재 할 일 · 음료 준비", artLabel);
            string drink = tutorialMenu != null ? tutorialMenu.PinnedDrinkName : null;
            string task = session.Selected?.State == VisitState.Active
                ? string.IsNullOrEmpty(drink) ? "손님과 이야기하고 만들 음료를 골라 주세요." : drink + " · 레시피를 보고 제조한 뒤 손님에게 건네 주세요."
                : "다음 손님의 주문을 기다려 주세요.";
            if (TryRecipeStep(out _, out string step)) task = step + "  ·  F";
            GUI.Label(new Rect(x + 18, 68, width - 36, 26), task, artSmall);
            string recipeKey = tutorialMenu != null ? tutorialMenu.toggleKey.ToString() : "Tab";
            GUI.Label(new Rect(x + width - 220, 40, 202, 24), recipeKey + "  레시피 보기 / 닫기", artLabel);
        }

        void DrawConversationPanel(float scale)
        {
            const float x = 160, y = 140, width = 760;
            // Keep a fixed viewport inside the fitted 1280x720 canvas. Only the log scrolls.
            Paper(new Rect(x, y, width, conversationListOpen || conversationFromList ? 462 : 408));
            if (conversationListOpen)
            {
                DrawStatusPanel();
                return;
            }

            var visits = ConversationThreadVisits();
            string name = visits.Count > 0 ? visits[visits.Count - 1].Person.name : session.Person?.name ?? "손님";
            GUI.Label(new Rect(x + 20, y + 18, width - 220, 28), name + " · 대화", artText);
            if (GUI.Button(new Rect(x + width - 174, y + 14, 154, 32), "현황 · F2", artButton)) OpenStatusPage(StatusPage.Overview);
            DrawConversationThread(visits, new Rect(x + 20, y + 60, width - 40, conversationFromList ? 320 : 278));
            if(conversationFromList)
            {
                GUI.Label(new Rect(x+20,y+406,width-40,30),"대화 기록 · 읽기 전용    /    Esc 이전 목록    /    E 현재 손님과 대화",artSmall);
                return;
            }

            bool live = ViewingLiveConversation;
            string status = !live ? "현재 대화할 수 없는 손님이에요. 기록만 볼 수 있어요."
                : session.ChatBusy ? session.ChatStatus
                : session.ChatClosed ? "대화를 마쳤어요. 다시 대화할 수 있어요."
                : session.ChatError ?? "Enter 전송 · Tab 레시피 · Esc 나가기";
            GUI.Label(new Rect(x + 20, y + 352, width - 202, 44), status, artSmall);
            Rect action = new Rect(x + width - 174, y + 352, 154, 32);
            if (live && session.ChatClosed && GUI.Button(action, "다시 대화", artButton)) session.ReopenChat();
            else if (live && session.ChatBusy && GUI.Button(action, "응답 중단", artButton)) session.CancelChat();

            if (Event.current.type == EventType.Repaint)
                PositionChatInput(new Rect(x + 20, 558, width - 158, 44), scale);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && inputCanEdit && string.IsNullOrEmpty(composition);
            if (GUI.Button(new Rect(x + width - 128, 558, 128, 44), "전송", artButton) && session.SendChat(draft))
            {
                draft = "";
                chatInput.SetValueWithoutNotify("");
            }
            GUI.enabled = enabled;
        }

        void DrawConversationList(Rect viewport)
        {
            var people = new List<Persona>();
            var seen = new HashSet<string>();
            if (session.CanChat) { people.Add(session.Person); seen.Add(session.Person.id); }
            for (int i = session.ConversationVisits.Count - 1; i >= 0; i--)
            {
                var person = session.ConversationVisits[i].Person;
                if (person != null && seen.Add(person.id)) people.Add(person);
            }
            if (people.Count == 0)
            {
                GUI.Label(viewport, "아직 나눈 대화가 없어요. 손님을 만나면 여기에 기록돼요.", artSmall);
                return;
            }
            conversationListScroll = GUI.BeginScrollView(viewport, conversationListScroll,
                new Rect(0, 0, viewport.width - 22, people.Count * 62), false, true);
            string chosen = null;
            for (int i = 0; i < people.Count; i++)
            {
                var person = people[i];
                bool live = session.CanChat && session.Person.id == person.id;
                int visits = 0;
                foreach (var visit in session.ConversationVisits) if (visit.Person?.id == person.id) visits++;
                string state = live ? "현재 손님 · 기록 보기" : "이전 손님 · 기록 보기";
                if (GUI.Button(new Rect(0, i * 62, viewport.width - 24, 52),
                    person.name + "   ·   " + visits + "번의 방문   /   " + state, artButton)) chosen = person.id;
            }
            GUI.EndScrollView();
            if (chosen != null) ViewConversation(chosen, true);
        }

        List<CafeVisit> ConversationThreadVisits()
        {
            var visits=new List<CafeVisit>();
            if(!conversationFromList)
            {
                if(session.Selected?.Person.id==conversationPersona)visits.Add(session.Selected);
                return visits;
            }
            foreach(var visit in session.ConversationVisits)
                if(visit.Person?.id==conversationPersona)visits.Add(visit);
            return visits;
        }

        void DrawConversationThread(List<CafeVisit> visits, Rect viewport)
        {
            const float labelWidth = 76;
            float width = viewport.width - 22, textWidth = width - labelWidth;
            float height = 0;
            int count = 0;
            foreach (var visit in visits)
            {
                height += 32;
                foreach (string entry in visit.History)
                {
                    if (entry.StartsWith("ARRIVING\n")) continue;
                    height += Mathf.Max(26, artSmall.CalcHeight(new GUIContent(ChatText(entry)), textWidth) + 10);
                    count++;
                }
            }
            if (threadEntryCount != count && (threadEntryCount < 0 || conversationScroll.y >= threadHeight - viewport.height - 24))
                conversationScroll.y = Mathf.Max(0, height - viewport.height);
            threadEntryCount = count;
            threadHeight = height;
            conversationScroll = GUI.BeginScrollView(viewport, conversationScroll,
                new Rect(0, 0, width, Mathf.Max(height, viewport.height)), false, true);
            float y = 0;
            foreach (var visit in visits)
            {
                GUI.Label(new Rect(0, y, width, 26), "방문 " + visit.VisitNumber +
                    (session.CanChat && session.Selected == visit ? " · 현재 대화" : " · 지난 기록"), artLabel);
                y += 32;
                foreach (string entry in visit.History)
                {
                    if (entry.StartsWith("ARRIVING\n")) continue;
                    string body = ChatText(entry);
                    float h = Mathf.Max(26, artSmall.CalcHeight(new GUIContent(body), textWidth) + 10);
                    GUI.Label(new Rect(0, y, labelWidth, 24), ChatLabel(entry), artLabel);
                    GUI.Label(new Rect(labelWidth, y, textWidth, h), body, artSmall);
                    y += h;
                }
            }
            GUI.EndScrollView();
        }
    }
}
