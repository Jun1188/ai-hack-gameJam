using System;
using UnityEngine;

namespace CafeSim
{
    // Only attached to CafeCustomerMvp.unity. No business-day ledger or collection writes.
    [DefaultExecutionOrder(-120)]
    public sealed class CafeCustomerMvp : MonoBehaviour
    {
        public CafeSession session;
        public CafeServeCounter counter;
        public TextAsset allPersonas;
        public Font font;
        public int personaIndex;
        Persona[] people;
        TextAsset selectedData;
        string draft = "안녕하세요", note = "";
        Vector2 scroll, profileScroll;
        GUIStyle label, title, button, field;

        void Awake()
        {
            people = JsonUtility.FromJson<PersonaTable>(allPersonas.text).items;
            SetPersonaData();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        void SetPersonaData()
        {
            if (selectedData != null) Destroy(selectedData);
            personaIndex = (personaIndex % people.Length + people.Length) % people.Length;
            selectedData = new TextAsset(JsonUtility.ToJson(new PersonaTable { items = new[] { people[personaIndex] } }));
            session.personaData = selectedData;
        }
        public void Choose(int index)
        {
            personaIndex = index;
            SetPersonaData();
            session.RestartModel();
            draft = "안녕하세요"; note = ""; scroll = profileScroll = Vector2.zero;
        }
        public bool Serve(bool failed)
        {
            var visit = session.Selected;
            if (visit?.State != VisitState.Active) return false;
            var item = Array.Find(session.Menu.items, m => m.id == visit.Order.menuId);
            string name = failed ? CafeQueueSim.FailedDrinkName : item?.name;
            if (!counter.TryReceive(name, visit.Order.temperature == "ICE", out var grip))
            { note = "손님이 카운터에 도착한 뒤 제공할 수 있어요."; return false; }
            // A small test mug also verifies that the runtime hand grip exists and follows the customer.
            var mug = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mug.name = "MVP served mug"; mug.transform.SetParent(grip, false);
            mug.transform.localPosition = Vector3.zero; mug.transform.localScale = new Vector3(.08f, .09f, .08f);
            Destroy(mug.GetComponent<Collider>());
            note = failed ? "신비한 음료 전달 · 별점 1점" : "정상 음료 전달";
            return true;
        }
        void OnDestroy() { if (selectedData != null) Destroy(selectedData); }
        void OnGUI()
        {
            if (people == null) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 17, wordWrap = true };
                title = new GUIStyle(label) { fontSize = 22 };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 16 };
                field = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 17 };
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            GUI.Box(new Rect(18, 18, 590, 684), "");
            var p = people[personaIndex];
            GUI.Label(new Rect(34, 28, 550, 32), $"손님 에셋 · 페르소나 MVP  {personaIndex + 1}/{people.Length}", title);
            if (GUI.Button(new Rect(34, 72, 170, 34), "이전 손님", button)) Choose(personaIndex - 1);
            if (GUI.Button(new Rect(214, 72, 170, 34), "같은 손님 다시", button)) Choose(personaIndex);
            if (GUI.Button(new Rect(394, 72, 192, 34), "다음 손님", button)) Choose(personaIndex + 1);
            var asset = CafeCustomerCatalog.Choose(p.id, new System.Random(1));
            GUI.Label(new Rect(34, 114, 550, 72), p.name + " · " + p.id + "\n" + (asset?.resource ?? "기존 외형 사용"), label);
            string profile = p.setting + "\n말투 · " + p.voice;
            float profileHeight = Mathf.Max(70, label.CalcHeight(new GUIContent(profile), 522));
            profileScroll = GUI.BeginScrollView(new Rect(34, 188, 552, 76), profileScroll, new Rect(0, 0, 528, profileHeight));
            GUI.Label(new Rect(0, 0, 522, profileHeight), profile, label);
            GUI.EndScrollView();
            GUI.Label(new Rect(34, 269, 550, 28), session.model?.Status ?? "준비 중", label);
            draft = GUI.TextField(new Rect(34, 304, 440, 36), draft, 300, field);
            GUI.enabled = session.CanChat && !session.ChatBusy;
            if (GUI.Button(new Rect(484, 304, 102, 36), "대화", button) && session.SendChat(draft)) draft = "";
            GUI.enabled = session.Selected?.State == VisitState.Active;
            if (GUI.Button(new Rect(34, 352, 265, 36), "정상 음료 제공", button)) Serve(false);
            if (GUI.Button(new Rect(309, 352, 277, 36), "실패 음료 제공 · 1점", button)) Serve(true);
            GUI.enabled = true;
            GUI.Label(new Rect(34, 394, 550, 32), note + "  " + (session.LastReplySource ?? ""), label);
            string dialogue = string.Join("\n\n", session.Chat);
            float height = Mathf.Max(240, label.CalcHeight(new GUIContent(dialogue), 522));
            scroll = GUI.BeginScrollView(new Rect(34, 432, 552, 248), scroll, new Rect(0, 0, 528, height));
            GUI.Label(new Rect(0, 0, 522, height), dialogue, label);
            GUI.EndScrollView();
            GUI.matrix = old;
        }
    }
}
