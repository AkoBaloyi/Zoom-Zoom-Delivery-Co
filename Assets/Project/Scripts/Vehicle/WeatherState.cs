using System;
using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// The weather over the whole city. One component, one value, read by the car every physics
    /// step and multiplied into whatever surface the wheels are on.
    ///
    /// WHY ONE GLOBAL VALUE AND NOT A PROPERTY ON EACH SURFACE
    /// Rain is a fact about the sky, not about a road piece. If wetness lived on the six hundred
    /// road_base objects, turning on rain would mean editing six hundred components and turning it
    /// off would mean editing them again, and a forgotten one would be a dry patch in a storm. One
    /// value in one place means the weather can change mid-shift from a script, a timer, or a
    /// playtest variant, and the whole city changes with it in the same frame.
    ///
    /// WHY DRY IS THE DEFAULT AND CHANGES NOTHING
    /// Every number in VehicleLab_Measurements was taken with no weather system in the project.
    /// Dry hands the surface back untouched, so a scene with no WeatherState in it, or one set to
    /// Dry, drives exactly as the measured car. Rain, Snow and Ice are opt-in and each one is a
    /// playtest variant waiting to be named in docs/playtesting/QUESTIONS.md.
    ///
    /// Visuals are not here. This is the handling half: what the tyres feel. Rain on the lens,
    /// particles, skybox and lighting belong to whoever owns the look, and they read Current from
    /// here so the two halves cannot disagree.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeatherState : MonoBehaviour
    {
        [Tooltip("The weather right now. Dry changes nothing. Edit while playing to feel the " +
                 "difference immediately; the car reads this every physics step.")]
        [SerializeField] private WeatherKind weather = WeatherKind.Dry;

        /// <summary>The weather the car should drive in. Dry when no WeatherState exists.</summary>
        public static WeatherKind Current => _instance != null ? _instance.weather : WeatherKind.Dry;

        /// <summary>Fires when the weather changes, for anything that wants to react once rather
        /// than poll: a HUD icon, a particle system, a sound.</summary>
        public static event Action<WeatherKind> Changed;

        private static WeatherState _instance;
        private WeatherKind _last;

        private void OnEnable()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning(
                    $"[WeatherState] Two WeatherStates in the scene ('{_instance.name}' and '{name}'). " +
                    "The sky has one weather. Keeping the first and disabling this one.", this);
                enabled = false;
                return;
            }
            _instance = this;
            _last = weather;
        }

        private void OnDisable()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            // Inspector edits during Play are the normal way to tune this, so the change event
            // has to come from polling the field rather than from a setter nobody called.
            if (weather != _last)
            {
                _last = weather;
                Changed?.Invoke(weather);
            }
        }

        /// <summary>Set the weather from code, for a timer, a shift script or a playtest variant.</summary>
        public void Set(WeatherKind kind)
        {
            if (weather == kind) return;
            weather = kind;
            _last = kind;
            Changed?.Invoke(kind);
        }
    }
}
