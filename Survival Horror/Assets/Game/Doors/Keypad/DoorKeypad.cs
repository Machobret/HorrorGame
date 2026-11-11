using HorrorEngine;
using TMPro;
using UnityEngine;

namespace HorrorGame.Doors
{
    public sealed class DoorKeypad : MonoBehaviour
    {
        public SequencePuzzle Puzzle;
        public CameraCloseup View;
        public Interaction3DContext Input;
        public Interactive Approach;
        public DoorLock DoorLock;
        public TMP_Text Display;
        public AudioSource Audio;
        public AudioClip SuccessSound;
        bool opened;

        void Awake()
        {
            Puzzle.OnSolved.AddListener(Solved);
            Puzzle.OnLoadedSolved.AddListener(LoadedSolved);
            Input.OnCancel.AddListener(Close);
        }
        System.Collections.IEnumerator Start()
        {
            // Saved state can load before the template display initializes.
            yield return null;
            if(Puzzle.IsSolved)LoadedSolved();
        }
        public void Open()
        {
            if(opened || Puzzle.IsSolved)return;
            Puzzle.Clear();View.Activate();Input.Activate();opened=true;
        }
        public void Close()
        {
            if(!opened)return;
            Input.Deactivate();View.Deactivate();opened=false;
            if(!Puzzle.IsSolved)Puzzle.Clear();
        }
        void Solved()
        {
            LoadedSolved();
            if(SuccessSound&&Audio)Audio.PlayOneShot(SuccessSound);
            GetComponent<SavableObjectState>().SaveState();
            Close();
        }
        void LoadedSolved()
        {
            if(DoorLock&&DoorLock.IsLocked)DoorLock.SetLocked(false);
            if(Display){Display.text="OPEN";Display.color=new Color(.25f,1,.4f);}
            if(Approach)Approach.gameObject.SetActive(false);
        }
    }
}
