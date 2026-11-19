using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [RequireComponent(typeof(Collider))]
    public sealed class ShutterSequenceNote : MonoBehaviour
    {
        public ShutterSequencePuzzle Puzzle;
        public bool IsOpen { get; private set; }
        private void Awake()
        {
            if (TryGetComponent(out Interactive interactive)) interactive.OnInteract.AddListener(_ => IsOpen = !IsOpen);
        }
        private void Update() { if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) IsOpen = false; }
        private void OnGUI()
        {
            if (!IsOpen || !Puzzle) return;
            Rect page = new Rect(Screen.width * .5f - 190, Screen.height * .5f - 150, 380, 300);
            GUI.Box(page, GameText.Translate("SHUTTER SEQUENCE"), GameText.DisplayStyle(GUI.skin.box));
            GUI.Label(new Rect(page.x + 24, page.y + 38, page.width - 48, 24), GameText.Translate("Open green shutters. Leave red shutters closed."), GameText.DisplayStyle(GUI.skin.label));
            bool[] solution = Puzzle.Solution;
            for (int row = 0; row < 3; ++row)
            for (int col = 0; col < 2; ++col)
            {
                int index = row * 2 + col;
                Color old = GUI.color;
                GUI.color = solution != null && index < solution.Length && solution[index] ? Color.green : Color.red;
                GUI.Box(new Rect(page.x + 72 + col * 125, page.y + 82 + row * 55, 90, 38), GameText.Translate(solution != null && solution[index] ? "OPEN" : "CLOSED"), GameText.DisplayStyle(GUI.skin.box));
                GUI.color = old;
            }
        }
    }
}
