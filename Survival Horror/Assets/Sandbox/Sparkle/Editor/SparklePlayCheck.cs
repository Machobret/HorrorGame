using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HorrorEngine;
using HorrorGame.Items;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HorrorGame.Tests
{
    public static class SparklePlayCheck
    {
        static readonly StringBuilder report = new StringBuilder();
        static ItemGoldenSparkle effect;
        static RoomSparkleTrigger trigger;
        static PlayerActor actor;
        static PlayerMovement movement;
        static bool movementEnabled;
        static Vector3 savedPosition;
        static Quaternion savedRotation;
        static float started, sampled;
        static int maxParticles;
        static bool savedRequireEntry, captured;
        static readonly FieldInfo Revealed = typeof(RoomSparkleTrigger).GetField("revealed", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo Occupants = typeof(RoomSparkleTrigger).GetField("occupants", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo Visible = typeof(ItemGoldenSparkle).GetField("roomVisible", BindingFlags.Instance | BindingFlags.NonPublic);

        [MenuItem("Horror Game/Tests/Check Cigarette Sparkle")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || !GameManager.Exists || !GameManager.Instance.Player)
                throw new InvalidOperationException("Enter Play mode with the player spawned first.");
            if (actor) throw new InvalidOperationException("Sparkle check already running.");
            effect = Object.FindObjectsByType<ItemGoldenSparkle>(FindObjectsSortMode.None)
                .FirstOrDefault(e => e.GetComponentInParent<PickupItem>() &&
                    e.GetComponentInParent<PickupItem>().Entry.Item.name == "CigarettePack");
            if (!effect) throw new InvalidOperationException("No active cigarette pickup with a sparkle was found in the open scene.");
            trigger = Object.FindObjectsByType<RoomSparkleTrigger>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.Targets.Contains(effect));
            if (!trigger) throw new InvalidOperationException("The cigarette sparkle has no linked trigger.");
            actor = GameManager.Instance.Player;
            savedPosition = actor.transform.position; savedRotation = actor.transform.rotation;
            movement = actor.GetComponent<PlayerMovement>(); movementEnabled = movement.enabled;
            movement.enabled = false;
            report.Clear(); maxParticles = 0;
            savedRequireEntry = effect.RequireRoomEntry;
            effect.RequireRoomEntry = true;
            effect.GetComponent<ParticleSystem>().Clear();
            captured = false;
            report.AppendLine($"Initial player={savedPosition} distance={Vector3.Distance(savedPosition,effect.transform.position):F2}");
            report.AppendLine($"Effect={PathOf(effect.transform)} pos={effect.transform.position} scale={effect.transform.lossyScale} size={effect.Size} alpha={effect.Tint.a} requireEntry={effect.RequireRoomEntry}");
            var box = trigger.GetComponent<BoxCollider>();
            report.AppendLine($"Trigger={PathOf(trigger.transform)} enabled={trigger.enabled} layer={trigger.gameObject.layer} size={box.bounds.size} center={box.bounds.center} delay={trigger.EntryDelay} targets={trigger.Targets.Length}");
            // Place the real player's capsule through the area; freeze locomotion, not its collider.
            Warp(box.bounds.center - Vector3.up * .8f);
            started = Time.time; sampled = -1;
            Physics.SyncTransforms();
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying || !actor || !effect) { Finish("INTERRUPTED"); return; }
                float elapsed = Time.time - started;
                var ps = effect.GetComponent<ParticleSystem>();
                maxParticles = Mathf.Max(maxParticles, ps.particleCount);
                if (!captured && elapsed > trigger.EntryDelay + .2f && ps.particleCount > 0)
                {
                    var buffer = new ParticleSystem.Particle[4];
                    int count = ps.GetParticles(buffer);
                    if (count > 0 && buffer[0].startLifetime - buffer[0].remainingLifetime > .2f)
                    { captured = true; Capture(); }
                }
                if (elapsed - sampled > .25f)
                {
                    sampled = elapsed;
                    var colliders = (System.Collections.Generic.HashSet<Collider>)Occupants.GetValue(trigger);
                    report.AppendLine($"t={elapsed:F2} contacts={colliders.Count} revealed={Revealed.GetValue(trigger)} roomVisible={Visible.GetValue(effect)} particles={ps.particleCount} playing={ps.isPlaying} player={actor.transform.position} distance={Vector3.Distance(actor.transform.position,effect.transform.position):F2} renderBounds={ps.GetComponent<ParticleSystemRenderer>().bounds.size} scaling={ps.main.scalingMode}");
                }
                if (elapsed > trigger.EntryDelay + 3.5f)
                    Finish($"RESULT: peakParticles={maxParticles}, revealed={Revealed.GetValue(trigger)}");
            }
            catch (Exception ex) { Finish(ex.ToString()); }
        }

        static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
        static void Capture()
        {
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Exports/Reviews/Sparkle");
            Directory.CreateDirectory(folder);
            var root = effect.GetComponentInParent<PickupItem>().transform;
            var objects = root.GetComponentsInChildren<Transform>(true);
            var layers = objects.Select(t => t.gameObject.layer).ToArray();
            var cameraObject = new GameObject("Sparkle review camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f,.03f,.04f,1); camera.fieldOfView = 65;
            camera.nearClipPlane = .01f; camera.farClipPlane = 20;
            var rt = new RenderTexture(320,180,24); rt.Create(); camera.targetTexture = rt;
            var renderer = effect.GetComponent<ParticleSystemRenderer>();
            var oldRT = RenderTexture.active;
            try
            {
                foreach (var t in objects) t.gameObject.layer = 31;
                camera.transform.position = effect.transform.position + Vector3.forward * 5;
                camera.transform.LookAt(effect.transform.position);
                renderer.enabled = false;
                camera.Render(); RenderTexture.active = rt;
                var off = new Texture2D(320,180,TextureFormat.RGB24,false);
                off.ReadPixels(new Rect(0,0,320,180),0,0); off.Apply();
                renderer.enabled = true;
                camera.Render();
                var on = new Texture2D(320,180,TextureFormat.RGB24,false);
                on.ReadPixels(new Rect(0,0,320,180),0,0); on.Apply();
                var a = off.GetPixels32(); var b = on.GetPixels32();
                int changed = 0, peak = 0;
                for (int i=0;i<a.Length;i++)
                {
                    int delta = Mathf.Max(Mathf.Abs(a[i].r-b[i].r),Mathf.Abs(a[i].g-b[i].g),Mathf.Abs(a[i].b-b[i].b));
                    if(delta>3)changed++; peak=Mathf.Max(peak,delta);
                }
                report.AppendLine($"RENDER at 320x180, camera 5m away: changedPixels={changed} peakChannelDifference={peak}/255 minScreenSize={renderer.minParticleSize}");
                File.WriteAllBytes(Path.Combine(folder,"CigaretteSparkle_5m.png"),on.EncodeToPNG());
                Object.DestroyImmediate(off); Object.DestroyImmediate(on);
            }
            finally
            {
                renderer.enabled = true;
                for(int i=0;i<objects.Length;i++) if(objects[i])objects[i].gameObject.layer=layers[i];
                RenderTexture.active = oldRT; camera.targetTexture=null;
                rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(cameraObject);
            }
        }
        static void Warp(Vector3 position)
        {
            var cc = actor.GetComponent<CharacterController>(); bool enabled = cc && cc.enabled;
            if (enabled) cc.enabled = false;
            actor.PlaceAt(position, savedRotation);
            var body = actor.GetComponent<Rigidbody>();
            if (body) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            if (enabled) cc.enabled = true;
        }
        static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            report.AppendLine(result);
            if (effect) effect.RequireRoomEntry = savedRequireEntry;
            if (actor) { Warp(savedPosition); if (movement) movement.enabled = movementEnabled; }
            actor = null;
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Exports/Reviews/Sparkle");
            Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder,"PlayCheck.txt"),report.ToString());
            Debug.Log("SPARKLE_CHECK: " + result);
        }
    }
}
