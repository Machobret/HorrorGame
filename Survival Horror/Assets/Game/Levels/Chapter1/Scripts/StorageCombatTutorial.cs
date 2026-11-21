using System;
using System.Collections;
using HorrorEngine;
using HorrorGame.UI;
using UnityEngine;
using UnityEngine.AI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HorrorGame.Chapter1
{
    [RequireComponent(typeof(SavableObjectState))]
    public sealed class StorageCombatTutorial : MonoBehaviour, ISavableObjectStateExtra
    {
        public PickupItem Pistol;
        public PickupItem Flashlight;
        private bool pistolTaken, tutorialSeen, officeHintSeen;
        private Coroutine lesson;

        private void Start()
        {
            MessageBuffer<ItemPickedUpMessage>.Subscribe(OnWeaponTaken);
            QueueLesson();
        }

        private void OnDestroy()
        {
            MessageBuffer<ItemPickedUpMessage>.Unsubscribe(OnWeaponTaken);
        }

        private void Update()
        {
            var officePickup = Flashlight ? Flashlight : Pistol;
            if (officeHintSeen || !officePickup || !officePickup.gameObject.activeInHierarchy ||
                !GameManager.Exists || !GameManager.Instance.IsPlaying ||
                (PauseController.Exists && PauseController.Instance.IsPaused)) return;
            var opening = Chapter1Opening.Instance;
            var player = GameManager.Instance.Player;
            if (!opening || !opening.DepotOfficeRoom || !player ||
                !opening.DepotOfficeRoom.ContainsWorldPosition(player.transform.position)) return;
            if (!PlayerMonologue.Show(Flashlight ? "This has a limited battery" : "A gun, maybe i should take it", fadeIn: .25f)) return;
            officeHintSeen = true;
            SaveProgress();
        }

        internal void ConfigureRunner(GameObject runner)
        {
            if (runner.TryGetComponent(out NavMeshAgent agent))
            {
                agent.speed = 1.1f;
                agent.acceleration = 5f;
                agent.angularSpeed = 180f;
            }

            var attack = runner.GetComponentInChildren<EnemyStateAttack>();
            if (attack) attack.Cooldown = 2.5f;
        }

        private void OnWeaponTaken(ItemPickedUpMessage message)
        {
            if (pistolTaken || !Pistol || message.Data != Pistol.Entry.Item) return;
            pistolTaken = true;
            SaveProgress();
            QueueLesson();
        }

        private void QueueLesson()
        {
            if (Pistol && pistolTaken && !tutorialSeen && lesson == null)
                lesson = StartCoroutine(ShowLesson());
        }

        private IEnumerator ShowLesson()
        {
            // The pickup splash and interaction state both need a frame to release their UI.
            yield return null;
            yield return new WaitUntil(() => UIManager.Exists && GameManager.Exists && GameManager.Instance.IsPlaying &&
                PauseController.Exists && !PauseController.Instance.IsPaused);
            var dialog = UIManager.Get<UIDialog>();
            var instructions = new DialogData();
            instructions.SetLines(new[]
            {
                Line("Open the inventory ({INVENTORY}), select the gun and choose Equip. Close the inventory."),
                Line("Hold Aim ({AIM}), then press Attack ({ATTACK}) to fire. Press Reload ({RELOAD}) to load more ammunition.")
            });
#if ENABLE_INPUT_SYSTEM
            using (var ui = new UIActions())
                instructions.SetTagReplacement("{INVENTORY}", Binding(ui.Gameplay.ToggleInventory, "Tab"));
            var player = GameManager.Instance.Player.GetComponentInChildren<PlayerInputListener>();
            var actions = player ? player.Actions : null;
            instructions.SetTagReplacement("{AIM}", Binding(actions?.Gameplay.Aiming, "RMB"));
            instructions.SetTagReplacement("{ATTACK}", Binding(actions?.Gameplay.Attack, "LMB"));
            instructions.SetTagReplacement("{RELOAD}", Binding(actions?.Gameplay.Reload, "R"));
#else
            instructions.SetTagReplacement("{INVENTORY}", "Tab");
            instructions.SetTagReplacement("{AIM}", "RMB");
            instructions.SetTagReplacement("{ATTACK}", "LMB");
            instructions.SetTagReplacement("{RELOAD}", "R");
#endif
            dialog.Show(instructions, textScale: 1.3f);
            yield return new WaitUntil(() => !dialog.isActiveAndEnabled);
            tutorialSeen = true;
            lesson = null;
            SaveProgress();
        }

#if ENABLE_INPUT_SYSTEM
        private static string Binding(InputAction action, string fallback)
        {
            string binding = action?.GetBindingDisplayString(group: "KeyboardAndMouse");
            return string.IsNullOrEmpty(binding) ? fallback : binding;
        }
#endif

        private static DialogLine Line(string text) => new DialogLine { LineText = new LocalizableText { Unlocalized = text } };

        private void SaveProgress() => GetComponent<SavableObjectState>().SaveState();

        public string GetSavableData() => JsonUtility.ToJson(new State
        {
            PistolTaken = pistolTaken, TutorialSeen = tutorialSeen, OfficeHintSeen = officeHintSeen
        });

        public void SetFromSavedData(string data)
        {
            var state = JsonUtility.FromJson<State>(data);
            pistolTaken = state.PistolTaken;
            tutorialSeen = state.TutorialSeen;
            officeHintSeen = state.OfficeHintSeen;
            QueueLesson();
        }

        [Serializable]
        private struct State
        {
            public bool PistolTaken, TutorialSeen, OfficeHintSeen;
        }
    }
}
