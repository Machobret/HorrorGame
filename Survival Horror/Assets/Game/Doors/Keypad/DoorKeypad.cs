using HorrorEngine;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

namespace HorrorGame.Doors
{
    [DefaultExecutionOrder(10000)]
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
        bool ready, navigationReleased;
        float nextNavigation, acceptInputAfter;
        CinemachineCamera cameraView;
        CinemachineBrain brain;
        int cameraOverride = -1;
        bool savedIgnoreTimeScale;
        IUIInput uiInput;
        Interactive[] keys;
        Coroutine opening;
        readonly HashSet<Key> heldKeys=new HashSet<Key>();
        readonly HashSet<Key> pressedKeys=new HashSet<Key>();
        static readonly string[] Grid={"Number1","Number2","Number3","Number4","Number5","Number6","Number7","Number8","Number9","Clear","Number0","Backspace"};
        static readonly Key[] Digits={Key.Digit0,Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5,Key.Digit6,Key.Digit7,Key.Digit8,Key.Digit9};
        static readonly Key[] Numpad={Key.Numpad0,Key.Numpad1,Key.Numpad2,Key.Numpad3,Key.Numpad4,Key.Numpad5,Key.Numpad6,Key.Numpad7,Key.Numpad8,Key.Numpad9};

        void Awake()
        {
            Puzzle.OnSolved.AddListener(Solved);
            Puzzle.OnLoadedSolved.AddListener(LoadedSolved);
            Input.OnCancel.AddListener(Close);
            Input.ExternalInput=true;
            keys=Input.GetComponentsInChildren<Interactive>(true);
            cameraView=View.GetComponentInChildren<CinemachineCamera>(true);
            // The door is the only world prompt. The panel's buttons are active only in close-up.
            if(Approach)Approach.gameObject.SetActive(false);
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
            opened=true;ready=false;navigationReleased=false;
            heldKeys.Clear();SampleKeyboard(Keyboard.current);pressedKeys.Clear();
            Puzzle.Clear();
            uiInput=UIManager.Instance.GetComponent<IUIInput>();
            View.Activate();opening=StartCoroutine(EnableAfterCamera());
        }
        public void Close()
        {
            if(!opened)return;
            if(opening!=null){StopCoroutine(opening);opening=null;}
            ready=false;
            Input.Deactivate();View.Deactivate();opened=false;
            if(!Puzzle.IsSolved)Puzzle.Clear();
        }
        IEnumerator EnableAfterCamera()
        {
            // Do not accept the opening interaction/submit again while the camera is fading in.
            while(opened && !cameraView.gameObject.activeSelf)yield return null;
            if(!opened)yield break;
            Input.Activate();
            yield return null;
            ready=true;acceptInputAfter=Time.unscaledTime+.15f;opening=null;
        }

        void Update()
        {
            if(!opened)return;
            var keyboard=Keyboard.current;
            SampleKeyboard(keyboard);
            if(!ready || Time.unscaledTime<acceptInputAfter)return;
            if(Down(Key.Escape)||uiInput.IsCancelDown()){Close();return;}
            if(keyboard!=null)
            {
                for(int digit=0;digit<10;digit++)
                    if(Down(Digits[digit])||Down(Numpad[digit]))
                    {Press("Number"+digit);return;}
                if(Down(Key.Backspace)){Press("Backspace");return;}
                if(Down(Key.Delete)){Press("Clear");return;}
            }
            Vector2 axis=uiInput.GetPrimaryAxis();
            // Screen-space up/right, independent of the render texture's flipped projection.
            if(keyboard!=null)
            {
                Vector2 arrows=new Vector2((keyboard.rightArrowKey.isPressed||keyboard.dKey.isPressed?1:0)-(keyboard.leftArrowKey.isPressed||keyboard.aKey.isPressed?1:0),
                    (keyboard.upArrowKey.isPressed||keyboard.wKey.isPressed?1:0)-(keyboard.downArrowKey.isPressed||keyboard.sKey.isPressed?1:0));
                if(arrows!=Vector2.zero)axis=arrows;
            }
            if(axis.sqrMagnitude<.25f)navigationReleased=true;
            else if(navigationReleased||Time.unscaledTime>=nextNavigation)
            {
                Navigate(axis);nextNavigation=Time.unscaledTime+(navigationReleased?.3f:.13f);navigationReleased=false;
            }
            if(uiInput.IsConfirmDown()||Down(Key.Enter)||Down(Key.NumpadEnter))Input.PressSelected();
        }

