using UnityEngine;
using UnityEngine.UI;

namespace CafeSim
{
    // Business-day overlays for the merged cafe: the opening-hours clock, the Esc pause menu
    // (with options) and the settlement card shown at closing time.
    public sealed partial class CafeHud
    {
        public CafeBusinessDay businessDay;
        bool optionsOpen, cardsWereOpen;
        Texture2D trackTex, fillTex;
        GUIStyle artTitle, artValue, artCenter;
        CafeOptionsGui options;
        RectTransform hudCanvas;
        Sprite paperSprite, trackSprite, fillSprite;
        Text clockLabel;
        RectTransform clockFill;

        bool Halted=>businessDay!=null&&(businessDay.Paused||businessDay.Closed);

        void DayStyles()
        {
            if(artTitle!=null)return;
            trackTex=Rounded(24,7,new Color32(232,214,182,255),ink,2);
            fillTex=Rounded(24,7,accent,ink,2);
            artTitle=new GUIStyle(artText){fontSize=30,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,wordWrap=false};
            artValue=new GUIStyle(artText){alignment=TextAnchor.UpperRight,wordWrap=false};
            artCenter=new GUIStyle(artSmall){alignment=TextAnchor.MiddleCenter,wordWrap=false};
        }
        void DestroyDayTextures()
        {
            options?.Dispose();DestroyReceiptTextures();DestroyNotebookTextures();
            foreach(var t in new[]{trackTex,fillTex})if(t!=null)Destroy(t);
            foreach(var s in new[]{paperSprite,trackSprite,fillSprite})if(s!=null)Destroy(s);
        }

        // Esc while the cafe is running opens the pause menu; inside it, Esc backs out one level.
        bool HandleDayKey(KeyCode key)
        {
            if(businessDay==null)return false;
            // Q opens the 영업 수첩 (pausing the day); Q or Esc closes it again.
            if(key==KeyCode.Q||(key==KeyCode.Escape&&notebookOpen)){ToggleNotebook();return true;}
            if(businessDay.Closed)return true;
            if(businessDay.Paused)
            {
                if(key==KeyCode.Escape){if(optionsOpen)optionsOpen=false;else Resume();}
                return true;
            }
            // The Esc that closes the drink cards must not also pause the game.
            if(key==KeyCode.Escape&&!promptOpen&&!cardsWereOpen){optionsOpen=false;businessDay.SetPaused(true);return true;}
            return false;
        }
        void Resume()
        {
            optionsOpen=false;businessDay.SetPaused(false);
            UnityEngine.Cursor.lockState=CursorLockMode.Locked;UnityEngine.Cursor.visible=false;
        }

        // The clock and speech bubble share a canvas below the Tab cards (sorting order 40).
        void UpdateClock()
        {
            if(businessDay==null)return;
            if(clockLabel==null)BuildClock();
            clockLabel.text=CafeBusinessDay.Day+"일차   "+businessDay.ClockText;
            clockFill.sizeDelta=new Vector2(Mathf.Max(10,(ClockWidth-44)*businessDay.Progress),10);
        }
        const float ClockWidth=212;
        RectTransform EnsureHudCanvas()
        {
            if (hudCanvas != null) return hudCanvas;
            ArtStyles();
            var root=new GameObject("Cafe HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            root.transform.SetParent(transform,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30;
            // Same scale as the IMGUI layer: the 1280x720 layout fitted inside the screen.
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            paperSprite=Sliced(paperTex,24);
            return hudCanvas = (RectTransform)root.transform;
        }
        void BuildClock()
        {
            var root = EnsureHudCanvas();
            DayStyles();
            trackSprite=Sliced(trackTex,9);fillSprite=Sliced(fillTex,9);
            var shadow=Box("Shadow",root,paperSprite,new Color(.16f,.09f,.05f,.28f),new Vector2(1,1),new Vector2(-24+4,-20-5),new Vector2(ClockWidth,58));
            var card=Box("Card",root,paperSprite,Color.white,new Vector2(1,1),new Vector2(-24,-20),new Vector2(ClockWidth,58));
            var label=new GameObject("Time",typeof(RectTransform),typeof(Text));
            var labelRect=(RectTransform)label.transform;labelRect.SetParent(card,false);
            labelRect.anchorMin=labelRect.anchorMax=labelRect.pivot=new Vector2(0,1);labelRect.anchoredPosition=new Vector2(22,-8);labelRect.sizeDelta=new Vector2(ClockWidth-44,26);
            clockLabel=label.GetComponent<Text>();clockLabel.font=artText.font;clockLabel.fontSize=18;clockLabel.color=ink;
            clockLabel.alignment=TextAnchor.MiddleLeft;clockLabel.raycastTarget=false;
            var track=Box("Day progress",card,trackSprite,Color.white,new Vector2(0,1),new Vector2(22,-38),new Vector2(ClockWidth-44,10));
            clockFill=Box("Elapsed",track,fillSprite,Color.white,new Vector2(0,1),Vector2.zero,new Vector2(10,10));
            shadow.SetAsFirstSibling();
        }
        static RectTransform Box(string name,Transform parent,Sprite sprite,Color color,Vector2 corner,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));
            var rect=(RectTransform)go.transform;rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=corner;rect.anchoredPosition=position;rect.sizeDelta=size;
            var image=go.GetComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;
            return rect;
        }
        static Sprite Sliced(Texture2D tex,float border)=>
            Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,Vector4.one*border);

        void DrawDayOverlay()
        {
            if(!Halted)return;
            if(notebookOpen){DrawNotebook();return;}
            if(businessDay.Closed){DrawClosing();return;}
            if(Event.current.type==EventType.Repaint)
            {
                var old=GUI.color;GUI.color=new Color(.12f,.07f,.04f,.55f);
                GUI.DrawTexture(new Rect(-2000,-2000,5280,4720),Texture2D.whiteTexture);GUI.color=old;
            }
            if(optionsOpen)DrawOptions();
            else DrawPause();
        }
        void DrawPause()
        {
            var card=new Rect(640-190,360-170,380,340);Paper(card);
            GUI.Label(new Rect(card.x,card.y+28,card.width,40),"일시정지",artTitle);
            GUI.Label(new Rect(card.x,card.y+72,card.width,24),CafeBusinessDay.Day+"일차 · "+businessDay.ClockText,artCenter);
            float y=card.y+120;
            if(GUI.Button(new Rect(card.x+60,y,260,48),"계속하기",artButton))Resume();
            if(GUI.Button(new Rect(card.x+60,y+62,260,48),"옵션",artButton))optionsOpen=true;
            if(GUI.Button(new Rect(card.x+60,y+124,260,48),"메인화면으로 나가기",artButton))businessDay.ExitToMenu();
        }
        void DrawOptions()
        {
            options??=new CafeOptionsGui(artText.font);
            bool back=options.Draw(new Rect(640-CafeOptionsGui.Width*.5f,360-CafeOptionsGui.Height*.5f,CafeOptionsGui.Width,CafeOptionsGui.Height));
            if(barista!=null)barista.MouseSensitivity=CafeSettings.MouseSensitivity;
            if(back)optionsOpen=false;
        }
        static string Won(int amount)=>amount.ToString("N0")+"원";
    }
}
