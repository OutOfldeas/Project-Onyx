using Onyx.Game.Session;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Onyx.Game.World
{
    /// <summary>
    /// Koppelt das globale 2D-Licht der Szene an die Tageszeit: tagsüber hell,
    /// nachts dunkel und bläulich. Sucht das Licht über seinen Namen „Global Light 2D“.
    /// </summary>
    public sealed class WorldLighting : MonoBehaviour
    {
        private const string GlobalLightName = "Global Light 2D";

        [SerializeField] private float nightIntensity = 0.18f;
        [SerializeField] private Color nightColor = new Color(0.45f, 0.52f, 0.95f);

        private Light2D globalLight;
        private float nextSearch;

        /// <summary>Im Dev-Modus abschaltbar, z. B. um nachts etwas zu sehen.</summary>
        public bool FollowTimeOfDay { get; set; } = true;

        private void Update()
        {
            if (globalLight == null)
            {
                if (Time.unscaledTime < nextSearch)
                {
                    return;
                }

                nextSearch = Time.unscaledTime + 1f;
                GameObject lightObject = GameObject.Find(GlobalLightName);
                globalLight = lightObject != null ? lightObject.GetComponent<Light2D>() : null;
                if (globalLight == null)
                {
                    return;
                }
            }

            GameSession session = GameSession.Instance;
            if (session == null)
            {
                return;
            }

            float daylight = FollowTimeOfDay ? session.World.Clock.Daylight : 1f;
            globalLight.intensity = Mathf.Lerp(nightIntensity, 1f, daylight);
            globalLight.color = Color.Lerp(nightColor, Color.white, daylight);
        }
    }
}
