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
        [SerializeField] private Color closedColor = new Color(1f, .12f, .06f);
        [SerializeField] private Color openColor = new Color(.18f, 1f, .25f);
        [SerializeField, Min(.01f)] private float colorTweenSeconds = .35f;

        [SerializeField] private bool manual;
        [SerializeField] private Color initialManualColor = new Color(1f, .67f, .12f);
        private Color? manualColor;

        public void SetManualColor(Color color) => manualColor = color;

        private FixtureAttachment fixture;
        private IndicatorLamp indicator;
        private Color normalColor, currentColor, fromColor, targetColor;
        private float normalIntensity, transitionStarted, nextFlicker;
        private bool flickerDark, wasAvailable, initialized;
        private System.Random flicker;

        private void Start()
        {
            TryGetComponent(out fixture);
            TryGetComponent(out indicator);
            Light emitter = fixture ? fixture.Emitter : null;
            if ((!emitter && !indicator) || (!manual && !risingShutter && !intercomGate && !UsesFlicker)) return;
            normalColor = emitter ? emitter.color : IndicatorLamp.Green;
            normalIntensity = emitter ? emitter.intensity : .9f;
            flicker = new System.Random(GetInstanceID());
            initialized = true;
            ApplyInitialState();
        }

        private void OnEnable()
        {
            if (initialized) ApplyInitialState();
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
                    return intercomGate.CourtyardPassageAvailable ? openColor :
                        intercomGate.CourtyardHasPower ? new Color(1f, .67f, .12f) : closedColor;
                CircuitDrivenObject source = risingShutter;
                if (!source) return normalColor;
                return source.Grid && source.Grid.IsCircuitPowered(source.CircuitId) ? openColor : closedColor;
            }
        }

        private bool UsesFlicker => !manual && teleportShutter;
        private bool PassageAvailable => teleportShutter && teleportShutter.CanTraverse;

        private void SetLight(Color color, float intensity)
        {
            if (indicator) indicator.SetColor(color, intensity);
            else fixture.SetIndicatorLight(color, intensity);
        }

        private void LateUpdate()
        {
            if (!initialized) return;
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
