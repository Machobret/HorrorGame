using System.Collections;
using HorrorEngine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorGame.Weapons
{
    public sealed class FlareTestRoom : MonoBehaviour
    {
        public ReloadableWeaponData FlareGun;
        public SignalFlare Projectile;
        public Transform TestCamera;
        private bool sideView, slowMotion;
        private SignalFlare latest;
        private int activeFlares;

        private IEnumerator Start()
        {
            SetCamera();
            while (!GameManager.Exists || !GameManager.Instance.Player) yield return null;
            var inventory = GameManager.Instance.Inventory;
            if (!inventory.TryGet(FlareGun, out var entry)) entry = inventory.Add(FlareGun, 1, 1);
            if (entry != null) inventory.Equip(entry);
            inventory.Add(FlareGun.AmmoItem, 10);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f6Key.wasPressedThisFrame) Demo();
                if (keyboard.f7Key.wasPressedThisFrame) SlowMotion();
                if (keyboard.f8Key.wasPressedThisFrame) ChangeView();
                if (keyboard.f9Key.wasPressedThisFrame) Refill();
            }
            activeFlares = 0;
            foreach (var flare in FindObjectsByType<SignalFlare>(FindObjectsSortMode.None))
            {
                ++activeFlares;
                if (!latest || flare.Age < latest.Age) latest = flare;
            }
        }

        public void Demo()
        {
            if (!Projectile) return;
            var flare = Instantiate(Projectile, new Vector3(0, 1.6f, 0), Quaternion.identity);
            flare.EnableDamage = false;
            var owner = GameManager.Exists && GameManager.Instance.Player
                ? GameManager.Instance.Player.GetComponent<Combatant>() : null;
            flare.Launch(new Vector3(0, 2.5f, 11), owner);
            latest = flare;
        }
        public void Refill()
        {
            if (GameManager.Exists && GameManager.Instance.Player && FlareGun)
                GameManager.Instance.Inventory.Add(FlareGun.AmmoItem, 10);
        }
        public void SlowMotion() { slowMotion = !slowMotion; Time.timeScale = slowMotion ? .25f : 1; }
        public void ChangeView() { sideView = !sideView; SetCamera(); }
        private void SetCamera()
        {
            if (!TestCamera) return;
            TestCamera.position = sideView ? new Vector3(9, 5, 4) : new Vector3(7, 6, -7);
            TestCamera.LookAt(new Vector3(0, 1, 4));
        }
        private void OnDisable() { if (slowMotion) Time.timeScale = 1; }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, Screen.height - 205, 460, 190), GUI.skin.box);
            GUILayout.Label("FLARE TEST ROOM — aim and fire with your normal controls");
            GUILayout.Label("Red floor marks: 2 / 4 / 6 / 8 / 10 metres");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("F6: Demo launch")) Demo();
            if (GUILayout.Button(slowMotion ? "F7: Normal speed" : "F7: Slow motion")) SlowMotion();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("F8: Change camera")) ChangeView();
            if (GUILayout.Button("F9: +10 flare rounds")) Refill();
            GUILayout.EndHorizontal();
            GUILayout.Label("Active flares: " + activeFlares + (latest ? " | Latest: " + (latest.HasLanded ? "LANDED" : "IN FLIGHT") + " | Age: " + latest.Age.ToString("F1") + "s" : " | No projectile spawned yet"));
            GUILayout.Label("Demo launches are harmless. Gun shots use the real projectile settings.");
            GUILayout.EndArea();
        }
    }
}
