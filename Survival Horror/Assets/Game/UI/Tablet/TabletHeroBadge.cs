using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    /// <summary>Flat service-agency shield and star, drawn as UI geometry.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TabletHeroBadge : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();var rect=GetPixelAdjustedRect();Vector2 centre=rect.center;
            float scale=Mathf.Min(rect.width,rect.height)*.5f;
            var shield=new[]{new Vector2(-.75f,.85f),new Vector2(.75f,.85f),new Vector2(.65f,-.3f),new Vector2(0,-.95f),new Vector2(-.65f,-.3f)};
            for(int i=0;i<shield.Length;i++)
            {
                int j=(i+1)%shield.Length;int start=helper.currentVertCount;
                foreach(var p in new[]{shield[i],shield[j],shield[j]*.91f,shield[i]*.91f})helper.AddVert(centre+p*scale,color,Vector2.zero);
                helper.AddTriangle(start,start+1,start+2);helper.AddTriangle(start,start+2,start+3);
            }
            int origin=helper.currentVertCount;helper.AddVert(centre+Vector2.up*scale*.08f,color,Vector2.zero);
            for(int i=0;i<10;i++)
            {
                float angle=(90-i*36)*Mathf.Deg2Rad;float radius=i%2==0?.48f:.21f;
                helper.AddVert(centre+(new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius+Vector2.up*.08f)*scale,color,Vector2.zero);
            }
            for(int i=0;i<10;i++)helper.AddTriangle(origin,origin+1+i,origin+1+(i+1)%10);
        }
    }
}
