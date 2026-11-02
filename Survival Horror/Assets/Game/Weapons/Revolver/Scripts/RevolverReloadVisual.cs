using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Weapons
{
    // Colt Python inspired swing-out-cylinder presentation. Ammo remains owned by
    // PlayerStateReload; these meshes only show the transaction it is committing.
    [DefaultExecutionOrder(275)]
    public sealed class RevolverReloadVisual : MonoBehaviour, IWeaponReloadPresentation
    {
        public Transform Cylinder;
        public Material BrassMaterial;
        public float SwingAngle = 82;
        public Vector3 ReloadHandPosition = new Vector3(.13f, 1.22f, .22f);
        public Vector3 RightElbowHint = new Vector3(.26f, .94f, .025f);
        public Vector3 LeftElbowHint = new Vector3(-.26f, .94f, .025f);
        public bool IsReloading { get; private set; }
        public float ReloadProgress { get; private set; }
        public float OpenAmount { get; private set; }
        public int LoadedRoundCount { get; private set; }
        public int EjectedCaseCount { get; private set; }

        private Transform frame, character, rightUpper, rightLower, rightHand, leftUpper, leftLower, leftHand;
        private Vector3 cylinderRest, center, pivot;
        private Quaternion cylinderRotation;
        private Transform roundsRoot, loader;
        private readonly Transform[] rounds = new Transform[6];
        private float started, duration;
        private int retained, incoming, spent;
        private bool ejected, initialized;
        private static Mesh roundMesh, spentMesh;
        private Quaternion readyWrist;
        private bool startedAimed;
        private Material ownedBrassMaterial;

        private void Awake() { Initialize(); }
        private void Initialize()
        {
            if (initialized) return;
            var weapon = GetComponent<Weapon>();
            if (!weapon || !weapon.Visuals) return;
            frame = weapon.Visuals.transform;
            if (!Cylinder)
                foreach (var child in GetComponentsInChildren<Transform>(true))
                    if (child.name == "CPCyilnder") { Cylinder = child; break; }
            if (!Cylinder) return;
            // A rebuilt prefab may not have its optional brass asset assigned.
            // Reuse the model's shader so its pipeline variant is already included.
            if (!BrassMaterial)
            {
                var renderer = Cylinder.GetComponent<Renderer>();
                var shader = renderer && renderer.sharedMaterial ? renderer.sharedMaterial.shader
                    : Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) shader = Shader.Find("Standard");
                if (shader)
                {
                    ownedBrassMaterial = new Material(shader) { name = "RevolverReloadBrass_Runtime" };
                    var brass = new Color(.62f, .43f, .16f, 1);
                    if (ownedBrassMaterial.HasProperty("_BaseColor")) ownedBrassMaterial.SetColor("_BaseColor", brass);
                    if (ownedBrassMaterial.HasProperty("_Color")) ownedBrassMaterial.SetColor("_Color", brass);
                    BrassMaterial = ownedBrassMaterial;
                }
            }
            cylinderRest = Cylinder.localPosition;
            cylinderRotation = Cylinder.localRotation;
            var filter = Cylinder.GetComponent<MeshFilter>();
            var bounds = new Bounds(frame.InverseTransformPoint(Cylinder.position), Vector3.zero);
            if (filter && filter.sharedMesh)
            {
                bool first = true;
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = frame.InverseTransformPoint(Cylinder.TransformPoint(vertex));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            center = bounds.center;
            // Crane hinge lies below the cylinder and runs along the barrel.
            pivot = new Vector3(center.x, bounds.min.y - .007f, center.z + .011f);
            roundsRoot = new GameObject("RevolverReloadRounds").transform;
            roundsRoot.SetParent(frame, false);
            if (!roundMesh) roundMesh = MakeCartridge(false);
            if (!spentMesh) spentMesh = MakeCartridge(true);
            for (int i = 0; i < rounds.Length; ++i)
            {
                var go = new GameObject("ReloadRound_" + i);
                rounds[i] = go.transform;
                go.transform.SetParent(roundsRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = roundMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = BrassMaterial;
            }
            // A short loader ring stays behind the cartridges, never inside the cylinder.
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "RevolverReloadLoader";
            Destroy(ring.GetComponent<Collider>());
            loader = ring.transform;
            loader.SetParent(roundsRoot, false);
            loader.localRotation = Quaternion.Euler(90, 0, 0);
            loader.localScale = new Vector3(.032f, .003f, .032f);
            ring.GetComponent<MeshRenderer>().sharedMaterial = BrassMaterial;
            roundsRoot.gameObject.SetActive(false);
            initialized = true;
        }
        public void BeginReload(float reloadDuration, float loaded, int roundsToLoad)
        {
            Initialize();
            if (!initialized) return;
            EndReload(false);
            var animator = GetComponentInParent<Animator>();
            var actor = GetComponentInParent<Actor>();
            if (!animator || !animator.isHuman || !actor) return;
            character = actor.transform;
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            readyWrist = Quaternion.Inverse(character.rotation) * rightHand.rotation;
            var reload = actor.GetComponentInChildren<PlayerStateReload>(true);
            startedAimed = reload && reload.StartedAimed;
            if (!startedAimed)
            {
                var forward = character.TransformDirection(new Vector3(0, -.2f, 1).normalized);
                readyWrist = Quaternion.Inverse(character.rotation) *
                    (Quaternion.FromToRotation(frame.forward, forward) * rightHand.rotation);
            }
            retained = Mathf.Clamp(Mathf.CeilToInt(loaded), 0, 6);
            incoming = Mathf.Clamp(roundsToLoad, 0, 6 - retained);
            spent = 6 - retained;
            duration = Mathf.Max(.1f, reloadDuration);
            started = Time.time;
            ReloadProgress = OpenAmount = 0;
            LoadedRoundCount = EjectedCaseCount = 0;
            ejected = false;
            IsReloading = true;
            roundsRoot.gameObject.SetActive(true);
            loader.gameObject.SetActive(retained == 0 && incoming > 1);
            for (int i = 0; i < rounds.Length; ++i) rounds[i].gameObject.SetActive(false);
        }
        private void LateUpdate()
        {
            if (!IsReloading || !Cylinder || !character) return;
            float t = ReloadProgress = Mathf.Clamp01((Time.time - started) / duration);
            float handling = Smooth(t, 0, .12f) * (1 - Smooth(t, .82f, .99f));
            // Right hand retains the grip and brings the mechanism down to chest level.
            var target = character.TransformPoint(ReloadHandPosition + (startedAimed ? Vector3.zero : new Vector3(0, -.055f, -.02f)));
            ReloadArmIK.Solve(rightUpper, rightLower, rightHand, character, target,
                character.rotation * Quaternion.Euler(18, 0, -18) * readyWrist, RightElbowHint, handling);

            OpenAmount = Smooth(t, .10f, .25f) * (1 - Smooth(t, .755f, .865f));
            Quaternion turn = Quaternion.AngleAxis(SwingAngle * OpenAmount, frame.forward);
            var axis = frame.TransformPoint(pivot);
            var restPosition = Cylinder.parent.TransformPoint(cylinderRest);
            var restRotation = Cylinder.parent.rotation * cylinderRotation;
            Cylinder.SetPositionAndRotation(axis + turn * (restPosition - axis), turn * restRotation);
            Vector3 openCenter = axis + turn * (frame.TransformPoint(center) - axis);
            Vector3 rear = openCenter - frame.forward * .026f;
            if (!ejected && t >= .44f)
            {
                ejected = true;
                for (int i = 0; i < spent; ++i) EjectCase(rear, i);
                EjectedCaseCount = spent;
            }

            // At the mechanism -> ejector -> belt -> rear cylinder -> close.
            Vector3 support = openCenter - frame.right * .035f;
            if (t >= .25f && t < .42f)
                support = Vector3.Lerp(support, openCenter + frame.forward * .06f, Smooth(t, .25f, .31f));
            else if (t >= .42f && t < .56f)
                support = Vector3.Lerp(openCenter + frame.forward * .06f,
                    character.TransformPoint(new Vector3(-.18f, 1.00f, .08f)), Smooth(t, .42f, .52f));
            else if (t >= .56f && t < .74f)
                support = Vector3.Lerp(character.TransformPoint(new Vector3(-.18f, 1.00f, .08f)),
                    rear - frame.forward * .04f - frame.right * .025f, Smooth(t, .56f, .65f));
            else if (t >= .74f) support = Vector3.Lerp(rear - frame.forward * .04f - frame.right * .025f,
                openCenter - frame.right * .038f, Smooth(t, .74f, .82f));
            // Withdraw to the support side before the grip descends. The final
            // blend then follows the current animator's idle/walk pose, rather
            // than crossing the firing arm on the way back from the cylinder.
            support = Vector3.Lerp(support,
                character.TransformPoint(new Vector3(-.18f, 1.06f, .17f)), Smooth(t, .78f, .92f));
            float leftWeight = Smooth(t, 0, .12f) * (1 - Smooth(t, .84f, .99f));
            ReloadArmIK.Solve(leftUpper, leftLower, leftHand, character, support,
                rightHand.rotation * Quaternion.Euler(0, 20, -20), LeftElbowHint, leftWeight);

            float insertion = Smooth(t, .62f, .74f);
            LoadedRoundCount = t >= .74f ? incoming : 0;
            roundsRoot.SetPositionAndRotation(rear - frame.forward * Mathf.Lerp(.080f, -.019f, insertion), frame.rotation);
            loader.localPosition = new Vector3(0, 0, -.026f);
            loader.gameObject.SetActive(retained == 0 && incoming > 1 && t >= .56f && t < .77f);
            for (int i = 0; i < rounds.Length; ++i)
            {
                bool visible = i < incoming && t >= .56f && t < .77f;
                rounds[i].gameObject.SetActive(visible);
                float angle = (retained + i) * Mathf.PI / 3;
                rounds[i].localPosition = new Vector3(Mathf.Cos(angle) * .0125f, Mathf.Sin(angle) * .0125f, 0);
                rounds[i].localRotation = Quaternion.identity;
            }
        }
        public void EndReload(bool completed)
        {
            IsReloading = false;
            OpenAmount = 0;
            if (Cylinder)
            {
                Cylinder.localPosition = cylinderRest;
                Cylinder.localRotation = cylinderRotation;
            }
            if (roundsRoot) roundsRoot.gameObject.SetActive(false);
        }
        private void OnDisable() { EndReload(false); }
        private void OnDestroy()
        {
            if (roundsRoot) Destroy(roundsRoot.gameObject);
            // Ejected cases live for six seconds independently of the weapon.
            if (ownedBrassMaterial) Destroy(ownedBrassMaterial, 6.1f);
        }
        private static float Smooth(float t, float a, float b) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));
        private void EjectCase(Vector3 rear, int index)
        {
            var go = new GameObject("Revolver_SpentCase");
            float angle = index * Mathf.PI / 3;
            go.transform.SetPositionAndRotation(rear + frame.right * (Mathf.Cos(angle) * .0125f)
                + frame.up * (Mathf.Sin(angle) * .0125f), frame.rotation);
            go.AddComponent<MeshFilter>().sharedMesh = spentMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = BrassMaterial;
            var collider = go.AddComponent<CapsuleCollider>();
            collider.direction = 2; collider.radius = .0046f; collider.height = .033f;
            var body = go.AddComponent<Rigidbody>();
            body.mass = .006f;
            body.linearVelocity = -frame.forward * .8f - frame.right * (.15f + index * .035f);
            body.angularVelocity = frame.right * (7 + index) + frame.up * 5;
            var actor = GetComponentInParent<Actor>();
            if (actor)
                foreach (var owner in actor.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(collider, owner);
            Destroy(go, 6);
        }
        private static Mesh MakeCartridge(bool empty)
        {
            const int sides = 8;
            float[] z = empty ? new[] { -.0165f, -.015f, .0165f, .0165f, .010f }
                : new[] { -.019f, -.0175f, .014f, .020f, .022f };
            float[] r = empty ? new[] { .0052f, .0046f, .0046f, .0037f, .0037f }
                : new[] { .0052f, .0046f, .0046f, .0038f, .002f };
            var vertices = new Vector3[sides * z.Length + 2];
            var triangles = new int[((z.Length - 1) * sides * 2 + sides * 2) * 3];
            int k = 0;
            for (int ring = 0; ring < z.Length; ++ring)
                for (int side = 0; side < sides; ++side)
                {
                    float a = side * Mathf.PI * 2 / sides;
                    vertices[ring * sides + side] = new Vector3(Mathf.Cos(a) * r[ring], Mathf.Sin(a) * r[ring], z[ring]);
                    if (ring == 0) continue;
                    int c = ring * sides + side, d = ring * sides + (side + 1) % sides, a0 = c - sides, b0 = d - sides;
                    triangles[k++] = a0; triangles[k++] = b0; triangles[k++] = c;
                    triangles[k++] = b0; triangles[k++] = d; triangles[k++] = c;
                }
            int back = vertices.Length - 2, front = vertices.Length - 1;
            vertices[back] = new Vector3(0, 0, z[0]); vertices[front] = new Vector3(0, 0, z[z.Length - 1]);
            for (int side = 0; side < sides; ++side)
            {
                triangles[k++] = back; triangles[k++] = (side + 1) % sides; triangles[k++] = side;
                triangles[k++] = front; triangles[k++] = (z.Length - 1) * sides + side;
                triangles[k++] = (z.Length - 1) * sides + (side + 1) % sides;
            }
            var mesh = new Mesh { name = empty ? "PSX_RevolverSpentCase" : "PSX_RevolverCartridge" };
            mesh.vertices = vertices; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}


