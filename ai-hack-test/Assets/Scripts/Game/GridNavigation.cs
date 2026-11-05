using System.Collections.Generic;
using UnityEngine;

namespace GrandpaRobot.Game
{
    public static class GridNavigation
    {
        const float Step=.5f;
        // Small bounded map: breadth-first search gives deterministic shortest cardinal paths.
        public static List<Vector3> Find(Vector3 start,Vector3 target,float width,float depth,System.Func<Vector3,bool> blocked,float reach=1.15f)
        {
            var source=new Vector2Int(Mathf.RoundToInt(start.x/Step),Mathf.RoundToInt(start.z/Step));
            var queue=new Queue<Vector2Int>(); var parent=new Dictionary<Vector2Int,Vector2Int>();
            queue.Enqueue(source); parent[source]=source;
            var directions=new[]{Vector2Int.right,Vector2Int.up,Vector2Int.left,Vector2Int.down};
            while(queue.Count>0)
            {
                var cell=queue.Dequeue(); var point=new Vector3(cell.x*Step,0,cell.y*Step);
                if(Vector2.Distance(new Vector2(point.x,point.z),new Vector2(target.x,target.z))<=reach)
                {
                    var path=new List<Vector3>();
                    while(cell!=source) { path.Add(new Vector3(cell.x*Step,0,cell.y*Step)); cell=parent[cell]; }
                    path.Reverse(); return path;
                }
                foreach(var d in directions)
                {
                    var next=cell+d; var p=new Vector3(next.x*Step,0,next.y*Step);
                    if(Mathf.Abs(p.x)>width/2-.5f || Mathf.Abs(p.z)>depth/2-.5f || parent.ContainsKey(next) || blocked(p)) continue;
                    parent[next]=cell; queue.Enqueue(next);
                }
            }
            return null;
        }
    }
}
