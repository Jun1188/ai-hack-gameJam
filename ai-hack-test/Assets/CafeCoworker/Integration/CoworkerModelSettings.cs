using CafeSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CafeCoworker.Integration
{
    // Optional UI adapter. Neither the coworker domain nor its UI assembly depends on CafeSim.
    public sealed class CoworkerModelSettings : MonoBehaviour
    {
        public CoopHud hud;
        public CoopSession session;
        public ExistingCafeTransportHost host;
        public Font font;
        public string mainMenuScene = "MainMenu2";
        CafeOptionsGui options;
        bool opened;
        bool previousPause,previousMenu,previousMotor;
        string previousRouting, previousKey;
        void Awake()
        {
            hud.ModelSettingsRequested+=Open;
            hud.MainMenuRequested+=ReturnToMenu;
            session.player.MouseSensitivity=CafeSettings.MouseSensitivity;
        }
        bool returningToMenu;
        void ReturnToMenu()
        {
            if(returningToMenu)return;
            if(!Application.CanStreamedLevelBeLoaded(mainMenuScene))
            {
                Debug.LogError($"'{mainMenuScene}' 씬이 빌드 씬 목록에 없습니다.",this);
                return;
            }
            returningToMenu=true;
            session.dialogue.Cancel();session.guests.Cancel();
            session.Paused=true;session.player.MenuOpen=true;session.player.Lock(false);
            SceneManager.LoadScene(mainMenuScene);
        }
        public void Open()
        {
            if(opened)return;
            options??=new CafeOptionsGui(font,true);options.ReloadModelDraft();
            previousRouting=PlayerPrefs.GetString(CafeModelPreferences.PreferenceKey,"");
            previousKey=CafeModelPreferences.SessionKey(CafeModelPreferences.Load());
            previousPause=session.Paused;previousMenu=session.player.MenuOpen;previousMotor=session.barista.enabled;
            session.Paused=true;session.player.MenuOpen=true;session.barista.enabled=false;
            opened=true;hud.ExternalModal=true;
            hud.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;
        }
        public void Close()
        {
            if(!opened)return;
            opened=false;hud.ExternalModal=false;hud.BlockInputForThisFrame();
            hud.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.Flex;
            session.Paused=previousPause;session.player.MenuOpen=previousMenu;session.barista.enabled=previousMotor;
            session.player.MouseSensitivity=CafeSettings.MouseSensitivity;
            bool changed=previousRouting!=PlayerPrefs.GetString(CafeModelPreferences.PreferenceKey,"")||
                previousKey!=CafeModelPreferences.SessionKey(CafeModelPreferences.Load());
            previousKey="";
            if(changed){session.dialogue.Cancel();session.guests.Cancel();host.Reboot();}
        }
        void Update(){if(opened&&Keyboard.current?.escapeKey.wasPressedThisFrame==true)Close();}
        void OnGUI()
        {
            if(!opened)return;
            var prior=GUI.matrix;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            Vector2 offset=(new Vector2(Screen.width,Screen.height)-new Vector2(1280,720)*scale)*.5f;
            GUI.matrix=Matrix4x4.TRS(offset,Quaternion.identity,Vector3.one*scale);
            if(options.Draw(new Rect(640-CafeOptionsGui.Width*.5f,360-CafeOptionsGui.Height*.5f,CafeOptionsGui.Width,CafeOptionsGui.Height)))Close();
            GUI.matrix=prior;
        }
        void OnDestroy()
        {
            if(hud!=null){hud.ModelSettingsRequested-=Open;hud.MainMenuRequested-=ReturnToMenu;}
            options?.Dispose();previousKey="";
        }
    }
}
