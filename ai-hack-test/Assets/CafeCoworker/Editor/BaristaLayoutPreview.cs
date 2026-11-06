using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CafeCoworker.Editor
{
    // Layout-only preview. It never starts gameplay, a model request, or a saved progression.
    public sealed class BaristaLayoutPreview : EditorWindow
    {
        Texture2D background;
        [MenuItem("Tools/Cafe Coworker/Preview conversation layout")]
        public static void ShowPreview()
        {
            var window=GetWindow<BaristaLayoutPreview>();
            window.titleContent=new GUIContent("Coworker Layout");
            window.position=new Rect(40,40,1280,720);
            window.Show();
        }
        public void CreateGUI()
        {
            var root=rootVisualElement;
            var picture=Path.GetFullPath("../docs/images/cafe-barista-scene-preview.png");
            if(File.Exists(picture))
            {
                background=new Texture2D(2,2);background.LoadImage(File.ReadAllBytes(picture));
                root.style.backgroundImage=new StyleBackground(background);
            }
            root.style.backgroundColor=new Color(.25f,.23f,.19f);
            var tree=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/CafeCoworker/UI/CoopHud.uxml");
            tree.CloneTree(root);
            root.style.unityFontDefinition=FontDefinition.FromFont(AssetDatabase.LoadAssetAtPath<Font>("Assets/Cafe/Menu/NotoSansKR.ttf"));
            root.Q<Label>("work").text="주문 1 · 카페라테\n\n나: 시럽 옆 준비대에서 우유 계량하기\n서온: 컵 선반에서 컵 준비하기";
            root.Q<Label>("notice").text="새 주문을 나눠 맡았어요. F를 누르고 있으면 내 작업을 진행합니다.";
            root.Q<Label>("hint").text="E 동료 대화 · F 작업 유지 · F2 기록 · Esc 메뉴";
            root.Q("modal").style.display=DisplayStyle.Flex;
            root.Q("historyPanel").style.display=DisplayStyle.None;
            root.Q("pausePanel").style.display=DisplayStyle.None;
            root.Q<Label>("threadTitle").text="서온 · 동료와 대화";
            root.Q<Label>("provider").text="Enter 전송 · Esc 닫기";
            var thread=root.Q<ScrollView>("thread");
            Add(thread,"서온","새 주문이에요. 재료 준비를 부탁할게요. 저는 컵부터 챙기고 추출하러 갈게요.");
            Add(thread,"나","함께 일할 때 무엇이 제일 중요하다고 생각해요?");
            Add(thread,"서온","서로 어디서 막혔는지 말해 주는 거요. 혼자 다 해결하려고 하면 더 급해지더라고요. 당신은 어떤 쪽이에요?");
            root.RegisterCallback<GeometryChangedEvent>(e=>root.EnableInClassList("compact",e.newRect.width<1050));
        }
        static void Add(ScrollView scroll,string speaker,string text)
        {
            var row=new VisualElement();row.AddToClassList("message");
            var who=new Label(speaker);who.AddToClassList("speaker");
            var speech=new Label(text);speech.AddToClassList("speech");row.Add(who);row.Add(speech);scroll.Add(row);
        }
        void OnDisable(){if(background!=null)DestroyImmediate(background);}
    }
}
