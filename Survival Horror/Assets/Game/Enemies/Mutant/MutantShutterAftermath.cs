using System.Collections;
using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Enemies
{
    public sealed class MutantShutterAftermath : MonoBehaviour
    {
        private readonly List<GameObject> marks = new();
        private Material blood;
        private Material flesh;
        private GameObject impact;
        private Vector3 floor;
        private PlayerActor player;
        private PlayerMovement movement;
        private Transform head;
        private Quaternion headPose;
        private bool headAdjusted;
        private float lookPitch;
        private SphereCollider reactionZone;
        private Mesh poolMesh;

        public static MutantShutterAftermath Create(Transform marker, Material blood, GameObject impact)
        {
            var root = new GameObject("Shutter escape aftermath");
            root.transform.SetParent(marker, false);
            var aftermath = root.AddComponent<MutantShutterAftermath>();
            aftermath.blood = blood;
            aftermath.impact = impact;
            aftermath.floor = Physics.Raycast(marker.position + Vector3.up * .3f, Vector3.down,
                out var hit, 1.5f, 1, QueryTriggerInteraction.Ignore) ? hit.point : marker.position;
            aftermath.AddStagingMarks();
            return aftermath;
        }

        private void AddStagingMarks()
        {
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 2.4f;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (.12f + i * .065f);
                marks.Add(CreateMark(floor + offset, .16f + .025f * i, angle * Mathf.Rad2Deg));
            }
        }

        public void ScheduleDrop(Vector3 opening)
        {
            StartCoroutine(DropAfterPause(opening));
        }

        private IEnumerator DropAfterPause(Vector3 opening)
        {
            yield return WaitUnpaused(2.7f);

            reactionZone = gameObject.AddComponent<SphereCollider>();
            reactionZone.isTrigger = true;
            reactionZone.center = transform.InverseTransformPoint(floor + Vector3.up * .75f);
            reactionZone.radius = 3.1f;
            var candidate = GameManager.Exists ? GameManager.Instance.Player : null;
            if (candidate && !candidate.IsDisabled && candidate.TryGetComponent<PlayerMovement>(out var motor) &&
                candidate.TryGetComponent<CharacterController>(out var capsule) &&
                reactionZone.bounds.Intersects(capsule.bounds) &&
                (reactionZone.ClosestPoint(capsule.bounds.center) - capsule.bounds.center).sqrMagnitude < .04f)
            {
                player = candidate;
                movement = motor;
                player.Disable(this);
                var animator = player.MainAnimator;
                if (animator && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
                yield return React(opening);
            }
            reactionZone.enabled = false;

            var part = CreateArm();
            Vector3 landing = floor + Vector3.up * .12f;
            Vector3 release = new Vector3(floor.x, Mathf.Max(opening.y - .16f, floor.y + 1.6f), floor.z);
            for (float time = 0f; time < .58f;)
            {
                float t = time / .58f;
                part.SetPositionAndRotation(Vector3.Lerp(release, landing, t * t),
                    Quaternion.Euler(150f * t, 190f * t, 60f + 30f * t));
                yield return null;
                time += UnpausedDelta;
            }
            part.SetPositionAndRotation(landing, Quaternion.Euler(0f, 28f, 90f));
            if (impact)
            {
                var splash = Instantiate(impact, floor + Vector3.up * .06f, Quaternion.identity, transform);
                Destroy(splash, 4f);
            }
            var pool = CreateMark(floor + Vector3.up * .007f, 1.55f, 31f);
            if (pool)
            {
                poolMesh = CreatePoolMesh();
                pool.GetComponent<MeshFilter>().sharedMesh = poolMesh;
                Vector3 size = pool.transform.localScale;
                pool.transform.localScale = size * .55f;
                for (float time = 0f; time < .4f;)
                {
                    pool.transform.localScale = size * Mathf.Lerp(.55f, 1f, time / .4f);
                    yield return null;
                    time += UnpausedDelta;
                }
                pool.transform.localScale = size;
            }
            foreach (var mark in marks)
                if (mark) Destroy(mark);
            marks.Clear();
            ReleasePlayer();
        }

        private IEnumerator React(Vector3 opening)
        {
            float elapsed = 0f;
            bool dripsStarted = false;
            while (elapsed < 1.15f && player)
            {
                yield return new WaitForFixedUpdate();
                if (!player || UnpausedDelta <= 0f) continue;
                elapsed += Time.fixedDeltaTime;
                Vector3 toward = opening - player.transform.position;
                toward.y = 0f;
                if (toward.sqrMagnitude > .01f)
                {
                    player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation,
                        Quaternion.LookRotation(toward), 460f * Time.fixedDeltaTime);
                }
                if (head)
                {
                    Vector3 gaze = opening - head.position;
                    float horizontal = Vector3.ProjectOnPlane(gaze, Vector3.up).magnitude;
                    lookPitch = Mathf.MoveTowards(lookPitch,
                        Mathf.Clamp(Mathf.Atan2(gaze.y, horizontal) * Mathf.Rad2Deg, 0f, 58f), 120f * Time.fixedDeltaTime);
                }
                if (elapsed > .35f)
                {
                    if (!dripsStarted)
                    {
                        dripsStarted = true;
                        StartCoroutine(PlayWarningDrips(opening));
                    }
                    movement.MoveCinematic(-player.transform.forward * (.55f * Time.fixedDeltaTime));
                }
            }
            if (movement) movement.StopCinematicMovement();
        }

        private IEnumerator PlayWarningDrips(Vector3 opening)
        {
            if (!blood) yield break;
            for (int i = 0; i < 3; i++)
            {
                StartCoroutine(FallWarningDrip(opening, i));
                yield return WaitUnpaused(.16f);
            }
        }

        private IEnumerator FallWarningDrip(Vector3 opening, int index)
        {
            float angle = index * 2.4f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (.05f + index * .036f);
            Vector3 start = opening + offset - Vector3.up * .18f;
            Vector3 end = floor + offset + Vector3.up * .025f;
            var drip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            drip.name = "Ceiling blood drip";
            drip.transform.SetParent(transform, false);
            drip.transform.position = start;
            drip.transform.localScale = new Vector3(.035f, .075f, .035f);
            DisableCollision(drip);
            drip.GetComponent<Renderer>().sharedMaterial = blood;

            const float fallTime = .43f;
            for (float elapsed = 0f; elapsed < fallTime;)
            {
                float t = elapsed / fallTime;
                drip.transform.position = Vector3.Lerp(start, end, t * t);
                yield return null;
                elapsed += UnpausedDelta;
            }
            Destroy(drip);
            marks.Add(CreateMark(end - Vector3.up * .022f, .073f, angle * Mathf.Rad2Deg));
        }

        private static Mesh CreatePoolMesh()
        {
            const int sides = 48;
            var vertices = new Vector3[sides + 1];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[sides * 3];
            uv[0] = new Vector2(.5f, .5f);
            for (int i = 0; i < sides; i++)
            {
                float angle = i * (Mathf.PI * 2f / sides);
                float radius = .44f + .035f * Mathf.Sin(angle * 5f + .6f) +
                    .022f * Mathf.Sin(angle * 8f) + .018f * Mathf.Cos(angle * 3f);
                Vector2 point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                vertices[i + 1] = new Vector3(point.x, point.y, 0f);
                uv[i + 1] = point + uv[0];
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % sides + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Shutter blood pool outline", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Update() => RestoreHead();

        private void LateUpdate()
        {
            if (!head || !player || lookPitch <= 0f) return;
            headPose = head.localRotation;
            head.rotation = Quaternion.AngleAxis(-lookPitch, player.transform.right) * head.rotation;
            headAdjusted = true;
        }

        private void RestoreHead()
        {
            if (headAdjusted && head) head.localRotation = headPose;
            headAdjusted = false;
        }

        private GameObject CreateMark(Vector3 point, float size, float yaw)
        {
            if (!blood) return null;
            var mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mark.name = "Floor blood";
            mark.transform.SetParent(transform, false);
            mark.transform.SetPositionAndRotation(point + Vector3.up * .012f,
                Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.Euler(90f, 0f, 0f));
            mark.transform.localScale = new Vector3(size, size * .85f, 1f);
            DisableCollision(mark);
            mark.GetComponent<Renderer>().sharedMaterial = blood;
            return mark;
        }

        private Transform CreateArm()
        {
            var part = new GameObject("Dropped forearm and hand").transform;
            part.SetParent(transform, false);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader)
                flesh = new Material(shader) { color = new Color(.38f, .23f, .20f) };
            AddPart(part, PrimitiveType.Capsule, new Vector3(0, .12f, 0), new Vector3(.14f, .29f, .12f));
            AddPart(part, PrimitiveType.Sphere, new Vector3(0, -.13f, 0), new Vector3(.17f, .19f, .085f));
            for (int i = 0; i < 4; i++)
                AddPart(part, PrimitiveType.Capsule, new Vector3((i - 1.5f) * .038f, -.245f, 0),
                    new Vector3(.035f, .067f - Mathf.Abs(i - 1.5f) * .008f, .036f));
            AddPart(part, PrimitiveType.Capsule, new Vector3(.093f, -.15f, 0), new Vector3(.045f, .058f, .048f));
            return part;
        }

        private void AddPart(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale)
        {
            var mesh = GameObject.CreatePrimitive(type);
            mesh.transform.SetParent(parent, false);
            mesh.transform.localPosition = position;
            mesh.transform.localScale = scale;
            DisableCollision(mesh);
            if (flesh) mesh.GetComponent<Renderer>().sharedMaterial = flesh;
        }

        private static void DisableCollision(GameObject mesh)
        {
            var collider = mesh.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
        }

        private static float UnpausedDelta =>
            PauseController.Exists && PauseController.Instance.IsPaused ? 0f : Time.deltaTime;

        private static IEnumerator WaitUnpaused(float duration)
        {
            for (float elapsed = 0f; elapsed < duration;)
            {
                yield return null;
                elapsed += UnpausedDelta;
            }
        }

        private void ReleasePlayer()
        {
            RestoreHead();
            if (movement) movement.StopCinematicMovement();
            if (player) player.Enable(this);
            movement = null;
            player = null;
            head = null;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            ReleasePlayer();
        }

        private void OnDestroy()
        {
            if (poolMesh) Destroy(poolMesh);
            if (flesh) Destroy(flesh);
        }
    }
}
