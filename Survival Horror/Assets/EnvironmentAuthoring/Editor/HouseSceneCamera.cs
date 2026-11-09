using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Temporary camera state belongs to this editing context and each individual view.
    internal sealed class HouseSceneCamera
    {
        private struct PreviousView
        {
            internal Quaternion RelativeRotation;
            internal bool Orthographic;
        }
        private readonly Dictionary<SceneView, PreviousView> previous = new Dictionary<SceneView, PreviousView>();

        internal bool IsTop(SceneView view) => view != null && previous.ContainsKey(view);

        private static float Heading(Quaternion rotation)
        {
            // Right stays horizontal in top view, unlike forward, whose heading is undefined.
            var right = rotation * Vector3.right;
            return Mathf.Atan2(-right.z, right.x) * Mathf.Rad2Deg;
        }

        internal void Toggle(SceneView view)
        {
            if (view == null) return;
            float heading = Heading(view.rotation);
            if (previous.TryGetValue(view, out var saved))
            {
                view.orthographic = saved.Orthographic;
                view.LookAtDirect(view.pivot, Quaternion.Euler(0, heading, 0) * saved.RelativeRotation, view.size);
                previous.Remove(view);
            }
            else
            {
                previous.Add(view, new PreviousView { Orthographic = view.orthographic,
                    RelativeRotation = Quaternion.Inverse(Quaternion.Euler(0, heading, 0)) * view.rotation });
                view.orthographic = true;
                view.LookAtDirect(view.pivot, Quaternion.Euler(90, heading, 0), view.size);
            }
            view.Repaint();
        }

        internal void Restore()
        {
            foreach (var entry in new List<SceneView>(previous.Keys))
                if (entry != null) Toggle(entry);
            previous.Clear();
        }
    }
}
