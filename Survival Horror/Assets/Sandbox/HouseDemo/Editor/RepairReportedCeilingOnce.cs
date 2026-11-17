using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
static class RepairReportedCeilingOnce
{
    static RepairReportedCeilingOnce() => EditorApplication.delayCall += Repair;
    static void Repair()
    {
        const string report="Logs/CutawayCheck/live.txt";
        if(File.Exists(report) || EditorApplication.isPlayingOrWillChangePlaymode)return;
        GlobalObjectId.TryParse("GlobalObjectId_V1-2-720785324909e3c46aa29956061cf6b1-392030292-0",out var id);
        var target=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
        if(!target || target.name!="Ceiling slab" || target.scene.path!="Assets/Game/Levels/Chapter0/Scenes/1stLevel.unity")return;
        var mesh=target.GetComponent<MeshFilter>().sharedMesh;
        bool wasHidden=SceneVisibilityManager.instance.IsHidden(target);
        var state=typeof(SceneVisibilityManager).Assembly.GetType("UnityEditor.SceneVisibilityState",true);
        state.GetMethod("SetGameObjectHidden").Invoke(null,new object[]{target,false,false});
        state.GetMethod("GeneratePersistentDataForLoadedScene").Invoke(null,new object[]{target.scene});
        SceneView.RepaintAll();
        File.WriteAllText(report,"Previously hidden="+wasHidden+"; now hidden="+SceneVisibilityManager.instance.IsHidden(target)+"; enabled="+target.GetComponent<MeshRenderer>().enabled+"; vertices="+(mesh?mesh.vertexCount:0)+"; collider matches="+(target.GetComponent<MeshCollider>().sharedMesh==mesh));
    }
}
