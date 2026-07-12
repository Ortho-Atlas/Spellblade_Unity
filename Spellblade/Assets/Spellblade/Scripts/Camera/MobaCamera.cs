using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// Fixed-angle MOBA follow camera: locked offset, locked ~50° tilt, no
    /// mouse-look. Smoothly tracks the player in LateUpdate so it never
    /// stutters against agent movement.
    /// </summary>
    public class MobaCamera : MonoBehaviour
    {
        public Transform target;

        [Header("Framing")]
        public float height = 12f;
        public float distance = 8f;
        [Range(20f, 80f)] public float tilt = 52f;

        [Header("Feel")]
        [Tooltip("Lower = snappier follow, higher = floatier.")]
        public float smoothTime = 0.10f;

        private Vector3 _velocity;

        private void LateUpdate()
        {
            if (target == null) return;

            var desired = target.position + new Vector3(0f, height, -distance);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
            transform.rotation = Quaternion.Euler(tilt, 0f, 0f); // locked — no mouse-look
        }

        /// <summary>Snap instantly to the target (used once at spawn so there's no fly-in).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.position + new Vector3(0f, height, -distance);
            transform.rotation = Quaternion.Euler(tilt, 0f, 0f);
            _velocity = Vector3.zero;
        }
    }
}
