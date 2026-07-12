using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Spellblade
{
    /// <summary>
    /// Right-click-to-move (LoL style) on a NavMeshAgent, built on the NEW
    /// Input System (this project enforces it — legacy Input would throw).
    ///
    /// Right-click is movement ONLY — never overloaded with anything else.
    /// Hold to steer continuously. Feeds agent speed into the Starter Assets
    /// animator ("Speed" / "MotionSpeed" / "Grounded") when a rig is present.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MobaController : MonoBehaviour
    {
        [Tooltip("Seconds between destination updates while right-click is held.")]
        public float heldRepathInterval = 0.08f;

        [Tooltip("Show a small flash where the player clicked.")]
        public bool showClickMarker = true;

        private NavMeshAgent _agent;
        private Animator _animator;
        private float _nextHeldRepath;

        // Animator parameter availability (Starter Assets rig vs bare capsule).
        private bool _hasSpeed, _hasMotionSpeed, _hasGrounded;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
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
            HandleMoveInput();
            DriveAnimator();
        }

        private void HandleMoveInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            bool pressed = mouse.rightButton.wasPressedThisFrame;
            bool held = mouse.rightButton.isPressed && Time.time >= _nextHeldRepath;
            if (!pressed && !held) return;

            _nextHeldRepath = Time.time + heldRepathInterval;

            var cam = Camera.main;
            if (cam == null) return;

            // Cursor → world: raycast the scene, then snap to the nearest NavMesh point.
            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 300f, ~0, QueryTriggerInteraction.Ignore)) return;
            if (!NavMesh.SamplePosition(hit.point, out var navHit, 2.5f, NavMesh.AllAreas)) return;

            _agent.SetDestination(navHit.position);

            if (showClickMarker && pressed)
                SpellbladeFx.Flash(navHit.position + Vector3.up * 0.1f, new Color(0.4f, 0.9f, 0.5f), 0.35f, 0.3f);
        }

        private void DriveAnimator()
        {
            if (_animator == null || !_hasSpeed) return;
            // Starter Assets blend tree: 0 = idle, ~2 = walk, ~5.3 = sprint.
            _animator.SetFloat("Speed", _agent.velocity.magnitude);
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
