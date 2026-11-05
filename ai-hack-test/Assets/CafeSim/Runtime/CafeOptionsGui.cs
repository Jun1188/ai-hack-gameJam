using UnityEngine;

namespace CafeSim
{
    // Volume and mouse-sensitivity card in the cafe's paper style, drawn with IMGUI.
    // The title screen and the in-game pause menu share it, so both edit the same CafeSettings.
    public sealed class CafeOptionsGui
    {
        public const float Width=460, Height=340;
        readonly Font font;
        Texture2D paperTex, buttonTex, buttonHoverTex, trackTex;
        GUIStyle box, title, text, value, button, track, thumb;

        public CafeOptionsGui(Font font){this.font=font;}

        // Returns true when the player presses "돌아가기".
        public bool Draw(Rect card)
        {
            Styles();
            if(Event.current.type==EventType.Repaint)
            {
                var old=GUI.color;GUI.color=new Color(.16f,.09f,.05f,.28f);
                box.Draw(new Rect(card.x+4,card.y+5,card.width,card.height),false,false,false,false);GUI.color=old;
                box.Draw(card,false,false,false,false);
            }
            GUI.Label(new Rect(card.x,card.y+28,card.width,40),"옵션",title);
            float volume=CafeSettings.Volume;
            float newVolume=Slider(new Rect(card.x+44,card.y+96,card.width-88,56),"전체 음량",volume,0,1,Mathf.RoundToInt(volume*100)+"%");
            if(!Mathf.Approximately(newVolume,volume))CafeSettings.Volume=newVolume;
            float sensitivity=CafeSettings.MouseSensitivity;
            float newSensitivity=Slider(new Rect(card.x+44,card.y+170,card.width-88,56),"마우스 감도",sensitivity,
                CafeSettings.MinSensitivity,CafeSettings.MaxSensitivity,(sensitivity/CafeSettings.DefaultSensitivity).ToString("0.0")+"x");
            if(!Mathf.Approximately(newSensitivity,sensitivity))CafeSettings.MouseSensitivity=newSensitivity;
            return GUI.Button(new Rect(card.x+100,card.y+260,card.width-200,48),"돌아가기",button);
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
            track=new GUIStyle{border=new RectOffset(9,9,9,9),fixedHeight=14};track.normal.background=trackTex;
            thumb=new GUIStyle{border=new RectOffset(10,10,10,10),fixedWidth=24,fixedHeight=24};
            thumb.normal.background=buttonTex;thumb.hover.background=thumb.active.background=buttonHoverTex;
        }
        public void Dispose(){foreach(var t in new[]{paperTex,buttonTex,buttonHoverTex,trackTex})if(t!=null)Object.Destroy(t);}
    }
}
