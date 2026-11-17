using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    public enum TabletSymbolKind { Player,LockedDoor,UnlockedDoor,Unexplored,PowerPoint }

    /// <summary>The map overlay and legend use the same glyph geometry and colours.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TabletMapSymbol : MaskableGraphic
    {
        public TabletSymbolKind Kind;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();Draw(mesh,Kind,rectTransform.rect.center,Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.34f);
        }
        public static Color SymbolColor(TabletSymbolKind kind)=>kind==TabletSymbolKind.LockedDoor?new Color(.95f,.66f,.43f):kind==TabletSymbolKind.Player?new Color(.90f,.97f,1):new Color(.65f,.84f,.88f);
        public static void Draw(VertexHelper mesh,TabletSymbolKind kind,Vector2 p,float size)
        {
            Color color=SymbolColor(kind);float width=Mathf.Max(1.5f,size*.20f);
            if(kind==TabletSymbolKind.Player)
            {
                int i=mesh.currentVertCount;mesh.AddVert(p+new Vector2(0,size),color,Vector2.zero);mesh.AddVert(p+new Vector2(-size*.75f,-size*.65f),color,Vector2.zero);mesh.AddVert(p+new Vector2(size*.75f,-size*.65f),color,Vector2.zero);mesh.AddTriangle(i,i+1,i+2);return;
            }
            if(kind==TabletSymbolKind.Unexplored)
            {
                Line(mesh,p+Vector2.up*size,p+Vector2.right*size,width,color);Line(mesh,p+Vector2.right*size,p-Vector2.up*size,width,color);Line(mesh,p-Vector2.up*size,p-Vector2.right*size,width,color);Line(mesh,p-Vector2.right*size,p+Vector2.up*size,width,color);return;
            }
            Line(mesh,p+new Vector2(-size,-size),p+new Vector2(size,-size),width,color);Line(mesh,p+new Vector2(-size,size),p+new Vector2(size,size),width,color);Line(mesh,p+new Vector2(-size,-size),p+new Vector2(-size,size),width,color);
            if(kind==TabletSymbolKind.UnlockedDoor)
            {
                Line(mesh,p+new Vector2(size,-size),p+new Vector2(size,-size*.5f),width,color);Line(mesh,p+new Vector2(size,size*.5f),p+new Vector2(size,size),width,color);return;
            }
            Line(mesh,p+new Vector2(size,-size),p+new Vector2(size,size),width,color);
            float inner=size*.5f;
            if(kind==TabletSymbolKind.LockedDoor){Line(mesh,p+new Vector2(-inner,-inner),p+new Vector2(inner,inner),width,color);Line(mesh,p+new Vector2(-inner,inner),p+new Vector2(inner,-inner),width,color);}
            else{Line(mesh,p+new Vector2(-inner,0),p+new Vector2(inner,0),width,color);Line(mesh,p+new Vector2(0,-inner),p+new Vector2(0,inner),width,color);}
        }
        static void Line(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color color){var normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int i=mesh.currentVertCount;mesh.AddVert(a-normal,color,Vector2.zero);mesh.AddVert(a+normal,color,Vector2.zero);mesh.AddVert(b+normal,color,Vector2.zero);mesh.AddVert(b-normal,color,Vector2.zero);mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);}
    }
}
