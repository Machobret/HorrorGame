using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Weapons
{
    // The rifle uses steady aiming clips; this adds a receiver-side bolt cycle,
    // rather than inheriting the template's left-hand shotgun pump motion.
    [DefaultExecutionOrder(260)]
    public sealed class RifleBoltVisual : MonoBehaviour
    {
        public Transform Bolt;
        public Vector3 Pull = new Vector3(0, 0, -.085f);
        public float LiftAngle = 60;
        public float CycleDuration = 1.4f;
        public Material CasingMaterial;
        public bool IsCycling => cycling;
        public bool HasEjectedCase { get; private set; }

        private AttackBase attack;
        private ActorStateController states;
        private Transform upper, lower, hand, character, frame;
        private Vector3 boltRest, pivot, handle, attachmentPosition;
        private Quaternion boltRotation, attachmentRotation;
        private Transform attachmentParent;
        private float started;
        private bool cycling, displaced;
        private static Mesh caseMesh;

        private void Awake()
        {
            attack = GetComponent<AttackBase>();
            if (!Bolt) { enabled = false; return; }
            boltRest = Bolt.localPosition;
            boltRotation = Bolt.localRotation;
            frame = GetComponent<Weapon>().Visuals.transform;
            var filter = Bolt.GetComponent<MeshFilter>();
            // Imported FBX meshes retain their own axis correction and scale.
            // Work in the normalized weapon frame instead of assuming mesh axes.
            Bounds bounds = new Bounds(frame.InverseTransformPoint(Bolt.position), Vector3.zero);
            handle = bounds.center;
            if (filter && filter.sharedMesh)
            {
                bool first = true;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 point = frame.InverseTransformPoint(Bolt.TransformPoint(vertex));
                    if (first) { bounds = new Bounds(point, Vector3.zero); handle = point; first = false; }
                    else bounds.Encapsulate(point);
                    if (point.x > handle.x) handle = point;
                }
            }
            // Bolt shaft runs just beneath the top of the assembly. Its knob
            // extends to the right and below the shaft when locked.
            pivot = new Vector3(0, bounds.max.y - .008f, bounds.center.z);
        }

        private void OnEnable()
        {
            if (attack) attack.OnAttackStart.AddListener(Cycle);
            var animator = GetComponentInParent<Animator>();
            var actor = GetComponentInParent<Actor>();
            if (!animator || !animator.isHuman || !actor) return;
            character = actor.transform;
            states = actor.GetComponentInChildren<ActorStateController>();
            upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            lower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        private void OnDisable()
        {
            if (attack) attack.OnAttackStart.RemoveListener(Cycle);
            ResetPose();
            upper = lower = hand = character = null;
            states = null;
        }

        private void Cycle()
        {
            RestoreAttachment();
            attachmentParent = transform.parent;
            attachmentPosition = transform.localPosition;
            attachmentRotation = transform.localRotation;
            started = Time.time;
            cycling = true;
            HasEjectedCase = false;
        }

        private void Update()
        {
            // Undo last frame's hand compensation before the animator evaluates.
            RestoreAttachment();
        }

        private void LateUpdate()
        {
            if (!cycling || !Bolt) return;
            if ((states && !(states.m_CurrentState is PlayerStateAttack)) || Time.time - started >= CycleDuration)
            {
                ResetPose();
                return;
            }
            float t = (Time.time - started) / Mathf.Max(.1f, CycleDuration);
            float lift = Smooth(t, .21f, .32f) * (1 - Smooth(t, .70f, .80f));
            float pull = Smooth(t, .33f, .49f) * (1 - Smooth(t, .55f, .70f));
            Vector3 restWorld = Bolt.parent.TransformPoint(boltRest);
            Quaternion restWorldRotation = Bolt.parent.rotation * boltRotation;
            Quaternion turn = Quaternion.AngleAxis(LiftAngle * lift, frame.forward);
            Vector3 axis = frame.TransformPoint(pivot);
            Vector3 shift = frame.TransformVector(Pull * pull);
            Bolt.SetPositionAndRotation(axis + turn * (restWorld - axis) + shift, turn * restWorldRotation);

            Vector3 weaponPosition = transform.position;
            Quaternion weaponRotation = transform.rotation;
            float recoil = (1 - Smooth(t, 0, .15f)) * Mathf.Sin(Mathf.Clamp01(t / .025f) * Mathf.PI * .5f);
            weaponPosition -= frame.forward * (.018f * recoil);
            transform.position = weaponPosition;

            // Right hand leaves the pistol grip, operates the bolt and returns.
            // Left hand stays under the fore-end. Keep the gun steady while solving
            // the right arm, because its normal attachment is on that hand.
            if (upper && lower && hand)
            {
                float reach = Smooth(t, .12f, .23f) * (1 - Smooth(t, .80f, .96f));
                Vector3 target = Vector3.Lerp(hand.position, (axis + turn * (frame.TransformPoint(handle) - axis) + shift), reach);
                Quaternion wrist = hand.rotation;
                SolveArm(target);
                hand.rotation = wrist * Quaternion.AngleAxis(-35 * lift, Vector3.forward);
                transform.SetPositionAndRotation(weaponPosition, weaponRotation);
                displaced = true;
            }
            else displaced = recoil > 0;

            if (!HasEjectedCase && t >= .47f)
            {
                HasEjectedCase = true;
                EjectCase();
            }
        }

        private static float Smooth(float time, float start, float end)
        {
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, time));
        }

        private void SolveArm(Vector3 target)
        {
            Vector3 shoulder = upper.position, elbow = lower.position;
            float first = Vector3.Distance(shoulder, elbow), second = Vector3.Distance(elbow, hand.position);
            Vector3 direction = target - shoulder;
            float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(first - second) + .001f, first + second - .001f);
            direction.Normalize();
            Vector3 bend = Vector3.ProjectOnPlane(elbow - shoulder, direction);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.Cross(direction, character.forward);
            float along = (first * first - second * second + distance * distance) / (2 * distance);
            Vector3 solved = shoulder + direction * along + bend.normalized * Mathf.Sqrt(Mathf.Max(0, first * first - along * along));
            upper.rotation = Quaternion.FromToRotation(elbow - shoulder, solved - shoulder) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation;
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

        private void ResetPose()
        {
            RestoreAttachment();
            cycling = false;
            if (!Bolt) return;
            Bolt.localPosition = boltRest;
            Bolt.localRotation = boltRotation;
        }

        private void EjectCase()
        {
            if (!CasingMaterial) return;
            var casing = new GameObject("HuntingRifle_SpentBrass");
            casing.transform.SetPositionAndRotation(frame.TransformPoint(pivot) + frame.right * .025f, frame.rotation);
            if (!caseMesh) caseMesh = BuildCaseMesh();
            casing.AddComponent<MeshFilter>().sharedMesh = caseMesh;
            casing.AddComponent<MeshRenderer>().sharedMaterial = CasingMaterial;
            var collider = casing.AddComponent<CapsuleCollider>();
            collider.direction = 2; collider.radius = .006f; collider.height = .064f;
            var body = casing.AddComponent<Rigidbody>();
            body.mass = .012f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = frame.right * 1.4f + frame.up * .8f;
            body.angularVelocity = frame.up * 17 + frame.forward * 8;
            var actor = GetComponentInParent<Actor>();
            if (actor)
                foreach (var ownerCollider in actor.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(collider, ownerCollider);
            Destroy(casing, 6);
        }

        private static Mesh BuildCaseMesh()
        {
            // Eight-sided bottleneck case with a shoulder, narrow open neck and
            // extraction rim. No projectile tip: the bullet has left the barrel.
            const int sides = 8;
            float[] z = { -.032f, -.030f, -.029f, .017f, .023f, .032f, .032f, .024f };
            float[] radius = { .0065f, .0065f, .0058f, .0058f, .0042f, .0042f, .0031f, .0031f };
            var vertices = new Vector3[z.Length * sides + 1];
            var triangles = new int[((z.Length - 1) * sides * 2 + sides) * 3];
            for (int ring = 0; ring < z.Length; ++ring)
                for (int side = 0; side < sides; ++side)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    vertices[ring * sides + side] = new Vector3(Mathf.Cos(angle) * radius[ring], Mathf.Sin(angle) * radius[ring], z[ring]);
                }
            vertices[vertices.Length - 1] = new Vector3(0, 0, z[0]);
            int index = 0;
            for (int ring = 0; ring < z.Length - 1; ++ring)
                for (int side = 0; side < sides; ++side)
                {
                    int a = ring * sides + side, b = ring * sides + (side + 1) % sides, c = a + sides, d = b + sides;
                    triangles[index++] = a; triangles[index++] = b; triangles[index++] = c;
                    triangles[index++] = b; triangles[index++] = d; triangles[index++] = c;
                }
            for (int side = 0; side < sides; ++side)
            {
                triangles[index++] = vertices.Length - 1;
                triangles[index++] = (side + 1) % sides;
                triangles[index++] = side;
            }
            var mesh = new Mesh { name = "PSX_RifleSpentBrass" };
            mesh.vertices = vertices; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
