using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace HorrorGame.Characters
{
    /// <summary>Animation and movement preview; never added to the gameplay prefab.</summary>
    public sealed class SupermanShowcase : MonoBehaviour
    {
        public Animator animator;
        public AnimationClip[] clips;
        public Transform character;
        public Camera reviewCamera;
        public bool automatic = true;
        public float secondsPerClip = 5f;
        public Vector3 reviewOffset;
        public bool freezeMotion;
        [Range(0, 75)] public float lookDownDegrees;
        public string CurrentClip => clips != null && clips.Length > 0 ? clips[index].name : "None";
        PlayableGraph graph;
        AnimationClipPlayable playable;
        AnimationClipPlayable nextPlayable;
        AnimationMixerPlayable mixer;
        AnimationScriptPlayable lookPlayable;
        bool blending;
        float blendElapsed;
        int index = -1;
        float elapsed;
        Transform chest;
        struct ChestLookJob : IAnimationJob
        {
            public TransformStreamHandle chest;
            public float degrees;
            public Vector3 right;
            public void ProcessAnimation(AnimationStream stream)
            {
                if (chest.IsValid(stream))
                    chest.SetRotation(stream, Quaternion.AngleAxis(degrees, right) * chest.GetRotation(stream));
            }
            public void ProcessRootMotion(AnimationStream stream) { }
        }

        void OnEnable()
        {
            if (animator) animator.fireEvents = false;
            foreach (var bone in character.GetComponentsInChildren<Transform>())
                if (bone.name == "DEF-spine.003") chest = bone;
            if (clips != null && clips.Length > 0) Select(0);
        }

        void Select(int next)
        {
            index = next % clips.Length;
            if (!graph.IsValid())
            {
                graph = PlayableGraph.Create("Superman cloth showcase");
                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                mixer = AnimationMixerPlayable.Create(graph, 2);
                playable = AnimationClipPlayable.Create(graph, clips[index]);
                mixer.ConnectInput(0, playable, 0); mixer.SetInputWeight(0, 1);
                var output = AnimationPlayableOutput.Create(graph, "Character", animator);
                lookPlayable = AnimationScriptPlayable.Create(graph, new ChestLookJob { chest = animator.BindStreamTransform(chest), right = character.right }, 1);
                graph.Connect(mixer, 0, lookPlayable, 0); lookPlayable.SetInputWeight(0, 1);
                output.SetSourcePlayable(lookPlayable); graph.Play();
            }
            else
            {
                if (blending) FinishBlend();
                nextPlayable = AnimationClipPlayable.Create(graph, clips[index]);
                mixer.ConnectInput(1, nextPlayable, 0);
                mixer.SetInputWeight(1, 0); blendElapsed = 0; blending = true;
            }
            elapsed = 0;
        }

        void FinishBlend()
        {
            mixer.DisconnectInput(0); graph.DestroyPlayable(playable);
            mixer.DisconnectInput(1); playable = nextPlayable;
            mixer.ConnectInput(0, playable, 0); mixer.SetInputWeight(0, 1);
            blending = false;
        }

        void Update()
        {
            if (!graph.IsValid()) return;
            var keys = Keyboard.current;
            if (keys != null && keys.spaceKey.wasPressedThisFrame)
            {
                automatic = false;
                Select(index + 1);
            }
            if (keys != null && keys.rKey.wasPressedThisFrame)
                character.GetComponentInChildren<SupermanCape>().ResetSimulation();
            elapsed += Time.deltaTime;
            if (automatic && elapsed > secondsPerClip) Select(index + 1);
            if (blending)
            {
                blendElapsed += Time.deltaTime;
                float weight = Mathf.Clamp01(blendElapsed / .2f);
                mixer.SetInputWeight(0, 1 - weight); mixer.SetInputWeight(1, weight);
                if (weight >= 1) FinishBlend();
            }
            float t = Time.timeSinceLevelLoad;
            character.position = reviewOffset + (freezeMotion ? Vector3.zero : new Vector3(Mathf.Sin(t * .8f) * .6f, 0, Mathf.Sin(t * .55f) * .35f));
            character.rotation = freezeMotion ? Quaternion.identity : Quaternion.Euler(0, Mathf.Sin(t * .7f) * 65, 0);
            var look = lookPlayable.GetJobData<ChestLookJob>();
            look.degrees = lookDownDegrees; look.right = character.right;
            lookPlayable.SetJobData(look);
        }

        void LateUpdate()
        {
            if (reviewCamera)
                reviewCamera.transform.LookAt(character.position + Vector3.up * 1.1f);
        }

        void OnGUI()
        {
            GUI.Label(new Rect(16, 16, 800, 60), "Superman CPU cloth — " + CurrentClip + "\nSpace: next animation   R: reset cape");
        }

        public void HoldClip(int clipIndex)
        {
            automatic = false;
            Select(clipIndex);
        }

        void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
