const string path="Assets/CafeSim/Resources/CafeChatPanelSettings.asset";
UnityEditor.AssetDatabase.Refresh();
var settings=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(path);
if(settings==null){settings=UnityEngine.ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();UnityEditor.AssetDatabase.CreateAsset(settings,path);}
settings.scaleMode=UnityEngine.UIElements.PanelScaleMode.ConstantPixelSize;settings.scale=1;
settings.themeStyleSheet=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.ThemeStyleSheet>("Assets/CafeSim/Resources/CafeChatTheme.tss");
if(settings.themeStyleSheet==null)throw new System.Exception("Chat theme import failed");
UnityEditor.EditorUtility.SetDirty(settings);UnityEditor.AssetDatabase.SaveAssetIfDirty(settings);
const string fontPath="Assets/CafeSim/Resources/CafeChatKoreanFont.asset";
var font=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>(fontPath);
if(font==null)
{
 var source=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/Cafe/Menu/NotoSansKR.ttf");
 if(source==null)throw new System.Exception("Existing Korean font is missing");
 font=UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(source);
 font.name="CafeChatKoreanFont";
 UnityEditor.AssetDatabase.CreateAsset(font,fontPath);
 if(font.material!=null)UnityEditor.AssetDatabase.AddObjectToAsset(font.material,font);
 foreach(var atlas in font.atlasTextures)if(atlas!=null)UnityEditor.AssetDatabase.AddObjectToAsset(atlas,font);
 UnityEditor.EditorUtility.SetDirty(font);UnityEditor.AssetDatabase.SaveAssetIfDirty(font);
}
return "Created scene-independent UI Toolkit chat panel; Input System setting preserved";
