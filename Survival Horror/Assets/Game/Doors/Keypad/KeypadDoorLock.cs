using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Doors
{
    [RequireComponent(typeof(SavableObjectState))]
    public sealed class KeypadDoorLock : DoorLock
    {
        public DoorKeypad Keypad;
        public override void OnTryToUnlock(out bool openImmediately)
        {openImmediately=false;if(Keypad)Keypad.Open();}
        public override void OnTryToUnlockFromExit(out bool openImmediately)
        {OnTryToUnlock(out openImmediately);}
    }
}
