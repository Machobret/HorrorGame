using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Weapons
{
    // Break-action signal launcher: open the chamber, withdraw the empty case,
    // bring a single red cartridge to the breech, seat it and close the barrel.
    // The reference is Orion's break-barrel launcher, not a pistol magazine reload.
    [DefaultExecutionOrder(275)]
    public sealed class FlareGunReloadVisual : MonoBehaviour, IWeaponReloadPresentation
    {
        public float BreakAngle = 55;
        public Vector3 ReloadHandPosition = new Vector3(.13f, 1.24f, .24f);
        public Vector3 RightElbowHint = new Vector3(.26f, .96f, .025f);
        public Vector3 LeftElbowHint = new Vector3(-.26f, .96f, .025f);
        public bool IsReloading { get; private set; }
        public float OpenAmount { get; private set; }
        public bool HasInsertedRound { get; private set; }
        public float NormalizedTime => IsReloading ? Mathf.Clamp01((Time.time - started) / duration) : 0;

        private Transform model, hinge, fresh, spent, character, rightHand, leftHand;
        private Transform rightUpper, rightLower, leftUpper, leftLower, attachmentParent;
        private MeshFilter body;
        private Mesh originalBody, fixedBody, movingBody, cartridgeMesh;
        private Material redMaterial, brassMaterial;
        private Transform barrel, originalBarrelParent;
        private Vector3 originalBarrelPosition, attachmentPosition, gripPosition, startWrist;
        private Quaternion originalBarrelRotation, attachmentRotation, gripRotation, startRotation, workRotation;
        private bool startedAimed;
        private float started, duration;
        private bool displaced, prepared;

        private void Awake() { PrepareModel(); }

        private void OnEnable()
        {
            var animator = GetComponentInParent<Animator>();
            var actor = GetComponentInParent<Actor>();
            if (!animator || !animator.isHuman || !actor) return;
            character = actor.transform;
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        }

        public void BeginReload(float reloadDuration, float loaded, int roundsToLoad)
        {
            PrepareModel();
            if (!prepared) return;
            RestoreAttachment();
            attachmentParent = transform.parent;
            attachmentPosition = transform.localPosition;
            attachmentRotation = transform.localRotation;
            duration = Mathf.Max(.1f, reloadDuration);
            started = Time.time;
            HasInsertedRound = false;
            IsReloading = true;
            if (character && rightHand)
            {
                startWrist = character.InverseTransformPoint(rightHand.position);
                startRotation = Quaternion.Inverse(character.rotation) * transform.rotation;
                var reload = GetComponentInParent<Actor>().GetComponentInChildren<PlayerStateReload>(true);
                startedAimed = reload && reload.StartedAimed;
                workRotation = startRotation;
                if (!startedAimed)
                {
                    // A relaxed reload works at the waist, with the breech clear
                    // of the body; do not break an already hanging barrel inward.
                    var forward = character.TransformDirection(new Vector3(0, -.18f, 1).normalized);
                    workRotation = Quaternion.Inverse(character.rotation) *
                        (Quaternion.FromToRotation(model.forward, forward) * transform.rotation);
                }
                gripPosition = transform.InverseTransformPoint(rightHand.position);
                gripRotation = Quaternion.Inverse(transform.rotation) * rightHand.rotation;
            }
        }

        public void EndReload(bool completed)
        {
            RestoreAttachment();
            IsReloading = false;
            OpenAmount = 0;
            HasInsertedRound = completed;
            if (hinge) hinge.localRotation = Quaternion.identity;
            if (fresh) fresh.gameObject.SetActive(false);
            if (spent) spent.gameObject.SetActive(false);
        }

        private void Update() { RestoreAttachment(); }

        private void LateUpdate()
        {
            if (!IsReloading || !prepared) return;
            float t = NormalizedTime;
            float handling = Smooth(t, 0, .12f) * (1 - Smooth(t, .82f, .99f));
            // Keep the firing grip intact while lowering the action into view.
            if (character && rightHand)
            {
                Quaternion reloadRotation = character.rotation * workRotation;
                Vector3 axis = reloadRotation * Quaternion.Inverse(transform.rotation) * model.right;
                reloadRotation = Quaternion.AngleAxis(18, axis) * reloadRotation;
                Vector3 wristTarget = character.TransformPoint(ReloadHandPosition +
                    (startedAimed ? Vector3.zero : new Vector3(0, -.055f, -.02f)));
                ReloadArmIK.Solve(rightUpper, rightLower, rightHand, character, wristTarget,
                    reloadRotation * gripRotation, RightElbowHint, handling);
                // Keep the authored grip attached to the actually blended hand,
                // rather than pinning the weapon to an independently blended target.
                transform.rotation = rightHand.rotation * Quaternion.Inverse(gripRotation);
                transform.position = rightHand.position - transform.TransformVector(gripPosition);
                displaced = true;
            }

            OpenAmount = Smooth(t, .10f, .25f) * (1 - Smooth(t, .76f, .89f));
            hinge.localRotation = Quaternion.Euler(BreakAngle * OpenAmount, 0, 0);
            Vector3 breech = new Vector3(0, .102f, .073f) - hinge.localPosition;
            float extraction = Smooth(t, .28f, .39f);
            spent.gameObject.SetActive(t >= .25f && t < .46f);
            spent.localPosition = breech + Vector3.back * (.065f * extraction);
            spent.localRotation = Quaternion.identity;
            float seat = Smooth(t, .58f, .73f);
            fresh.gameObject.SetActive(t >= .48f && t < .79f);
            fresh.localPosition = breech + new Vector3(-.045f * (1 - seat), .018f * (1 - seat), -.09f * (1 - seat));
            fresh.localRotation = Quaternion.identity;
            HasInsertedRound = t >= .73f;

            if (leftHand && character)
            {
                float reaching = Smooth(t, .01f, .13f) * (1 - Smooth(t, .84f, .99f));
                Vector3 target;
                // These are wrist targets, not finger contact points. Keep the
                // wrist behind the case so the character's long mitten fingers
                // reach it without the palm covering the open breech.
                Vector3 caseWrist = hinge.TransformVector(new Vector3(-.025f, -.012f, -.085f));
                if (t < .27f) target = hinge.TransformPoint(new Vector3(-.024f, .045f, .005f));
                else if (t < .46f) target = spent.position + caseWrist;
                else if (t < .53f)
                {
                    Vector3 nearPocket = character.TransformPoint(new Vector3(-.18f, .91f, .19f));
                    target = Vector3.Lerp(nearPocket, fresh.position + caseWrist, Smooth(t, .46f, .53f));
                }
                else target = Vector3.Lerp(fresh.position + caseWrist,
                    hinge.TransformPoint(new Vector3(-.025f, .045f, .02f)), Smooth(t, .73f, .81f));
                // Clear the firing arm on the left before lowering the weapon.
                // Fading directly from a hand at the breech to the idle hand while
                // the grip lowers made both forearms pass through each other.
                Vector3 retreat = character.TransformPoint(new Vector3(-.18f, 1.08f, .19f));
                target = Vector3.Lerp(target, retreat, Smooth(t, .78f, .92f));
                // Match the curled equipped support grip, with only a slight
                // inward wrist angle; the earlier 55-degree turn made a flat tray.
                Quaternion palm = rightHand ? rightHand.rotation * Quaternion.Euler(0, -12, 10) : leftHand.rotation;
                ReloadArmIK.Solve(leftUpper, leftLower, leftHand, character, target, palm, LeftElbowHint, reaching);
            }
        }

        private void OnDisable()
        {
            EndReload(false);
            character = rightHand = leftHand = rightUpper = rightLower = leftUpper = leftLower = null;
        }

        private void PrepareModel()
        {
            if (prepared) return;
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.name == "FlareGunBody") body = filter;
                if (filter.name == "FlareGunBarrel") barrel = filter.transform;
            }
            if (!body || !body.sharedMesh || !barrel) return;
            model = body.transform.parent;
            originalBody = body.sharedMesh;
            hinge = new GameObject("FlareReloadHinge").transform;
            hinge.SetParent(model, false);
            // Measured from this FBX: the front housing meets the fixed trigger
            // frame at y=.0403, z=.0548. Forward is +Z in its imported model frame.
            hinge.localPosition = new Vector3(0, .0403f, .0548f);
            originalBarrelParent = barrel.parent;
            originalBarrelPosition = barrel.localPosition;
            originalBarrelRotation = barrel.localRotation;
            barrel.SetParent(hinge, true);
            fixedBody = ExtractBody(false);
            movingBody = ExtractBody(true);
            body.sharedMesh = fixedBody;
            var chamber = new GameObject("FlareReloadChamber");
            chamber.transform.SetParent(hinge, false);
            chamber.transform.localPosition = -hinge.localPosition;
            chamber.AddComponent<MeshFilter>().sharedMesh = movingBody;
            chamber.AddComponent<MeshRenderer>().sharedMaterials = body.GetComponent<MeshRenderer>().sharedMaterials;
            redMaterial = MakeMaterial("FlareReload_RedCardboard", new Color(.55f, .055f, .035f));
            brassMaterial = MakeMaterial("FlareReload_BrassRim", new Color(.49f, .34f, .12f));
            cartridgeMesh = MakeCartridge();
            fresh = MakeCartridgeObject("FlareReloadFreshCartridge");
            spent = MakeCartridgeObject("FlareReloadSpentCase");
            fresh.gameObject.SetActive(false);
            spent.gameObject.SetActive(false);
            prepared = true;
        }

        private Mesh ExtractBody(bool moving)
        {
            var source = originalBody.vertices;
            var vertices = new Vector3[source.Length];
            for (int i = 0; i < source.Length; ++i) vertices[i] = model.InverseTransformPoint(body.transform.TransformPoint(source[i]));
            var mesh = new Mesh { name = moving ? "FlareReload_MovingHousing" : "FlareReload_FixedFrame" };
            // Bake the model transform into each extracted mesh while preserving UVs.
            // The static frame goes back into its original MeshFilter coordinates.
            if (!moving)
                for (int i = 0; i < vertices.Length; ++i) vertices[i] = source[i];
            mesh.vertices = vertices;
            mesh.uv = originalBody.uv;
            mesh.subMeshCount = originalBody.subMeshCount;
            for (int sub = 0; sub < mesh.subMeshCount; ++sub)
            {
                var input = originalBody.GetTriangles(sub);
                var output = new List<int>();
                for (int i = 0; i < input.Length; i += 3)
                {
                    bool forward = true;
                    for (int corner = 0; corner < 3; ++corner)
                    {
                        Vector3 p = model.InverseTransformPoint(body.transform.TransformPoint(source[input[i + corner]]));
                        forward &= p.z >= .0547f && p.y >= .032f;
                    }
                    if (forward == moving) { output.Add(input[i]); output.Add(input[i + 1]); output.Add(input[i + 2]); }
                }
                mesh.SetTriangles(output, sub);
            }
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private Transform MakeCartridgeObject(string name)
        {
            var item = new GameObject(name);
            item.transform.SetParent(hinge, false);
            item.AddComponent<MeshFilter>().sharedMesh = cartridgeMesh;
            item.AddComponent<MeshRenderer>().sharedMaterials = new[] { redMaterial, brassMaterial };
            return item.transform;
        }

        private static Material MakeMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color); material.color = color;
            return material;
        }

        private static Mesh MakeCartridge()
        {
            const int sides = 8;
            var verts = new List<Vector3>();
            var bodyTriangles = new List<int>(); var rimTriangles = new List<int>();
            float[] z = { -.012f, -.009f, -.008f, .052f };
            float[] radius = { .014f, .014f, .012f, .012f };
            for (int ring = 0; ring < z.Length; ++ring)
                for (int side = 0; side < sides; ++side)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    verts.Add(new Vector3(Mathf.Cos(angle) * radius[ring], Mathf.Sin(angle) * radius[ring], z[ring]));
                }
            for (int ring = 0; ring < z.Length - 1; ++ring)
                for (int side = 0; side < sides; ++side)
                {
                    int a = ring * sides + side, b = ring * sides + (side + 1) % sides;
                    var triangles = ring < 2 ? rimTriangles : bodyTriangles;
                    triangles.AddRange(new[] { a, b, a + sides, b, b + sides, a + sides });
                }
            verts.Add(new Vector3(0, 0, z[0]));
            for (int side = 0; side < sides; ++side) rimTriangles.AddRange(new[] { verts.Count - 1, (side + 1) % sides, side });
            var mesh = new Mesh { name = "FlareReload_EightSidedCartridge", subMeshCount = 2 };
            mesh.SetVertices(verts); mesh.SetTriangles(bodyTriangles, 0); mesh.SetTriangles(rimTriangles, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private void RestoreAttachment()
        {
            if (!displaced) return;
            if (transform.parent == attachmentParent) { transform.localPosition = attachmentPosition; transform.localRotation = attachmentRotation; }
            displaced = false;
        }

        private void OnDestroy()
        {
            if (body) body.sharedMesh = originalBody;
            if (barrel && originalBarrelParent)
            {
                barrel.SetParent(originalBarrelParent, false);
                barrel.localPosition = originalBarrelPosition; barrel.localRotation = originalBarrelRotation;
            }
            if (fixedBody) Destroy(fixedBody); if (movingBody) Destroy(movingBody);
            if (cartridgeMesh) Destroy(cartridgeMesh); if (redMaterial) Destroy(redMaterial); if (brassMaterial) Destroy(brassMaterial);
            if (hinge) Destroy(hinge.gameObject);
        }

        private static float Smooth(float t, float a, float b) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));
    }
}
