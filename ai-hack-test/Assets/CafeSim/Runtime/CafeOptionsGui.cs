using UnityEngine;

namespace CafeSim
{
    // Volume and mouse-sensitivity card in the cafe's paper style, drawn with IMGUI.
    // The title screen and the in-game pause menu share it, so both edit the same CafeSettings.
    public sealed class CafeOptionsGui
    {
        public const float Width=640, Height=650;
        readonly Font font;
        readonly bool applyOnClose;
        Texture2D paperTex, buttonTex, buttonHoverTex, trackTex;
        GUIStyle box, title, text, value, button, track, thumb, entry, help;
        CafeModelPreferenceData modelDraft;
        string apiKeyDraft="", modelNotice="";
        public void ReloadModelDraft()
        {modelDraft=CafeModelPreferences.Load();apiKeyDraft=CafeModelPreferences.SessionKey(modelDraft);modelNotice="";}

        public CafeOptionsGui(Font font,bool applyOnClose=false){this.font=font;this.applyOnClose=applyOnClose;}

        // Returns true when the player presses "돌아가기".
        public bool Draw(Rect card)
        {
            Styles();
            if(modelDraft==null)ReloadModelDraft();
            if(Event.current.type==EventType.Repaint)
            {
                var old=GUI.color;GUI.color=new Color(.16f,.09f,.05f,.28f);
                box.Draw(new Rect(card.x+4,card.y+5,card.width,card.height),false,false,false,false);GUI.color=old;
                box.Draw(card,false,false,false,false);
            }
            GUI.Label(new Rect(card.x,card.y+28,card.width,40),"옵션",title);
            float volume=CafeSettings.Volume;
            float newVolume=Slider(new Rect(card.x+44,card.y+76,card.width-88,50),"전체 음량",volume,0,1,Mathf.RoundToInt(volume*100)+"%");
            if(!Mathf.Approximately(newVolume,volume))CafeSettings.Volume=newVolume;
            float sensitivity=CafeSettings.MouseSensitivity;
            float newSensitivity=Slider(new Rect(card.x+44,card.y+139,card.width-88,50),"마우스 감도",sensitivity,
                CafeSettings.MinSensitivity,CafeSettings.MaxSensitivity,(sensitivity/CafeSettings.DefaultSensitivity).ToString("0.0")+"x");
            if(!Mathf.Approximately(newSensitivity,sensitivity))CafeSettings.MouseSensitivity=newSensitivity;
            GUI.Label(new Rect(card.x+44,card.y+202,card.width-88,24),"대화 모델",text);
            float half=(card.width-100)*.5f;
            if(GUI.Toggle(new Rect(card.x+44,card.y+232,half,36),modelDraft.useApi,"API 우선",button))modelDraft.useApi=true;
            if(GUI.Toggle(new Rect(card.x+56+half,card.y+232,half,36),!modelDraft.useApi,"로컬 모델",button))modelDraft.useApi=false;
            bool enabled=GUI.enabled;GUI.enabled=enabled&&modelDraft.useApi;
            int selected=GUI.Toolbar(new Rect(card.x+44,card.y+282,card.width-88,36),(int)modelDraft.provider,new[]{"Gemini","OpenAI","호환 API"},button);
            if(selected!=(int)modelDraft.provider)
            {CafeModelPreferences.SelectProvider(modelDraft,(CafeApiProvider)selected);apiKeyDraft="";modelNotice="";GUI.FocusControl(null);}
            GUI.Label(new Rect(card.x+44,card.y+336,100,28),"모델 이름",text);
            modelDraft.model=GUI.TextField(new Rect(card.x+150,card.y+332,card.width-194,36),modelDraft.model??"",128,entry);
            GUI.Label(new Rect(card.x+44,card.y+385,100,28),"API 주소",text);
            string endpoint=GUI.TextField(new Rect(card.x+150,card.y+381,card.width-194,36),modelDraft.endpoint??"",512,entry);
            if(endpoint!=modelDraft.endpoint){modelDraft.endpoint=endpoint;apiKeyDraft="";modelNotice="";}
            GUI.Label(new Rect(card.x+44,card.y+434,100,28),"API 키",text);
            apiKeyDraft=GUI.PasswordField(new Rect(card.x+150,card.y+430,card.width-194,36),apiKeyDraft,'●',1024,entry);
            GUI.enabled=enabled;
            string note=modelDraft.useApi?"키 입력은 선택입니다. 비워 두면 해당 공급자의 기존 설정을 사용합니다. 입력한 키는 이번 실행 동안만 보관합니다.":
                "설치된 로컬 모델로 대화합니다. 로컬 모델을 사용할 수 없으면 준비된 자동 답변으로 이어집니다.";
            GUI.Label(new Rect(card.x+44,card.y+478,card.width-88,56),note,help);
            string applyHint=applyOnClose?"적용 후 설정창을 닫으면 동료 대화 모델을 바꿉니다.":"적용한 설정은 다음 게임 시작부터 사용합니다.";
            GUI.Label(new Rect(card.x+44,card.y+538,card.width-88,42),string.IsNullOrEmpty(modelNotice)?applyHint:modelNotice,help);
            if(GUI.Button(new Rect(card.x+44,card.y+587,half,42),"모델 설정 적용",button))
            {
                modelNotice=CafeModelPreferences.Save(modelDraft,apiKeyDraft,out var reason)?(applyOnClose?"저장했어요. 설정창을 닫으면 적용됩니다.":"저장했어요. 다음 게임 시작부터 적용됩니다."):reason;
            }
            bool back=GUI.Button(new Rect(card.x+56+half,card.y+587,half,42),"돌아가기",button);
            if(back)modelDraft=null;
            return back;
        }
        float Slider(Rect r,string label,float current,float min,float max,string readout)
        {
            GUI.Label(new Rect(r.x,r.y,r.width,24),label,text);
            GUI.Label(new Rect(r.x,r.y,r.width,24),readout,value);
            return GUI.HorizontalSlider(new Rect(r.x,r.y+32,r.width,24),current,min,max,track,thumb);
        }
        void Styles()
        {
            if(box!=null)return;
            paperTex=CafeHud.Rounded(64,22,CafeHud.paper,CafeHud.ink,3);
            buttonTex=CafeHud.Rounded(32,10,CafeHud.accent,CafeHud.ink,2.5f);
            buttonHoverTex=CafeHud.Rounded(32,10,Color.Lerp(CafeHud.accent,Color.white,.18f),CafeHud.ink,2.5f);
            trackTex=CafeHud.Rounded(24,7,new Color32(232,214,182,255),CafeHud.ink,2);
            box=new GUIStyle{border=new RectOffset(24,24,24,24)};box.normal.background=paperTex;
            text=new GUIStyle{font=font,fontSize=18,wordWrap=false};text.normal.textColor=CafeHud.ink;
            value=new GUIStyle(text){alignment=TextAnchor.UpperRight};
            title=new GUIStyle(text){fontSize=30,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            button=new GUIStyle(text){fontSize=16,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,border=new RectOffset(12,12,12,12),padding=new RectOffset(8,8,2,3)};
            button.normal.background=buttonTex;button.hover.background=button.active.background=buttonHoverTex;
            button.normal.textColor=button.hover.textColor=button.active.textColor=CafeHud.paper;
            button.onNormal.background=buttonHoverTex;button.onNormal.textColor=CafeHud.paper;
            entry=new GUIStyle(GUI.skin.textField){font=font,fontSize=17,padding=new RectOffset(8,8,6,4)};
            help=new GUIStyle(text){fontSize=14,wordWrap=true};
            track=new GUIStyle{border=new RectOffset(9,9,9,9),fixedHeight=14};track.normal.background=trackTex;
            thumb=new GUIStyle{border=new RectOffset(10,10,10,10),fixedWidth=24,fixedHeight=24};
            thumb.normal.background=buttonTex;thumb.hover.background=thumb.active.background=buttonHoverTex;
        }
        public void Dispose(){foreach(var t in new[]{paperTex,buttonTex,buttonHoverTex,trackTex})if(t!=null)Object.Destroy(t);}
    }
}
