using System;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>How the game shows the player where to drive.</summary>
    public enum GuidanceMode
    {
        /// <summary>Beacons in the world and arrows on screen. The default.</summary>
        Markers,
        /// <summary>Beacons in the world and a GPS route line on the road, no arrows.</summary>
        Gps,
        /// <summary>Beacons, arrows and the GPS line.</summary>
        Both
    }

    /// <summary>
    /// Player settings that last between sessions, saved with PlayerPrefs. Read and changed from
    /// the main menu's settings panel (GuidanceSettingButton) and in game (G or D-pad up cycles
    /// the guidance mode).
    ///
    /// Markers is the default because playtesting showed the GPS line made players follow it
    /// rather than choose their own route, and the beacons alone are enough to navigate by.
    /// </summary>
    public static class GameSettings
    {
        private const string GuidanceKey = "ZoomZoom.Guidance";

        private static bool _loaded;
        private static GuidanceMode _guidance;

        /// <summary>Fires whenever the guidance mode changes, with the new mode.</summary>
        public static event Action<GuidanceMode> GuidanceChanged;

        public static GuidanceMode Guidance
        {
            get
            {
                if (!_loaded)
                {
                    int saved = PlayerPrefs.GetInt(GuidanceKey, (int)GuidanceMode.Markers);
                    _guidance = saved >= 0 && saved <= (int)GuidanceMode.Both ? (GuidanceMode)saved : GuidanceMode.Markers;
                    _loaded = true;
                }
                return _guidance;
            }
            set
            {
                if (_loaded && _guidance == value) return;
                _guidance = value;
                _loaded = true;
                PlayerPrefs.SetInt(GuidanceKey, (int)value);
                PlayerPrefs.Save();
                GuidanceChanged?.Invoke(value);
            }
        }

        /// <summary>Screen arrows are shown in Markers and Both.</summary>
        public static bool ShowArrows => Guidance != GuidanceMode.Gps;

        /// <summary>The GPS line is shown in GPS and Both.</summary>
        public static bool ShowGps => Guidance != GuidanceMode.Markers;

        public static void CycleGuidance()
        {
            Guidance = (GuidanceMode)(((int)Guidance + 1) % 3);
        }

        /// <summary>What to show on a button or in a popup.</summary>
        public static string Describe(GuidanceMode mode)
        {
            switch (mode)
            {
                case GuidanceMode.Gps: return "GPS route";
                case GuidanceMode.Both: return "Markers and GPS";
                default: return "Markers";
            }
        }
    }
}
