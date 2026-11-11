using System;
using System.Linq;
using HorrorEngine;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorGame.Chapter1.Editor
{
    public static class DistrictThirdPersonCameraSetup
    {
        const string ScenePath = "Assets/Game/Levels/Chapter1/Scenes/Electricity_Puzzle_Dom.unity";

        [MenuItem("Horror Game/Chapter 1/Use Third Person Camera in Dom Scene")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath)
                throw new InvalidOperationException("Open Electricity_Puzzle_Dom outside Play Mode first.");
            var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var spawn = objects.Select(t => t.GetComponent<PlayerSpawnPoint>()).Where(p => p).OrderByDescending(p => p.IsDefault).FirstOrDefault();
            if (!spawn) throw new InvalidOperationException("The scene needs its native player spawn point.");
            foreach (var fixedView in objects.Select(t => t.GetComponent<CameraPOV>()).Where(p => p))
            {
                Undo.RecordObject(fixedView.gameObject, "Disable fixed camera in Dom scene");
                fixedView.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fixedView.gameObject);
            }
            // HECore creates the native CameraSystem output at runtime. The old scene camera must not render over it.
            foreach (var camera in objects.Select(t => t.GetComponent<Camera>()).Where(c => c && !c.GetComponentInParent<CameraSystem>() && !c.GetComponent<CinemachineBrain>()))
            {
                Undo.RecordObject(camera, "Disable static scene camera"); camera.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
                var listener = camera.GetComponent<AudioListener>();
                if (listener) { Undo.RecordObject(listener, "Disable duplicate audio listener"); listener.enabled = false; }
            }
            var rig = objects.Select(t => t.GetComponent<DistrictThirdPersonCamera>()).FirstOrDefault(c => c);
            if (!rig)
            {
                var go = new GameObject("District_ThirdPersonCamera");
                Undo.RegisterCreatedObjectUndo(go, "Add district third person camera");
                rig = Undo.AddComponent<DistrictThirdPersonCamera>(go);
            }
            Undo.RecordObject(rig, "Configure third person camera");
            if (!rig.FollowPivot)
            {
                var pivot = new GameObject("District_PlayerFollowPivot");
                Undo.RegisterCreatedObjectUndo(pivot, "Add player follow pivot");
                rig.FollowPivot = pivot.transform;
            }
            rig.TargetHeight = 1.35f; rig.DownwardPitch = 12; rig.TurnSmoothTime = .2f;
            rig.FollowPivot.SetPositionAndRotation(spawn.transform.position + Vector3.up * rig.TargetHeight,
                Quaternion.Euler(rig.DownwardPitch, spawn.transform.eulerAngles.y, 0));
            // The pivot stays independent of the virtual camera to avoid a parent/follow feedback loop.
            rig.FollowPivot.SetParent(null, true);
            var cam = rig.GetComponent<CinemachineCamera>();
            Undo.RecordObject(cam, "Configure follow lens");
            cam.Priority = 0; // Native closeup cameras use priority 10 and can still take over.
            cam.Follow = rig.FollowPivot;
            var lens = cam.Lens; lens.FieldOfView = 65; lens.NearClipPlane = .08f; lens.FarClipPlane = 300; cam.Lens = lens;
            var follow = rig.GetComponent<CinemachineThirdPersonFollow>();
            Undo.RecordObject(follow, "Configure third person follow");
            follow.Damping = new Vector3(.12f, .2f, .18f);
            follow.ShoulderOffset = new Vector3(.3f, 0, 0);
            follow.VerticalArmLength = .15f; follow.CameraSide = 1; follow.CameraDistance = 3.2f;
            follow.AvoidObstacles = new CinemachineThirdPersonFollow.ObstacleSettings
            {
                Enabled = true,
                CollisionFilter = ~LayerMask.GetMask("Player", "OnlyWithPlayer", "InteractionSystem", "Combat", "MapSystem", "Ignore Raycast", "UI"),
                IgnoreTag = "Player", CameraRadius = .18f, DampingIntoCollision = 0, DampingFromCollision = .35f
            };
            EditorUtility.SetDirty(rig); EditorUtility.SetDirty(cam); EditorUtility.SetDirty(follow);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save Dom scene.");
            Debug.Log("DOM_THIRD_PERSON_READY: one follow rig, registered-player binding, wall collision enabled, old fixed/static views disabled.");
        }
    }
}
