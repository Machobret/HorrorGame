using System.Collections;
using System.Collections.Generic;
using HorrorEngine;
using HorrorGame.Lighting;
using HorrorGame.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace HorrorGame.Chapter1
{
    [DefaultExecutionOrder(100)]
    internal sealed class FogMovementTutorial : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float demonstrationSeconds = .75f;
        [SerializeField, Min(.25f)] private float cutEveryMeters = 4.4f;
        [SerializeField, Min(.5f)] private float visibilityRadius = 2.3f;
        [SerializeField, Min(.1f)] private float visibilityFalloff = 1.75f;
        [SerializeField, Min(0f)] private float radiusGrowthPerLesson = .2f;
        [SerializeField, Min(.5f)] private float leverVisibilityRadius = 5.8f;
        [SerializeField, Min(.1f)] private float revealSeconds = 1.8f;

        private enum Phase { Movement, FindLever, PassLever, Inventory, WalkOut, Handoff }
        private const int FogLayer = 13;
        private static readonly int FogCenter = Shader.PropertyToID("_TutorialFogPlayerCenter");
        private const int TileSize = 16;
        private const float GroundHeight = 2000f;
        internal static readonly Vector3 StagingPosition = new(0f, GroundHeight, 0f);
        internal static bool LoadingNewGame { get; set; }
        private static readonly Color FogColor = new(.73f, .77f, .76f);
        private static readonly string[] Directions = { "up", "left", "right", "down" };
        private static readonly string[] TankInstructions = { "Walk forward", "Turn left", "Turn right", "Walk backwards" };
        private static readonly string[] AlternateInstructions = { "Move up", "Move left", "Move right", "Move down" };
        private static readonly int[] AlternateOrder = { 0, 1, 3, 2 };

        private readonly List<(GameObject Object, int Layer)> playerLayers = new();
        private readonly Transform[] tiles = new Transform[9];
        private Transform groundSurface;
        private readonly List<ButtonControl> bindings = new();
        private PlayerActor player;
        private PlayerSpawnPoint spawn;
        private PlayerMovement movement;
        private PlayerInputListener input;
        private UIInventory inventory;
        private Camera output;
        private CinemachineBrain brain;
        private CinemachineBlendDefinition oldBlend;
        private int oldMask;
        private CameraClearFlags oldClear;
        private Color oldBackground;
        private UniversalAdditionalCameraData cameraData;
        private bool oldPostProcessing;
        private bool cameraOwned;
        private CinemachineCamera shot;
        private float shotYaw;
        private float shotDistance;
        private float shotDirection;
        private int cutsUntilWideAngle;
        private Material envelope;
        private Material leverMaterial;
        private Light fill;
        private Image cover;
        private Phase phase;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private float travel, progress;
        private int step;
        private bool promptShown, released, acceptedTurn, alternate;
        private Transform lever;
        private Transform leverSupport;
        private Vector3 leverDirection;
        private bool leverApproached, inventoryOpened;
        private float currentRadius, targetRadius;
        private bool leverRevealed;
        private Mesh leverMesh;
        private InteractionData leverInteractionData;

        internal static void Begin(PlayerActor actor, PlayerSpawnPoint spawnPoint, Mesh leverMesh)
        {
            if (!actor || !spawnPoint || !CameraSystem.Exists || !CameraSystem.Instance.MainCamera) return;
            var shader = Resources.Load<Shader>("TutorialGroundEnvelope");
            if (!shader || !actor.TryGetComponent(out PlayerMovement playerMovement))
            {
                Debug.LogError("Fog tutorial could not initialize its ground shader or player movement.");
                return;
            }

            var root = new GameObject("Chapter 1 Fog Tutorial");
            var tutorial = root.AddComponent<FogMovementTutorial>();
            tutorial.leverMesh = leverMesh;
            tutorial.Initialize(actor, playerMovement, spawnPoint, shader);
        }

        private void Initialize(PlayerActor actor, PlayerMovement playerMovement, PlayerSpawnPoint spawnPoint, Shader shader)
        {
            player = actor;
            movement = playerMovement;
            spawn = spawnPoint;
            input = player.GetComponentInChildren<PlayerInputListener>(true);
            alternate = IsAlternate();
            currentRadius = targetRadius = visibilityRadius;
            envelope = new Material(shader);
            envelope.SetColor("_FogColor", FogColor);
            envelope.SetColor("_SurfaceColor", new Color(.36f, .38f, .37f));
            for (int i = 0; i < tiles.Length; i++)
                tiles[i] = CreateGroundCollider("Reusable ground");
            groundSurface = CreatePrimitive(PrimitiveType.Plane, "Continuous ground surface", envelope, false);
            groundSurface.localScale = new Vector3(2f, 1f, 2f);

            output = CameraSystem.Instance.MainCamera;
            brain = output.GetComponent<CinemachineBrain>();
            oldBlend = brain.DefaultBlend;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            oldMask = output.cullingMask;
            oldClear = output.clearFlags;
            oldBackground = output.backgroundColor;
            cameraData = output.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData)
            {
                oldPostProcessing = cameraData.renderPostProcessing;
                cameraData.renderPostProcessing = false;
            }
            cameraOwned = true;
            RenderPipelineManager.beginCameraRendering += UpdateFogBeforeRendering;
            AtmosphericFog.PlayerEnvelopeCamera = output;
            output.clearFlags = CameraClearFlags.SolidColor;
            output.backgroundColor = FogColor;
            output.cullingMask = 1 << FogLayer;

            foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                var obj = renderer.gameObject;
                if (obj.layer == FogLayer) continue;
                playerLayers.Add((obj, obj.layer));
                obj.layer = FogLayer;
            }
            fill = new GameObject("Fog character fill").AddComponent<Light>();
            fill.transform.SetParent(transform, false);
            fill.type = LightType.Directional;
            fill.cullingMask = 1 << FogLayer;
            fill.intensity = .8f;
            fill.transform.rotation = Quaternion.Euler(48f, -30f, 0f);

            Vector3 start = StagingPosition;
            RecycleGround(start);
            PlacePlayer(start, spawn.transform.rotation);
            UpdateEnvelope(start);
            Cut(start);
        }

        private bool IsAlternate() => movement.MovementTypeSetting &&
            movement.MovementTypeSetting.GetAsEnum<PlayerMovementType>() == PlayerMovementType.Alternate;

        private void PlacePlayer(Vector3 position, Quaternion rotation)
        {
            // Actor.PlaceAt owns teleport notifications and camera trigger recovery.
            player.PlaceAt(position, rotation);
            Physics.SyncTransforms();
            previousPosition = player.transform.position;
            previousRotation = player.transform.rotation;
        }

        private Transform CreatePrimitive(PrimitiveType type, string objectName, Material material, bool collides)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = objectName;
            obj.layer = FogLayer;
            obj.transform.SetParent(transform, false);
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collides) obj.GetComponent<Collider>().enabled = false;
            return obj.transform;
        }

        private void RecycleGround(Vector3 position)
        {
            groundSurface.position = new Vector3(position.x, GroundHeight, position.z);
            int x = Mathf.FloorToInt(position.x / TileSize);
            int z = Mathf.FloorToInt(position.z / TileSize);
            if (leverSupport && lever.gameObject.activeSelf)
            {
                int leverX = Mathf.FloorToInt(lever.position.x / TileSize);
                int leverZ = Mathf.FloorToInt(lever.position.z / TileSize);
                leverSupport.gameObject.SetActive(Mathf.Abs(x - leverX) > 1 || Mathf.Abs(z - leverZ) > 1);
            }
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int tx = x + dx, tz = z + dz;
                int index = ((tx % 3 + 3) % 3) + 3 * ((tz % 3 + 3) % 3);
                var tile = tiles[index];
                tile.localScale = new Vector3(TileSize, 1f, TileSize);
                tile.position = new Vector3((tx + .5f) * TileSize, GroundHeight - .5f, (tz + .5f) * TileSize);
            }
        }

        private Transform CreateGroundCollider(string objectName)
        {
            var tile = new GameObject(objectName, typeof(BoxCollider));
            tile.layer = FogLayer;
            tile.transform.SetParent(transform, false);
            return tile.transform;
        }

        private void UpdateEnvelope(Vector3 position)
        {
            Shader.SetGlobalVector(FogCenter, position);
            SetEnvelope(envelope);
            if (leverMaterial) SetEnvelope(leverMaterial);
        }

        private void UpdateFogBeforeRendering(ScriptableRenderContext context, Camera camera)
        {
            if (cameraOwned && camera == output && player)
                Shader.SetGlobalVector(FogCenter, player.transform.position);
        }

        private void SetEnvelope(Material material)
        {
            material.SetFloat("_Radius", currentRadius);
            material.SetFloat("_Falloff", visibilityFalloff + (currentRadius - visibilityRadius) * .45f);
        }

        private void Cut(Vector3 center, bool focusLever = false)
        {
            if (!shot)
            {
                shotDistance = Random.Range(7.5f, 9.5f);
                shotYaw = Random.Range(0f, 360f);
                shotDirection = Random.value < .5f ? -1f : 1f;
                cutsUntilWideAngle = Random.Range(3, 6);
            }
            else if (--cutsUntilWideAngle == 0)
            {
                shotDistance = Random.Range(7.1f, 10.5f);
                shotYaw += shotDirection * Random.Range(30f, 48f);
                cutsUntilWideAngle = Random.Range(3, 6);
            }
            else
            {
                shotDistance = Mathf.Clamp(shotDistance + Random.Range(-1.25f, 1.25f), 7.1f, 10.5f);
                shotYaw += shotDirection * Random.Range(7f, 14f);
            }

            shotYaw = Mathf.Repeat(shotYaw, 360f);
            var obj = new GameObject("Fog fixed shot");
            obj.transform.SetParent(transform, false);
            Vector3 framingCenter = focusLever ? Vector3.Lerp(center, lever.position, .65f) : center;
            float distance = focusLever ? shotDistance * .8f : shotDistance;
            Vector3 offset = Quaternion.Euler(0f, shotYaw, 0f) * new Vector3(0f, distance * .58f, -distance);
            Vector3 aim = framingCenter + Vector3.up * .9f;
            obj.transform.SetPositionAndRotation(framingCenter + offset, Quaternion.LookRotation(aim - framingCenter - offset));
            var next = obj.AddComponent<CinemachineCamera>();
            next.Priority = 5000;
            var lens = next.Lens;
            lens.FieldOfView = 60f;
            next.Lens = lens;
            if (shot)
            {
                shot.enabled = false;
                Destroy(shot.gameObject);
            }
            shot = next;
        }

        private void LateUpdate()
        {
            if (!player || !cameraOwned) return;
            Vector3 position = player.transform.position;
            Vector3 delta = Vector3.ProjectOnPlane(position - previousPosition, Vector3.up);
            float turn = Vector3.SignedAngle(previousRotation * Vector3.forward, player.transform.forward, Vector3.up);
            previousPosition = position;
            previousRotation = player.transform.rotation;
            RecycleGround(position);
            currentRadius = Mathf.MoveTowards(currentRadius, targetRadius, 4f * Time.deltaTime);
            UpdateEnvelope(position);
            if (phase == Phase.Handoff) return;
            if (!inventory && UIManager.Exists) inventory = UIManager.Get<UIInventory>(true);
            bool inventoryVisible = inventory && inventory.isActiveAndEnabled;
            if (phase == Phase.Inventory) inventoryOpened |= inventoryVisible;
            if (!GameManager.Instance.IsPlaying || inventoryVisible) return;

            travel += delta.magnitude;
            if (travel >= cutEveryMeters && phase != Phase.WalkOut)
            {
                Cut(position);
                travel = 0f;
            }

            switch (phase)
            {
                case Phase.Movement: UpdateLesson(delta, turn); break;
                case Phase.FindLever:
                    if (delta.sqrMagnitude > .000001f) PlaceLever(position, delta.normalized);
                    break;
                case Phase.PassLever: UpdateLever(position); break;
                case Phase.Inventory:
                    if (inventoryOpened)
                    {
                        PlayerMonologue.Clear(this);
                        travel = 0f;
                        phase = Phase.WalkOut;
                    }
                    break;
                case Phase.WalkOut:
                    if (travel >= .85f && delta.sqrMagnitude > .000001f)
                        StartCoroutine(Handoff(delta.normalized));
                    break;
            }
        }

        private void UpdateLesson(Vector3 delta, float turn)
        {
            if (input && input.Actions == null) return;
            int direction = step < 4 ? (alternate ? AlternateOrder[step] : step) : -1;
            if (!promptShown)
            {
                string key = CaptureBindings(step == 4 ? "Run" : step == 5 ? "Turn180" : "PrimaryAxis",
                    direction < 0 ? null : Directions[direction]);
                string instruction = step switch
                {
                    4 => $"Hold {key} while moving to run",
                    5 => $"{key} — Turn 180°",
                    _ => $"{key} — {(alternate ? AlternateInstructions : TankInstructions)[direction]}"
                };
                PlayerMonologue.ShowUntilCleared(this, instruction);
                released = !BindingHeld();
                promptShown = true;
                return;
            }

            bool held = BindingHeld();
            if (!held) released = true;
            if (step == 5)
            {
                if (released && held) acceptedTurn = true;
                if (acceptedTurn && player.StateController.CurrentState is PlayerStateTurn180)
                {
                    targetRadius += radiusGrowthPerLesson;
                    AdvanceLesson();
                }
                return;
            }
            if (!released || !held || !(player.StateController.CurrentState is PlayerStateMotion)) return;

            Vector2 axis = movement.InputAxis;
            float forward = Vector3.Dot(delta, player.transform.forward);
            bool moved = delta.sqrMagnitude > .000001f;
            bool demonstrated = step == 4 ? moved && movement.IsRunning : direction switch
            {
                0 => axis.y > .25f && (alternate ? moved : forward > .0001f),
                1 => axis.x < -.25f && (alternate ? moved : turn < -.01f),
                2 => axis.x > .25f && (alternate ? moved : turn > .01f),
                _ => axis.y < -.25f && (alternate ? moved : forward < -.0001f)
            };
            if (demonstrated)
            {
                float gained = Mathf.Min(Time.deltaTime, demonstrationSeconds - progress);
                progress += gained;
                targetRadius += radiusGrowthPerLesson * (gained / demonstrationSeconds);
            }
            if (progress >= demonstrationSeconds) AdvanceLesson();
        }

        private void AdvanceLesson()
        {
            step++;
            progress = 0f;
            promptShown = false;
            if (step >= (alternate ? 5 : 6))
            {
                PlayerMonologue.Clear(this);
                phase = Phase.FindLever;
            }
        }

        private string CaptureBindings(string name, string part)
        {
            bindings.Clear();
            var action = input?.Actions?.asset.FindAction("Gameplay/" + name);
            var keyboard = Keyboard.current;
            if (action != null && keyboard != null)
            {
                string display = null;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    string path = binding.effectivePath;
                    if ((part != null && part != binding.name) || string.IsNullOrEmpty(path) ||
                        !path.StartsWith("<Keyboard>/", System.StringComparison.OrdinalIgnoreCase)) continue;
                    var key = keyboard.TryGetChildControl<ButtonControl>(path.Substring("<Keyboard>/".Length));
                    if (key == null) continue;
                    bindings.Add(key);
                    display ??= action.GetBindingDisplayString(i);
                }
                if (!string.IsNullOrEmpty(display)) return display;
            }
            if (keyboard != null)
            {
                bindings.Add(part switch
                {
                    "up" => keyboard.wKey, "left" => keyboard.aKey, "right" => keyboard.dKey,
                    "down" => keyboard.sKey,
                    _ => name switch { "Run" => keyboard.leftShiftKey, "Interact" => keyboard.eKey, _ => keyboard.qKey }
                });
                return bindings[0].displayName;
            }
            return name;
        }

        private bool BindingHeld()
        {
            foreach (var key in bindings)
                if (key.isPressed) return true;
            return false;
        }

        private void PlaceLever(Vector3 playerPosition, Vector3 direction)
        {
            leverDirection = direction;
            leverMaterial = new Material(envelope);
            leverMaterial.SetColor("_SurfaceColor", new Color(.18f, .12f, .08f));
            lever = new GameObject("Tutorial control lever").transform;
            lever.SetParent(transform, false);
            Vector3 location = playerPosition + direction * (targetRadius + .85f);
            location.y = GroundHeight;
            lever.SetPositionAndRotation(location, Quaternion.LookRotation(direction));
            leverSupport = CreateGroundCollider("Lever ground reserve");
            leverSupport.localScale = new Vector3(TileSize, 1f, TileSize);
            leverSupport.position = new Vector3((Mathf.Floor(location.x / TileSize) + .5f) * TileSize,
                GroundHeight - .5f, (Mathf.Floor(location.z / TileSize) + .5f) * TileSize);
            leverSupport.gameObject.SetActive(false);
            var visual = new GameObject("Electrical box lever", typeof(MeshFilter), typeof(MeshRenderer));
            visual.layer = FogLayer;
            visual.transform.SetParent(lever, false);
            if (leverMesh)
            {
                visual.GetComponent<MeshFilter>().sharedMesh = leverMesh;
                visual.GetComponent<MeshRenderer>().sharedMaterial = leverMaterial;
                Bounds bounds = leverMesh.bounds;
                float scale = .65f / Mathf.Max(.001f, bounds.size.magnitude);
                visual.transform.localScale = Vector3.one * scale;
                visual.transform.localPosition = Vector3.up * .8f - bounds.center * scale;
            }
            var stand = CreatePrimitive(PrimitiveType.Cube, "Lever stand", leverMaterial, false);
            stand.SetParent(lever, false);
            stand.localPosition = Vector3.up * .28f;
            stand.localScale = new Vector3(.28f, .56f, .25f);

            lever.gameObject.layer = LayerMask.NameToLayer("InteractionSystem");
            var trigger = lever.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up * .55f;
            trigger.size = new Vector3(1.1f, 1.1f, 1.1f);
            var interaction = lever.gameObject.AddComponent<Interactive>();
            leverInteractionData = ScriptableObject.CreateInstance<InteractionData>();
            leverInteractionData.RotateToInteractive = false;
            interaction.SetInteractionData(leverInteractionData);
            interaction.SetPromptText("Switch control type");
            interaction.CanReachFrom = position =>
                Vector3.ProjectOnPlane(position - lever.position, Vector3.up).sqrMagnitude <= 1.3f * 1.3f;
            interaction.OnInteract = new OnInteractionEvent();
            interaction.OnInteract.AddListener(_ => ToggleLeverMode());
            phase = Phase.PassLever;
        }


        private void UpdateLever(Vector3 position)
        {
            Vector3 offset = Vector3.ProjectOnPlane(position - lever.position, Vector3.up);
            float distance = offset.magnitude;
            float along = Vector3.Dot(offset, leverDirection);
            leverApproached |= distance < currentRadius || (along >= 0f && distance < currentRadius * 2f);
            if (!leverRevealed && leverApproached)
            {
                leverRevealed = true;
                targetRadius = Mathf.Max(targetRadius, leverVisibilityRadius);
                Cut(position, focusLever: true);
                travel = 0f;
                return;
            }
            if (!leverApproached || along <= 0f || distance < targetRadius) return;
            lever.gameObject.SetActive(false);
            leverSupport.gameObject.SetActive(false);
            phase = Phase.Inventory;
            PlayerMonologue.ShowUntilCleared(this, "Press I to open the inventory, then close it to continue.");
        }

        private void ToggleLeverMode()
        {
            if (phase != Phase.PassLever || !movement.MovementTypeSetting) return;
            alternate = !IsAlternate();
            SettingsManager.Instance.Set(movement.MovementTypeSetting,
                (alternate ? PlayerMovementType.Alternate : PlayerMovementType.Tank).ToString());
            SettingsManager.Instance.SaveAndApply();
            string up = CaptureBindings("PrimaryAxis", "up");
            string left = CaptureBindings("PrimaryAxis", "left");
            string down = CaptureBindings("PrimaryAxis", "down");
            string right = CaptureBindings("PrimaryAxis", "right");
            string run = CaptureBindings("Run", null);
            string recap = alternate ? $"Alternate: {up} up · {left} left · {down} down · {right} right · {run} + move to run" :
                $"Tank: {up} forward · {left}/{right} turn · {down} back · {run} + move to run · {CaptureBindings("Turn180", null)} turn 180°";
            PlayerMonologue.ShowUntilCleared(this, recap, 4f);
        }

        private IEnumerator Handoff(Vector3 walkingDirection)
        {
            phase = Phase.Handoff;
            cover = CreateCover();

            // Translate the whole shot with the player so the screen-space
            // composition and the held alternate-control basis stay continuous.
            yield return FadeCover(0f, .38f, .65f);
            Vector3 destination = spawn.transform.position;
            Vector3 approach = FindRoadApproach(destination, walkingDirection);
            Vector3 shift = approach - player.transform.position;
            if (shot) shot.transform.position += shift;
            PlacePlayer(approach, player.transform.rotation);
            if (brain) brain.ManualUpdate();

            RestoreCamera();
            AtmosphericFog.BeginTutorialDispersal(output, FogColor);
            float reveal = 0f;
            bool cameraReleased = false;
            float approachDistance = Vector3.ProjectOnPlane(destination - approach, Vector3.up).magnitude;
            while (!cameraReleased || reveal < 1f)
            {
                Vector3 toSpawn = Vector3.ProjectOnPlane(destination - player.transform.position, Vector3.up);
                bool arrived = approachDistance < .01f || toSpawn.magnitude <= .65f ||
                    Vector3.Dot(toSpawn, walkingDirection) <= 0f;
                if (!cameraReleased && arrived)
                {
                    // Allow the spawn's existing camera trigger to register before
                    // dropping the tutorial shot's priority.
                    yield return new WaitForFixedUpdate();
                    if (brain) brain.DefaultBlend = new CinemachineBlendDefinition(
                        CinemachineBlendDefinition.Styles.Cut, 0f);
                    if (shot) shot.Priority = -10000;
                    cameraReleased = true;
                }

                float limit = cameraReleased ? 1f : .65f;
                reveal = Mathf.MoveTowards(reveal, limit, Time.deltaTime / revealSeconds);
                float eased = Mathf.SmoothStep(0f, 1f, reveal);
                AtmosphericFog.TutorialDispersal = eased;
                SetCoverAlpha(.38f * (1f - eased));
                yield return null;
            }

            Destroy(gameObject);
        }

        private Vector3 FindRoadApproach(Vector3 destination, Vector3 direction)
        {
            int roadMask = oldMask & ~(1 << FogLayer) & ~LayerMask.GetMask("Player", "InteractionSystem");
            if (!Physics.Raycast(destination + Vector3.up, Vector3.down, out var spawnGround, 2.5f,
                roadMask, QueryTriggerInteraction.Ignore)) return destination;

            for (float distance = 2.6f; distance >= 1f; distance -= .4f)
            {
                Vector3 candidate = destination - direction * distance;
                if (!Physics.Raycast(candidate + Vector3.up, Vector3.down, out var road, 2.5f,
                    roadMask, QueryTriggerInteraction.Ignore) || road.normal.y < .7f) continue;
                if (Mathf.Abs(road.point.y - spawnGround.point.y) > .4f ||
                    Physics.Linecast(road.point + Vector3.up * .5f,
                        spawnGround.point + Vector3.up * .5f, roadMask, QueryTriggerInteraction.Ignore)) continue;
                return new Vector3(candidate.x, road.point.y + destination.y - spawnGround.point.y, candidate.z);
            }
            return destination;
        }

        private Image CreateCover()
        {
            var canvasObject = new GameObject("Fog handoff cover", typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var image = new GameObject("Fog", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(canvas.transform, false);
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            image.raycastTarget = false;
            image.color = new Color(FogColor.r, FogColor.g, FogColor.b, 0f);
            return image;
        }

        private IEnumerator FadeCover(float from, float to, float duration)
        {
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                SetCoverAlpha(Mathf.SmoothStep(from, to, time / duration));
                yield return null;
            }
            SetCoverAlpha(to);
        }

        private void SetCoverAlpha(float alpha) => cover.color = new Color(FogColor.r, FogColor.g, FogColor.b, alpha);

        private void RestoreCamera()
        {
            if (!cameraOwned) return;
            cameraOwned = false;
            RenderPipelineManager.beginCameraRendering -= UpdateFogBeforeRendering;
            if (fill) fill.enabled = false;
            if (AtmosphericFog.PlayerEnvelopeCamera == output) AtmosphericFog.PlayerEnvelopeCamera = null;
            if (output)
            {
                output.cullingMask = oldMask;
                output.clearFlags = oldClear;
                output.backgroundColor = oldBackground;
            }
            if (cameraData) cameraData.renderPostProcessing = oldPostProcessing;
            foreach (var item in playerLayers)
                if (item.Object) item.Object.layer = item.Layer;
        }

        private void OnDestroy()
        {
            AtmosphericFog.EndTutorialDispersal(output);
            RestoreCamera();
            if (brain) brain.DefaultBlend = oldBlend;
            PlayerMonologue.Clear(this);
            if (envelope) Destroy(envelope);
            if (leverMaterial) Destroy(leverMaterial);
            if (leverInteractionData) Destroy(leverInteractionData);
        }
    }
}
