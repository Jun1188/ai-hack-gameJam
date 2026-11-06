// Editor-only preview and reference checks; never enters Play Mode or edits a scene.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

CafeSim.CafeCustomerCatalog.Reload();
var preview = new PreviewRenderUtility();
try
{
    preview.camera.backgroundColor = new Color(.17f, .20f, .23f);
    preview.camera.clearFlags = CameraClearFlags.SolidColor;
    preview.camera.orthographic = true;
    preview.camera.orthographicSize = 1.1f;
    preview.camera.transform.position = new Vector3(0, .9f, 4);
    preview.camera.transform.LookAt(new Vector3(0, .9f, 0));
    preview.lights[0].intensity = 1.2f;
    preview.lights[0].transform.rotation = Quaternion.Euler(35, 150, 0);
    preview.lights[1].intensity = .8f;
    preview.ambientColor = new Color(.55f, .55f, .55f);
    int index = 0;
    foreach (string id in new[] { "cus_pangyo", "cus_student" })
    {
        var entry = CafeSim.CafeCustomerCatalog.Choose(id, new System.Random(1));
        if (entry == null || entry.personaIds.Length != 1 || entry.personaIds[0] != id)
            throw new Exception("Dedicated mapping failed: " + id);
        var prefab = Resources.Load<GameObject>(entry.resource);
        var model = UnityEngine.Object.Instantiate(prefab);
        preview.AddSingleGO(model);
        var animator = model.GetComponentInChildren<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            throw new Exception("Invalid humanoid: " + id);
        if (animator.GetBoneTransform(HumanBodyBones.RightHand) == null)
            throw new Exception("Missing right hand: " + id);
        var clips = animator.runtimeAnimatorController.animationClips;
        if (clips.Length != 2 || clips.Any(c => !c.humanMotion || c.length <= 0 || !c.isLooping))
            throw new Exception("Invalid Idle/Walk clips: " + id);
        animator.applyRootMotion = false;
        var controller = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
        var idle = (AnimationClip)controller.layers[0].stateMachine.states.Single(s => s.state.name == "Idle").state.motion;
        idle.SampleAnimation(model, .5f);
        var renderers = model.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        float scale = entry.height / bounds.size.y;
        model.transform.localScale *= scale;
        model.transform.position = new Vector3(index++ == 0 ? -.65f : .65f, -bounds.min.y * scale, 0);
        foreach (var r in renderers)
            foreach (var material in r.sharedMaterials)
                if (material == null || material.GetTexture("_BaseMap") == null || material.GetTexture("_BumpMap") == null)
                    throw new Exception("Missing outfit texture: " + id);
    }
    preview.BeginStaticPreview(new Rect(0, 0, 1000, 800));
    preview.Render(true);
    var image = preview.EndStaticPreview();
    File.WriteAllBytes("../docs/images/cafe-additional-customers-preview.png", image.EncodeToPNG());
    UnityEngine.Object.DestroyImmediate(image);
}
finally { preview.Cleanup(); }
return "PASS dedicated runtime selection, valid humanoid/right hand, two looping humanoid clips and outfit textures. Editor preview saved; no Play/build.";
