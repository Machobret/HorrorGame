using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HorrorEngine;
using HorrorGame.Enemies;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;

public class WitchValidation : MonoBehaviour
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Debug.Log("WITCH PASS: " + name); }
    static void Invoke(object obj,string method) { obj.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,null); }
    public static void Run()
    {
        EditorApplication.playModeStateChanged += mode => {
            if(mode==PlayModeStateChange.EnteredPlayMode) new GameObject("Witch validation").AddComponent<WitchValidation>();
        };
        EditorSettings.enterPlayModeOptionsEnabled=true;
        EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
        EditorApplication.isPlaying=true;
    }
    void Start() { StartCoroutine(Guard(Tests())); }
    IEnumerator Guard(IEnumerator tests)
    {
        while(true) {
            object next;
            try { if(!tests.MoveNext()) break; next=tests.Current; }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); yield break; }
            yield return next;
        }
        Debug.Log("WITCH VALIDATION COMPLETE");EditorApplication.Exit(0);
    }
    IEnumerator Tests()
    {
        var model=Resources.Load<GameObject>("Witch/Witch_Model");Check(model,"FBX imported");
        var mesh=model.GetComponentInChildren<SkinnedMeshRenderer>();Check(mesh && mesh.bones.Length==18,"18-bone skinned mesh imported");
        var clips=Resources.LoadAll<AnimationClip>("Witch/Witch_Model").Where(c=>!c.name.StartsWith("__preview__")).ToArray();
        Check(clips.Length==7 && clips.All(c=>c.legacy && c.length>0),"seven playable legacy animation clips");
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.position=new Vector3(0,-.1f,0);ground.transform.localScale=new Vector3(30,.2f,30);
        var sources=new System.Collections.Generic.List<NavMeshBuildSource>{new NavMeshBuildSource {shape=NavMeshBuildSourceShape.Box,transform=Matrix4x4.TRS(ground.transform.position,Quaternion.identity,Vector3.one),size=ground.transform.localScale,area=0}};
        var nav=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,new Bounds(Vector3.zero,new Vector3(30,5,30)),Vector3.zero,Quaternion.identity);
        Check(nav,"NavMesh built");NavMesh.AddNavMeshData(nav);
        var target=new GameObject("Test player");target.AddComponent<Combatant>();var th=target.AddComponent<Health>();th.Max=th.Value=3;
        var hurtbox=target.AddComponent<CapsuleCollider>();hurtbox.isTrigger=true;hurtbox.center=Vector3.up;hurtbox.height=2;
        var damageable=target.AddComponent<Damageable>();damageable.Type=AssetDatabase.LoadAssetAtPath<DamageableType>("Assets/PlayerType.asset");target.transform.position=new Vector3(0,0,5);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Monster_Witch.prefab");Check(prefab,"prefab imported");
        var go=Instantiate(prefab);var witch=go.GetComponent<WitchController>();witch.Target=target.transform;
        yield return null;
        Check(witch.State==WitchController.WitchState.Idle,"calm before provocation");
        Check(go.GetComponent<NavMeshAgent>().isOnNavMesh,"agent bound to NavMesh");
        var visual=go.GetComponentInChildren<Animation>();Check(visual && visual.IsPlaying("Idle"),"idle animation playing");
        var head=visual.GetComponentsInChildren<Transform>().First(t=>t.name=="head");var pose=head.localRotation;
        yield return new WaitForSeconds(.25f);Check(Quaternion.Angle(pose,head.localRotation)>.01f,"rig actually animates");
        target.transform.position=new Vector3(0,0,2.8f);yield return new WaitForSeconds(.25f);
        Check(witch.State==WitchController.WitchState.Warning && !witch.IsProvoked,"proximity gives warning");
        target.transform.position=new Vector3(0,0,5);yield return new WaitForSeconds(.25f);Check(witch.State==WitchController.WitchState.Idle,"retreat cancels warning");
        target.transform.position=new Vector3(0,0,2.8f);witch.WarningDuration=.3f;yield return new WaitForSeconds(.65f);
        Check(witch.IsProvoked,"lingering provokes");
        witch.OnReset();go.GetComponent<Health>().TakeDamage(.1f);Check(witch.IsProvoked,"damage immediately provokes");
        target.transform.position=new Vector3(0,0,9);var start=go.transform.position;yield return new WaitForSeconds(.8f);
        Check(Vector3.Distance(start,go.transform.position)>.5f,"persistent pursuit moves agent");
        var last=witch.LastKnownPosition;
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,1.5f,6);wall.transform.localScale=new Vector3(20,3,.2f);
        target.transform.position=new Vector3(4,0,9);Physics.SyncTransforms();yield return new WaitForSeconds(.25f);
        Check(witch.IsProvoked && Vector3.Distance(last,witch.LastKnownPosition)<.01f,"occlusion preserves hostility without tracking hidden player");
        Destroy(wall);yield return null;
        go.GetComponent<NavMeshAgent>().Warp(Vector3.zero);go.transform.rotation=Quaternion.identity;witch.OnReset();target.transform.position=new Vector3(0,0,1.5f);witch.Provoke();
        yield return new WaitForSeconds(.2f);Check(witch.State==WitchController.WitchState.Windup,"attack wind-up");
        target.transform.position=new Vector3(2.5f,0,1.5f);yield return new WaitForSeconds(1f);
        Check(th.Value==3 && witch.State==WitchController.WitchState.Recover,"sidestep evades committed slash with recovery");
        witch.OnReset();go.GetComponent<NavMeshAgent>().Warp(Vector3.zero);go.transform.rotation=Quaternion.identity;target.transform.position=new Vector3(0,0,1.5f);witch.Provoke();
        yield return new WaitForSeconds(1.1f);Check(th.Value==2,"slash damages once through template attack system");
        witch.EnableDamage=false;witch.OnReset();go.GetComponent<NavMeshAgent>().Warp(Vector3.zero);go.transform.rotation=Quaternion.identity;target.transform.position=new Vector3(0,0,1.5f);witch.Provoke();
        yield return new WaitForSeconds(1.1f);Check(th.Value==2,"damage toggle disables damage");
        go.GetComponent<Health>().Kill();yield return null;
        Check(witch.State==WitchController.WitchState.Dead && go.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"death stops combat and disables hitboxes");
        go.GetComponent<Health>().OnReset();witch.OnReset();Check(!witch.IsProvoked && witch.State==WitchController.WitchState.Idle,"reset restores calm");
    }
}
