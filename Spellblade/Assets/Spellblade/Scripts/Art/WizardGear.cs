using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// WIZARD GEAR PACK (procedural, Elden Ring vibes)
    /// Dresses the player as a battle-wizard of the Shadow Biome:
    ///   - Tall crooked wizard hat (charcoal cloth, cold-silver band) on the head
    ///   - Gnarled staff with a glowing orb in the right hand — the orb retints
    ///     to the active discipline via DisciplineAura
    ///   - Robes darkened to charcoal so the rig reads as cloth, not sportswear
    ///
    /// Gear follows animation bones by POSITION but stays world-upright
    /// (FollowBone), so it never inherits weird bone axes and always reads
    /// clean from the MOBA camera. Works on both the Starter Assets rig and
    /// the capsule fallback.
    /// </summary>
    public static class WizardGear
    {
        private static readonly Color Cloth = new(0.095f, 0.095f, 0.115f); // charcoal cloth
        private static readonly Color Silver = new(0.48f, 0.52f, 0.58f);   // cold silver
        private static readonly Color Wood = new(0.16f, 0.13f, 0.10f);     // gnarled dark wood

        /// <summary>Dress the player. rigRoot may be null (capsule fallback).</summary>
        public static void Dress(GameObject player, Transform rigRoot)
        {
            DarkenRobes(rigRoot);

            var headBone = FindHeadBone(rigRoot);
            var handBone = FindRightHandBone(rigRoot);
            var hipsBone = FindHipsBone(rigRoot);

            BuildHat(player,
                bone: headBone != null ? headBone : MakeAnchor(player, new Vector3(0f, 1.92f, 0f)),
                offset: headBone != null ? new Vector3(0f, 0.14f, 0f) : Vector3.zero);

            BuildStaff(player,
                bone: handBone != null ? handBone : MakeAnchor(player, new Vector3(0.5f, 1.0f, 0.15f)),
                offset: handBone != null ? new Vector3(0.1f, 0f, 0.05f) : Vector3.zero);

            BuildRobe(player,
                bone: hipsBone != null ? hipsBone : MakeAnchor(player, new Vector3(0f, 0.95f, 0f)));

            ApplyEquippedGear(player); // [PHASE2-05] cosmetics from the save, additive on the base kit
        }

        // ------------------------------------------------------------ [PHASE2-05]

        /// <summary>Render every equipped cosmetic from SaveSystem.Data.equippedGear.
        /// All variants are ADDITIVE pieces on the base hat/staff/robe (the base kit
        /// never changes shape, so unequipping is just not adding).</summary>
        private static void ApplyEquippedGear(GameObject player)
        {
            var hat = player.transform.Find("Wizard Hat");
            var staff = player.transform.Find("Wizard Staff");
            var robe = player.transform.Find("Wizard Robe");

            foreach (var id in SaveSystem.Data.equippedGear)
            {
                switch (id)
                {
                    case "hat_umbral_court" when hat != null:
                    {
                        // Taller: two extra crooked tiers past the tip, violet band glow.
                        var violet = ElementMath.ColorOf(ElementType.Umbra);
                        var glow = SpellbladeFx.MakeEmissive(violet * 0.5f, violet, 2.2f);
                        var cloth = SpellbladeFx.MakeLit(Cloth, 0.12f);
                        Piece(hat.gameObject, glow, PrimitiveType.Cylinder, new Vector3(0f, 0.105f, 0f), new Vector3(0.36f, 0.012f, 0.36f), 0f); // violet band glow
                        Piece(hat.gameObject, cloth, PrimitiveType.Cylinder, new Vector3(0.24f, 1.00f, 0f), new Vector3(0.045f, 0.10f, 0.045f), 44f);
                        Piece(hat.gameObject, cloth, PrimitiveType.Cylinder, new Vector3(0.33f, 1.13f, 0f), new Vector3(0.028f, 0.08f, 0.028f), 58f);
                        Piece(hat.gameObject, glow, PrimitiveType.Sphere, new Vector3(0.39f, 1.21f, 0f), Vector3.one * 0.05f, 0f);
                        break;
                    }
                    case "rimeholt_crown" when hat != null:
                    {
                        // Icy circlet: ring of frost spikes around the band.
                        var ice = ElementMath.ColorOf(ElementType.Frost);
                        var iceMat = SpellbladeFx.MakeEmissive(ice * 0.45f, ice, 1.8f);
                        for (int i = 0; i < 8; i++)
                        {
                            float angle = i * 45f * Mathf.Deg2Rad;
                            Piece(hat.gameObject, iceMat, PrimitiveType.Cube,
                                  new Vector3(Mathf.Cos(angle) * 0.30f, 0.115f, Mathf.Sin(angle) * 0.30f),
                                  new Vector3(0.03f, 0.09f, 0.03f), 0f);
                        }
                        break;
                    }
                    case "duelists_plume" when hat != null:
                    {
                        var plumeMat = SpellbladeFx.MakeLit(new Color(0.85f, 0.82f, 0.75f), 0.35f);
                        Piece(hat.gameObject, plumeMat, PrimitiveType.Cube, new Vector3(-0.30f, 0.30f, 0f), new Vector3(0.03f, 0.34f, 0.09f), -28f);
                        Piece(hat.gameObject, plumeMat, PrimitiveType.Sphere, new Vector3(-0.40f, 0.46f, 0f), new Vector3(0.06f, 0.12f, 0.1f), 0f);
                        break;
                    }
                    case "staff_crystal_shadow" when staff != null:
                        AddStaffCrystals(staff.gameObject, ElementMath.ColorOf(ElementType.Umbra));
                        break;
                    case "staff_crystal_frost" when staff != null:
                        AddStaffCrystals(staff.gameObject, ElementMath.ColorOf(ElementType.Frost));
                        break;
                    case "robe_tint_shadow" when robe != null:
                        AddRobeTrim(robe.gameObject, ElementMath.ColorOf(ElementType.Umbra));
                        break;
                    case "robe_tint_frost" when robe != null:
                        AddRobeTrim(robe.gameObject, ElementMath.ColorOf(ElementType.Frost));
                        break;
                }
            }
        }

        /// <summary>Shard cluster orbiting the staff orb. Named so DisciplineAura's
        /// "Staff Orb" retint never touches them — the crystals keep their region color.</summary>
        private static void AddStaffCrystals(GameObject staff, Color color)
        {
            var mat = SpellbladeFx.MakeEmissive(color * 0.5f, color, 2.6f);
            var offsets = new[]
            {
                new Vector3(0.12f, 0.90f, 0.03f),
                new Vector3(-0.07f, 0.99f, -0.04f),
                new Vector3(0.05f, 1.05f, 0.08f),
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                var shard = Piece(staff, mat, PrimitiveType.Cube, offsets[i],
                                  new Vector3(0.035f, 0.09f, 0.035f), 30f + i * 40f);
                shard.name = "Staff Crystal";
            }
        }

        private static void AddRobeTrim(GameObject robe, Color color)
        {
            var mat = SpellbladeFx.MakeEmissive(color * 0.45f, color, 1.2f);
            Piece(robe, mat, PrimitiveType.Cylinder, new Vector3(0f, -0.70f, 0f), new Vector3(0.585f, 0.010f, 0.585f), 0f); // tinted hem ring
            Piece(robe, mat, PrimitiveType.Cylinder, new Vector3(0f, -0.30f, 0f), new Vector3(0.435f, 0.008f, 0.435f), 0f); // waist ring
        }

        // -- The hat: brim + four tapering, increasingly crooked tiers -----------

        private static void BuildHat(GameObject player, Transform bone, Vector3 offset)
        {
            var hat = new GameObject("Wizard Hat");
            hat.transform.SetParent(player.transform); // same root → projectiles ignore it
            var follow = hat.AddComponent<FollowBone>();
            follow.bone = bone;
            follow.localOffset = offset;
            follow.tilt = 4f; // whole hat leans a touch

            var clothMat = SpellbladeFx.MakeLit(Cloth, 0.12f);
            // Silver band + trim GLOW faintly — cold moonlit metal, reads through the gloom.
            var bandMat = SpellbladeFx.MakeEmissive(Silver * 0.55f, Silver, 1.4f);

            Piece(hat, clothMat, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.62f, 0.025f, 0.62f), 0f);     // wide brim
            Piece(hat, bandMat, PrimitiveType.Cylinder, new Vector3(0f, 0.045f, 0f), new Vector3(0.645f, 0.008f, 0.645f), 0f);   // glowing brim trim
            Piece(hat, bandMat, PrimitiveType.Cylinder, new Vector3(0f, 0.075f, 0f), new Vector3(0.35f, 0.028f, 0.35f), 0f);     // silver band
            Piece(hat, clothMat, PrimitiveType.Cylinder, new Vector3(0f, 0.19f, 0f), new Vector3(0.32f, 0.15f, 0.32f), 0f);      // crown base
            Piece(hat, clothMat, PrimitiveType.Cylinder, new Vector3(0.03f, 0.44f, 0f), new Vector3(0.22f, 0.14f, 0.22f), 8f);   // mid tier
            Piece(hat, clothMat, PrimitiveType.Cylinder, new Vector3(0.09f, 0.66f, 0f), new Vector3(0.14f, 0.12f, 0.14f), 18f);  // upper tier
            Piece(hat, clothMat, PrimitiveType.Cylinder, new Vector3(0.17f, 0.84f, 0f), new Vector3(0.065f, 0.11f, 0.065f), 32f); // crooked tip
            Piece(hat, bandMat, PrimitiveType.Sphere, new Vector3(0.225f, 0.945f, 0f), Vector3.one * 0.055f, 0f);                // glowing tip stud
        }

        // -- The staff: shaft, knot, and the discipline orb ------------------------

        private static void BuildStaff(GameObject player, Transform bone, Vector3 offset)
        {
            var staff = new GameObject("Wizard Staff");
            staff.transform.SetParent(player.transform);
            var follow = staff.AddComponent<FollowBone>();
            follow.bone = bone;
            follow.localOffset = offset;
            follow.tilt = 10f; // held slightly angled, not parade-straight

            var woodMat = SpellbladeFx.MakeLit(Wood, 0.3f);
            var silverMat = SpellbladeFx.MakeEmissive(Silver * 0.55f, Silver, 1.2f);

            Piece(staff, woodMat, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(0.055f, 0.8f, 0.055f), 0f);    // longer shaft
            Piece(staff, woodMat, PrimitiveType.Cube, new Vector3(0.02f, 0.76f, 0f), new Vector3(0.12f, 0.12f, 0.12f), 20f);     // gnarled knot
            Piece(staff, silverMat, PrimitiveType.Cylinder, new Vector3(0.02f, 0.85f, 0f), new Vector3(0.085f, 0.015f, 0.085f), 0f); // silver collar

            // The orb — DisciplineAura finds it by NAME and retints it with QWER.
            var orb = Piece(staff, SpellbladeFx.MakeEmissive(ElementMath.ColorOf(ElementType.Umbra) * 0.5f,
                                                             ElementMath.ColorOf(ElementType.Umbra), 3f),
                            PrimitiveType.Sphere, new Vector3(0.02f, 0.95f, 0f), Vector3.one * 0.21f, 0f);
            orb.name = "Staff Orb";

            var glow = orb.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 4f;
            glow.intensity = 2.5f;
            glow.color = ElementMath.ColorOf(ElementType.Umbra);
        }

        // -- The robe: skirt + flare + back cloak, following the hips ---------------

        private static void BuildRobe(GameObject player, Transform bone)
        {
            var robe = new GameObject("Wizard Robe");
            robe.transform.SetParent(player.transform);
            var follow = robe.AddComponent<FollowBone>();
            follow.bone = bone;
            follow.localOffset = Vector3.zero;
            follow.tilt = 0f;

            var clothMat = SpellbladeFx.MakeLit(Cloth, 0.12f);
            var trimMat = SpellbladeFx.MakeEmissive(Silver * 0.5f, Silver, 0.9f);

            Piece(robe, clothMat, PrimitiveType.Cylinder, new Vector3(0f, -0.18f, 0f), new Vector3(0.42f, 0.22f, 0.42f), 0f);   // waist wrap
            Piece(robe, clothMat, PrimitiveType.Cylinder, new Vector3(0f, -0.55f, 0f), new Vector3(0.56f, 0.22f, 0.56f), 0f);   // flared skirt
            Piece(robe, trimMat, PrimitiveType.Cylinder, new Vector3(0f, -0.74f, 0f), new Vector3(0.58f, 0.012f, 0.58f), 0f);   // glowing hem trim
            Piece(robe, clothMat, PrimitiveType.Cube, new Vector3(0f, -0.15f, -0.26f), new Vector3(0.44f, 0.85f, 0.05f), 0f);   // back cloak panel
        }

        // -- Helpers -----------------------------------------------------------------

        private static GameObject Piece(GameObject parent, Material mat, PrimitiveType type,
                                        Vector3 localPos, Vector3 scale, float tiltZ)
        {
            var go = GameObject.CreatePrimitive(type);
            // Gear must never block projectiles or the NavMesh bake.
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tiltZ);
            go.GetComponent<Renderer>().material = mat;
            return go;
        }

        /// <summary>Charcoal-tint every rig material so the outfit reads as wizard robes.</summary>
        private static void DarkenRobes(Transform rigRoot)
        {
            if (rigRoot == null) return;
            foreach (var renderer in rigRoot.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.materials)
                {
                    if (!mat.HasProperty("_BaseColor")) continue;
                    var c = mat.GetColor("_BaseColor");
                    mat.SetColor("_BaseColor", Color.Lerp(c, new Color(0.12f, 0.12f, 0.16f), 0.65f));
                }
            }
        }

        private static Transform FindHeadBone(Transform rigRoot)
        {
            if (rigRoot == null) return null;
            foreach (var t in rigRoot.GetComponentsInChildren<Transform>())
            {
                var n = t.name.ToLowerInvariant();
                if (n == "head" || n.EndsWith("head")) return t;
            }
            return null;
        }

        private static Transform FindHipsBone(Transform rigRoot)
        {
            if (rigRoot == null) return null;
            foreach (var t in rigRoot.GetComponentsInChildren<Transform>())
            {
                var n = t.name.ToLowerInvariant();
                if (n.Contains("hips") || n.Contains("pelvis")) return t;
            }
            return null;
        }

        private static Transform FindRightHandBone(Transform rigRoot)
        {
            if (rigRoot == null) return null;
            Transform fallback = null;
            foreach (var t in rigRoot.GetComponentsInChildren<Transform>())
            {
                var n = t.name.ToLowerInvariant();
                if (!n.Contains("hand")) continue;
                if (n.Contains("_r") || n.Contains(".r") || n.Contains("right") || n.EndsWith("r"))
                    return t;
                fallback ??= t;
            }
            return fallback;
        }

        /// <summary>Static attachment point for the capsule fallback (no bones to follow).</summary>
        private static Transform MakeAnchor(GameObject player, Vector3 localPos)
        {
            var anchor = new GameObject("Gear Anchor").transform;
            anchor.SetParent(player.transform, false);
            anchor.localPosition = localPos;
            return anchor;
        }
    }

    /// <summary>
    /// Follows a bone's position (with a yaw-relative offset) while staying
    /// world-upright. Gear tracks the animation without inheriting bone axes —
    /// the hat never ends up sideways mid-run.
    /// </summary>
    public class FollowBone : MonoBehaviour
    {
        public Transform bone;
        public Vector3 localOffset;
        public float tilt;

        private void LateUpdate() // after animation has posed the bones
        {
            if (bone == null) return;
            float yaw = bone.eulerAngles.y;
            transform.position = bone.position + Quaternion.Euler(0f, yaw, 0f) * localOffset;
            transform.rotation = Quaternion.Euler(0f, yaw, tilt);
        }
    }
}
