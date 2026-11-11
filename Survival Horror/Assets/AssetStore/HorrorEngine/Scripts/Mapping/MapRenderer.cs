using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace HorrorEngine
{
    public class MapRenderer : SingletonBehaviour<MapRenderer>
    {
        [SerializeField] private Transform m_MapContent;
        [SerializeField] private Transform m_MapTarget;
        [SerializeField] private Transform m_MapPlayer;
        [SerializeField] private LayerMask m_Mask;
        [SerializeField] private float m_CameraInterpolationSpeed = 10f;
        [SerializeField] private float m_RenderTextureScale = 1f;
        [SerializeField] private FilterMode m_RenderTextureFilterMode = FilterMode.Bilinear;
        [Header("Map zoom")]
        [SerializeField, Min(1f)] private float m_MinZoom = 100f;
        [SerializeField, Min(1f)] private float m_MaxZoom = 750f;
        [SerializeField, Min(1f)] private float m_ZoomStep = 75f;

        [Header("Visuals")]
        public Material GridMaterial;
        [Tooltip("Shows the faint authoring grid behind the player map.")]
        [SerializeField] private bool m_ShowGridInRuntime = true;
        [Tooltip("Extra world-space room around the drawn map that the player may pan into.")]
        [SerializeField, Min(0f)] private float m_MapPanPadding = 500f;

        public ShapeCreationProcess RoomIncompletedSP;
        public ShapeCreationProcess RoomCompletedSP;
        public ShapeCreationProcess RoomNotVisitedSP;
        public ShapeCreationProcess WallsSP;

        public Material DoorUnknownMaterial;
        public Material DoorUnlockedMaterial;
        public Material DoorLockedMaterial;

        public Material ImageMaterial;

        private Camera m_Cam;
        private MapData m_Map;
        private RenderTexture m_RenderTexture;
        private Material m_RuntimeGridMaterial;
        private Material m_RuntimeMarkerMaterial;
        private Material m_RuntimeContourMaterial;
        private Bounds m_ContentBounds;
        private float m_DefaultZoom;

        [Header("Contour readability")]
        [Tooltip("Renders room borders as high-contrast white lines in the player map.")]
        [SerializeField] private bool m_UseBoldWhiteContours = true;
        [SerializeField, Min(1f)] private float m_ContourThicknessMultiplier = 2.25f;
        
        public RenderTexture RenderTexture => m_RenderTexture;
        public MapData CurrentMap => m_Map;
        public Vector2 TargetViewportPosition
        {
            get
            {
                if (m_Cam == null || m_MapTarget == null) return new Vector2(.5f, .5f);
                Vector3 viewport = m_Cam.WorldToViewportPoint(m_MapTarget.position);
                return new Vector2(viewport.x, viewport.y);
            }
        }

        // Height layers
        private readonly int m_GridHeight = -1;
        //private readonly int m_RoomsHeight = 0;
        private readonly int m_DetailingHeight = 1;
        private readonly int m_WallsHeight = 2;
        private readonly int m_DoorsHeight = 3;
        private readonly int m_PlayerHeight = 5;
        private readonly int m_TargetHeight = 9;
        private readonly int m_CameraHeight = 20;

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();
            m_Cam = GetComponentInChildren<Camera>();
            Debug.Assert(m_Cam, "MapRenderer does not contain a camera");
            m_DefaultZoom = m_Cam.orthographicSize;
            // The crosshair uses very long line meshes. Occlusion culling can
            // incorrectly cull one line while the orthographic map camera pans.
            m_Cam.useOcclusionCulling = false;
            var cameraData = m_Cam.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null) cameraData = m_Cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            // Renderer index 1 has no global full-screen effects. The primary
            // renderer keeps the game's Retro Composite pass for normal cameras.
            cameraData.SetRenderer(1);
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraData.dithering = false;
            CreateStableMapMaterials();
        }

        // --------------------------------------------------------------------

        private void CreateStableMapMaterials()
        {
            // The original map assets use a mix of legacy Standard and URP Lit
            // shaders. In a URP project that causes the grid and marker alpha to
            // vary with rendering order. Map overlays do not need lighting, so
            // use dedicated unlit materials with deterministic colours instead.
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Unlit/Transparent");
            if (unlit == null) return;

            Texture gridTexture = GridMaterial != null ? GridMaterial.mainTexture : null;
            // Keep the grid faint; room contours remain the primary readable layer.
            m_RuntimeGridMaterial = CreateMapMaterial(unlit, new Color(.34f, .36f, .40f, .42f), gridTexture, false);
            m_RuntimeMarkerMaterial = CreateMapMaterial(unlit, Color.white, m_MapPlayer.GetComponentInChildren<Renderer>(true).sharedMaterial.mainTexture, true);
            m_RuntimeContourMaterial = CreateMapMaterial(unlit, Color.white, Texture2D.whiteTexture, true);

            GridMaterial = m_RuntimeGridMaterial;
            foreach (var renderer in m_MapPlayer.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = m_RuntimeMarkerMaterial;
            foreach (var renderer in m_MapTarget.GetComponentsInChildren<Renderer>(true))
            {
                // UIMap renders the crosshair as a screen-space overlay instead.
                // Rendering these enormous world-space planes into the texture
                // was the source of the flashing at the map panel's edges.
                renderer.enabled = false;
            }
        }

        private static Material CreateMapMaterial(Shader shader, Color color, Texture texture, bool alphaClip)
        {
            var material = new Material(shader) { name = "Runtime Map Overlay" };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (alphaClip)
            {
                // Keep the grid and triangle solid while preserving their texture
                // silhouettes. Alpha clipping writes depth and avoids sorting
                // flashes when the map camera moves.
                material.SetFloat("_AlphaClip", 1);
                material.SetFloat("_Cutoff", .1f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = 2450;
            }
            else
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_Blend", 0);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3000;
            }
            return material;
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            Destroy(m_RuntimeGridMaterial);
            Destroy(m_RuntimeMarkerMaterial);
            Destroy(m_RuntimeContourMaterial);
        }

        // --------------------------------------------------------------------

        private void Start()
        {
            m_RenderTexture = new RenderTexture((int)(Screen.width* m_RenderTextureScale), (int)(Screen.height* m_RenderTextureScale), 16);
            m_RenderTexture.filterMode = m_RenderTextureFilterMode;
            m_RenderTexture.wrapMode = TextureWrapMode.Clamp;
            m_RenderTexture.useMipMap = false;
            m_RenderTexture.autoGenerateMips = false;
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            Vector3 targetPos = new Vector3(m_MapTarget.position.x, m_Cam.transform.position.y, m_MapTarget.position.z);
            m_Cam.transform.position = Vector3.Lerp(m_Cam.transform.position, targetPos, Time.unscaledDeltaTime * m_CameraInterpolationSpeed);
        }

        // --------------------------------------------------------------------

        private int GetLayer()
        {
           return (int)(Mathf.Log((uint)m_Mask.value, 2));
        }

        // --------------------------------------------------------------------

        public void GenerateAll(MapData map, bool revealUnknownRooms = false)
        {
            m_Map = map;
            m_ContentBounds = CalculateContentBounds(map);

            // Clear map
            for (int i = 0; i < m_MapContent.childCount; ++i)
            {
                if (Application.isPlaying)
                {
                    // Destroy is deferred until the end of the frame. Hiding the old
                    // room immediately avoids one-frame white-outline flashes while
                    // the new map is being generated.
                    m_MapContent.GetChild(i).gameObject.SetActive(false);
                    Destroy(m_MapContent.GetChild(i).gameObject);
                }
                else
                    DestroyImmediate(m_MapContent.GetChild(i).gameObject);
            }

            // The grid helps while authoring, but is visual noise in the player map.
            var gridGen = GetComponentInChildren<GridGenerator>();
            if (gridGen)
            {
                // The player map always renders a subtle grid. It establishes scale
                // while panning and is shared with the editor's map representation.
                bool showGrid = true;
                gridGen.gameObject.SetActive(showGrid);
                if (showGrid)
                {
                    // The map canvas is defined by the generated drawing's
                    // bounds, not MapData.Size (which is only the authoring
                    // grid). Generate enough cells to cover that full canvas.
                    int cellSize = Mathf.Max(1, map.CellSize);
                    int cellsX = Mathf.Max(1, Mathf.CeilToInt(m_ContentBounds.size.x / cellSize));
                    int cellsY = Mathf.Max(1, Mathf.CeilToInt(m_ContentBounds.size.z / cellSize));
                    gridGen.gameObject.layer = GetLayer();
                    gridGen.Size = new Vector2Int(cellsX, cellsY);
                    gridGen.Generate(cellsX, cellsY);
                    // The grid is visual only. Keep its authored density instead
                    // of multiplying it by the room transform scale.
                    gridGen.transform.localScale = new Vector3(1, 1, -1) * cellSize;
                    gridGen.transform.rotation = Quaternion.Euler(-90, 0, 0);
                    // A rotated grid expands toward negative Z, so anchor its
                    // top-left at the calculated canvas bounds.
                    gridGen.transform.position = new Vector3(m_ContentBounds.min.x, m_GridHeight, m_ContentBounds.max.z);
                    gridGen.GetComponent<MeshRenderer>().sharedMaterial = GridMaterial;
                }
            }

            // Generate camera
            m_Cam = GetComponentInChildren<Camera>();
            m_Cam.targetTexture = m_RenderTexture;
            m_Cam.transform.position = new Vector3(map.CellSize * map.Size.x * 0.5f, m_CameraHeight, -map.CellSize * map.Size.y * 0.5f);
            m_Cam.cullingMask = m_Mask;

            Vector3 center = GetCenter();
            center.y = m_TargetHeight;
            m_MapTarget.position = center;

            // ---------------------------------------------- Map Specific

            HashSet<string> m_VisibleElements = new HashSet<string>();

            // Generate rooms
            var rooms = map.Rooms;
            for (int i = 0; i < rooms.Count; ++i)
            {

                if (GenerateRoom(rooms[i], revealUnknownRooms))
                {
                    foreach (var elm in rooms[i].LinkedElements)
                        m_VisibleElements.Add(elm);
                }
            }

            // Generate rooms
            var doors = map.Doors;
            for (int i = 0; i < doors.Count; ++i)
            {
                if (m_VisibleElements.Contains(doors[i].UniqueId))
                    GenerateDoor( doors[i]);
            }

        }
        // --------------------------------------------------------------------

        private bool GenerateRoom(MapRoomSerializedData room, bool revealUnknownRooms)
        {
            MapRoomState state = room.GetState();
            bool hasMap = GameManager.Instance.Inventory.Maps.Contains(m_Map);
            if (state == MapRoomState.Unknown)
            {
                if (hasMap || revealUnknownRooms) // Tablet panels may show their authored service map before discovery.
                    state = MapRoomState.NotVisited;
                else
                    return false;
            }

            ShapeCreationProcess roomSP = RoomNotVisitedSP;
            
            if (state == MapRoomState.Visited)
                roomSP = RoomIncompletedSP;
            else if (state == MapRoomState.Completed)
                roomSP = RoomCompletedSP;

            int layer = GetLayer();
            Matrix4x4 roomTRS = m_Map.GetTRS(room.Transform.Offset, room.Transform.Rotation, room.Transform.Scale, room.Transform.ZOrder);

            GameObject roomGO = roomSP.Create(room.Name, m_MapContent, layer, room.Shapes, roomTRS);

            var walls = WallsSP.Create(room.Name, m_MapContent, layer, room.Shapes, roomTRS);
            walls.transform.localPosition = new Vector3(walls.transform.localPosition.x, m_WallsHeight, walls.transform.localPosition.z);
            ApplyContourStyle(walls);

            // Draw Details
            int index = 0;
            foreach(var details in room.Details)
            {
                var dTRS = roomTRS * Matrix4x4.TRS(new Vector3(details.Transform.Offset.x, details.Transform.ZOrder * 0.01f, details.Transform.Offset.y), Quaternion.Euler(0, details.Transform.Rotation, 0), details.Transform.Scale);
                var newDetails = details.CreationProcess.Create(room.Name + "_Detailing_" + index, m_MapContent, layer, new ShapeData[]{ details.Shape }, dTRS);
                newDetails.transform.localPosition = new Vector3(newDetails.transform.localPosition.x, m_DetailingHeight, newDetails.transform.localPosition.z);

                bool isActive = details.GetState();
                newDetails.gameObject.SetActive(isActive);

                ++index;
            }

            // Draw Images
            index = 0;
            foreach (var image in room.Images)
            {
                var dTRS = roomTRS * Matrix4x4.TRS(new Vector3(image.Transform.Offset.x, image.Transform.ZOrder * 0.01f, image.Transform.Offset.y), Quaternion.Euler(0, image.Transform.Rotation, 0), image.Transform.Scale);
                GameObject newImage = GameObject.CreatePrimitive(PrimitiveType.Quad);
                newImage.name = room.Name + "_Image_" + index;
                newImage.layer = layer;
                newImage.transform.SetParent(m_MapContent);
                newImage.transform.localRotation = dTRS.rotation * Quaternion.Euler(90, 0, 0);
                newImage.transform.localScale = dTRS.MultiplyVector(Vector3.one);
                newImage.transform.localPosition = dTRS.MultiplyPoint(Vector3.zero);
                
                Material mat = new Material(ImageMaterial);
                mat.SetTexture("_MainTex", image.Texture);
                newImage.GetComponent<Renderer>().material = mat;

                bool isActive = image.GetState();
                newImage.gameObject.SetActive(isActive);

                ++index;
            }

            return true;
        }

        private void ApplyContourStyle(GameObject walls)
        {
            if (!m_UseBoldWhiteContours || walls == null || m_RuntimeContourMaterial == null) return;
            foreach (LineRenderer line in walls.GetComponentsInChildren<LineRenderer>(true))
            {
                line.sharedMaterial = m_RuntimeContourMaterial;
                line.widthMultiplier *= m_ContourThicknessMultiplier;
                line.numCapVertices = Mathf.Max(line.numCapVertices, 4);
                line.numCornerVertices = Mathf.Max(line.numCornerVertices, 4);
            }
        }

        // --------------------------------------------------------------------

        private void GenerateDoor( MapDoorSerializedData door)
        {
            GameObject go = new GameObject(door.Name + "_Mesh");
            go.transform.parent = m_MapContent;
            go.transform.position = new Vector3(0, m_DoorsHeight, 0);
            go.layer = GetLayer();

            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.receiveShadows = false;

            MapDoorState state = door.GetState();
            if (state == MapDoorState.Unknown)
                meshRenderer.sharedMaterial = DoorUnknownMaterial;
            else if (state == MapDoorState.Locked)
                meshRenderer.sharedMaterial = DoorLockedMaterial;
            else if (state == MapDoorState.Unlocked)
                meshRenderer.sharedMaterial = DoorUnlockedMaterial;

            Mesh mesh = new Mesh();
            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-door.Size.x, 0, -door.Size.y),
                new Vector3(door.Size.x, 0,-door.Size.y),
                new Vector3(door.Size.x, 0,door.Size.y),
                new Vector3(-door.Size.x, 0,door.Size.y)
            };

            Matrix4x4 t = m_Map.GetTRS(door.Transform.Offset, door.Transform.Rotation, door.Transform.Scale, door.Transform.ZOrder);
            for (int i = 0; i < vertices.Length; ++i)
            {
                vertices[i] = t.MultiplyPoint(vertices[i]);
            }
            mesh.vertices = vertices;

            int[] tris = new int[6]
            {
                // lower left triangle
                0, 2, 1,
                // upper right triangle
                0, 3, 2
            };
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            meshFilter.mesh = mesh;
        }

        // --------------------------------------------------------------------

        public void MoveTarget(Vector2 movement)
        {
            m_MapTarget.Translate(new Vector3(movement.x, 0, movement.y), Space.World);
            ClampTargetToSafeArea();
        }

        /// <summary>Zooms the map camera. Positive scroll zooms in.</summary>
        public void Zoom(float scrollDirection)
        {
            if (!m_Cam || Mathf.Approximately(scrollDirection, 0f)) return;
            float min = Mathf.Min(m_MinZoom, m_MaxZoom);
            float max = Mathf.Max(m_MinZoom, m_MaxZoom);
            m_Cam.orthographicSize = Mathf.Clamp(m_Cam.orthographicSize - Mathf.Sign(scrollDirection) * m_ZoomStep, min, max);
            ClampTargetToSafeArea();
        }

        /// <summary>
        /// Keeps map panning proportional to the visible area without making
        /// high zoom levels feel uncontrollably fast.
        /// </summary>
        public float GetPanSpeedMultiplier()
        {
            if (!m_Cam || m_DefaultZoom <= 0f) return 1f;
            return Mathf.Clamp(Mathf.Sqrt(m_Cam.orthographicSize / m_DefaultZoom), .65f, 1.5f);
        }

        // --------------------------------------------------------------------

        public void SetTargetPosition(Vector2 pos, bool snapCamera = false)
        {
            m_MapTarget.position = new Vector3(pos.x, m_TargetHeight, pos.y);
            ClampTargetToSafeArea();
            if (snapCamera && m_Cam)
                m_Cam.transform.position = new Vector3(m_MapTarget.position.x, m_Cam.transform.position.y, m_MapTarget.position.z);
        }

        // --------------------------------------------------------------------

        private void ClampTargetToSafeArea()
        {
            Vector3 center = GetCenter();
            Vector3 safeArea = GetSafeArea() * 0.5f;
            Vector3 targetPos = m_MapTarget.position;

            float left = center.x - safeArea.x;
            float right= center.x + safeArea.x;
            float top = center.z - safeArea.z;
            float bottom = center.z + safeArea.z;

            if (targetPos.x < left) targetPos.x = left;
            if (targetPos.x > right) targetPos.x = right;
            if (targetPos.z < top) targetPos.z = top;
            if (targetPos.z > bottom) targetPos.z = bottom;

            m_MapTarget.position = targetPos;
        }

        // --------------------------------------------------------------------

        public void SetPlayerPositionAndRotation(Vector2 position, Quaternion rotation)
        {
            m_MapPlayer.position = new Vector3(position.x, m_PlayerHeight, position.y);
            m_MapPlayer.rotation = rotation;
        }


        // --------------------------------------------------------------------

        public void SetPlayerVisible(bool visible)
        {
            m_MapPlayer.gameObject.SetActive(visible);
            // The map player marker has no gameplay visibility state; make every
            // renderer explicit after UIMap reactivates the map camera.
            foreach (var renderer in m_MapPlayer.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = visible;
        }

        // --------------------------------------------------------------------

        private Vector3 GetCenter()
        {
            return new Vector3(m_ContentBounds.center.x, 0, m_ContentBounds.center.z);
        }

        private Vector3 GetSafeArea()
        {
            // This is the permitted position range for the map crosshair, not
            // the camera's viewport range. Subtracting the viewport size here
            // made the crosshair stop one half-screen before the end of every
            // drawn room. Clamp it to the contour bounds themselves instead.
            return new Vector3(m_ContentBounds.size.x, 0, m_ContentBounds.size.z);
        }

        /// <summary>Uses rendered room geometry rather than MapData.Size, which is only an authoring grid size.</summary>
        private Bounds CalculateContentBounds(MapData map)
        {
            bool hasPoint = false;
            Bounds bounds = new Bounds();
            foreach (MapRoomSerializedData room in map.Rooms)
            {
                if (room?.Shapes == null) continue;
                Matrix4x4 matrix = map.GetTRS(room.Transform.Offset, room.Transform.Rotation, room.Transform.Scale, room.Transform.ZOrder);
                foreach (ShapeData shape in room.Shapes)
                {
                    if (shape?.Points == null) continue;
                    foreach (Vector3 localPoint in shape.Points)
                    {
                        Vector3 point = matrix.MultiplyPoint(localPoint);
                        if (!hasPoint) { bounds = new Bounds(point, Vector3.zero); hasPoint = true; }
                        else bounds.Encapsulate(point);
                    }
                }

                // Paths added through the room's + Path control are rendered
                // details, not room collision geometry. They still count as
                // visible map drawing and must therefore extend the pan bounds.
                if (room.Details == null) continue;
                foreach (MapDetailsSerializedData details in room.Details)
                {
                    if (details?.Shape?.Points == null) continue;
                    Matrix4x4 detailMatrix = matrix * Matrix4x4.TRS(
                        new Vector3(details.Transform.Offset.x, details.Transform.ZOrder * .01f, details.Transform.Offset.y),
                        Quaternion.Euler(0, details.Transform.Rotation, 0),
                        details.Transform.Scale);
                    foreach (Vector3 localPoint in details.Shape.Points)
                    {
                        Vector3 point = detailMatrix.MultiplyPoint(localPoint);
                        if (!hasPoint) { bounds = new Bounds(point, Vector3.zero); hasPoint = true; }
                        else bounds.Encapsulate(point);
                    }
                }
            }
            float canvasWidth = map.CellSize * map.Size.x * map.GlobalScale;
            float canvasHeight = map.CellSize * map.Size.y * map.GlobalScale;
            Bounds minimumCanvas = new Bounds(
                new Vector3(canvasWidth * .5f, 0, -canvasHeight * .5f),
                new Vector3(canvasWidth, 0, canvasHeight));
            if (hasPoint)
            {
                // Clamp to the actual map drawing, with a small breathing margin.
                bounds.Expand(new Vector3(m_MapPanPadding * 2f, 0f, m_MapPanPadding * 2f));
                return bounds;
            }
            return minimumCanvas;
        }

        // --------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (m_Map)
            {
                Gizmos.DrawWireCube(GetCenter(), GetSafeArea());
            }
        }
    }
}
