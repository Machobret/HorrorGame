using System;
using System.Collections.Generic;
using System.Linq;
using HorrorEngine;
using HouseToolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorGame.Chapter1.Editor
{
    /// <summary>Projects the integrated level's playable floors and courtyard into the tablet coordinate system.</summary>
    public static class IntegratedTabletMapBuilder
    {
        private const string ScenePath = "Assets/Game/Levels/Chapter1/Scenes/1stLevel_Integrated.unity";
        private const string Maps = "Assets/Game/Levels/Chapter1/MapData/";
        private const string Assets = "Assets/Game/Levels/Chapter1/Assets/";
        private const string Databases = "Assets/AssetStore/HorrorEngine/ExampleContent/Databases/";
        private static readonly Vector2 MapOrigin = new(400f, 650f);

        // Local file IDs identify the authored ground-floor HouseRoom components in this scene.
        // Multiple floors in a gameplay discovery region retain that region's existing room ID.
        private static readonly (string Name, ulong[] Floors)[] StoreRegions = {
            ("Store", new ulong[] { 635049019 })
        };
        private static readonly (string Name, ulong[] Floors)[] StorageRegions = {
            ("Storage Unit Office", new ulong[] { 2031259627, 1859519065 }),
            ("Storage Unit Entrance", new ulong[] { 1653452306, 698938099, 1879970349 }),
            ("Storage Unit Thin Hall", new ulong[] { 558345558, 1082708985, 444736075, 185209208,
                620870821, 1851683430, 1567501598, 225721534, 575854359, 1569886508, 2061610432 }),
            ("Storage Unit Big Hall", new ulong[] { 485682043, 1927736934, 1530352041 }),
            ("Storage Unit Shelves", new ulong[] { 907678891, 1620561197 })
        };

        [MenuItem("Horror Game/Chapter 1/Rebuild Integrated Tablet Maps")]
        public static void Rebuild()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("Open 1stLevel_Integrated before rebuilding its tablet maps.");
            Build(scene);
        }

        public static void RebuildBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use the menu from the integrated scene.");
            Build(EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single));
        }

        private static void Build(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var controllers = Components<MapController>(roots).ToDictionary(controller => controller.name);
            var street = controllers["District 7 Street"];
            var storage = controllers["District 7 Storage"];
            NormalizeMapCoordinates(street);
            NormalizeMapCoordinates(storage);
            var streetRooms = street.GetComponentsInChildren<MapRoom>(true).ToDictionary(room => room.Name);
            var storageRooms = storage.GetComponentsInChildren<MapRoom>(true).ToDictionary(room => room.Name);
            var houseRooms = Components<HouseRoom>(roots).ToDictionary(LocalId);
            var panel = Components<PowerPanelController>(roots).Single();
            var dots = panel.GetComponentsInChildren<PowerCircuitPanelElement>(true);
            var physicalTargets = dots.ToDictionary(dot => dot, dot => dot.LinkedTargets.FirstOrDefault(target => target));
            foreach (var pair in physicalTargets)
                if (!pair.Key.Room || !pair.Value)
                    throw new InvalidOperationException($"Circuit {pair.Key.CircuitId} needs its authored room and physical target.");

            var streetMap = MapVariant(Maps + "District 7.asset", Maps + "DistrictStreet_IntegratedMap.asset",
                "5fba2770-ece1-4b74-9515-e870b4bc2739");
            streetMap.Name.Unlocalized = "Courtyard Approach";
            streetMap.Abbreviation.Unlocalized = "CA";
            var storageMap = MapVariant(Maps + "District Storage.asset", Maps + "DistrictStorage_IntegratedMap.asset",
                "126d6fc2-9883-47f1-8bf7-5c8a55016061");
            var oldLayout = Load<PowerPanelLayout>(Maps + "District Storage_Circuits.asset");
            var layout = Copy<PowerPanelLayout>(Maps + "District Storage_Circuits.asset", Maps + "DistrictStorage_IntegratedCircuits.asset");
            var oldDiagram = Load<CircuitDiagramData>(Assets + "Storage_Unit_Entrance_CircuitDiagram.asset");
            var diagram = Copy<CircuitDiagramData>(Assets + "Storage_Unit_Entrance_CircuitDiagram.asset",
                Maps + "DistrictStorage_IntegratedEntranceDiagram.asset");
            diagram.UniqueId = "88bf679f-dd23-47cb-8971-2dc09e6f807a";
            diagram.Map = storageMap;
            EditorUtility.SetDirty(diagram);
            var replacements = new Dictionary<UnityEngine.Object, UnityEngine.Object> {
                { Load<MapData>(Maps + "District 7.asset"), streetMap },
                { Load<MapData>(Maps + "District7.asset"), streetMap },
                { Load<MapData>(Maps + "District Storage.asset"), storageMap },
                { oldLayout, layout },
                { oldDiagram, diagram }
            };
            SwitchSceneReferences(roots, replacements);
            SetControllerId(street, "bde58325-1180-4d28-a280-98ea94708261");
            SetControllerId(storage, "cebbdc2c-1b4e-4d01-b53a-5dac20572231");

            ApplyFloorRegions(streetMap, streetRooms, houseRooms, StoreRegions);
            ApplyFloorRegions(storageMap, storageRooms, houseRooms, StorageRegions);

            var storeFloor = FloorOutline(houseRooms[635049019]);
            float storeEast = storeFloor.Max(point => point.x);
            float storeNorth = storeFloor.Max(point => point.z);
            var frontFloor = FloorOutline(houseRooms[1620561197]);
            float storageNorth = frontFloor.Max(point => point.z);
            float storageWest = frontFloor.Min(point => point.x);
            var opening = Components<Chapter1Opening>(roots).SingleOrDefault();
            // Share the room boundary so the first tablet view includes the space in front of its pickup.
            float alleyBoundary = opening ? storageNorth + .45f : storageNorth;
            SetOutlines(streetRooms["Street"], streetMap, new List<Vector3[]> { Outline(
                (storeEast, storeNorth), (storageWest, storeNorth),
                (storageWest, alleyBoundary), (storeEast, alleyBoundary)) });

            // Keep discovery inside the playable courtyard and snap its building edges to the real walls.
            const float yardWest = 1.25f;
            const float yardSouth = -33.87f;
            const float yardNorth = -21.07f;
            var office = FloorOutline(houseRooms[2031259627]);
            float officeWest = office.Min(point => point.x);
            float officeNorth = office.Max(point => point.z);
            SetOutlines(storageRooms["Outside"], storageMap, new List<Vector3[]> { Outline(
                (storeEast, alleyBoundary), (storageWest, alleyBoundary), (storageWest, officeNorth),
                (officeWest, officeNorth), (officeWest, yardSouth),
                (yardWest, yardSouth), (yardWest, yardNorth), (storeEast, yardNorth)) });

            // The old hand-drawn wall marks were duplicates of the now-projected floor boundaries.
            foreach (var detail in storage.GetComponentsInChildren<MapDetailingShape>(true))
                Undo.DestroyObjectImmediate(detail.gameObject);

            var oldAutoSerialize = panel.AutoSerializeChildren;
            panel.AutoSerializeChildren = false;
            foreach (var pair in physicalTargets)
            {
                var dot = pair.Key;
                var target = pair.Value;
                Vector3 local = dot.Room.transform.InverseTransformPoint(GetCircuitAnchor(target));
                dot.RoomOffset = new Vector2(local.x, local.z);
                dot.MapData = dot.Room.GetComponentInParent<MapController>().Data;
                Record(dot);
            }
            layout.CanvasSize = panel.CanvasSize;
            layout.Circuits.Clear();
            foreach (var dot in dots) layout.Circuits.Add(dot.ToShape(storageMap));
            panel.AutoSerializeChildren = oldAutoSerialize;
            Record(panel);
            EditorUtility.SetDirty(layout);
            MapSerializer.SerializeAndSaveMap(street);
            MapSerializer.SerializeAndSaveMap(storage);

            AddRegister(Load<MapDatabase>(Databases + "MapDB.asset"), streetMap);
            AddRegister(Load<MapDatabase>(Databases + "MapDB.asset"), storageMap);
            AddRegister(Load<DocumentDatabase>(Databases + "DocumentDB.asset"), diagram);
            Verify(street, storage, physicalTargets);
            Vector3 insideStoreFront = new((storeEast + storeFloor.Min(point => point.x)) * .5f, 0f,
                storeFloor.Max(point => point.z) - .2f);
            if (!streetRooms["Store"].ContainsWorldPosition(insideStoreFront) ||
                streetRooms["Street"].ContainsWorldPosition(insideStoreFront))
                throw new InvalidOperationException("The courtyard approach and store overlap across the storefront.");
            if (streetRooms["Street"].ContainsWorldPosition(new Vector3(8f, 0f, 1.38f)) ||
                storageRooms["Outside"].ContainsWorldPosition(new Vector3(5.42f, 0f, -44.75f)))
                throw new InvalidOperationException("The tablet map includes roads beyond the playable courtyard.");
            var outside = storageRooms["Outside"];
            if (!outside.ContainsWorldPosition(new Vector3(12.42f, 0f, -24.96f)))
                throw new InvalidOperationException("The courtyard map misses the depot approach.");
            if (opening && !outside.ContainsWorldPosition(opening.transform.position))
                throw new InvalidOperationException("The alley electrical box is outside the playable map.");
            if (opening && !outside.ContainsWorldPosition(new Vector3(11.9f, 0f, -18.35f)))
                throw new InvalidOperationException("The alley tablet pickup point is outside the initial map.");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the integrated scene.");
            AssetDatabase.SaveAssets();
            Debug.Log($"INTEGRATED_TABLET_MAP_OK {streetMap.Rooms.Count + storageMap.Rooms.Count} rooms, {dots.Length} circuit anchors");
        }

        private static void ApplyFloorRegions(MapData map, Dictionary<string, MapRoom> mapRooms,
            Dictionary<ulong, HouseRoom> floors, (string Name, ulong[] Floors)[] regions)
        {
            foreach (var (name, sources) in regions)
                SetOutlines(mapRooms[name], map, sources.Select(id => FloorOutline(floors[id])).ToList());
        }

        private static Vector3[] FloorOutline(HouseRoom room) =>
            room.polygon.Select(point => room.transform.TransformPoint(new Vector3(point.x, 0f, point.y))).ToArray();

        private static Vector3[] Outline(params (float X, float Z)[] points) =>
            points.Select(point => new Vector3(point.X, 0f, point.Z)).ToArray();

        private static void SetOutlines(MapRoom room, MapData map, List<Vector3[]> outlines)
        {
            room.Scale = Vector3.one;
            room.Rotation = 90f;
            room.Offset = Project(room.transform.position, map);
            Record(room);
            Shape[] shapes = room.GetComponents<Shape>();
            for (int i = 0; i < outlines.Count; i++)
            {
                Shape shape = i < shapes.Length ? shapes[i] : Undo.AddComponent<Shape>(room.gameObject);
                shape.Points = outlines[i].Select(point =>
                {
                    Vector3 local = room.transform.InverseTransformPoint(point);
                    return new Vector3(local.x, 0f, local.z);
                }).ToList();
                shape.CloseShape = true;
                shape.Connections.Clear();
                Record(shape);
            }
            for (int i = outlines.Count; i < shapes.Length; i++) Undo.DestroyObjectImmediate(shapes[i]);
        }

        private static Vector2 Project(Vector3 point, MapData map) =>
            MapOrigin + map.GlobalScale * new Vector2(-point.z, -point.x);

        private static Vector3 GetCircuitAnchor(CircuitDrivenObject target)
        {
            if (target is Chapter1Opening opening && opening.AlleyPowerBox)
                return opening.AlleyPowerBox.position;
            if (target.TryGetComponent(out GarageDoor door) && door.leaf)
                return door.leaf.bounds.center;
            if (target is CircuitShutterTeleport shutter && shutter.ExitPoint && shutter.OtherExitPoint)
                return (shutter.ExitPoint.position + shutter.OtherExitPoint.position) * .5f;
            return target.transform.position;
        }

        private static void SwitchSceneReferences(GameObject[] roots, Dictionary<UnityEngine.Object, UnityEngine.Object> replacements)
        {
            foreach (var component in Components<MonoBehaviour>(roots))
            {
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference ||
                        !property.objectReferenceValue || !replacements.TryGetValue(property.objectReferenceValue, out var replacement))
                        continue;
                    property.objectReferenceValue = replacement;
                    changed = true;
                }
                if (changed)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    Record(component);
                }
            }
        }

        private static void Verify(MapController street, MapController storage,
            Dictionary<PowerCircuitPanelElement, CircuitDrivenObject> targets)
        {
            foreach (var controller in new[] { street, storage })
            foreach (var room in controller.GetComponentsInChildren<MapRoom>(true))
            {
                var matrix = controller.Data.GetTRS(room.Offset, room.Rotation, room.Scale, room.ZOrder);
                Shape[] shapes = room.GetComponents<Shape>();
                foreach (var shape in shapes)
                {
                    foreach (var local in shape.Points)
                    {
                        Vector3 projected = matrix.MultiplyPoint(local);
                        var expected = Project(room.transform.TransformPoint(local), controller.Data);
                        if (Vector2.Distance(new Vector2(projected.x, -projected.z), expected) > .02f)
                            throw new InvalidOperationException($"World projection mismatch in {room.Name}.");
                    }
                    var polygon = new CompositeShape(new[] { shape });
                    Mesh mesh = polygon.GetMesh();
                    UnityEngine.Object.DestroyImmediate(mesh);
                    if (polygon.triangles.Length < 3)
                        throw new InvalidOperationException($"Invalid map polygon in {room.Name}.");
                    Vector3 interior = (polygon.vertices[polygon.triangles[0]] + polygon.vertices[polygon.triangles[1]] +
                        polygon.vertices[polygon.triangles[2]]) / 3f;
                    if (!room.ContainsWorldPosition(room.transform.TransformPoint(interior)))
                        throw new InvalidOperationException($"World room lookup missed a polygon in {room.Name}.");
                }
            }
            foreach (var pair in targets)
            {
                var map = pair.Key.GetMapData();
                var shape = pair.Key.ToShape(map);
                Vector3 anchor = GetCircuitAnchor(pair.Value);
                Vector3 delta = shape.WorldPosition - anchor;
                if (new Vector2(delta.x, delta.z).sqrMagnitude > .0001f ||
                    Vector2.Distance(shape.MapPosition, Project(anchor, map)) > .02f)
                    throw new InvalidOperationException($"World circuit mismatch for {pair.Key.CircuitId}.");
            }
        }

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) is T asset && asset ? asset :
                throw new InvalidOperationException($"Required asset missing at {path}");

        private static T Copy<T>(string original, string destination) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(destination)) return Load<T>(destination);
            if (!AssetDatabase.CopyAsset(original, destination))
                throw new InvalidOperationException($"Could not copy {original} to {destination}");
            return Load<T>(destination);
        }

        private static MapData MapVariant(string original, string destination, string id)
        {
            var map = Copy<MapData>(original, destination);
            map.UniqueId = id;
            EditorUtility.SetDirty(map);
            return map;
        }

        private static void SetControllerId(MapController controller, string id)
        {
            var identity = controller.GetComponent<ObjectUniqueId>();
            identity.SetId(id);
            Record(identity);
        }

        private static void NormalizeMapCoordinates(MapController controller)
        {
            Undo.RecordObject(controller.transform, "Align tablet map to world");
            controller.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            controller.transform.localScale = Vector3.one;
            Record(controller.transform);
        }

        private static void AddRegister<T>(RegisterDatabase<T> database, T entry) where T : Register
        {
            if (database.Registers.Contains(entry)) return;
            database.Registers.Add(entry);
            EditorUtility.SetDirty(database);
        }

        private static IEnumerable<T> Components<T>(GameObject[] roots) where T : Component =>
            roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).Where(component => component);

        private static ulong LocalId(Component component) => GlobalObjectId.GetGlobalObjectIdSlow(component).targetObjectId;

        private static void Record(UnityEngine.Object item)
        {
            EditorUtility.SetDirty(item);
            if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
        }
    }
}
