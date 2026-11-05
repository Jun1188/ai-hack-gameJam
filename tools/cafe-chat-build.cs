var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(scene.path!=CafeSim.Editor.CafeSceneBuilder.ScenePath)throw new System.Exception("Expected open cafe scene; preserve user context");
if(UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Stop Play before building");
const string snapshot="Assets/CafeSim/CafeChatBuildSnapshot3.unity";
if(System.IO.File.Exists(snapshot))throw new System.Exception("Snapshot already exists; inspect it before overwriting");
bool dirty=scene.isDirty;
if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,snapshot,true))throw new System.Exception("Could not save build snapshot");
if(scene.path!=CafeSim.Editor.CafeSceneBuilder.ScenePath||scene.isDirty!=dirty)throw new System.Exception("Live scene changed while snapshotting");
return new{snapshot,original=scene.path,originalStillDirty=scene.isDirty};
