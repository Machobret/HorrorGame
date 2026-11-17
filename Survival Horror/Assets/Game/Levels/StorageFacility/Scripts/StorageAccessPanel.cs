using HorrorEngine;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame.StorageFacility
{
    /// <summary>A small scene-authored adapter between native interactions and persistent access flags.</summary>
    [RequireComponent(typeof(Interactive),typeof(FlaggedState))]
    public sealed class StorageAccessPanel : MonoBehaviour
    {
        public FlaggedState[] Prerequisites=System.Array.Empty<FlaggedState>();
        public GameObject[] ReleasedBarriers=System.Array.Empty<GameObject>();
        public TMP_Text Status;
        public string CompletedText="ACCESS GRANTED";
        public DialogData MissingRequirement;
        public DialogData Information;
        public UnityEvent OnCompleted=new UnityEvent();
        public FlaggedState State { get; private set; }
        public bool CanUse
        {
            get{foreach(var state in Prerequisites)if(!state||!state.IsFlagged)return false;return true;}
        }
        void Awake()
        {
            State=GetComponent<FlaggedState>();
            GetComponent<Interactive>().OnInteract.AddListener(_=>Use());
            State.OnFlag.AddListener(ApplyCompleted);
            State.OnLoadedFlagged.AddListener(ApplyCompleted);
        }
        public void Use()
        {
            if(!CanUse){if(MissingRequirement.IsValid())UIManager.Get<UIDialog>().Show(MissingRequirement);return;}
            if(!State.IsFlagged)
            {
                State.Flag();GetComponent<SavableObjectState>().SaveState();OnCompleted.Invoke();
            }
            if(Information.IsValid())UIManager.Get<UIDialog>().Show(Information);
        }
        void ApplyCompleted()
        {
            foreach(var barrier in ReleasedBarriers)if(barrier)barrier.SetActive(false);
            if(Status){Status.text=CompletedText;Status.color=new Color(.45f,.9f,.66f);}
        }
    }
}
