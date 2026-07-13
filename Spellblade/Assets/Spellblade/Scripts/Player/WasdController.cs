using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Spellblade
{
    /// <summary>
    /// Direct WASD movement (Phase 2) on a NavMeshAgent, built on the NEW
    /// Input System (this project enforces it — legacy Input would throw).
    ///
    /// The agent is driven with agent.Move each frame — no SetDestination —
    /// so zero input means zero drift, while walls and pillars keep blocking.
    /// Direction is built from the camera's flattened forward/right: with the
    /// fixed MOBA yaw that equals world axes today, but a future camera
    /// rotation "just works". Faces the movement direction with a smooth turn;
    /// FaceToward stays public for SpellCaster cast-snapping.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class WasdController : MonoBehaviour
    {
        [Tooltip("Degrees per second the character turns toward its movement direction.")]
        public float turnSpeed = 720f;

        private NavMeshAgent _agent;
        private Animator _animator;
        private Vector3 _lastPosition;
        private float _observedSpeed;

        // Animator parameter availability (Starter Assets rig vs bare capsule).
        private bool _hasSpeed, _hasMotionSpeed, _hasGrounded;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.updateRotation = false; // rotation is ours; agent only slides + collides
            _lastPosition = transform.position;
        }

        /// <summary>Called by the bootstrap after the visual rig is attached.</summary>
        public void BindAnimator(Animator animator)
        {
            _animator = animator;
            if (_animator == null) return;

            foreach (var p in _animator.parameters)
            {
                if (p.name == "Speed") _hasSpeed = true;
                if (p.name == "MotionSpeed") _hasMotionSpeed = true;
                if (p.name == "Grounded") _hasGrounded = true;
            }
            if (_hasGrounded) _animator.SetBool("Grounded", true);
            if (_hasMotionSpeed) _animator.SetFloat("MotionSpeed", 1f);
        }

        private void Update()
        {
            var dir = ReadMoveDirection();

            if (dir.sqrMagnitude > 0.0001f)
            {
                _agent.Move(dir * (_agent.speed * Time.deltaTime));

                // Smooth-turn toward where we're headed (~720°/s).
                var look = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
            }

            DriveAnimator();
        }

        /// <summary>WASD (+ arrow keys) → normalized world-space direction. Diagonals never faster.</summary>
        private Vector3 ReadMoveDirection()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector3.zero;

            float x = 0f, z = 0f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) z += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) z -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (x == 0f && z == 0f) return Vector3.zero;

            // Camera-flattened basis: world axes under the fixed MOBA yaw, but a
            // rotated camera later would keep input screen-relative for free.
            Vector3 fwd = Vector3.forward, right = Vector3.right;
            var cam = Camera.main;
            if (cam != null)
            {
                fwd = cam.transform.forward;
                fwd.y = 0f;
                right = cam.transform.right;
                right.y = 0f;
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward; // top-down degenerate case
                fwd.Normalize();
                if (right.sqrMagnitude < 0.001f) right = Vector3.right;
                right.Normalize();
            }

            return (fwd * z + right * x).normalized;
        }

        private void DriveAnimator()
        {
            // Actual velocity from observed displacement — agent.velocity is not
            // reliably populated when the agent is driven via Move().
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                var delta = transform.position - _lastPosition;
                delta.y = 0f;
                // Light smoothing so the blend tree doesn't flicker on tiny frames.
                _observedSpeed = Mathf.Lerp(_observedSpeed, delta.magnitude / dt, 0.35f);
            }
            _lastPosition = transform.position;

            if (_animator == null || !_hasSpeed) return;
            // Starter Assets blend tree: 0 = idle, ~2 = walk, ~5.3 = sprint.
            _animator.SetFloat("Speed", _observedSpeed);
        }

        /// <summary>Snap-face a world point (used by SpellCaster so casts aim correctly).</summary>
        public void FaceToward(Vector3 worldPoint)
        {
            var flat = worldPoint - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(flat);
        }
    }
}
