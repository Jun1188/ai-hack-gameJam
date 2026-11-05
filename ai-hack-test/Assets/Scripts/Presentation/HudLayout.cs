using UnityEngine;

namespace GrandpaRobot.Presentation
{
    public sealed class HudLayout
    {
        public readonly Rect SafePixels;
        public readonly float Scale,Width,Height;
        public HudLayout(int screenWidth,int screenHeight,Rect safeArea)
        {
            var full=new Rect(0,0,Mathf.Max(1,screenWidth),Mathf.Max(1,screenHeight));
            if(safeArea.width<=0||safeArea.height<=0)safeArea=full;
            var left=Mathf.Clamp(safeArea.xMin,0,full.width);
            var right=Mathf.Clamp(safeArea.xMax,left,full.width);
            var bottom=Mathf.Clamp(safeArea.yMin,0,full.height);
            var top=Mathf.Clamp(safeArea.yMax,bottom,full.height);
            SafePixels=right-left<1||top-bottom<1?full:new Rect(left,screenHeight-top,right-left,top-bottom);
            Scale=Mathf.Min(SafePixels.width/1100f,SafePixels.height/720f);
            Width=SafePixels.width/Scale;Height=SafePixels.height/Scale;
        }
        public Rect Modal=>new Rect((Width-720)/2,(Height-240)/2,720,240);
        public bool TryLabel(Vector3 screenPoint,int screenHeight,float headerHeight,float footerTop,out Rect rect)
        {
            var x=(screenPoint.x-SafePixels.x)/Scale;
            var y=(screenHeight-screenPoint.y-SafePixels.y)/Scale;
            rect=new Rect(x-25,y-12,50,24);
            return screenPoint.z>0&&rect.xMin>=8&&rect.xMax<=Width-8&&rect.yMin>=headerHeight+8&&rect.yMax<=footerTop-8;
        }
    }
}
