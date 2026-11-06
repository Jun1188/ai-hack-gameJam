// Run with script eval --file. Checks real selection/spawn in an isolated Editor preview.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CafeSim;
using UnityEditor;
using UnityEngine;

if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run outside Play Mode");
void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
var people = JsonUtility.FromJson<PersonaTable>(File.ReadAllText("Assets/CafeSim/Data/personas.json")).items;
var choose = typeof(CafeQueueSim).GetMethod("ChoosePersona", BindingFlags.NonPublic | BindingFlags.Static);
Persona Draw(Persona[] cast, System.Random random) => (Persona)choose.Invoke(null, new object[] { cast, random });
var rng = new System.Random(418);
int women = 0, miku = 0;
for (int i = 0; i < 50000; i++)
{
    var p = Draw(people, rng);
    if (p.gender.Contains("여성")) women++;
    if (p.id == "cus_miku") miku++;
}
double expected = Math.Max(.40, .05 + .95 * people.Count(p => p.id != "cus_miku" && p.gender.Contains("여성")) / (people.Length - 1));
Check(Math.Abs(women / 50000.0 - expected) < .008, "Full cast female draw probability");
Check(Math.Abs(miku / 50000.0 - .05) < .004, "Miku must retain 5 percent");
var minority = Enumerable.Range(0, 9).Select(i => new Persona { id = "man" + i, gender = "청년 남성" })
    .Concat(new[] { new Persona { id = "woman", gender = "청년 여성" } }).ToArray();
int minorityWomen = 0;
for (int i = 0; i < 50000; i++) if (Draw(minority, rng).id == "woman") minorityWomen++;
Check(Math.Abs(minorityWomen / 50000.0 - .40) < .008, "Female minority must be raised to 40 percent");
foreach (var p in people) Check(Draw(new[] { p }, rng) == p, "Single-person scene selection");
Check(Draw(minority.Take(9).ToArray(), rng).gender.Contains("남성"), "All-male cast remains usable");
CafeCustomerCatalog.Reload();
var preview = new PreviewRenderUtility();
try
{
    var sessionGo = new GameObject("Isolated cast check"); preview.AddSingleGO(sessionGo);
    var session = sessionGo.AddComponent<CafeSession>(); session.entrance = sessionGo.transform;
    var spawn = typeof(CafeCustomerAppearance).GetMethod("Spawn", BindingFlags.NonPublic | BindingFlags.Static);
    var grip = typeof(CafeCustomerAppearance).GetMethod("PrepareMugGrip", BindingFlags.NonPublic | BindingFlags.Instance);
    var featured = new[] { "cus_gyeongsang", "cus_jeolla", "cus_elder" };
    foreach (var p in people)
    {
        var asset = CafeCustomerCatalog.Choose(p.id, rng);
        Check(asset != null && asset.status == "ready", "Ready mapping missing: " + p.id);
        if (p.id == "cus_gyeongsang") Check(asset.personaIds.Length == 1 && asset.resource.Contains("cus_gyeongsang"), "Dedicated Gyeongsang mapping");
        if (p.id != "cus_miku") Check(!asset.resource.Contains("miku"), "Miku appearance must remain exclusive");
        var visit = new CafeVisit { Id = 1, Person = p };
        var model = (Transform)spawn.Invoke(null, new object[] { session, visit, rng });
        preview.AddSingleGO(model.gameObject);
        Check(model.name.Contains(asset.resource), "Spawn fell back from mapped appearance: " + p.id);
        Check(grip.Invoke(model.GetComponent<CafeCustomerAppearance>(), null) != null, "Cup grip missing: " + p.id);
        var animator = model.GetComponentInChildren<Animator>();
        Check(animator != null && animator.runtimeAnimatorController != null && animator.runtimeAnimatorController.animationClips.Length == 2, "Idle/Walk controller missing: " + p.id);
        int index = Array.IndexOf(featured, p.id);
        if (index < 0) { UnityEngine.Object.DestroyImmediate(model.gameObject); continue; }
        model.position = new Vector3((1 - index) * 1.1f, 0, 0);
        var controller = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
        var idle = (AnimationClip)controller.layers[0].stateMachine.states.Single(s => s.state.name == "Idle").state.motion;
        idle.SampleAnimation(animator.gameObject, .5f);
    }
    preview.camera.backgroundColor = new Color(.17f, .20f, .23f);
    preview.camera.clearFlags = CameraClearFlags.SolidColor;
    preview.camera.orthographic = true; preview.camera.orthographicSize = 1.13f;
    preview.camera.transform.position = new Vector3(0, .9f, 5);
    preview.camera.transform.LookAt(new Vector3(0, .9f, 0));
    preview.lights[0].intensity = 1.3f; preview.lights[0].transform.rotation = Quaternion.Euler(35, 150, 0);
    preview.lights[1].intensity = .8f; preview.ambientColor = new Color(.55f, .55f, .55f);
    preview.BeginStaticPreview(new Rect(0, 0, 1200, 700)); preview.Render(true);
    var image = preview.EndStaticPreview();
    File.WriteAllBytes("../docs/images/cafe-cast-mapping-preview.png", image.EncodeToPNG());
    UnityEngine.Object.DestroyImmediate(image);
}
finally { preview.Cleanup(); }
return $"PASS all {people.Length} ready mappings, real Spawn and mug grips; female {women / 500.0:F2}% (expected {expected * 100:F2}%), Miku {miku / 500.0:F2}%, minority-cast women {minorityWomen / 500.0:F2}%; isolated Editor preview only, no Play/build.";
