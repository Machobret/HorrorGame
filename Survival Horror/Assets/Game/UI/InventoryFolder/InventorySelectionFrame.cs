using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    /// <summary>A hollow selection outline with heavier corner brackets; never covers the item icon.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InventorySelectionFrame : MaskableGraphic
    {
        [Min(.5f)] public float LineWidth=1.1f;
        [Min(.5f)] public float CornerWidth=2.6f;
        [Min(2)] public float CornerLength=10;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;float inset=1;
            float left=r.xMin+inset,right=r.xMax-inset,bottom=r.yMin+inset,top=r.yMax-inset;
            Quad(mesh,left,bottom,right,bottom+LineWidth);Quad(mesh,left,top-LineWidth,right,top);
            Quad(mesh,left,bottom,left+LineWidth,top);Quad(mesh,right-LineWidth,bottom,right,top);
            float length=Mathf.Min(CornerLength,Mathf.Min(right-left,top-bottom)*.25f);
            foreach(bool upper in new[]{false,true})foreach(bool rightSide in new[]{false,true})
            {
                float x=rightSide?right:left,y=upper?top:bottom;
                Quad(mesh,rightSide?x-length:x,upper?y-CornerWidth:y,rightSide?x:x+length,upper?y:y+CornerWidth);
                Quad(mesh,rightSide?x-CornerWidth:x,upper?y-length:y,rightSide?x:x+CornerWidth,upper?y:y+length);
            }
        }
        void Quad(VertexHelper mesh,float left,float bottom,float right,float top)
        {
            int start=mesh.currentVertCount;mesh.AddVert(new Vector2(left,bottom),color,Vector2.zero);mesh.AddVert(new Vector2(left,top),color,Vector2.zero);mesh.AddVert(new Vector2(right,top),color,Vector2.zero);mesh.AddVert(new Vector2(right,bottom),color,Vector2.zero);mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
