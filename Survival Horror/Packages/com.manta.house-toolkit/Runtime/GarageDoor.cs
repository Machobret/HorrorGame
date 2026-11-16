using System.Collections;
using UnityEngine;

namespace HouseToolkit
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GarageDoor : MonoBehaviour
    {
        public Transform leftGuide, rightGuide, header;
        public MeshRenderer leaf;
        [Min(.01f)] public float speed = 2;
        [SerializeField, HideInInspector] Vector2 clearSize = new Vector2(2.8f,2.4f);
        [SerializeField, HideInInspector] int tile;
        [SerializeField, HideInInspector] bool operable;
        public bool Operable => operable;
        public int FinishTile => tile;
        public bool IsOpen => heightRaised >= clearSize.y;
        static readonly int tileId = Shader.PropertyToID("_AtlasTile"), visibleId = Shader.PropertyToID("_VisibleHeight");
        MaterialPropertyBlock block;
        float heightRaised;
        bool requestedOpen;
        Coroutine travel;

        public bool Configure(Vector2 size,int finish,bool canOpen)
        {
            if(clearSize==size && tile==finish && operable==canOpen)return false;
            #if UNITY_EDITOR
            if(!Application.IsPlaying(gameObject))UnityEditor.Undo.RecordObject(this,"Configure garage door");
            #endif
            clearSize=size;tile=finish;operable=canOpen;
            Refresh();
            return true;
        }
        void OnEnable() => Refresh();
#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall-=RefreshAfterEdit;
            UnityEditor.EditorApplication.delayCall+=RefreshAfterEdit;
        }
        void RefreshAfterEdit(){if(this && isActiveAndEnabled && !Application.IsPlaying(gameObject))Refresh();}
#endif
        void OnDisable()
        {
            if(travel!=null)StopCoroutine(travel);
            travel=null;
        }
        void Refresh()
        {
            if(!leaf || !header || !leftGuide || !rightGuide)return;
            float w=clearSize.x,h=clearSize.y,j=GarageDoorInsert.Jamb,top=GarageDoorInsert.Header;
            leftGuide.localPosition=new Vector3(-(w+j)*.5f,h*.5f,0);
            rightGuide.localPosition=new Vector3((w+j)*.5f,h*.5f,0);
            leftGuide.localScale=rightGuide.localScale=new Vector3(j,h,.13f);
            header.localPosition=new Vector3(0,h+top*.5f,0);
            header.localScale=new Vector3(w+2*j,top,.18f);
            heightRaised=operable ? Mathf.Clamp(heightRaised,0,h) : 0;
            Pose();
            if(Application.IsPlaying(gameObject) && operable && travel==null && heightRaised!=(requestedOpen?h:0))SetOpen(requestedOpen);
        }
        public void SetOpen(bool open)
        {
            if(!operable || !Application.IsPlaying(gameObject))return;
            requestedOpen=open;
            if(isActiveAndEnabled && travel==null && heightRaised!=(open?clearSize.y:0))
                travel=StartCoroutine(Move());
        }
        IEnumerator Move()
        {
            // Defer the first step so even an instant move completes after the coroutine is assigned.
            yield return null;
            while(heightRaised!=(requestedOpen?clearSize.y:0))
            {
                heightRaised=Mathf.MoveTowards(heightRaised,requestedOpen?clearSize.y:0,Mathf.Max(.01f,speed)*Time.deltaTime);
                Pose();
                yield return null;
            }
            travel=null;
        }
        void Pose()
        {
            float remaining=clearSize.y-heightRaised;
            leaf.gameObject.SetActive(remaining>0);
            leaf.transform.localPosition=Vector3.up*heightRaised;
            leaf.transform.localScale=new Vector3(clearSize.x,Mathf.Max(.00001f,remaining),.05f);
            block??=new MaterialPropertyBlock();
            leaf.GetPropertyBlock(block);
            block.SetFloat(tileId,tile);
            block.SetFloat(visibleId,remaining/clearSize.y);
            leaf.SetPropertyBlock(block);
        }
    }
}
