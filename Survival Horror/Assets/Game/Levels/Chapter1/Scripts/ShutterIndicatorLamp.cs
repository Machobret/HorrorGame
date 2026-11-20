using Game.Environment;
using HouseToolkit;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    public sealed class ShutterIndicatorLamp : MonoBehaviour
    {
        [SerializeField] private CircuitDrivenObject risingShutter;
        [SerializeField] private CircuitShutterTeleport teleportShutter;
        [SerializeField] private Chapter1Opening intercomGate;
        [SerializeField] private ElectricalBoxPower supply;
        [SerializeField] private Color openColor = new Color(.18f, 1f, .25f);
        [SerializeField, Min(.01f)] private float colorTweenSeconds = .35f;

        [SerializeField] private bool manual;
        [SerializeField] private Color initialManualColor = new Color(1f, .67f, .12f);
        private Color? manualColor;

        public void SetManualColor(Color color) => manualColor = color;

        private FixtureAttachment fixture;
        private IndicatorLamp indicator;
        private DeviceIndicator device;
        private Color normalColor, currentColor, fromColor, targetColor;
        private float normalIntensity, transitionStarted, nextFlicker;
        private bool flickerDark, wasAvailable, initialized;
        private System.Random flicker;

        private void Start()
        {
            TryGetComponent(out fixture);
            TryGetComponent(out indicator);
            TryGetComponent(out device);
            Light emitter = fixture ? fixture.Emitter : null;
            if (!emitter && !indicator && !device) return;
            if (!manual && !risingShutter && !intercomGate && !UsesFlicker)
            {
                if (supply && emitter) FixtureAttachment.SetPower(emitter, supply.IsOn);
                return;
            }
            normalColor = emitter ? emitter.color : IndicatorLamp.Green;
            normalIntensity = emitter ? emitter.intensity : .9f;
            flicker = new System.Random(GetInstanceID());
            initialized = true;
            ApplyInitialState();
        }

        private void OnEnable()
        {
            if (supply) supply.PowerChanged += OnSupplyChanged;
            if (supply) OnSupplyChanged(supply.IsOn);
            else if (initialized) ApplyInitialState();
        }

        private void OnDisable()
        {
            if (supply) supply.PowerChanged -= OnSupplyChanged;
        }

        private void OnSupplyChanged(bool powered)
        {
            if (initialized) ApplyInitialState();
            else if (fixture && fixture.Emitter) FixtureAttachment.SetPower(fixture.Emitter, powered);
        }

        private void ApplyInitialState()
        {
            currentColor = fromColor = targetColor = DesiredColor;
            if (UsesFlicker)
            {
                wasAvailable = PassageAvailable;
                flickerDark = false;
                nextFlicker = Time.unscaledTime + .15f;
            }
            SetLight(currentColor, normalIntensity);
        }

        private Color DesiredColor
        {
            get
            {
                if (manual) return manualColor ?? initialManualColor;
                if (intercomGate)
                    return intercomGate.CourtyardPassageAvailable ? openColor : IndicatorLamp.InactiveYellow;
                CircuitDrivenObject source = risingShutter;
                if (!source) return normalColor;
                return source.Grid && source.Grid.IsCircuitPowered(source.CircuitId) ? openColor : IndicatorLamp.InactiveYellow;
            }
        }

        private bool UsesFlicker => !manual && teleportShutter;
        private bool PassageAvailable => teleportShutter && teleportShutter.CanTraverse;

        private void SetLight(Color color, float intensity)
        {
            if (supply && !supply.IsOn) intensity = 0f;
            if (device) device.SetDeviceState(color, intensity);
            else if (indicator) indicator.SetColor(color, intensity);
            else fixture.SetIndicatorLight(color, intensity);
        }

        private void LateUpdate()
        {
            if (!initialized || (supply && !supply.IsOn)) return;
            float now = Time.unscaledTime;
            bool colorChanged = UpdateColor(now);
            if (UsesFlicker)
            {
                bool available = PassageAvailable;
                if (available)
                {
                    if (!wasAvailable || colorChanged) SetLight(currentColor, normalIntensity);
                }
                else if (wasAvailable || now >= nextFlicker)
                {
                    flickerDark = wasAvailable || !flickerDark;
                    float delay = (float)flicker.NextDouble();
                    nextFlicker = now + (flickerDark ? Mathf.Lerp(.035f, .13f, delay) : Mathf.Lerp(.09f, .55f, delay));
                    SetLight(currentColor, flickerDark ? 0f : normalIntensity);
                }
                else if (colorChanged && !flickerDark) SetLight(currentColor, normalIntensity);
                wasAvailable = available;
                return;
            }

            if (colorChanged) SetLight(currentColor, normalIntensity);
        }

        private bool UpdateColor(float now)
        {
            Color desired = DesiredColor;
            if (targetColor != desired)
            {
                fromColor = currentColor;
                targetColor = desired;
                transitionStarted = now;
            }
            if (currentColor == targetColor) return false;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now - transitionStarted) / colorTweenSeconds));
            currentColor = Color.Lerp(fromColor, targetColor, progress);
            return true;
        }
    }
}
