using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal sealed class SceneSelection<T> where T : struct
    {
        readonly Func<T,T,bool> same;
        readonly Func<T,string> kind;
        readonly List<T> items = new List<T>();
        internal IReadOnlyList<T> Items => items;
        internal T Active => items.Count==0 ? default : items[items.Count-1];
        internal bool Has(T item) => items.Any(i=>same(i,item));
        internal SceneSelection(Func<T,T,bool> same,Func<T,string> kind) { this.same=same; this.kind=kind; }
        internal void Pick(T item,bool additive)
        {
            if (!additive) items.Clear();
            if (kind(item)==null) return;
            int index=items.FindIndex(i=>same(i,item));
            if(index>=0) items.RemoveAt(index); else items.Add(item);
        }
        internal void RemoveWhere(Predicate<T> remove) => items.RemoveAll(remove);
        internal void Filter(string type,bool keep) => items.RemoveAll(i=>(kind(i)==type)!=keep);
        internal bool Mixed => items.Count>1 && items.Any(i=>kind(i)!=kind(Active));
        internal bool DrawFilter(Action changed)
        {
            if(!Mixed) return false;
            foreach(var group in items.GroupBy(kind).ToArray())
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(group.Key+" ("+group.Count()+")",GUILayout.MinWidth(45));
                bool keep=GUILayout.Button("Keep only",GUILayout.Width(72));
                bool discard=GUILayout.Button("Discard",GUILayout.Width(64));
                EditorGUILayout.EndHorizontal();
                if(keep || discard) { Filter(group.Key,keep);changed();GUIUtility.ExitGUI(); }
            }
            return true;
        }
    }

    internal static class MultiField
    {
        internal static void Draw<T,V>(IEnumerable<T> targets, Func<T,V> read, Func<V,V> field, Action<V> apply)
        {
            var values=targets.Select(read).ToArray();
            if(values.Length==0)return;
            bool previous=EditorGUI.showMixedValue;
            EditorGUI.showMixedValue=values.Any(v=>!EqualityComparer<V>.Default.Equals(v,values[0]));
            EditorGUI.BeginChangeCheck();
            V value=field(values[0]);
            bool changed=EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue=previous;
            if(changed) apply(value);
        }
    }
}
