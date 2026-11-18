using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [AddComponentMenu("Horror Game/Tablet Objective Trigger")]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TabletObjectiveTrigger : MonoBehaviour
    {
        [TextArea, Tooltip("Leave blank to clear the current objective.")]
        public string Objective;

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerActor>())
                ApplyObjective();
        }

        public void ApplyObjective() => Chapter1MapTutorial.SetQuest(Objective);
    }
}
