using System;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [RequireComponent(typeof(SavableObjectState))]
    public sealed class ShutterSequencePuzzle : MonoBehaviour, ISavableObjectStateExtra
    {
        [Tooltip("Exactly six non-electrical shutters, arranged in your desired 2x3 order.")]
        public SimplePuzzleShutter[] Shutters = new SimplePuzzleShutter[6];
        public SimplePuzzleShutter MainShutter;
        public GameObject WrongAnswerCamera;
        public AudioSource WrongAnswerBuzzer;
        [Min(.1f)] public float WrongCameraSeconds = 2f;
        [SerializeField] private bool[] solution = new bool[6];
        [SerializeField] private bool solved;
        private float wrongCameraUntil;

        public bool[] Solution => solution;
        public bool IsSolved => solved;
        private void Awake()
        {
            if (solution == null || solution.Length != 6 || CountSolution() == 0) GenerateSolution();
            Apply();
        }
        private void Update() { if (WrongAnswerCamera && WrongAnswerCamera.activeSelf && Time.unscaledTime >= wrongCameraUntil) WrongAnswerCamera.SetActive(false); }

        /// <summary>Assign this to a normal Interactive's UnityEvent to submit the current shutter pattern.</summary>
        public void CheckAnswer()
        {
            if (solved) return;
            for (int i = 0; i < 6; ++i)
            {
                bool open = Shutters != null && i < Shutters.Length && Shutters[i] && Shutters[i].IsOpen;
                if (open != solution[i]) { Wrong(); return; }
            }
            solved = true; Apply(); GetComponent<SavableObjectState>().SaveState();
        }
        private void Wrong()
        {
            if (WrongAnswerCamera) { WrongAnswerCamera.SetActive(true); wrongCameraUntil = Time.unscaledTime + WrongCameraSeconds; }
            if (WrongAnswerBuzzer) WrongAnswerBuzzer.Play();
        }
        private void Apply() { if (MainShutter) MainShutter.SetOpen(solved); }
        private void GenerateSolution()
        {
            solution = new bool[6];
            int count = UnityEngine.Random.Range(2, 5);
            while (count > 0) { int index = UnityEngine.Random.Range(0, 6); if (solution[index]) continue; solution[index] = true; --count; }
        }
        private int CountSolution() { int count = 0; foreach (bool value in solution) if (value) ++count; return count; }
        public string GetSavableData() => JsonUtility.ToJson(new State { Solution = solution, Solved = solved });
        public void SetFromSavedData(string data) { State state = JsonUtility.FromJson<State>(data); solution = state.Solution; solved = state.Solved; Apply(); }
        [Serializable] private struct State { public bool[] Solution; public bool Solved; }
    }
}
