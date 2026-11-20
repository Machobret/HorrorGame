using System.Collections;
using UnityEngine;

namespace HouseToolkit
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GarageDoor : MonoBehaviour
    {
        public Transform leftGuide, rightGuide, header;
        public MeshRenderer leaf;
        [SerializeField] Transform leafAttachment;
        [Min(.01f)] public float speed = 2;
        [SerializeField, HideInInspector] Vector2 clearSize = new Vector2(2.8f,2.4f);
        [SerializeField, HideInInspector] int tile;
        [SerializeField, HideInInspector] bool operable;
        public bool Operable => operable;
        public int FinishTile => tile;
        public bool IsOpen => heightRaised >= clearSize.y;
        public bool IsClosed => heightRaised <= 0f;
        static readonly int tileId = Shader.PropertyToID("_AtlasTile");
        MaterialPropertyBlock block;
        float heightRaised;
        bool requestedOpen;
        bool motorPowered = true;
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
            leaf.gameObject.SetActive(true);
            leaf.transform.localScale=new Vector3(w,h,.05f);
            heightRaised=operable ? Mathf.Clamp(heightRaised,0,h) : 0;
            UpdateLeafPosition();
            block??=new MaterialPropertyBlock();
            leaf.GetPropertyBlock(block);
            block.SetFloat(tileId,tile);
            leaf.SetPropertyBlock(block);
            if(Application.IsPlaying(gameObject) && operable && travel==null && heightRaised!=(requestedOpen?h:0))SetOpen(requestedOpen);
        }
        private void UpdateLeafPosition()
        {
            var position = Vector3.up * heightRaised;
            leaf.transform.localPosition = position;
            if (leafAttachment) leafAttachment.localPosition = position;
        }

        public void SetOpen(bool open) => SetOpen(open, false);

        public void SetOpen(bool open, bool animateWhilePaused)
        {
            if(!operable || !Application.IsPlaying(gameObject))return;
            requestedOpen=open;
            if(motorPowered && isActiveAndEnabled && travel==null && heightRaised!=(open?clearSize.y:0))
                travel=StartCoroutine(Move(animateWhilePaused));
        }

        public void RestoreOpen(bool open)
        {
            if (!operable || !leaf) return;
            if (travel != null) StopCoroutine(travel);
            travel = null;
            requestedOpen = open;
            heightRaised = open ? clearSize.y : 0f;
            UpdateLeafPosition();
        }

        public void SetMotorPowered(bool powered)
        {
            if (motorPowered == powered) return;
            motorPowered = powered;
            if (!powered && travel != null)
            {
                StopCoroutine(travel);
                travel = null;
            }
        }
        IEnumerator Move(bool animateWhilePaused)
        {
            // Defer the first step so even an instant move completes after the coroutine is assigned.
            yield return null;
            while(heightRaised!=(requestedOpen?clearSize.y:0))
            {
                float delta = animateWhilePaused ? Time.unscaledDeltaTime : Time.deltaTime;
                heightRaised=Mathf.MoveTowards(heightRaised,requestedOpen?clearSize.y:0,Mathf.Max(.01f,speed)*delta);
                UpdateLeafPosition();
                yield return null;
            }
            travel=null;
        }
    }
}
