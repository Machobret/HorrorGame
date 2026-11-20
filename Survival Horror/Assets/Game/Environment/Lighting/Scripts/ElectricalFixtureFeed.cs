using System.Collections.Generic;
using Game.Environment;
using HouseToolkit;
using UnityEngine;

namespace HorrorGame.Lighting
{
    public sealed class ElectricalFixtureFeed : MonoBehaviour
    {
        [SerializeField] private ElectricalBoxPower supply;
        [SerializeField] private Light[] emitters = System.Array.Empty<Light>();
        [SerializeField] private FogClearingLampLight[] fogVolumes = System.Array.Empty<FogClearingLampLight>();

        private readonly HashSet<Light> locallyDisconnected = new();
        private readonly Dictionary<Light, bool> enabledBeforeOutage = new();

        public void SetLocalPower(Light emitter, bool powered)
        {
            if (!emitter) return;
            bool changed = powered ? locallyDisconnected.Remove(emitter) : locallyDisconnected.Add(emitter);
            if (changed && isActiveAndEnabled) Apply(!supply || supply.IsOn);
        }

        private void OnEnable()
        {
            if (supply) supply.PowerChanged += Apply;
            Apply(!supply || supply.IsOn);
        }

        private void OnDisable()
        {
            if (supply) supply.PowerChanged -= Apply;
            foreach (var emitter in emitters)
                if (emitter) ApplyEmitter(emitter, true);
            foreach (var volume in fogVolumes)
                if (volume) volume.SetPowered(true);
        }

        private void Apply(bool powered)
        {
            foreach (var emitter in emitters)
                if (emitter) ApplyEmitter(emitter, powered && !locallyDisconnected.Contains(emitter));
            foreach (var volume in fogVolumes)
                if (volume) volume.SetPowered(powered && !locallyDisconnected.Contains(volume.SourceLight));
        }

        private void ApplyEmitter(Light emitter, bool powered)
        {
            if (!powered)
            {
                if (!enabledBeforeOutage.TryAdd(emitter, emitter.enabled)) return;
                FixtureAttachment.SetPower(emitter, false);
                return;
            }
            if (!enabledBeforeOutage.Remove(emitter, out bool wasEnabled)) return;
            var fixture = emitter.GetComponentInParent<FixtureAttachment>();
            if (fixture && fixture.Emitter == emitter)
                FixtureAttachment.SetPower(emitter, true);
            else
                emitter.enabled = wasEnabled;
        }
    }
}
