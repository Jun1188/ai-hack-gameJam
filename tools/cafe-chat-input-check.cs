var panel=UnityEngine.Resources.Load<UnityEngine.UIElements.PanelSettings>("CafeChatPanelSettings");
var font=UnityEngine.Resources.Load<UnityEngine.TextCore.Text.FontAsset>("CafeChatKoreanFont");
if(panel==null||panel.themeStyleSheet==null||font==null||font.sourceFontFile==null)throw new System.Exception("Missing Korean input resources");
if(!font.HasCharacter('한',true,true)||!font.HasCharacter('글',true,true))throw new System.Exception("Korean font glyphs unavailable");
var text=new UnityEngine.UIElements.TextField{maxLength=300,multiline=false};text.value="한글로 대화할 수 있나요?";
if(text.value!="한글로 대화할 수 있나요?")throw new System.Exception("Korean text round-trip failed");
return "PASS persisted panel/theme, Korean font source and glyphs, UI Toolkit Korean value round-trip. OS IME composition remains user Play validation.";
