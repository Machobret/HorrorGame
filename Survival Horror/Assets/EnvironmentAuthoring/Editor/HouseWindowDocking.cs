using System;
using System.Reflection;
using UnityEditor;

namespace EnvironmentAuthoring.Editor
{
    // Unity exposes ShowTab, but not a dock group's active pane. Keep that optional,
    // version-dependent read here; authoring must work even when it is unavailable.
    [Serializable]
    internal sealed class HouseWindowDocking
    {
        private static readonly FieldInfo Parent = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic);
        [UnityEngine.SerializeField] private EditorWindow previous;
        [UnityEngine.SerializeField] private UnityEngine.Object group;
        [UnityEngine.SerializeField] private bool captured;

        internal static bool TryReadGroup(EditorWindow window, out UnityEngine.Object dock, out EditorWindow visible)
        {
            dock = null; visible = null;
            if (window == null || !window.docked || Parent == null) return false;
            try
            {
                dock = Parent.GetValue(window) as UnityEngine.Object;
                if (dock == null || dock.GetType().FullName != "UnityEditor.DockArea") return false;
                var active = dock.GetType().GetProperty("actualView", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                visible = active?.GetValue(dock) as EditorWindow;
                return visible != null;
            }
            catch (Exception error) when (error is ArgumentException || error is TargetException || error is TargetInvocationException || error is MemberAccessException)
            { dock = null; visible = null; return false; }
        }

        internal void CaptureOnce(EditorWindow house)
        {
            if (captured) return;
            captured = true;
            previous = null; group = null;
            if (TryReadGroup(house, out var dock, out var visible) && visible != house)
            { previous = visible; group = dock; }
        }

        internal void Restore(EditorWindow house)
        {
            captured = false;
            var target = previous;
            previous = null;
            if (target != null && group != null
                && TryReadGroup(house, out var houseGroup, out var visible) && houseGroup == group && visible == house
                && TryReadGroup(target, out var targetGroup, out _) && targetGroup == group)
                target.ShowTab();
            group = null;
        }
    }
}
