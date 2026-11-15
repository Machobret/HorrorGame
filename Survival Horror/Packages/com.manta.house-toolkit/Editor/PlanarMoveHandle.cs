using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class PlanarMoveHandle
    {
        internal static Vector3 Draw(Vector3 pivot, Quaternion rotation)
        {
            float size=HandleUtility.GetHandleSize(pivot)*.72f;
            var x=rotation*Vector3.right;
            var z=rotation*Vector3.forward;
            var moved=pivot;
            using(new Handles.DrawingScope(Handles.xAxisColor))
                moved+=Handles.Slider(pivot,x,size,Handles.ArrowHandleCap,0)-pivot;
            using(new Handles.DrawingScope(Handles.zAxisColor))
                moved+=Handles.Slider(pivot,z,size,Handles.ArrowHandleCap,0)-pivot;
            using(new Handles.DrawingScope(Color.yellow))
                moved+=Handles.Slider2D(pivot,rotation*Vector3.up,x,z,size*.13f,Handles.RectangleHandleCap,Vector2.zero)-pivot;
            return moved;
        }
    }
}
