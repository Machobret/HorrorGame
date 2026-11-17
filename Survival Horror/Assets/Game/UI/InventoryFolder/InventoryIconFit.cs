using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    /// <summary>Fit visible sprite pixels, rather than transparent padding, to an inventory icon box.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class InventoryIconFit : BaseMeshEffect
    {
        public InventoryIconProfile Profile;
        [Range(.1f,1)] public float Fill=.84f;
        public override void ModifyMesh(VertexHelper mesh)
        {
            var image=graphic as Image;
            if(!IsActive() || !image || !image.sprite || !Profile || mesh.currentVertCount!=4)return;
            UIVertex vertex=default;
            var entry=Profile.GetEntry(image.sprite);var visible=entry.Visible;
            var box=image.rectTransform.rect;
            Vector2 fullSize=box.size;
            float aspect=image.sprite.rect.width/image.sprite.rect.height;
            if(fullSize.x/fullSize.y>aspect)fullSize.x=fullSize.y*aspect;else fullSize.y=fullSize.x/aspect;
            bool wide=entry.Wide && box.width/box.height>1.8f;float pixelsToLocal=fullSize.x/image.sprite.rect.width;
            Vector2 visibleSize=wide?entry.WideBounds.size*pixelsToLocal:Vector2.Scale(fullSize,visible.size);
            if(visibleSize.x<.001f || visibleSize.y<.001f)return;
            float scale=Mathf.Min(box.width/visibleSize.x,box.height/visibleSize.y)*Fill;
            Vector2 center=wide?box.center+entry.WideBounds.center*pixelsToLocal:box.center+Vector2.Scale(fullSize,visible.center-new Vector2(.5f,.5f));
            for(int i=0;i<4;i++){mesh.PopulateUIVertex(ref vertex,i);Vector2 original=vertex.position;if(wide)original=(Vector2)(Quaternion.Euler(0,0,entry.Rotation)*(original-box.center))+box.center;Vector2 p=(original-center)*scale+box.center;vertex.position=new Vector3(p.x,p.y,vertex.position.z);mesh.SetUIVertex(vertex,i);}
        }
    }
}
