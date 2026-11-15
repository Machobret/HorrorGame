using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HouseToolkit.Editor
{
    [InitializeOnLoad]
    static class FamilyMaterials
    {
        static FamilyMaterials()
        {
            EditorSceneManager.sceneOpened += (scene, mode) => Refresh(scene, checkLayout: true);
            Undo.undoRedoPerformed += RefreshAll;
        }
        public static void RefreshAll()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++) Refresh(SceneManager.GetSceneAt(i));
        }
        static void Refresh(Scene scene, bool checkLayout = false)
        {
            if (!scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var wall in root.GetComponentsInChildren<HouseWall>(true))
                    {
                        if (checkLayout && !EditorApplication.isPlayingOrWillChangePlaymode && !wall.boundaryOnly && wall.Family && wall.Family.IsComplex)
                        {
                            var output = wall.GetComponentInChildren<WallOutput>(true);
                            if (output && output.builtPanelLayout != wall.panelLayout)
                            {
                                if (WallBuilder.Rebuild(wall, out var issue)) EditorSceneManager.MarkSceneDirty(scene);
                                else Debug.LogWarning(issue, wall);
                            }
                        }
                        WallBuilder.ApplyMaterials(wall);
                    }
                    foreach (var slab in root.GetComponentsInChildren<HouseSlab>(true)) slab.Refresh();
                    foreach (var joint in root.GetComponentsInChildren<CornerJoint>(true)) joint.Refresh();
                    if (checkLayout && !EditorApplication.isPlayingOrWillChangePlaymode)
                        foreach (var house in root.GetComponentsInChildren<House>(true)) BooleanOutput.Upgrade(house);
                }
        }
    }

    [CustomEditor(typeof(WallFamily))]
    class FamilyInspector : UnityEditor.Editor
    {
        UnityEditorInternal.ReorderableList panelList;
        void OnEnable()
        {
            var panels = serializedObject.FindProperty("panels");
            panelList = new UnityEditorInternal.ReorderableList(serializedObject, panels, true, true, true, true);
            panelList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Repeating panels");
            panelList.drawElementCallback = (rect, index, active, focused) =>
            {
                rect.height = EditorGUIUtility.singleLineHeight;
                rect.y += 1;
                EditorGUI.PropertyField(rect, panels.GetArrayElementAtIndex(index),
                    new GUIContent(index == 0 ? "Primary panel" : $"Panel {index + 1}"));
            };
        }
        public override void OnInspectorGUI()
        {
            int group=Undo.GetCurrentGroup();
            serializedObject.Update();
            var f=(WallFamily)target;
            var previousLayout = f.defaultPanelLayout;
            EditorGUILayout.LabelField(f.IsComplex?"Complex wall family":"Plain wall family",EditorStyles.boldLabel);
            void Field(string name)=>EditorGUILayout.PropertyField(serializedObject.FindProperty(name),true);
            if(f.IsComplex)
            {
                Field("defaultPanelLayout");
                panelList.DoLayoutList();Field("surrounds");Field("cornerPost");
                if(!f.panel)EditorGUILayout.HelpBox("Assign a repeating wall segment.",MessageType.Info);
                if(f.panel && !f.panel.mesh)Thickness();
            }
            else Thickness();
            void Thickness()=>EditorGUILayout.PropertyField(serializedObject.FindProperty("plainThickness"),new GUIContent("Thickness (m)"));
            var surface = serializedObject.FindProperty("surface");
            EditorGUI.BeginChangeCheck();
            var finish = FinishPicker.Draw(f.IsComplex ? "Frame material" : "Default finish", surface.objectReferenceValue as Material);
            if (EditorGUI.EndChangeCheck()) surface.objectReferenceValue = finish;
            Field("surfaceFinishes");
            if(f.IsComplex){Field("glass");Field("glassFinishes");}
            Field("defaultInserts");
            if(!serializedObject.ApplyModifiedProperties())return;
            var floors=Object.FindObjectsByType<HouseFloor>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(f=>f.House && f.GetComponentsInChildren<HouseWall>(true).Any(w=>w.Family==target)).ToArray();
            foreach (var floor in floors)
                if (!new FloorEdit(floor).Prepare(out var issue))
                {
                    Undo.FlushUndoRecordObjects();
                    Undo.RevertAllDownToGroup(group);
                    HouseScene.Message=issue; HouseScene.Repaint();
                    Debug.LogWarning(issue,floor);
                    return;
                }
            foreach (var wall in Object.FindObjectsByType<HouseWall>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if (wall.Family == f && (f.defaultPanelLayout == previousLayout ||
                    wall.panelLayoutOverride == PanelLayoutOverride.Inherit))
                    WallBuilder.Rebuild(wall,out _);
            foreach (var floor in floors) WallJunctions.Refresh(floor,out _);
            Undo.CollapseUndoOperations(group);
            FamilyMaterials.RefreshAll();
        }
    }

    [CustomEditor(typeof(HouseFloor))]
    class FloorInspector : UnityEditor.Editor
    {
        string issue;
        public override void OnInspectorGUI()
        {
            var floor = (HouseFloor)target;
            if (GUILayout.Button(floor.EditLabel)) HouseScene.Enter(floor);
            if (floor.House) { if (GUILayout.Button(HouseProperties.WindowTitle)) HouseProperties.Open(); return; }
            EditorGUI.BeginChangeCheck();
            float h = EditorGUILayout.DelayedFloatField("Floor height (m)", floor.height);
            if (EditorGUI.EndChangeCheck()) WallBuilder.SetHeight(floor, h, out issue);
            EditorGUI.BeginChangeCheck();
            var family = (WallFamily)EditorGUILayout.ObjectField("Default family", floor.defaultFamily, typeof(WallFamily), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(floor, "Change floor family");
                var previous = floor.defaultFamily;
                floor.defaultFamily = family;
                if (!WallBuilder.SetHeight(floor, floor.height, out issue)) floor.defaultFamily = previous;
            }
            if (!string.IsNullOrEmpty(issue)) EditorGUILayout.HelpBox(issue, MessageType.Warning);
        }
    }

    [CustomEditor(typeof(HouseWall))]
    class WallInspector : UnityEditor.Editor
    {
        OpeningKind kind;
        HouseInsert chosen;
        float distance, sill = 1;
        string issue;
        void OnEnable() { distance = ((HouseWall)target).length / 2; }

        public override void OnInspectorGUI()
        {
            var wall = (HouseWall)target;
            var floor = wall.Floor;
            if (floor && floor.House)
            {
                if (GUILayout.Button("Edit Wall")) { HouseScene.Enter(floor); HouseScene.SelectWall(wall); HouseProperties.Open(); }
                return;
            }
            if (!floor) { EditorGUILayout.HelpBox("Parent this wall to a house floor.", MessageType.Info); return; }
            EditorGUI.BeginChangeCheck();
            float length = EditorGUILayout.DelayedFloatField("Length (m)", wall.length);
            var family = (WallFamily)EditorGUILayout.ObjectField("Family override", wall.familyOverride, typeof(WallFamily), false);
            var layout = HouseProperties.PanelLayoutField(wall, family, wall.boundaryOnly);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(wall, "Edit wall");
                var oldLayout = wall.panelLayoutOverride;
                wall.panelLayoutOverride = layout;
                var oldLength = wall.length;
                var oldFamily = wall.familyOverride;
                wall.length = length;
                wall.familyOverride = family;
                if (!WallBuilder.Rebuild(wall, out issue)) { wall.length = oldLength; wall.familyOverride = oldFamily; wall.panelLayoutOverride = oldLayout; }
            }
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("surfaceOverride"), new GUIContent(wall.Family && wall.Family.panel ? "Frame override" : "Surface override"));
            if (wall.Family && wall.Family.panel) EditorGUILayout.PropertyField(serializedObject.FindProperty("glassOverride"));
            if (serializedObject.ApplyModifiedProperties()) WallBuilder.ApplyMaterials(wall);
            if (!wall.Family) return;

            EditorGUILayout.Space();
            kind = (OpeningKind)EditorGUILayout.EnumPopup("Place opening", kind);
            distance = EditorGUILayout.FloatField("Distance along wall (m)", distance);
            if (kind == OpeningKind.Window && !wall.Family.panel) sill = EditorGUILayout.FloatField("Sill height (m)", sill);
            var defaults = wall.Family.defaultInserts;
            var items = OpeningPalette.Choices(wall, kind);
            if (items.Count > 0)
            {
                int index = Mathf.Max(0, items.IndexOf(chosen));
                index = EditorGUILayout.Popup("Insert", index, items.Select(i => i.name + (defaults.Contains(i) ? " (family default)" : "")).ToArray());
                chosen = items[index];
                float bottom = kind == OpeningKind.Door ? 0 : sill;
                var candidate = new PlacedOpening {insert = chosen, distance = distance, sill = bottom};
                bool fits = wall.TryLayout(floor.height, new List<FittedOpening>(), out string fitIssue, candidate) &&
                    WallJunctions.CheckOpening(wall,new List<PlacedOpening>(wall.openings){candidate},out fitIssue);
                using (new EditorGUI.DisabledScope(!fits))
                    if (GUILayout.Button("Place " + kind.ToString().ToLowerInvariant())) WallBuilder.Place(wall, chosen, distance, bottom, out issue);
                if (!fits) EditorGUILayout.HelpBox(fitIssue, MessageType.Info);
            }
            else EditorGUILayout.HelpBox("No matching insert for this wall family.", MessageType.Info);
            foreach (var entry in wall.openings.ToArray())
                if (GUILayout.Button("Remove " + entry.insert.name + " at " + entry.distance.ToString("0.##") + " m"))
                {
                    Undo.RecordObject(wall, "Remove opening");
                    wall.openings.Remove(entry);
                    WallBuilder.Rebuild(wall, out issue);
                }
            if (!string.IsNullOrEmpty(issue)) EditorGUILayout.HelpBox(issue, MessageType.Warning);
        }
    }
}
