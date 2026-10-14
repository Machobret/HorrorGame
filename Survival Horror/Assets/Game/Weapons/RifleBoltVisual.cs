using System.Collections;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Weapons
{
    public class RifleBoltVisual : MonoBehaviour
    {
        public Transform Bolt;
        public Vector3 Pull = new Vector3(0, 0, -.055f);
        private Vector3 rest;
        private AttackBase attack;
        private void Awake() { rest = Bolt.localPosition; attack = GetComponent<AttackBase>(); }
        private void OnEnable() { attack.OnAttackStart.AddListener(Cycle); }
        private void OnDisable()
        {
            attack.OnAttackStart.RemoveListener(Cycle); StopAllCoroutines();
            if (Bolt) Bolt.localPosition = rest;
        }
        private void Cycle() { StopAllCoroutines(); StartCoroutine(Animate()); }
        private IEnumerator Animate()
        {
            yield return new WaitForSeconds(.35f);
            for (float t = 0; t < .25f; t += Time.deltaTime)
            { Bolt.localPosition = rest + Pull * Mathf.Sin(t / .25f * Mathf.PI); yield return null; }
            Bolt.localPosition = rest;
        }
    }
}
