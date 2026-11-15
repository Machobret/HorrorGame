using System;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    // Ordinary presses commit immediately. Alt shares its button with Scene view orbit.
    internal sealed class PointPlacement
    {
        SceneView view;
        Vector2 pressMouse, point;
        bool alt, finish;
        int control;

        internal void Cancel()
        {
            if(view && !alt && GUIUtility.hotControl==control)GUIUtility.hotControl=0;
            view=null;
        }

        internal void Press(SceneView scene,Event e,int id,Vector2 position,bool finishes,Action<Vector2,bool> place)
        {
            if(e.button!=0 || e.rawType!=EventType.MouseDown)return;
            if(!e.alt && (e.type!=EventType.MouseDown || Tools.viewToolActive || GUIUtility.hotControl!=0 || HandleUtility.nearestControl!=id))return;
            view=scene;control=id;pressMouse=e.mousePosition;point=position;finish=finishes;alt=e.alt;
            if(alt)return;
            GUIUtility.hotControl=id;
            e.Use();
            place(position,finishes);
        }

        internal bool Track(SceneView scene,Event e,Action<Vector2,bool> place)
        {
            if(!view || view!=scene)return false;
            var type=e.rawType;
            if(type==EventType.Ignore || type==EventType.MouseLeaveWindow || type==EventType.MouseDown && e.button!=0)
            {
                Cancel();
                return false;
            }
            if(e.button!=0 || (type!=EventType.MouseDrag && type!=EventType.MouseUp))return false;
            if(alt && (e.mousePosition-pressMouse).sqrMagnitude>=16)
            {
                Cancel(); // Crossing the orbit threshold is final, even if the pointer comes back.
                return true;
            }
            if(type==EventType.MouseUp)
            {
                bool placeOnRelease=alt;
                Cancel();
                if(placeOnRelease)place(point,finish);
                else if(e.type!=EventType.Used)e.Use();
                return true;
            }
            if(!alt)scene.Repaint();
            return alt;
        }
    }
}
