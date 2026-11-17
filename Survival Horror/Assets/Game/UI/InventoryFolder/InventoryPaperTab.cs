using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    public sealed class InventoryPaperTab : MaskableGraphic
    {
        public bool Corner;
        public Texture PaperTexture;
        public override Texture mainTexture => PaperTexture ? PaperTexture : Texture2D.whiteTexture;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;float cut=Mathf.Min(12,r.width*.1f);
            if(Corner){mesh.AddVert(new Vector2(r.xMin,r.yMax),color,Vector2.zero);mesh.AddVert(new Vector2(r.xMax,r.yMax),color,Vector2.zero);mesh.AddVert(new Vector2(r.xMax,r.yMin),color,Vector2.zero);mesh.AddTriangle(0,1,2);return;}
            Vector2[] points={new Vector2(r.xMin,r.yMin),new Vector2(r.xMin,r.yMax-cut),new Vector2(r.xMin+cut,r.yMax),new Vector2(r.xMax-cut,r.yMax),new Vector2(r.xMax,r.yMax-cut),new Vector2(r.xMax,r.yMin)};
            foreach(var point in points)mesh.AddVert(point,color,new Vector2(.12f+.20f*(point.x-r.xMin)/r.width,.3f+.25f*(point.y-r.yMin)/r.height));
            for(int i=1;i<5;i++)mesh.AddTriangle(0,i,i+1);
        }
    }
}
