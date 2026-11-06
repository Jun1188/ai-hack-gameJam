using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using CafeSim;
using MapleCafe;

if(!Application.isPlaying)throw new Exception("Run in the isolated CafeServiceRegression Play scene");
var hud=UnityEngine.Object.FindAnyObjectByType<CafeHud>();
var session=hud.session;
var type=typeof(CafeHud);var flags=BindingFlags.Instance|BindingFlags.NonPublic;
var results=new List<string>();
void Set(string field,object value)=>type.GetField(field,flags).SetValue(hud,value);
object Read(string field)=>type.GetField(field,flags).GetValue(hud);
void Invoke(string method,params object[] args)=>type.GetMethod(method,flags).Invoke(hud,args);
void Check(bool okay,string message){if(!okay)throw new Exception(message);results.Add(message);}
void Key(KeyCode key){Set("shortcutFrame",-1);Invoke("HandleOverlayKey",key);}
var stage=type.GetField("tutorialStage",flags);
void Tutorial(string name)=>stage.SetValue(hud,Enum.Parse(stage.FieldType,name));
if(session.Queue==null||!session.CanChat)throw new Exception("Wait for a fresh active ordering customer");
if((bool)Read("notebookOpen"))Invoke("ToggleNotebook");
Tutorial("Done");hud.businessDay.SetPaused(false);Invoke("SetPrompt",false);
var body=session.BodyOf(session.Selected);
if(body!=null)body.position=session.servicePoint.position;

// Reproduce the reported state: a real active customer, empty hands, and a lingering IME fragment
// after the conversation was closed. Do not replace this with a successful menu selection.
Set("composition","가");Set("compositionFrame",Time.frameCount);Set("inputFocused",false);
Key(KeyCode.E);
Check((bool)Read("promptOpen"),"E opens customer conversation after tutorial completion despite stale unfocused IME state");
Check(session.CanChat&&session.Selected.State==VisitState.Active,"opening dialogue preserves active serving customer");
Check(!hud.barista.CanInteract,"conversation owns input while open");
Invoke("SetPrompt",false);
Check(string.IsNullOrEmpty((string)Read("composition"))&&(int)Read("compositionFrame")==-10,"closing conversation clears composition and its frame guard");

Key(KeyCode.E);Set("inputFocused",true);Set("composition","가");Set("compositionFrame",Time.frameCount);
Key(KeyCode.E);
Check((bool)Read("promptOpen"),"E remains a typing key during an active Korean composition");
Invoke("SetPrompt",false);
Set("inputFocused",false);

var menu=UnityEngine.Object.FindAnyObjectByType<CafeMenuBook>();
typeof(CafeMenuBook).GetMethod("ToggleList",flags).Invoke(menu,null);
Check(hud.barista.ChoosingMenuCard,"recipe list opened for transition regression");
Key(KeyCode.E);
Check((bool)Read("promptOpen")&&!hud.barista.ChoosingMenuCard,"E closes recipe list and opens customer conversation");
Invoke("SetPrompt",false);

Tutorial("OpenMenu");typeof(CafeMenuBook).GetProperty("TutorialTabRequired",flags|BindingFlags.Public).SetValue(menu,true);
hud.businessDay.SetPaused(true);
Invoke("DismissTutorialForConversation");Key(KeyCode.E);
Check((bool)Read("promptOpen")&&!hud.businessDay.Paused,"tutorial request can yield to customer conversation");
Invoke("SetPrompt",false);Tutorial("Done");

var counter=UnityEngine.Object.FindAnyObjectByType<CafeServeCounter>();
Check(!counter.Describe(null,false).Contains("E"),"central serving message omits E guidance as requested");
Check(!counter.TryReceive(null,false,out var grip),"empty-hand conversation does not accidentally serve");
Key(KeyCode.E);
Check((bool)Read("promptOpen"),"customer conversation remains available after recipe/tutorial transitions");
return new{scope="Play-mode regression using the same overlay handler; scripted state setup, no OS IME claim",checks=results.ToArray()};
