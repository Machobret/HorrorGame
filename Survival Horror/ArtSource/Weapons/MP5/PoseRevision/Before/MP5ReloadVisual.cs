using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Weapons
{
    // HK's shoulder-supported sequence: lock handle back, magazine out/in,
    // seating tug, palm release. This component never changes inventory ammo.
    [DefaultExecutionOrder(275), DisallowMultipleComponent]
    public sealed class MP5ReloadVisual : MonoBehaviour, IWeaponReloadPresentation
    {
        public Transform ModelFrame;
        public Transform Magazine;
        public Transform ChargingHandle;
        public Vector3 ChargingHandlePosition = new Vector3(.0127f, .1187f, .2286f);
        public float ChargingHandleTravel = .085f;
        public Vector3 ReloadHandPosition = new Vector3(.14f, 1.38f, .26f);
        public Vector3 PocketPosition = new Vector3(-.23f, 1.02f, .12f);
        public Vector3 RightElbowHint = new Vector3(.28f, 1.08f, .04f);
        public Vector3 LeftElbowHint = new Vector3(-.31f, 1.10f, .10f);
        public bool IsReloading { get; private set; }
        public bool MagazineSeated { get; private set; } = true;
        public bool HandleLocked { get; private set; }
        public float ReloadProgress => IsReloading ? Mathf.Clamp01((Time.time - started) / duration) : 0;

        private Transform character, rightShoulder, rightUpper, rightLower, rightHand;
        private Transform leftShoulder, leftUpper, leftLower, leftHand;
        private Transform attachmentParent;
        private Vector3 attachmentPosition, gripPosition, magazineRest, magazineCenter, handleRest;
        private Quaternion attachmentRotation, gripRotation, workRotation, magazineRotation, handleRotation;
        private float started, duration;
        private bool initialized, displaced, startedAimed, magazineWasActive;
        private MP5HandlingPose support;
        private MeshFilter splitBody;
        private Mesh originalBody, fixedBody, movingHandle;

        private void Awake() { Prepare(); }
        private void OnEnable() { Prepare(); BindCharacter(); }

        private void Prepare()
        {
            if (initialized) return;
            var weapon = GetComponent<Weapon>();
            if (!ModelFrame && weapon && weapon.Visuals) ModelFrame = weapon.Visuals.transform;
            if (!ModelFrame) return;
            if (!Magazine)
                foreach (var child in ModelFrame.GetComponentsInChildren<Transform>(true))
                    if (child.name == "MP5Magazene") { Magazine = child; break; }
            if (!Magazine) return;
            magazineRest = ModelFrame.InverseTransformPoint(Magazine.position);
            magazineRotation = Quaternion.Inverse(ModelFrame.rotation) * Magazine.rotation;
            var magazineRenderer = Magazine.GetComponentInChildren<Renderer>();
            magazineCenter = magazineRenderer ? ModelFrame.InverseTransformPoint(magazineRenderer.bounds.center) : magazineRest;
            if (!ChargingHandle)
            {
                ChargingHandle = new GameObject("MP5ReloadChargingHandle").transform;
                ChargingHandle.SetParent(ModelFrame, false);
                ChargingHandle.localPosition = ChargingHandlePosition;
                if (!ExtractHandle())
                {
                    var lever = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    lever.name = "ChargingHandleLever";
                    lever.transform.SetParent(ChargingHandle, false);
                    lever.transform.localPosition = new Vector3(-.008f, 0, 0);
                    lever.transform.localScale = new Vector3(.022f, .012f, .020f);
                    var collider = lever.GetComponent<Collider>();
                    if (collider) Destroy(collider);
                    var bodyRenderer = ModelFrame.GetComponentInChildren<Renderer>();
                    if (bodyRenderer) lever.GetComponent<Renderer>().sharedMaterial = bodyRenderer.sharedMaterial;
                }
            }
            handleRest = ChargingHandle.localPosition;
            handleRotation = ChargingHandle.localRotation;
            initialized = true;
        }

        private void BindCharacter()
        {
            var actor = GetComponentInParent<Actor>();
            var animator = actor ? actor.MainAnimator : null;
            if (!actor || !animator || !animator.isHuman) return;
            character = actor.transform;
            rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            support = GetComponent<MP5HandlingPose>();
        }

        public void BeginReload(float reloadDuration, float loaded, int roundsToLoad)
        {
            Prepare(); BindCharacter(); RestoreAttachment();
            if (support) support.RestoreAttachment();
            if (!initialized || !rightHand || !leftHand) return;
            attachmentParent = transform.parent;
            attachmentPosition = transform.localPosition;
            attachmentRotation = transform.localRotation;
            gripPosition = transform.InverseTransformPoint(rightHand.position);
            gripRotation = Quaternion.Inverse(transform.rotation) * rightHand.rotation;
            var reloadState = GetComponentInParent<Actor>().GetComponentInChildren<PlayerStateReload>(true);
            startedAimed = reloadState && reloadState.StartedAimed;
            var direction = character.TransformDirection(new Vector3(0, startedAimed ? -.06f : -.24f, 1).normalized);
            workRotation = Quaternion.Inverse(character.rotation) *
                (Quaternion.FromToRotation(ModelFrame.forward, direction) * transform.rotation);
            magazineWasActive = Magazine.gameObject.activeSelf;
            Magazine.gameObject.SetActive(true);
            duration = Mathf.Max(.1f, reloadDuration);
            started = Time.time;
            IsReloading = true; MagazineSeated = true; HandleLocked = false;
        }

        // The equipment socket remains the owner outside this frame's pose solve.
        private void Update() { RestoreAttachment(); }

        private void LateUpdate()
        {
            if (!IsReloading || !initialized || !character) return;
            float t = ReloadProgress;
            float handling = Smooth(t, 0, .10f) * (1 - Smooth(t, .84f, .995f));
            Quaternion rotation = character.rotation * workRotation;
            Vector3 firingWrist = character.TransformPoint(ReloadHandPosition +
                (startedAimed ? Vector3.zero : new Vector3(0, -.065f, -.02f)));
            ReloadArmIK.Solve(rightUpper, rightLower, rightHand, character, firingWrist,
                rotation * gripRotation, RightElbowHint, handling, rightShoulder, .35f);
            transform.rotation = rightHand.rotation * Quaternion.Inverse(gripRotation);
            transform.position = rightHand.position - transform.TransformVector(gripPosition);
            displaced = true;

            float rack = Smooth(t, .14f, .225f) * (1 - Smooth(t, .785f, .81f));
            ChargingHandle.localPosition = handleRest + ModelFrame.InverseTransformVector(ModelFrame.forward * (-ChargingHandleTravel * rack));
            ChargingHandle.localRotation = handleRotation * Quaternion.Euler(0, 0, -28 * Smooth(t, .215f, .245f) * (1 - Smooth(t, .78f, .80f)));
            HandleLocked = t >= .245f && t < .80f;

            // The imported magazine may share the model's origin; translate its
            // vertices by a measured offset instead of assuming a magazine pivot.
            Vector3 pocket = ModelFrame.InverseTransformPoint(character.TransformPoint(PocketPosition));
            Vector3 extracted = magazineCenter + Vector3.down * .115f;
            Vector3 magazinePoint;
            if (t < .40f) magazinePoint = Vector3.Lerp(magazineCenter, extracted, Smooth(t, .31f, .40f));
            else if (t < .47f) magazinePoint = Vector3.Lerp(extracted, pocket, Smooth(t, .40f, .47f));
            else if (t < .58f) magazinePoint = Vector3.Lerp(pocket, extracted, Smooth(t, .49f, .58f));
            else magazinePoint = Vector3.Lerp(extracted, magazineCenter, Smooth(t, .58f, .675f));
            // Small downward seating check after insertion, not a second removal.
            magazinePoint += Vector3.down * (.009f * Smooth(t, .68f, .695f) * (1 - Smooth(t, .695f, .715f)));
            Magazine.position = ModelFrame.TransformPoint(magazineRest + magazinePoint - magazineCenter);
            Magazine.rotation = ModelFrame.rotation * magazineRotation;
            Magazine.gameObject.SetActive(t < .465f || t >= .49f);
            MagazineSeated = t < .31f || t >= .675f;

            Vector3 handguard = support ? support.ForeEndWrist : new Vector3(-.04f, .035f, .235f);
            Vector3 handleWrist = ModelFrame.InverseTransformPoint(ChargingHandle.position) + new Vector3(-.022f, -.012f, -.035f);
            Vector3 magWrist = magazinePoint + new Vector3(-.035f, -.018f, -.025f);
            Vector3 supportWrist = handguard;
            if (t < .245f) supportWrist = Vector3.Lerp(handguard, handleWrist, Smooth(t, .025f, .13f));
            else if (t < .31f) supportWrist = Vector3.Lerp(handleWrist, magWrist, Smooth(t, .245f, .31f));
            else if (t < .715f) supportWrist = magWrist;
            else if (t < .78f) supportWrist = Vector3.Lerp(magWrist, handleWrist + Vector3.up * .025f, Smooth(t, .715f, .77f));
            else if (t < .84f) supportWrist = Vector3.Lerp(handleWrist + Vector3.up * .025f,
                handleWrist + new Vector3(-.025f, -.025f, .015f), Smooth(t, .78f, .815f));
            else supportWrist = Vector3.Lerp(handleWrist + new Vector3(-.025f, -.025f, .015f), handguard, Smooth(t, .84f, .92f));
            float onMagazine = Smooth(t, .245f, .31f) * (1 - Smooth(t, .715f, .77f));
            Quaternion handguardRotation = support ? support.WristRotation : Quaternion.Euler(0, 0, -85);
            Quaternion palm = ModelFrame.rotation * Quaternion.Slerp(handguardRotation,
                Quaternion.Euler(5, -15, -70), onMagazine);
            float supportWeight = Smooth(t, 0, .09f) * (1 - Smooth(t, .94f, .999f));
            ReloadArmIK.Solve(leftUpper, leftLower, leftHand, character, ModelFrame.TransformPoint(supportWrist),
                palm, LeftElbowHint, supportWeight, leftShoulder, .65f);
        }

        public void EndReload(bool completed)
        {
            RestoreAttachment();
            if (initialized)
            {
                Magazine.position = ModelFrame.TransformPoint(magazineRest);
                Magazine.rotation = ModelFrame.rotation * magazineRotation;
                Magazine.gameObject.SetActive(magazineWasActive || !IsReloading);
                ChargingHandle.localPosition = handleRest;
                ChargingHandle.localRotation = handleRotation;
            }
            IsReloading = false; MagazineSeated = true; HandleLocked = false;
        }

        private void RestoreAttachment()
        {
            if (!displaced) return;
            if (transform.parent == attachmentParent)
            {
                transform.localPosition = attachmentPosition;
                transform.localRotation = attachmentRotation;
            }
            displaced = false;
        }

        private void OnDisable() { EndReload(false); }

        private bool ExtractHandle()
        {
            foreach (var filter in ModelFrame.GetComponentsInChildren<MeshFilter>(true))
                if (filter.name == "MP5BodyFull") { splitBody = filter; break; }
            if (!splitBody || !splitBody.sharedMesh || !splitBody.sharedMesh.isReadable) return false;
            originalBody = splitBody.sharedMesh;
            var source = originalBody.vertices;
            var handleVertices = new Vector3[source.Length];
            var isHandle = new bool[source.Length];
            for (int i = 0; i < source.Length; ++i)
            {
                Vector3 p = ModelFrame.InverseTransformPoint(splitBody.transform.TransformPoint(source[i]));
                // The normalized source has eight isolated handle vertices here.
                isHandle[i] = p.x >= .001f && p.x <= .024f && p.y >= .108f && p.y <= .130f && p.z >= .216f && p.z <= .241f;
                handleVertices[i] = p - ChargingHandle.localPosition;
            }
            var staticTriangles = new System.Collections.Generic.List<int>[originalBody.subMeshCount];
            var handleTriangles = new System.Collections.Generic.List<int>[originalBody.subMeshCount];
            int extracted = 0;
            for (int sub = 0; sub < originalBody.subMeshCount; ++sub)
            {
                staticTriangles[sub] = new System.Collections.Generic.List<int>();
                handleTriangles[sub] = new System.Collections.Generic.List<int>();
                int[] triangles = originalBody.GetTriangles(sub);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    bool moving = isHandle[triangles[i]] && isHandle[triangles[i + 1]] && isHandle[triangles[i + 2]];
                    var destination = moving ? handleTriangles[sub] : staticTriangles[sub];
                    destination.Add(triangles[i]); destination.Add(triangles[i + 1]); destination.Add(triangles[i + 2]);
                    if (moving) ++extracted;
                }
            }
            if (extracted == 0) { splitBody = null; originalBody = null; return false; }
            fixedBody = Instantiate(originalBody); fixedBody.name = "MP5BodyWithoutMovingHandle";
            var compactVertices = new System.Collections.Generic.List<Vector3>();
            var compactUV = new System.Collections.Generic.List<Vector2>();
            var compactColors = new System.Collections.Generic.List<Color>();
            var remap = new System.Collections.Generic.Dictionary<int, int>();
            var sourceUV = originalBody.uv;
            var sourceColors = originalBody.colors;
            for (int sub = 0; sub < handleTriangles.Length; ++sub)
                for (int i = 0; i < handleTriangles[sub].Count; ++i)
                {
                    int sourceIndex = handleTriangles[sub][i];
                    if (!remap.TryGetValue(sourceIndex, out int compactIndex))
                    {
                        compactIndex = compactVertices.Count; remap.Add(sourceIndex, compactIndex);
                        compactVertices.Add(handleVertices[sourceIndex]);
                        compactUV.Add(sourceUV.Length == source.Length ? sourceUV[sourceIndex] : Vector2.zero);
                        if (sourceColors.Length == source.Length) compactColors.Add(sourceColors[sourceIndex]);
                    }
                    handleTriangles[sub][i] = compactIndex;
                }
            movingHandle = new Mesh { name = "MP5OriginalChargingHandle", subMeshCount = originalBody.subMeshCount };
            movingHandle.SetVertices(compactVertices); movingHandle.SetUVs(0, compactUV);
            if (compactColors.Count > 0) movingHandle.SetColors(compactColors);
            for (int sub = 0; sub < originalBody.subMeshCount; ++sub)
            {
                fixedBody.SetTriangles(staticTriangles[sub], sub);
                movingHandle.SetTriangles(handleTriangles[sub], sub);
            }
            fixedBody.RecalculateBounds(); movingHandle.RecalculateBounds(); movingHandle.RecalculateNormals();
            splitBody.sharedMesh = fixedBody;
            var lever = new GameObject("OriginalChargingHandleMesh");
            lever.transform.SetParent(ChargingHandle, false);
            lever.AddComponent<MeshFilter>().sharedMesh = movingHandle;
            lever.AddComponent<MeshRenderer>().sharedMaterials = splitBody.GetComponent<MeshRenderer>().sharedMaterials;
            return true;
        }

        private void OnDestroy()
        {
            if (splitBody && originalBody) splitBody.sharedMesh = originalBody;
            if (fixedBody) Destroy(fixedBody);
            if (movingHandle) Destroy(movingHandle);
        }
        private static float Smooth(float t, float a, float b) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));
    }
}
