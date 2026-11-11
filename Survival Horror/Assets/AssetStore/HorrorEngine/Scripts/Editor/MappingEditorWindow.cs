using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using HorrorGame.Chapter1;

namespace HorrorEngine
{
    public class MappingEditorWindow : EditorWindow
    {
        public Texture2D ToolWindowIcon;
        public GameObject MapRoomPrefab;
        public GameObject MapDoorPrefab;
        public GameObject MapDetailsLinePrefab;
        public GameObject MapDetailsShapePrefab;
        //public GameObject MapDetailsStairsPrefab;
        public GameObject MapImagePrefab;
        public Texture2D RoomIcon;
        public Texture2D DoorIcon;
        public Material ImageMaterial;

        private Vector2 m_ScrollPos;
        private Vector2 m_ScrollPosInspector;

        private bool m_Moving;
        private bool m_Rotating;

        private MapController m_SelectedMap;
        private MapElement m_Selected;
        private MapDetailingShape m_SelectedPath;
        private bool m_ConnectingPathPoints;
        private Shape m_ConnectionStartShape;
        private int m_ConnectionStartPoint = -1;
        private PowerCircuitPanelElement m_SelectedCircuit;
        // Circuit editing selects the dot GameObject, so keep its room context
        // independently from Unity's one-object Selection state.
        private MapRoom m_CircuitRoomContext;
        private Material m_Mat;

        private bool m_EditingMapDetails;
        private float m_InspectorWidth = 250;

        // --------------------------------------------------------------------

        public static void Init(MapController selected)
        {
            MappingEditorWindow window = (MappingEditorWindow)EditorWindow.GetWindow(typeof(MappingEditorWindow));
            window.Show();
            window.rootVisualElement.focusable = true;
            window.m_SelectedMap = selected;
        }


        // --------------------------------------------------------------------

