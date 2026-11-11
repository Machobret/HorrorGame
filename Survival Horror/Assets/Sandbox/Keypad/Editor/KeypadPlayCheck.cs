using System;
using System.IO;
using System.Linq;
using HorrorEngine;
using HorrorGame.Doors;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HorrorGame.Tests
{
    public static class KeypadPlayCheck
    {
        static DoorKeypad keypad;
        static double next;
        static int stage;
        static string report;
        [MenuItem("Horror Game/Tests/Check Storage Keypad")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            keypad=Object.FindObjectsByType<DoorKeypad>(FindObjectsSortMode.None).Single();
            Check(keypad.DoorLock.IsLocked,"Initially locked");
            report="Initially locked: PASS\n";
            keypad.DoorLock.OnTryToUnlock(out bool immediate);
            Check(!immediate,"Door must not open before PIN");
            stage=0;next=EditorApplication.timeSinceStartup+15;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
        static void Keys(string numbers)
        {
            foreach(char n in numbers)
                keypad.Input.GetComponentsInChildren<Interactive>(true).Single(k=>k.name=="Number"+n).Interact(keypad.Input);
        }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            try
            {
                if(!EditorApplication.isPlaying)throw new Exception("Play mode ended");
                if(stage==0)
                {
                    Check(keypad.Input.enabled,"Keypad input active");
                    Check(Time.timeScale==0,"Close-up pauses locomotion");
                    var view=keypad.View.GetComponentInChildren<Unity.Cinemachine.CinemachineCamera>(true);
                    Check(view.gameObject.activeSelf,"Close-up camera enabled");
                    Check(Vector3.Distance(Camera.main.transform.position,view.transform.position)<.05f,"Close-up camera is the rendered view");
                    Keys("1111");
                    Check(!keypad.Puzzle.IsSolved&&keypad.DoorLock.IsLocked,"Wrong PIN must stay locked");
                    report+="Wrong PIN, native button events: PASS\n";
                    keypad.Input.OnCancel.Invoke();
                }
                else if(stage==1)
                {
                    Check(!keypad.Input.enabled&&Time.timeScale>0,"Cancel restores gameplay");
                    report+="Cancel restores input/time: PASS\n";
                    keypad.Open();
                }
                else if(stage==2)
                {
                    Keys("12");
                    keypad.Input.GetComponentsInChildren<Interactive>(true).Single(k=>k.name=="Backspace").Interact(keypad.Input);
                    keypad.Input.GetComponentsInChildren<Interactive>(true).Single(k=>k.name=="Clear").Interact(keypad.Input);
                    Keys("1998");
                    Check(keypad.Puzzle.IsSolved&&!keypad.DoorLock.IsLocked,"Correct PIN unlocks door");
                    report+="Clear/backspace then correct PIN: PASS\n";
                }
                else
                {
                    Check(!keypad.Input.enabled&&Time.timeScale>0,"Success restores gameplay");
                    Check(keypad.Display.text=="OPEN"&&!keypad.Approach.gameObject.activeSelf,"Unlocked display and approach state");
                    report+="Success restores gameplay and shows OPEN: PASS\n";
                    Finish();return;
                }
                stage++;next=EditorApplication.timeSinceStartup+2;
            }
            catch(Exception e){report+="FAIL: "+e;Finish();}
        }
        static void Finish()
        {
            EditorApplication.update-=Tick;
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Exports/Reviews/Keypad");
            Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"PlayCheck.txt"),report);
            Debug.Log("KEYPAD_PLAY_CHECK\n"+report);
        }
    }
}
