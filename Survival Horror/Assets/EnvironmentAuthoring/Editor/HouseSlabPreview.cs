using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Rendering only: the session owns the candidate, validity and gesture lifetime.
    // Thickness edits keep footprints fixed, so triangulate each panel once and scale
    // its unit-depth mesh between the candidate's current slab surfaces while drawing.
    internal sealed class HouseSlabPreview : IDisposable
    {
        private readonly HouseEditPreview preview;
        private readonly Matrix4x4 housePose;
        private readonly HouseSceneFeedback feedback = new HouseSceneFeedback();
        private readonly List<Panel> panels = new List<Panel>();
        private sealed class Panel
        {
            internal Mesh Mesh;
            internal string Lower, Upper;
        }
        internal HouseSlabPreview(HouseEditPreview preview, IReadOnlyList<HouseSlabLayout.Patch> patches)
        {
            this.preview = preview;
            housePose = preview.SourceBuilding.transform.localToWorldMatrix;
            try
            {
                foreach (var patch in patches)
                {
                    var shape = SlabGeometry.VisibleShape(patch);
                    var mesh = SlabGeometry.Extrude(shape, 0, 1, Vector2.one);
                    if (mesh != null) panels.Add(new Panel { Mesh = mesh, Lower = patch.Lower?.Id,
                        Upper = patch.Upper?.Id });
                }
            }
            catch { Dispose(); throw; }
        }
        internal void Draw(SceneView view)
        {
            if (Event.current.type != EventType.Repaint) return;
            var candidate = preview.Building;
            foreach (var panel in panels)
            {
                float top = HouseVerticalLayout.BoundaryTop(candidate, candidate.Find(panel.Lower), candidate.Find(panel.Upper));
                var slab = HouseVerticalLayout.SlabAt(candidate, top);
                var matrix = housePose * Matrix4x4.TRS(Vector3.up * slab.Bottom,
                    Quaternion.identity, new Vector3(1, candidate.SlabThickness, 1));
                feedback.DrawMeshHighlight(panel.Mesh, matrix, new Color(.3f, .7f, 1), .3f);
            }
        }
        public void Dispose()
        {
            foreach (var panel in panels) if (panel.Mesh != null) UnityEngine.Object.DestroyImmediate(panel.Mesh);
            panels.Clear();
            feedback.Dispose();
        }
    }
}
