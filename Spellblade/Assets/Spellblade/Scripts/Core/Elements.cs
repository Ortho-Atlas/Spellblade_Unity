using UnityEngine;

namespace Spellblade
{
    /// <summary>The four magic elements — one per discipline.</summary>
    public enum ElementType { Umbra, Frost, Storm, Blood }

    /// <summary>
    /// The elemental counter-wheel:
    ///   Umbra → beats Frost → beats Storm → beats Blood → beats Umbra.
    /// An enemy attuned to element X RESISTS X (0.3x) and is WEAK to the
    /// element that beats X (1.75x). Everything else lands normal.
    /// </summary>
    public static class ElementMath
    {
        public const float ResistMultiplier = 0.3f;
        public const float WeaknessMultiplier = 1.75f;

        public static ElementType Beats(ElementType e) => e switch
        {
            ElementType.Umbra => ElementType.Frost,
            ElementType.Frost => ElementType.Storm,
            ElementType.Storm => ElementType.Blood,
            _ => ElementType.Umbra,
        };

        public static float Modifier(ElementType incoming, ElementType attunement)
        {
            if (incoming == attunement) return ResistMultiplier;
            if (Beats(incoming) == attunement) return WeaknessMultiplier;
            return 1f;
        }

        /// <summary>Theme colors — same hues the disciplines use.</summary>
        public static Color ColorOf(ElementType e) => e switch
        {
            ElementType.Umbra => new Color(0.55f, 0.20f, 0.95f),
            ElementType.Frost => new Color(0.30f, 0.85f, 0.95f),
            ElementType.Storm => new Color(0.75f, 0.85f, 1.00f),
            _ => new Color(0.85f, 0.10f, 0.20f),
        };
    }

    /// <summary>
    /// Attunement tag for anything damageable. Damage dealers look this up and
    /// scale their damage through the counter-wheel.
    /// </summary>
    public class ElementalAffinity : MonoBehaviour
    {
        public ElementType attunement;

        public float ModifierFor(ElementType incoming) => ElementMath.Modifier(incoming, attunement);
    }
}
