using Unity.Netcode;
using UnityEngine;

namespace Onyx.Game.CameraRig
{
    /// <summary>
    /// Folgt der eigenen Spielfigur. Die Pixel Perfect Camera am selben Objekt sorgt dafür,
    /// dass die Pixel dabei scharf bleiben.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow : MonoBehaviour
    {
        [Tooltip("Wie schnell die Kamera nachzieht. 0 = gar nicht, höher = straffer.")]
        [SerializeField] private float smoothing = 10f;

        private void LateUpdate()
        {
            Transform target = FindLocalPlayer();
            if (target == null)
            {
                return;
            }

            Vector3 goal = new Vector3(target.position.x, target.position.y, transform.position.z);
            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, goal, t);
        }

        private static Transform FindLocalPlayer()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsClient || manager.LocalClient == null)
            {
                return null;
            }

            NetworkObject player = manager.LocalClient.PlayerObject;
            return player != null ? player.transform : null;
        }
    }
}