        void SampleKeyboard(Keyboard keyboard)
        {
            // Panel-local edges also work while game time is paused; holding a digit never repeats it.
            pressedKeys.Clear();
            if(keyboard==null){heldKeys.Clear();return;}
            foreach(var key in keyboard.allKeys)
            {
                if(key.isPressed){if(heldKeys.Add(key.keyCode))pressedKeys.Add(key.keyCode);}
                else heldKeys.Remove(key.keyCode);
            }
        }
        bool Down(Key key)=>pressedKeys.Contains(key);

        public void Navigate(Vector2 direction)
        {
            int index=System.Array.IndexOf(Grid,Input.Selected?Input.Selected.name:"Number5");
            if(index<0)index=4;
            int row=index/3,column=index%3;
            if(Mathf.Abs(direction.x)>Mathf.Abs(direction.y))column=Mathf.Clamp(column+(direction.x>0?1:-1),0,2);
            else row=Mathf.Clamp(row+(direction.y>0?-1:1),0,3);
            var target=keys.FirstOrDefault(key=>key.name==Grid[row*3+column]);
            if(target)Input.Select(target);
        }
        public void Press(string name)
        {
            if(!opened||!ready)return;
            var key=keys.FirstOrDefault(candidate=>candidate.name==name);
            if(!key)return;
            Input.Select(key);Input.PressSelected();
        }

        void LateUpdate()
        {
            if(!cameraView || !cameraView.gameObject.activeInHierarchy){ReleaseCamera();return;}
            var output=Camera.main;
            if(!output)return;
            if(!brain)
            {
                brain=output.GetComponent<CinemachineBrain>();
                if(brain){savedIgnoreTimeScale=brain.IgnoreTimeScale;brain.IgnoreTimeScale=true;}
            }
            FramePanel(output.aspect);
            if(brain)
            {
                // A paused blend can otherwise freeze halfway between the gameplay and keypad cameras.
                cameraOverride=brain.SetCameraOverride(cameraOverride,10000,null,cameraView,1,Time.unscaledDeltaTime);
                brain.ActiveBlend=null;brain.ManualUpdate();
            }
        }
        void FramePanel(float aspect)
        {
            var renderers=GetComponentsInChildren<MeshRenderer>(true);
            if(renderers.Length==0)return;
            var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            Vector3 normal=transform.right,up=transform.up,right=Vector3.Cross(up,-normal);
            float halfWidth=0,halfHeight=0,halfDepth=0;
            for(int corner=0;corner<8;corner++)
            {
                var v=Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                halfWidth=Mathf.Max(halfWidth,Mathf.Abs(Vector3.Dot(v,right)));
                halfHeight=Mathf.Max(halfHeight,Mathf.Abs(Vector3.Dot(v,up)));
                halfDepth=Mathf.Max(halfDepth,Mathf.Abs(Vector3.Dot(v,normal)));
            }
            var lens=cameraView.Lens;lens.FieldOfView=45;lens.NearClipPlane=.01f;cameraView.Lens=lens;
            float tangent=Mathf.Tan(22.5f*Mathf.Deg2Rad);
            float distance=halfDepth+1.2f*Mathf.Max(halfHeight/tangent,halfWidth/(tangent*Mathf.Max(.25f,aspect)));
            cameraView.transform.SetPositionAndRotation(bounds.center+normal*distance,Quaternion.LookRotation(-normal,up));
        }
        void ReleaseCamera()
        {
            if(brain)
            {
                if(cameraOverride>=0)brain.ReleaseCameraOverride(cameraOverride);
                brain.IgnoreTimeScale=savedIgnoreTimeScale;
            }
            cameraOverride=-1;brain=null;
        }
        void OnDisable(){ReleaseCamera();}
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
