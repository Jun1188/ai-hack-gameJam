string[] snapshots={"Assets/CafeSim/CafeChatBuildSnapshot.unity","Assets/CafeSim/CafeChatBuildSnapshot2.unity","Assets/CafeSim/CafeChatBuildSnapshot3.unity"};
foreach(var path in snapshots)
{
 for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
  if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path==path)throw new System.Exception("Snapshot is open; do not delete it");
 if(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)!=null&&!UnityEditor.AssetDatabase.DeleteAsset(path))throw new System.Exception("Could not remove own build snapshot");
}
return "Removed only temporary build snapshot assets; original cafe scene preserved";