        void OnGUI()
        {
            SetupMaterial();

            titleContent = new GUIContent("HE Map Editor", ToolWindowIcon);
            UpdateKeyboardEvents();

            if (m_SelectedMap && m_SelectedMap.Data)
            {
                MapData map = m_SelectedMap.Data;
                DrawGrid(map.Size.x, map.Size.y, map.CellSize, new Color(0.35f, 0.35f, 0.35f, 1f));

                GameObject selectedGO = Selection.activeObject as GameObject;
                m_Selected = selectedGO ? selectedGO.GetComponent<MapElement>() : null;
                m_SelectedPath = selectedGO ? selectedGO.GetComponent<MapDetailingShape>() : null;
                m_SelectedCircuit = selectedGO ? selectedGO.GetComponent<PowerCircuitPanelElement>() : null;
                if (m_Selected is MapRoom selectedRoom) m_CircuitRoomContext = selectedRoom;
                else if (m_SelectedCircuit && m_SelectedCircuit.Room) m_CircuitRoomContext = m_SelectedCircuit.Room;

                var rooms = m_SelectedMap.GetComponentsInChildren<MapRoom>(true);
                for (int i = 0; i < rooms.Length; ++i)
                {
                    DrawRoom(m_SelectedMap, rooms[i], m_Selected == rooms[i]);
                }

                var doors = m_SelectedMap.GetComponentsInChildren<MapDoor>(true);
                for (int i = 0; i < doors.Length; ++i)
                {
                    DrawDoor(m_SelectedMap, doors[i], m_Selected == doors[i]);
                }

                DrawPowerCircuits();
                DrawPathConnectionPoints(rooms);
                HandlePathConnectionClick(rooms);
                DrawNavigationIndicators(rooms);

                // Images need to go last since GUI.DrawTexture seems to interfere with other graphic GL calls
                foreach (var room in rooms)
                {
                    Matrix4x4 trs = GetElementTRS(m_SelectedMap.Data, room);
                    DrawImages(m_SelectedMap, room);
                }

                // This needs to go before the inspector so new items are already in the arrays
                if (m_Selected || m_SelectedCircuit || m_SelectedPath)
                {
                    UpdateTransformOps();
                    if (m_Selected) DrawSelectedProperties();
                    else if (m_SelectedCircuit) DrawSelectedCircuitProperties();
                    else DrawSelectedPathProperties(m_SelectedPath);
                }

                int defaultFontSize = GUI.skin.label.fontSize;
                GUI.skin.label.fontSize = 32;
                GUI.Label(new Rect(m_InspectorWidth + 10, 10, 1000, 50), map.Name);
                
                GUI.skin.label.fontSize = 42;
                GUI.Label(new Rect(m_InspectorWidth + 10, 50, 1000, 50), map.Abbreviation);
                GUI.skin.label.fontSize = defaultFontSize;

                DrawInspector(m_SelectedMap, rooms, doors);

            }
            else
            {
                float w = Mathf.Max(position.width * 0.5f, 200f);
                float h = Mathf.Max(position.height * 0.25f, 100f);
                GUILayout.BeginArea(new Rect(position.width * 0.5f - w * 0.5f, position.height * 0.5f - h * 0.5f, w, h));
                GUILayout.BeginVertical();

                MapController[] availableMaps = FindObjectsByType<MapController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                m_SelectedMap = null;
                foreach (MapController map in availableMaps)
                {
                    if (map && map.Data) { m_SelectedMap = map; break; }
                }
                if (!m_SelectedMap && availableMaps.Length > 0) m_SelectedMap = availableMaps[0];
                if (!m_SelectedMap)
                {
                    EditorGUILayout.HelpBox("There is no MapController in the scene", MessageType.Error);
                    if (GUILayout.Button("Create"))
                    {
                        NewMapWizard.ShowWindow();
                    }
                }
                else if (!m_SelectedMap.Data)
                {
                    EditorGUILayout.HelpBox("The MapController has no Data", MessageType.Error);
                    if (GUILayout.Button("Create"))
                    {
                        NewMapWizard.ShowWindow();
                    }

                    int pickMapDataControlI = EditorGUIUtility.GetControlID(FocusType.Passive) + 100;
                    if (GUILayout.Button("Select"))
                    {
                        EditorGUIUtility.ShowObjectPicker<MapData>(null, false, "t:MapData", pickMapDataControlI);
                    }

                    if (Event.current.commandName == "ObjectSelectorUpdated")
                    {
                        int controlId = EditorGUIUtility.GetObjectPickerControlID();
                        if (controlId == pickMapDataControlI)
                        {
                            MapData selectedMap = EditorGUIUtility.GetObjectPickerObject() as MapData;
                            if (selectedMap)
                            {
                                m_SelectedMap.Data = selectedMap;
                                EditorUtility.SetDirty(m_SelectedMap);
                            }
                        }
                    }
                }
                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }

        // --------------------------------------------------------------------

        private void SetupMaterial()
        {
            // Fixes Mac error "Metal: Vertex shader missing buffer binding at index 0"
            // https://issuetracker.unity3d.com/issues/macos-metal-cannot-write-to-unused-constant-buffer-dot-error-is-thrown-when-using-unityengine-dot-gl-inside-onscenegui
            if (m_Mat == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                m_Mat = new Material(shader);
                m_Mat.hideFlags = HideFlags.HideAndDontSave;
            }
            m_Mat.SetPass(0);
        }

        // --------------------------------------------------------------------

        private void UpdateKeyboardEvents()
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {

                GUI.FocusControl(null);
                //e.Use();
            }
            if (e.type == EventType.MouseDrag && e.button == 2)
            {
                m_ScrollPos += e.delta;
                e.Use();
            }

            if (GUIUtility.keyboardControl == 0)
            {
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.G)
                {
                    GUI.FocusControl(null);
                    m_Moving = true;
                    e.Use();
                }
                else if (e.type == EventType.KeyUp && e.keyCode == KeyCode.G)
                {
                    GUI.FocusControl(null);
                    m_Moving = false;
                    e.Use();
                }
                else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
                {
                    GUI.FocusControl(null);
                    m_Rotating = true;
                    e.Use();
                }
                else if (e.type == EventType.KeyUp && e.keyCode == KeyCode.R)
                {
                    GUI.FocusControl(null);
                    m_Rotating = false;
                    e.Use();
                }
                else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.A)
                {
                    GUI.FocusControl(null);
                    m_ScrollPos = Vector2.zero;
                    e.Use();
                }
            }
        }

        // --------------------------------------------------------------------

        private void UpdateTransformOps()
        {
            Vector2 scrolledMousePos = Event.current.mousePosition - m_ScrollPos;
            if (m_SelectedCircuit)
            {
                if (m_Moving)
                {
                    if (m_SelectedCircuit.Room)
                    {
                        Matrix4x4 roomTransform = GetElementTRS(m_SelectedMap.Data, m_SelectedCircuit.Room);
                        Vector3 localPoint = roomTransform.inverse.MultiplyPoint(new Vector3(Event.current.mousePosition.x, 0, Event.current.mousePosition.y));
                        m_SelectedCircuit.RoomOffset = new Vector2(localPoint.x, localPoint.z);
                    }
                    else
                    {
                        m_SelectedCircuit.transform.localPosition = new Vector3(scrolledMousePos.x, 0, scrolledMousePos.y);
                    }
                    PowerPanelController controller = m_SelectedCircuit.GetComponentInParent<PowerPanelController>();
                    controller?.SerializeToData();
                    Repaint();
                    EditorUtility.SetDirty(m_SelectedCircuit);
                }
                return;
            }

            if (!m_Selected) return;
            if (m_Moving)
            {
                m_Selected.Offset = scrolledMousePos;
                Repaint();
                EditorUtility.SetDirty(m_Selected);
            }
            else if (m_Rotating)
            {
                float angle = Vector2.SignedAngle(Vector2.up, scrolledMousePos - m_Selected.Offset);
                m_Selected.Rotation = -Mathf.RoundToInt(angle / 5.0f) * 5; // Round to neares 5
                Repaint();
                EditorUtility.SetDirty(m_Selected);
            }

        }

        // --------------------------------------------------------------------

        private void DrawSelectedProperties()
        {
            if (m_Selected is MapRoom)
            {
                DrawSelectedRoomProperties(m_Selected as MapRoom);
            }

            if (m_Selected is MapDoor)
            {
                DrawSelectedDoorProperties(m_Selected as MapDoor);
            }

            GUI.Label(new Rect(m_InspectorWidth + 10, 90, 300, 50), "Hold [G] for moving");
            GUI.Label(new Rect(m_InspectorWidth + 10, 110, 300, 50), "Hold [R] for rotating");

            if (m_Selected.Scale != m_Selected.transform.lossyScale)
            {
                m_Selected.Scale = m_Selected.transform.lossyScale;
                EditorUtility.SetDirty(m_Selected);
            }
        }

        private void DrawSelectedCircuitProperties()
        {
            string roomName = m_SelectedCircuit.Room
                ? (string.IsNullOrEmpty(m_SelectedCircuit.Room.Name) ? m_SelectedCircuit.Room.name : m_SelectedCircuit.Room.Name)
                : "No room assigned";
            GUIStyle propsStyle = new GUIStyle();
            propsStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.75f));

            GUILayout.BeginArea(new Rect(m_InspectorWidth + 20, position.height - 325, 350, 315));
            GUILayout.BeginVertical(propsStyle);
            GUILayout.Label("Power Circuit", EditorStyles.boldLabel);

            string previousId = m_SelectedCircuit.CircuitId;
            EditorGUI.BeginChangeCheck();
            m_SelectedCircuit.Label = EditorGUILayout.TextField("Name", m_SelectedCircuit.Label);
            EditorGUILayout.LabelField("Circuit ID", string.IsNullOrEmpty(m_SelectedCircuit.CircuitId) ? "Generated after naming" : m_SelectedCircuit.CircuitId);
            m_SelectedCircuit.Consumption = Mathf.Max(0, EditorGUILayout.IntField("Power Draw", m_SelectedCircuit.Consumption));
            m_SelectedCircuit.StartsEnabled = EditorGUILayout.Toggle("Starts Powered", m_SelectedCircuit.StartsEnabled);
            EditorGUILayout.LabelField("Map Room", roomName);
            GUILayout.Space(4);
            GUILayout.Label("Powered Targets", EditorStyles.boldLabel);

            bool targetsChanged = false;
            CircuitDrivenObject removedTarget = null;
            for (int i = 0; i < m_SelectedCircuit.LinkedTargets.Count; i++)
            {
                GUILayout.BeginHorizontal();
                m_SelectedCircuit.LinkedTargets[i] = (CircuitDrivenObject)EditorGUILayout.ObjectField(m_SelectedCircuit.LinkedTargets[i], typeof(CircuitDrivenObject), true);
                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    removedTarget = m_SelectedCircuit.LinkedTargets[i];
                    m_SelectedCircuit.LinkedTargets.RemoveAt(i);
                    targetsChanged = true;
                    i--;
                }
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Add Target")) { m_SelectedCircuit.LinkedTargets.Add(null); targetsChanged = true; }

            if (EditorGUI.EndChangeCheck() || targetsChanged)
            {
                Undo.RecordObject(m_SelectedCircuit, "Change power circuit");
                if (removedTarget && removedTarget.CircuitId == previousId)
                {
                    Undo.RecordObject(removedTarget, "Unlink power circuit target");
                    removedTarget.CircuitId = string.Empty;
                    EditorUtility.SetDirty(removedTarget);
                }
                PowerPanelController controller = m_SelectedCircuit.GetComponentInParent<PowerPanelController>();
                if (controller && previousId != m_SelectedCircuit.CircuitId)
                    controller.SyncCircuitData(m_SelectedCircuit, previousId);
                else controller?.RefreshAutomaticCircuitId(m_SelectedCircuit);
                controller?.SerializeToData();
                EditorUtility.SetDirty(m_SelectedCircuit);
                if (controller) EditorUtility.SetDirty(controller);
                EditorSceneManager.MarkSceneDirty(m_SelectedCircuit.gameObject.scene);
                AssetDatabase.SaveAssets();
            }
            GUILayout.Space(5);
            GUILayout.Label("Hold [G] to position the dot inside its room", EditorStyles.miniLabel);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawSelectedPathProperties(MapDetailingShape path)
        {
            if (!path || !path.TryGetComponent(out Shape shape)) return;

            GUIStyle propsStyle = new GUIStyle();
            propsStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.75f));

            GUILayout.BeginArea(new Rect(m_InspectorWidth + 20, position.height - 390, 390, 380));
            GUILayout.BeginVertical(propsStyle);
            GUILayout.Label("Map Path", EditorStyles.boldLabel);
            GUILayout.Label("Independent paths can share endpoints to form branches.", EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            string pathName = EditorGUILayout.TextField("Name", path.gameObject.name);
            bool closed = EditorGUILayout.Toggle("Close Into Loop", shape.CloseShape);
            GUILayout.Label("Points", EditorStyles.boldLabel);
            int removePoint = -1;
            for (int i = 0; i < shape.Points.Count; ++i)
            {
                GUILayout.BeginHorizontal();
                shape.Points[i] = EditorGUILayout.Vector3Field(i.ToString(), shape.Points[i]);
                if (GUILayout.Button("-", GUILayout.Width(24))) removePoint = i;
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Add Point"))
            {
                Undo.RecordObject(shape, "Add Map Path Point");
                shape.Points.Add(shape.Points.Count > 0 ? shape.Points[shape.Points.Count - 1] : Vector3.zero);
                EditorUtility.SetDirty(shape);
                EditorSceneManager.MarkSceneDirty(path.gameObject.scene);
            }
            if (removePoint >= 0)
            {
                Undo.RecordObject(shape, "Remove Map Path Point");
                shape.Points.RemoveAt(removePoint);
                EditorUtility.SetDirty(shape);
                EditorSceneManager.MarkSceneDirty(path.gameObject.scene);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(path.gameObject, "Edit Map Path");
                Undo.RecordObject(shape, "Edit Map Path");
                path.gameObject.name = pathName;
                shape.CloseShape = closed;
                EditorUtility.SetDirty(path);
                EditorUtility.SetDirty(shape);
                EditorSceneManager.MarkSceneDirty(path.gameObject.scene);
            }

            GUILayout.Space(4);
            if (GUILayout.Button(m_ConnectingPathPoints ? "Cancel Point Connection" : "Connect Existing Points"))
            {
                m_ConnectingPathPoints = !m_ConnectingPathPoints;
                m_ConnectionStartShape = null;
                m_ConnectionStartPoint = -1;
                Repaint();
            }
            if (m_ConnectingPathPoints)
                EditorGUILayout.HelpBox("Click the first point on the map, then click the point to join to it. Selecting this path's first and last point closes it into a loop.", MessageType.Info);
            else
                GUILayout.Label("Close Into Loop joins this path's last point back to its first.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // --------------------------------------------------------------------

        private void DrawSelectedRoomProperties(MapRoom room)
        {
            GUIStyle propsStyle = new GUIStyle();
            propsStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.75f));

            GUILayout.BeginArea(new Rect(m_InspectorWidth + 20, position.height - 190, 300, 200));
            GUILayout.BeginVertical(propsStyle);

            GUILayout.Label("Room", EditorStyles.boldLabel);

            GUILayout.Space(10);

            EditorGUI.BeginChangeCheck();
            room.Name = EditorGUILayout.TextField("Name", room.Name);
            room.Offset = EditorGUILayout.Vector2Field("Offset", room.Offset);
            room.Rotation = EditorGUILayout.FloatField("Rotation", room.Rotation);
            room.ZOrder = EditorGUILayout.FloatField("ZOrder", room.ZOrder);
            if (EditorGUI.EndChangeCheck())
            {
                room.gameObject.name = "MapRoom " + room.Name;
                Undo.RecordObject(room, "Changed room properties");
                EditorUtility.SetDirty(room);
            }
            GUILayout.Label("Details", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("+ Shape"))
            {
                var detail = PrefabUtility.InstantiatePrefab(MapDetailsShapePrefab, room.transform) as GameObject;
                detail.transform.localPosition = Vector3.zero;
                Selection.activeObject = detail;
            }

            if (GUILayout.Button("+ Path"))
            {
                var detail = PrefabUtility.InstantiatePrefab(MapDetailsLinePrefab, room.transform) as GameObject;
                detail.transform.localPosition = Vector3.zero;
                Selection.activeObject = detail;
            }

            if (GUILayout.Button("+ Image"))
            {
                var image = PrefabUtility.InstantiatePrefab(MapImagePrefab, room.transform) as GameObject;
                image.transform.localPosition = Vector3.zero;
                Selection.activeObject = image;
            }
            /* 
            if (GUILayout.Button("+ Stairs"))
            {
                var shape = PrefabUtility.InstantiatePrefab(MapDetailsStairsPrefab) as GameObject;
                shape.transform.SetParent(room.transform);
                shape.transform.localPosition = Vector3.zero;
            }
            if (GUILayout.Button("+ Circle"))
            {
                var shape = PrefabUtility.InstantiatePrefab(MapDetailsStairsPrefab) as GameObject;
                shape.transform.SetParent(room.transform);
                shape.transform.localPosition = Vector3.zero;
            }
            */
            GUILayout.EndHorizontal();
            GUILayout.Label("Paths are independent: add more to create branches, then close any path to make a loop.", EditorStyles.miniLabel);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // --------------------------------------------------------------------

        private void DrawSelectedDoorProperties(MapDoor door)
        {
            GUIStyle propsStyle = new GUIStyle();
            propsStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.75f));

            GUILayout.BeginArea(new Rect(m_InspectorWidth + 20, position.height - 180, 300, 200));
            GUILayout.BeginVertical(propsStyle);

            GUILayout.Label("Door", EditorStyles.boldLabel);

            GUILayout.Space(10);

            EditorGUI.BeginChangeCheck();
            door.Name = EditorGUILayout.TextField("Name", door.Name);
            door.Offset = EditorGUILayout.Vector2Field("Offset", door.Offset);
            door.Size = EditorGUILayout.Vector2Field("Size", door.Size);
            door.Rotation = EditorGUILayout.FloatField("Rotation", door.Rotation);
            door.ZOrder = EditorGUILayout.FloatField("ZOrder", door.ZOrder);
            if (EditorGUI.EndChangeCheck())
            {
                door.gameObject.name = "MapDoor " + door.Name;
                Undo.RecordObject(door, "Changed door properties");
                EditorUtility.SetDirty(door);
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // --------------------------------------------------------------------

        private void DrawInspector(MapController mapCtrl, MapRoom[] rooms, MapDoor[] doors)
        {
            MapData map = mapCtrl.Data;

            GUIStyle inspectorStyle = new GUIStyle();
            inspectorStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.8f));

            GUIStyle detailsStyle = new GUIStyle();
            detailsStyle.normal.background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.15f, 1f));
            detailsStyle.margin = new RectOffset(5, 5, 5, 5);
            detailsStyle.border = new RectOffset(2, 2, 2, 2);

            GUIStyle selectedStyle = new GUIStyle(detailsStyle);
            selectedStyle.normal.background = MakeTex(2, 2, new Color(0f, 1f, 0f, 0.5f));

            GUILayout.BeginArea(new Rect(0, 0, m_InspectorWidth, position.height), inspectorStyle);

            m_ScrollPosInspector = EditorGUILayout.BeginScrollView(m_ScrollPosInspector);

            // ------------- Map selector
            MapController[] mapCtrls = FindObjectsByType<MapController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            GUILayout.BeginVertical();
            GUILayout.Label("Maps", EditorStyles.boldLabel);

            GUILayout.Space(10);

            for (int i = 0; i < mapCtrls.Length; ++i)
            {
                GUILayout.BeginHorizontal(m_SelectedMap == mapCtrls[i] ? selectedStyle : detailsStyle, GUILayout.Height(24));
                GUILayout.Box(ToolWindowIcon, GUILayout.Width(24), GUILayout.Height(16));
                string mapName = mapCtrls[i].Data ? mapCtrls[i].Data.Name : mapCtrls[i].name;

                if (GUILayout.Button(mapName))
                {
                    if (m_SelectedMap != mapCtrls[i])
                    {
                        m_EditingMapDetails = true;
                        m_SelectedMap = mapCtrls[i];
                    }
                    else
                    {
                        m_EditingMapDetails = !m_EditingMapDetails;
                    }
                    Selection.activeObject = mapCtrls[i].gameObject;
                }

                GUILayout.EndHorizontal();


                if (m_SelectedMap == mapCtrls[i] && m_EditingMapDetails)
                {
                    DrawMapDetails(m_SelectedMap, detailsStyle);
                }
            }

            GUILayout.EndVertical();

           

            GUILayout.Space(20);


            // ----------------- Rooms
            GUILayout.BeginVertical();
            GUILayout.Label("Rooms", EditorStyles.boldLabel);

            GUILayout.Space(10);

            for (int i = 0; i < rooms.Length; ++i)
            {
                GUILayout.BeginHorizontal(m_Selected == rooms[i] ? selectedStyle : detailsStyle, GUILayout.Height(24));
                GUILayout.Box(RoomIcon, GUILayout.Width(24), GUILayout.Height(16));
                string roomName = string.IsNullOrEmpty(rooms[i].Name) ? rooms[i].name : rooms[i].Name;

                if (GUILayout.Button(roomName))
                {
                    Selection.activeObject = rooms[i].gameObject;
                    SceneView.lastActiveSceneView.Frame(rooms[i].GetBounds());
                }
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Room"))
            {
                GameObject newRoom = PrefabUtility.InstantiatePrefab(MapRoomPrefab, mapCtrl.transform) as GameObject;
                var objectUniqueId = newRoom.GetComponent<ObjectUniqueId>();
                objectUniqueId?.RegenerateId();
                newRoom.transform.position = Vector3.zero;

                MapRoom room = newRoom.GetComponent<MapRoom>();
                room.Offset = new Vector2(map.CellSize * map.Size.x, map.CellSize * map.Size.y) * 0.5f;

                Selection.activeObject = newRoom;
            }


            GUILayout.EndVertical();

            GUILayout.Space(20);

            // ----------- Chapter 1 power-circuit dots
            PowerPanelController powerController = FindFirstObjectByType<PowerPanelController>();
            // The map selector can briefly clear its selection while changing maps.
            // Do not begin a circuit layout group until there is a valid map to
            // scope it to; otherwise the null exception leaves IMGUI's stack open.
            if (powerController && m_SelectedMap && m_SelectedMap.Data)
            {
                GUILayout.BeginVertical();
                GUILayout.Label("Power Circuits", EditorStyles.boldLabel);
                foreach (PowerCircuitPanelElement circuit in powerController.GetComponentsInChildren<PowerCircuitPanelElement>(true))
                {
                    // Circuits are permanently scoped to the MapData they were created on.
                    // A shared PowerPanelController must never make dots leak to another map.
                    if (circuit.GetMapData() != m_SelectedMap.Data) continue;
                    if (m_CircuitRoomContext && circuit.Room != m_CircuitRoomContext) continue;
                    if (GUILayout.Button(string.IsNullOrEmpty(circuit.Label) ? circuit.CircuitId : circuit.Label))
                        Selection.activeObject = circuit.gameObject;
                }
                if (GUILayout.Button("+ Add Circuit"))
                {
                    MapRoom room = m_CircuitRoomContext;
                    if (!room)
                    {
                        EditorUtility.DisplayDialog("Add Power Circuit", "Select a room first. Circuit dots are anchored to map rooms.", "OK");
                    }
                    else
                    {
                        GameObject dot = new GameObject("Circuit Dot — New circuit");
                        dot.transform.SetParent(powerController.transform);
                        PowerCircuitPanelElement circuit = dot.AddComponent<PowerCircuitPanelElement>();
                        circuit.Label = "New circuit";
                        circuit.MapData = m_SelectedMap.Data;
                        circuit.Room = room;
                        circuit.RoomOffset = Vector2.zero;
                        Undo.RegisterCreatedObjectUndo(dot, "Add Power Circuit");
                        powerController.RefreshAutomaticCircuitId(circuit);
                        powerController.SerializeToData();
                        Selection.activeObject = dot;
                        EditorUtility.SetDirty(powerController);
                        EditorSceneManager.MarkSceneDirty(dot.scene);
                        AssetDatabase.SaveAssets();
                    }
                }
                if (m_CircuitRoomContext && GUILayout.Button("+ Create Circuit Diagram For Room"))
                    CreateRoomCircuitDiagram(m_CircuitRoomContext);
                GUILayout.EndVertical();
                GUILayout.Space(20);
            }


            // ----------- Doors
            GUILayout.BeginVertical();

            GUILayout.Label("Doors", EditorStyles.boldLabel);

            GUILayout.Space(10);

            for (int i = 0; i < doors.Length; ++i)
            {
                GUILayout.BeginHorizontal(m_Selected == doors[i] ? selectedStyle : detailsStyle, GUILayout.Height(24));
                GUILayout.Box(DoorIcon, GUILayout.Width(24), GUILayout.Height(16));

                string doorName = string.IsNullOrEmpty(doors[i].Name) ? doors[i].name : doors[i].Name;
                if (GUILayout.Button(doorName))
                {
                    Selection.activeObject = doors[i].gameObject;
                }
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Door"))
            {
                GameObject newDoor = PrefabUtility.InstantiatePrefab(MapDoorPrefab, mapCtrl.transform) as GameObject;
                var objectUniqueId = newDoor.GetComponent<ObjectUniqueId>();
                objectUniqueId?.RegenerateId();
                newDoor.transform.position = Vector3.zero;
                MapDoor door = newDoor.GetComponent<MapDoor>();
                door.Offset = new Vector2(map.CellSize * map.Size.x, map.CellSize * map.Size.y) * 0.5f;
                Selection.activeObject = newDoor;
            }

            GUILayout.EndVertical();

            EditorGUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void CreateRoomCircuitDiagram(MapRoom room)
        {
            if (!room || !m_SelectedMap || !m_SelectedMap.Data) return;
            if (!room.TryGetComponent(out ObjectUniqueId roomId)) return;
            CircuitDiagramData diagram = ScriptableObject.CreateInstance<CircuitDiagramData>();
            diagram.Map = m_SelectedMap.Data;
            diagram.RoomUniqueId = roomId.Id;
            string roomName = string.IsNullOrWhiteSpace(room.Name) ? room.name : room.Name;
            diagram.Name = new LocalizableText { IsLocalized = false, Unlocalized = roomName + " Circuit Diagram" };
            diagram.ShowPageCount = false;
            diagram.Pages = new System.Collections.Generic.List<DocumentPage>
            {
                new DocumentPage { Text = new LocalizableText { IsLocalized = false, Unlocalized = "Circuit controls for this room have been added to the map." } }
            };
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Game/Levels/Chapter1/Assets/" + roomName.Replace(" ", "_") + "_CircuitDiagram.asset");
            AssetDatabase.CreateAsset(diagram, path);
            DistrictPowerTablet tablet = FindFirstObjectByType<DistrictPowerTablet>();
            if (tablet)
            {
                var diagrams = new System.Collections.Generic.List<CircuitDiagramData>(tablet.CircuitDiagrams ?? System.Array.Empty<CircuitDiagramData>());
                diagrams.Add(diagram);
                tablet.CircuitDiagrams = diagrams.ToArray();
                EditorUtility.SetDirty(tablet);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(m_SelectedMap.gameObject.scene);
            Selection.activeObject = diagram;
        }

        // --------------------------------------------------------------------

        private void DrawMapDetails(MapController mapCtrl, GUIStyle detailsStyle)
        {
            // ------------- Map details
            GUILayout.BeginVertical();
            GUILayout.Label("Map details", EditorStyles.boldLabel);

            GUILayout.BeginVertical(detailsStyle);
            GUILayout.Space(10);

            EditorGUI.BeginChangeCheck();
            MapData map = mapCtrl.Data;
            var mapSerialized = new SerializedObject(map);

            EditorGUILayout.PropertyField(mapSerialized.FindProperty(nameof(MapData.Name)));
            EditorGUILayout.PropertyField(mapSerialized.FindProperty(nameof(MapData.Abbreviation)));
            map.CellSize = EditorGUILayout.IntField("CellSize", map.CellSize);
            map.Size = EditorGUILayout.Vector2IntField("Size", map.Size);
            map.GlobalScale = EditorGUILayout.FloatField("Global Scale", map.GlobalScale);
            map.MapSet = EditorGUILayout.ObjectField("Set", map.MapSet, typeof(MapDataSet), false) as MapDataSet;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(map, "Changed map properties");
                EditorUtility.SetDirty(map);
            }

            if (GUILayout.Button("Save"))
            {
                MapSerializer.SerializeAndSaveMap(mapCtrl);
                EditorUtility.SetDirty(mapCtrl);
                EditorUtility.SetDirty(mapCtrl.Data);
            }
            GUILayout.EndVertical();

            GUILayout.EndVertical();
        }

        // --------------------------------------------------------------------

        private Matrix4x4 GetElementTRS(MapData map, MapElement element)
        {
            Vector3 scale = element.Scale;
            scale.z *= -1;
            return Matrix4x4.TRS(new Vector3(element.Offset.x, 0, element.Offset.y) + new Vector3(m_ScrollPos.x, 0, m_ScrollPos.y), Quaternion.Euler(0, element.Rotation, 0), scale * map.GlobalScale);
        }

        private void DrawPowerCircuits()
        {
            PowerPanelController controller = FindFirstObjectByType<PowerPanelController>();
            if (!controller) return;
            GL.PushMatrix(); GL.LoadPixelMatrix(); GL.Begin(GL.QUADS);
            foreach (PowerCircuitPanelElement circuit in controller.GetComponentsInChildren<PowerCircuitPanelElement>(true))
            {
                if (!m_SelectedMap || circuit.GetMapData() != m_SelectedMap.Data) continue;
                if (m_CircuitRoomContext && circuit.Room != m_CircuitRoomContext) continue;
                Vector3 p;
                if (circuit.Room)
                {
                    Matrix4x4 roomTransform = GetElementTRS(m_SelectedMap.Data, circuit.Room);
                    p = roomTransform.MultiplyPoint(new Vector3(circuit.RoomOffset.x, 0, circuit.RoomOffset.y));
                }
                else p = circuit.transform.localPosition + new Vector3(m_ScrollPos.x, 0, m_ScrollPos.y);
                float radius = 10f;
                GL.Color(Selection.activeObject == circuit.gameObject ? Color.green : Color.yellow);
                GL.Vertex3(p.x - radius, p.z - radius, 0); GL.Vertex3(p.x + radius, p.z - radius, 0);
                GL.Vertex3(p.x + radius, p.z + radius, 0); GL.Vertex3(p.x - radius, p.z + radius, 0);
            }
            GL.End(); GL.PopMatrix();
        }

        private void DrawNavigationIndicators(MapRoom[] rooms)
        {
            // Read the selected controller's hierarchy directly. This makes the
            // editor preview reliable for prefab instances and inactive objects.
            MapNavigationIndicator[] indicators = m_SelectedMap.GetComponentsInChildren<MapNavigationIndicator>(true);
            foreach (MapNavigationIndicator indicator in indicators)
            {
                if (!indicator) continue;
                Shape area = indicator.TriggerArea ? indicator.TriggerArea : indicator.GetComponent<Shape>();
                Color color = indicator.Type == MapNavigationIndicatorType.ObjectiveArrow ? Color.cyan : Color.red;
                string displayText = string.IsNullOrWhiteSpace(indicator.DisplayText)
                    ? (indicator.Type == MapNavigationIndicatorType.ObjectiveArrow ? "Objective" : "Blocked")
                    : indicator.DisplayText;
                if (!area)
                {
                    Matrix4x4 markerTRS = Matrix4x4.TRS(new Vector3(m_ScrollPos.x, 0, m_ScrollPos.y), Quaternion.identity, Vector3.one)
                        * m_SelectedMap.transform.worldToLocalMatrix * indicator.transform.localToWorldMatrix;
                    Vector3 markerPosition = markerTRS.MultiplyPoint(Vector3.zero);
                    Handles.color = color;
                    Handles.DrawSolidDisc(new Vector3(markerPosition.x, markerPosition.z, 0), Vector3.forward, 8f);
                    Handles.Label(new Vector3(markerPosition.x + 10, markerPosition.z, 0), displayText + " (add Shape)");
                    continue;
                }

                MapRoom containingRoom = area.GetComponentInParent<MapRoom>();
                Matrix4x4 shapeTRS;
                if (containingRoom)
                {
                    shapeTRS = GetElementTRS(m_SelectedMap.Data, containingRoom) * containingRoom.transform.worldToLocalMatrix * area.transform.localToWorldMatrix;
                }
                else
                {
                    // Indicators directly under the MapController use their
                    // authored local position on the editor canvas.
                    shapeTRS = Matrix4x4.TRS(new Vector3(m_ScrollPos.x, 0, m_ScrollPos.y), Quaternion.identity, Vector3.one)
                        * m_SelectedMap.transform.worldToLocalMatrix * area.transform.localToWorldMatrix;
                }

                DrawShape(area, shapeTRS, color);
                if (area.Points == null || area.Points.Count == 0) continue;

                Vector3 center = Vector3.zero;
                foreach (Vector3 point in area.Points) center += shapeTRS.MultiplyPoint(point);
                center /= area.Points.Count;
                Handles.color = color;
                Handles.DrawSolidDisc(new Vector3(center.x, center.z, 0), Vector3.forward, 8f);
                Handles.Label(new Vector3(center.x + 10, center.z, 0), displayText);
            }
        }

        private void DrawPathConnectionPoints(MapRoom[] rooms)
        {
            if (!m_ConnectingPathPoints || !m_SelectedMap) return;

            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.QUADS);
            foreach (MapRoom room in rooms)
            {
                if (!room) continue;
                Matrix4x4 roomTRS = GetElementTRS(m_SelectedMap.Data, room);
                Matrix4x4 roomToEditor = GetElementTRS(m_SelectedMap.Data, room);
                foreach (Shape shape in room.GetComponentsInChildren<Shape>(true))
                {
                    if (!shape) continue;
                    Matrix4x4 pathTRS = roomToEditor * room.transform.worldToLocalMatrix * shape.transform.localToWorldMatrix;
                    for (int i = 0; i < shape.Points.Count; ++i)
                    {
                        Vector3 point = pathTRS.MultiplyPoint(shape.Points[i]);
                        bool isStart = shape == m_ConnectionStartShape && i == m_ConnectionStartPoint;
                        GL.Color(isStart ? Color.green : new Color(.15f, .65f, 1f, 1f));
                        const float radius = 8f;
                        GL.Vertex3(point.x - radius, point.z - radius, 0);
                        GL.Vertex3(point.x + radius, point.z - radius, 0);
                        GL.Vertex3(point.x + radius, point.z + radius, 0);
                        GL.Vertex3(point.x - radius, point.z + radius, 0);
                    }
                }
            }
            GL.End();
            GL.PopMatrix();
        }

        private void HandlePathConnectionClick(MapRoom[] rooms)
        {
            if (!m_ConnectingPathPoints || Event.current.type != EventType.MouseDown || Event.current.button != 0) return;
            if (Event.current.mousePosition.x <= m_InspectorWidth) return;

            Shape closestShape = null;
            int closestPoint = -1;
            float closestDistance = 12f * 12f;
            foreach (MapRoom room in rooms)
            {
                if (!room) continue;
                Matrix4x4 roomToEditor = GetElementTRS(m_SelectedMap.Data, room);
                foreach (Shape shape in room.GetComponentsInChildren<Shape>(true))
                {
                    if (!shape) continue;
                    Matrix4x4 pathTRS = roomToEditor * room.transform.worldToLocalMatrix * shape.transform.localToWorldMatrix;
                    for (int i = 0; i < shape.Points.Count; ++i)
                    {
                        Vector3 point = pathTRS.MultiplyPoint(shape.Points[i]);
                        float distance = (new Vector2(point.x, point.z) - Event.current.mousePosition).sqrMagnitude;
                        if (distance >= closestDistance) continue;
                        closestDistance = distance;
                        closestShape = shape;
                        closestPoint = i;
                    }
                }
            }

            if (!closestShape) return;
            if (!m_ConnectionStartShape)
            {
                m_ConnectionStartShape = closestShape;
                m_ConnectionStartPoint = closestPoint;
                Repaint();
                Event.current.Use();
                return;
            }

            ConnectShapePoints(m_ConnectionStartShape, m_ConnectionStartPoint, closestShape, closestPoint);
            m_ConnectionStartShape = null;
            m_ConnectionStartPoint = -1;
            Repaint();
            Event.current.Use();
        }

        private static void ConnectShapePoints(Shape firstShape, int firstPoint, Shape secondShape, int secondPoint)
        {
            if (firstPoint < 0 || firstPoint >= firstShape.Points.Count || secondPoint < 0 || secondPoint >= secondShape.Points.Count) return;
            if (firstShape != secondShape)
            {
                Debug.LogWarning("A single map Shape can only connect points belonging to that same Shape.");
                return;
            }

            // Selecting the two ends of one path is the direct "make loop"
            // operation: the renderer joins them without creating duplicate points.
            bool samePathEnds =
                ((firstPoint == 0 && secondPoint == firstShape.Points.Count - 1) ||
                 (secondPoint == 0 && firstPoint == firstShape.Points.Count - 1));
            if (samePathEnds)
            {
                Undo.RecordObject(firstShape, "Close Map Path Loop");
                firstShape.CloseShape = true;
                EditorUtility.SetDirty(firstShape);
                EditorSceneManager.MarkSceneDirty(firstShape.gameObject.scene);
                return;
            }
            Undo.RecordObject(firstShape, "Connect Map Shape Points");
            if (firstShape.Connections == null) firstShape.Connections = new System.Collections.Generic.List<ShapeEdge>();
            firstShape.Connections.Add(new ShapeEdge { From = firstPoint, To = secondPoint });
            EditorUtility.SetDirty(firstShape);
            EditorSceneManager.MarkSceneDirty(firstShape.gameObject.scene);
        }

        // --------------------------------------------------------------------

        private void DrawDoor(MapController mapCtrl, MapDoor door, bool selected)
        {
            Matrix4x4 t = GetElementTRS(mapCtrl.Data, door);

            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.QUADS);

            Color color = Color.yellow;
            if (door.Door && door.Door.IsLocked())
                color = Color.red;
            else if (!door)
                color = Color.magenta;

            GL.Color(color);

            Vector3 p1 = t.MultiplyPoint(new Vector3(-door.Size.x, 0, -door.Size.y));
            Vector3 p2 = t.MultiplyPoint(new Vector3(door.Size.x, 0, -door.Size.y));
            Vector3 p3 = t.MultiplyPoint(new Vector3(door.Size.x, 0, door.Size.y));
            Vector3 p4 = t.MultiplyPoint(new Vector3(-door.Size.x, 0, door.Size.y));

            GL.Vertex3(p1.x, p1.z, 0);
            GL.Vertex3(p2.x, p2.z, 0);
            GL.Vertex3(p3.x, p3.z, 0);
            GL.Vertex3(p4.x, p4.z, 0);

            GL.End();

            if (selected)
            {
                GL.Begin(GL.LINES);
                GL.Color(Color.green);

                p1 = t.MultiplyPoint(new Vector3(-door.Size.x * 2f, 0, -door.Size.y * 2f));
                p2 = t.MultiplyPoint(new Vector3(door.Size.x * 2f, 0, -door.Size.y * 2f));
                p3 = t.MultiplyPoint(new Vector3(door.Size.x * 2f, 0, door.Size.y * 2f));
                p4 = t.MultiplyPoint(new Vector3(-door.Size.x * 2f, 0, door.Size.y * 2f));

                GL.Vertex3(p1.x, p1.z, 0);
                GL.Vertex3(p2.x, p2.z, 0);

                GL.Vertex3(p2.x, p2.z, 0);
                GL.Vertex3(p3.x, p3.z, 0);

                GL.Vertex3(p3.x, p3.z, 0);
                GL.Vertex3(p4.x, p4.z, 0);

                GL.Vertex3(p4.x, p4.z, 0);
                GL.Vertex3(p1.x, p1.z, 0);

                GL.End();
            }


            GL.PopMatrix();

        }

        // --------------------------------------------------------------------

        private void DrawRoom(MapController mapCtrl, MapRoom room, bool selected)
        {
            Matrix4x4 trs = GetElementTRS(mapCtrl.Data, room);

            DrawShape(room.GetComponent<Shape>(), trs, selected ? Color.green : Color.white);

            DrawDetails(mapCtrl, room);
            //DrawImages(mapCtrl, room);

            if (selected)
            {
                // Draw Arrow
                DrawArrow(trs);

                // Linked elements
                GL.Begin(GL.LINES);
                GL.Color(Color.cyan);
                foreach (var elm in room.LinkedElements)
                {
                    if (elm)
                    {
                        Vector3 p1 = trs.MultiplyPoint(Vector3.zero);
                        Matrix4x4 elementT = GetElementTRS(mapCtrl.Data, elm);
                        Vector3 p2 = elementT.MultiplyPoint(Vector3.zero);
                        GL.Vertex3(p1.x, p1.z, 0);
                        GL.Vertex3(p2.x, p2.z, 0);
                    }
                }

                GL.End();

                GL.Begin(GL.QUADS);
                GL.Color(Color.cyan);

                // Completion steps
                foreach (var step in room.CompletionSteps)
                {
                    var localPos = room.transform.InverseTransformPoint(step.transform.position);
                    var pos = trs.MultiplyPoint(localPos);

                    float size = 5f;
                    Vector3 p1 = new Vector3(-size, 0, -size);
                    Vector3 p2 = new Vector3(size, 0, -size);
                    Vector3 p3 = new Vector3(size, 0, size);
                    Vector3 p4 = new Vector3(-size, 0, size);

                    GL.Vertex3(pos.x + p1.x, pos.z + p1.z, 0);
                    GL.Vertex3(pos.x + p2.x, pos.z + p2.z, 0);
                    GL.Vertex3(pos.x + p3.x, pos.z + p3.z, 0);
                    GL.Vertex3(pos.x + p4.x, pos.z + p4.z, 0);
                }

                GL.End();
            }

        }

        // --------------------------------------------------------------------

        private void DrawArrow(Matrix4x4 trs)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.white);

            Vector3 arrowStart = trs.MultiplyPoint(Vector3.zero);
            Vector3 arrowL = trs.MultiplyPoint(Vector3.left + Vector3.forward * 0.5f);
            Vector3 arrowR = trs.MultiplyPoint(Vector3.right + Vector3.forward * 0.5f);
            Vector3 arrowEnd = trs.MultiplyPoint(Vector3.forward * 2);
            GL.Vertex3(arrowStart.x, arrowStart.z, 0);
            GL.Vertex3(arrowEnd.x, arrowEnd.z, 0);
            GL.Vertex3(arrowL.x, arrowL.z, 0);
            GL.Vertex3(arrowEnd.x, arrowEnd.z, 0);
            GL.Vertex3(arrowR.x, arrowR.z, 0);
            GL.Vertex3(arrowEnd.x, arrowEnd.z, 0);

            GL.End();

            GL.Begin(GL.QUADS);
            GL.Color(Color.cyan);
        }

        // --------------------------------------------------------------------

        private void DrawShape(Shape shape, Matrix4x4 trs, Color color)
        {
            Vector3[] points = new Vector3[shape.Points.Count + (shape.CloseShape ? 1 : 0)];
            for (int i = 0; i < shape.Points.Count; i++)
            {
                Vector3 position = trs.MultiplyPoint(shape.Points[i]);
                points[i] = new Vector3(position.x, position.z, 0);
            }

            if (shape.Points.Count >= 3 && shape.CloseShape)
            {
                Vector3 p2 = trs.MultiplyPoint(shape.Points[0]);
                points[points.Length - 1] = new Vector3(p2.x, p2.z, 0);
            }

            Handles.color = color;
            Handles.DrawAAPolyLine(
                        Texture2D.whiteTexture,
                        3,
                        points);
        }

        // --------------------------------------------------------------------

        private void DrawDetails(MapController mapCtrl, MapRoom room)
        {
            Matrix4x4 trs = GetElementTRS(mapCtrl.Data, room);

            MapDetailingShape[] detailing = room.GetComponentsInChildren<MapDetailingShape>();
            foreach (var details in detailing)
            {
                var dTRS = Matrix4x4.TRS(new Vector3(details.transform.localPosition.x, 0, details.transform.localPosition.z), details.transform.localRotation, details.transform.localScale);
                DrawShape(details.GetComponent<Shape>(), trs * dTRS, Color.grey);
            }
        }

        // --------------------------------------------------------------------

        private void DrawImages(MapController mapCtrl, MapRoom room)
        {
            Matrix4x4 trs = GetElementTRS(mapCtrl.Data, room);

            MapImage[] images = room.GetComponentsInChildren<MapImage>();
            foreach (var image in images)
            {
                var dTRS = trs * Matrix4x4.TRS(new Vector3(image.transform.localPosition.x, 0, image.transform.localPosition.z), image.transform.localRotation, image.transform.localScale);
                Vector3 pos = dTRS.MultiplyPoint(new Vector3(0, 0, 0));

                float w = dTRS.lossyScale.x;
                float h = dTRS.lossyScale.z;

                // TODO - This does not support rotation atm :(
                Handles.BeginGUI();
                GUI.DrawTexture(new Rect(pos.x + w * 0.5f, pos.z - h * 0.5f, -w, h), image.Texture, ScaleMode.ScaleAndCrop, true, 1, Color.white, 0, 0);
                GUI.color = Color.white;
                Handles.EndGUI();
            }
        }

        // --------------------------------------------------------------------

        private void DrawGrid(int countX, int countY, int cellSize, Color cellColor)
        {
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.LINES);
            for (int x = 0; x < countX; x++)
            {
                for (int y = 0; y < countY; y++)
                {
                    DrawGridBox(m_ScrollPos.x + x * cellSize, m_ScrollPos.y + y * cellSize, cellSize, cellSize, cellColor);
                }
            }

            // Mid lines
            Color midColor = Color.cyan;
            midColor.a = 0.5f;
            GL.Color(midColor);
            GL.Vertex3(m_ScrollPos.x + cellSize * countX * 0.5f, m_ScrollPos.y, 0);
            GL.Vertex3(m_ScrollPos.x + cellSize * countX * 0.5f, m_ScrollPos.y + cellSize * countY, 0);

            GL.Vertex3(m_ScrollPos.x, m_ScrollPos.y + cellSize * countY * 0.5f, 0);
            GL.Vertex3(m_ScrollPos.x + cellSize * countX, m_ScrollPos.y + cellSize * countY * 0.5f, 0);

            GL.End();
            GL.PopMatrix();
        }

        // --------------------------------------------------------------------

        private void DrawGridBox(float x, float y, float width, float height, Color color)
        {
            GL.Color(color);

            GL.Vertex3(x, y, 0);
            GL.Vertex3(x + width, y, 0);

            GL.Vertex3(x + width, y, 0);
            GL.Vertex3(x + width, y + height, 0);

            GL.Vertex3(x + width, y + height, 0);
            GL.Vertex3(x, y + height, 0);

            GL.Vertex3(x, y + height, 0);
            GL.Vertex3(x, y, 0);
        }

        // --------------------------------------------------------------------

        private Texture2D MakeTex(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // --------------------------------------------------------------------

    }
}
