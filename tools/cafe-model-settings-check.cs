using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using CafeSim;
var checks=new List<string>();
void Check(bool okay,string label){if(!okay)throw new Exception(label);checks.Add(label);}
bool existed=PlayerPrefs.HasKey(CafeModelPreferences.PreferenceKey);
string before=PlayerPrefs.GetString(CafeModelPreferences.PreferenceKey,"");
var flags=BindingFlags.NonPublic|BindingFlags.Static;
var keyField=typeof(CafeModelPreferences).GetField("sessionKey",flags);
var identityField=typeof(CafeModelPreferences).GetField("keyIdentity",flags);
object oldKey=keyField.GetValue(null),oldIdentity=identityField.GetValue(null);
var obj=new GameObject("Temporary model settings verification");
var model=obj.AddComponent<LocalCafeModel>();
try
{
    var data=new CafeModelPreferenceData();
    Check(CafeModelPreferences.Validate(data,out _),"default Gemini routing validates");
    data.useApi=false;
    Check(CafeModelPreferences.Save(data,"",out _),"local selection saves");
    CafeModelPreferences.Apply(model);
    Check(model.backend==CafeResponseBackend.LocalModel&&!model.UseApi,"local selection never chooses API routing");
    data.useApi=true;
    const string sampleKey="test-only-cafe-session-secret";
    Check(CafeModelPreferences.Save(data,sampleKey,out _),"API routing saves");
    Check(!PlayerPrefs.GetString(CafeModelPreferences.PreferenceKey).Contains(sampleKey),"API key is not persisted with preferences");
    CafeModelPreferences.Apply(model);
    Check(model.backend==CafeResponseBackend.Gemini&&model.runtimeApiKey==sampleKey,"Gemini selection receives its own session key");
    CafeModelPreferences.SelectProvider(data,CafeApiProvider.OpenAI);
    Check(!CafeModelPreferences.Validate(data,out _),"provider change requires an explicit model name");
    data.model="test-model";
    Check(CafeModelPreferences.SessionKey(data)=="","provider switch does not reuse another provider's key");
    Check(CafeModelPreferences.Save(data,"",out _),"OpenAI routing saves without a plaintext key");
    CafeModelPreferences.Apply(model);
    Check(model.backend==CafeResponseBackend.CompatibleApi&&model.apiKeyEnvironmentVariable=="OPENAI_API_KEY"&&model.runtimeApiKey=="","OpenAI routing and credential source are separate from Gemini");
    var bundled=typeof(LocalCafeModel).GetProperty("AllowsBundledGeminiKey",BindingFlags.NonPublic|BindingFlags.Instance);
    Check(!(bool)bundled.GetValue(model),"non-Gemini provider cannot use a bundled Gemini credential");
    CafeModelPreferences.SelectProvider(data,CafeApiProvider.Compatible);data.model="local-test";data.endpoint="http://127.0.0.1:8766/v1/chat/completions";
    Check(CafeModelPreferences.Validate(data,out _),"loopback compatible endpoint allowed");
    data.endpoint="http://example.com/v1/chat/completions";
    Check(!CafeModelPreferences.Validate(data,out _),"remote plaintext HTTP endpoint rejected");
    data.endpoint="https://name:password@example.com/v1/chat/completions";
    Check(!CafeModelPreferences.Validate(data,out _),"credentials in endpoint URL rejected");
    data.endpoint="https://one.example/v1/chat/completions";
    CafeModelPreferences.Save(data,sampleKey,out _);
    data.endpoint="https://two.example/v1/chat/completions";
    Check(CafeModelPreferences.SessionKey(data)=="","changing API origin does not reuse the old origin's key");
    data.useApi=false;CafeModelPreferences.Save(data,"",out _);
    Check(!CafeModelPreferences.Load().useApi,"backend choice reloads from persisted preferences");
}
finally
{
    if(existed)PlayerPrefs.SetString(CafeModelPreferences.PreferenceKey,before);else PlayerPrefs.DeleteKey(CafeModelPreferences.PreferenceKey);
    PlayerPrefs.Save();keyField.SetValue(null,oldKey);identityField.SetValue(null,oldIdentity);
    UnityEngine.Object.DestroyImmediate(obj);
}
return new{scope="Routing, validation and persistence only; no paid API calls or model downloads",checks=checks.ToArray()};
