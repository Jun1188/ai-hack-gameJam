using System.Collections.Generic;
using GrandpaRobot.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace GrandpaRobot.Presentation
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class WellHud : MonoBehaviour
    {
        public WellGame game;
        UIDocument document;
        VisualElement root,world,chat;
        Label title,phase,status,message,recall,success;
        TextField input;
        Button request,stop;
        bool previousChat;
        string composition="";
        readonly Dictionary<string,Label> labels=new Dictionary<string,Label>();
        void OnEnable()
        {
            document=GetComponent<UIDocument>();
            if(!Application.isPlaying)return;
            root=document.rootVisualElement;
            var font=game&&game.catalog.uiFont?game.catalog.uiFont:Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},18);
            root.style.unityFontDefinition=FontDefinition.FromFont(font);
            title=root.Q<Label>("title");phase=root.Q<Label>("phase");status=root.Q<Label>("status");message=root.Q<Label>("message");
            recall=root.Q<Label>("recall");success=root.Q<Label>("success");world=root.Q("world");chat=root.Q("chat");
            input=root.Q<TextField>("prompt");request=root.Q<Button>("request");stop=root.Q<Button>("stop");
            root.Q<Button>("house").clicked+=()=>game.LoadStage(0);
            root.Q<Button>("well").clicked+=()=>game.LoadStage(1);
            root.Q<Button>("recall-button").clicked+=()=>game.Recall();
            request.clicked+=()=>game.ToggleChat();
            stop.clicked+=()=>{if(game.Busy)game.Cancel();else game.Restart();};
            root.Q<Button>("send").clicked+=Send;
            root.Q<Button>("close").clicked+=()=>game.CloseChat();
            input.RegisterCallback<FocusInEvent>(_=>SetIme(true));
            input.RegisterCallback<FocusOutEvent>(_=>SetIme(false));
            input.RegisterCallback<KeyDownEvent>(evt=>{
                if(evt.keyCode==KeyCode.Escape){game.CloseChat();evt.StopPropagation();}
                else if((evt.keyCode==KeyCode.Return||evt.keyCode==KeyCode.KeypadEnter)&&(evt.ctrlKey||evt.commandKey)&&composition.Length==0)
                {Send();evt.StopPropagation();}
            });
            if(Keyboard.current!=null)Keyboard.current.onIMECompositionChange+=Composition;
            world.pickingMode=PickingMode.Ignore;
            root.RegisterCallback<GeometryChangedEvent>(_=>root.EnableInClassList("compact",root.resolvedStyle.width<760));
        }
        void Composition(IMECompositionString value){composition=value.ToString();}
        static void SetIme(bool enabled){if(Keyboard.current!=null)Keyboard.current.SetIMEEnabled(enabled);}
        void OnDisable()
        {
            if(Keyboard.current!=null)Keyboard.current.onIMECompositionChange-=Composition;
            if(Application.isPlaying)SetIme(false);
        }
        void Send()
        {
            if(game.Busy||composition.Length>0||string.IsNullOrWhiteSpace(input.value))return;
            game.Submit(input.value);input.SetValueWithoutNotify("");
        }
        void Update()
        {
            if(!Application.isPlaying||root==null||game==null||game.State==null)return;
            title.text=game.IsHouse?"농장 / 무너진 곳간 복구":"할아버지와 로봇 / 우물";
            phase.text=game.PhaseText;
            status.text=game.Provider;
            message.text=game.Message;
            request.SetEnabled(!game.Busy);stop.text=game.Busy?"중단":"초기화";
            root.Q<Button>("house").SetEnabled(!game.Busy);root.Q<Button>("well").SetEnabled(!game.Busy);
            recall.style.display=game.Recalling?DisplayStyle.Flex:DisplayStyle.None;
            success.style.display=game.State.Solved?DisplayStyle.Flex:DisplayStyle.None;
            chat.style.display=game.ChatOpen?DisplayStyle.Flex:DisplayStyle.None;
            if(game.ChatOpen&&!previousChat)
            {
                SetIme(true);input.schedule.Execute(()=>{input.Focus();SetIme(true);});
            }
            if(!game.ChatOpen&&previousChat){input.Blur();SetIme(false);composition="";}
            previousChat=game.ChatOpen;
            if(world.panel==null)return;
            if(game.ChatOpen&&Keyboard.current!=null)
            {
                Keyboard.current.SetIMECursorPosition(new Vector2(input.worldBound.xMin,input.worldBound.yMax));
            }
            foreach(var entry in labels)entry.Value.style.display=DisplayStyle.None;
            foreach(var entry in game.Labels)
            {
                if(game.State.Nodes.ContainsKey(entry.Key)&&!game.NodeVisible(entry.Key))continue;
                var screen=Camera.main.WorldToViewportPoint(entry.Value);
                if(screen.z<=0||screen.x<=0||screen.x>=1||screen.y<=0||screen.y>=1)continue;
                var point=RuntimePanelUtils.CameraTransformWorldToPanel(world.panel,entry.Value,Camera.main);
                point=world.WorldToLocal(point);
                if(point.x<28||point.x>world.resolvedStyle.width-28||point.y<14||point.y>world.resolvedStyle.height-14)continue;
                if(!labels.TryGetValue(entry.Key,out var label))
                {
                    label=new Label(entry.Key);label.AddToClassList("world-label");label.pickingMode=PickingMode.Ignore;world.Add(label);labels.Add(entry.Key,label);
                }
                label.style.display=DisplayStyle.Flex;label.style.left=point.x-25;label.style.top=point.y-12;
            }
        }
    }
}
