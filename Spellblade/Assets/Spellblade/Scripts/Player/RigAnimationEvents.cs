using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// Receiver for the Starter Assets animation events (OnFootstep / OnLand).
    /// The original ThirdPersonController received these; we strip that script,
    /// so without this stub every run cycle logs "no receiver" errors.
    /// Later: hook footstep audio / dust VFX here.
    /// </summary>
    public class RigAnimationEvents : MonoBehaviour
    {
        private void OnFootstep(AnimationEvent animationEvent) { }
        private void OnLand(AnimationEvent animationEvent) { }
    }
}
