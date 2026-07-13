using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// Corridor layout for Traversal nodes (Plan 04): three connected chambers
    /// (~22×20 each) along +Z with offset door gaps, built from the same
    /// wall/pillar kit and palette as the square arena. Player enters chamber 1
    /// (south); the exit portal sits in chamber 3 (north). Doors are open —
    /// sprinting past every fight is legal and fun.
    ///
    /// ShadowBiomeArt.Build is NOT used here: its dressing is hand-placed for
    /// the 30×30 square and would intersect the corridor walls. Same mood is
    /// kept with rubble, pillar stubs, and the ground mist.
    /// </summary>
    public static class TraversalArena
    {
        public const float Width = 22f;    // x span
        public const float Length = 60f;   // z span, -30..+30 — three 20-deep chambers

        public static void Build(float wallHeight, BiomeStyle style) // [BIOME] corridor wears its region's palette
        {
            var arena = new GameObject("Arena (Traversal)").transform;

            var groundMat = SpellbladeFx.MakeLit(style.groundColor, style.groundSmoothness);
            var wallMat = SpellbladeFx.MakeLit(style.wallColor, 0.2f);
            var pillarMat = SpellbladeFx.MakeLit(style.pillarColor, 0.25f);

            // Floor: one long plane (Unity plane is 10×10 at scale 1).
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(arena);
            ground.transform.localScale = new Vector3(Width / 10f + 0.2f, 1f, Length / 10f + 0.2f);
            ground.GetComponent<Renderer>().material = groundMat;

            float halfW = Width / 2f, halfL = Length / 2f;

            // Perimeter.
            Wall(arena, wallMat, new Vector3(0f, wallHeight / 2f, halfL), new Vector3(Width + 1f, wallHeight, 1f));
            Wall(arena, wallMat, new Vector3(0f, wallHeight / 2f, -halfL), new Vector3(Width + 1f, wallHeight, 1f));
            Wall(arena, wallMat, new Vector3(halfW, wallHeight / 2f, 0f), new Vector3(1f, wallHeight, Length + 1f));
            Wall(arena, wallMat, new Vector3(-halfW, wallHeight / 2f, 0f), new Vector3(1f, wallHeight, Length + 1f));

            // Chamber dividers at z = ±10 with offset 4m door gaps (a soft S-path).
            Divider(arena, wallMat, wallHeight, z: -10f, doorCenterX: -3f);
            Divider(arena, wallMat, wallHeight, z: 10f, doorCenterX: 3f);

            // Pillar stubs + rubble — enough cover to duck Cultist bolts.
            var dressing = new (Vector3 pos, Vector3 scale)[]
            {
                (new Vector3(-7f, 2f, -24f), new Vector3(1.3f, 4f, 1.3f)),
                (new Vector3(7.5f, 2f, -16f), new Vector3(1.3f, 4f, 1.3f)),
                (new Vector3(-7.5f, 2f, -3f), new Vector3(1.3f, 4f, 1.3f)),
                (new Vector3(7f, 2f, 3f), new Vector3(1.3f, 4f, 1.3f)),
                (new Vector3(-6.5f, 2f, 20f), new Vector3(1.3f, 4f, 1.3f)),
                (new Vector3(6.5f, 1.5f, 24f), new Vector3(3.5f, 3f, 1f)), // broken wall by the gate
            };
            foreach (var (pos, scale) in dressing)
                Wall(arena, pillarMat, pos, scale, "Pillar");

            var rubbleMat = SpellbladeFx.MakeLit(
                Color.Lerp(style.pillarColor, style.groundColor, 0.4f), 0.2f); // [BIOME]
            var rubble = new (Vector3 pos, float size, float yaw)[]
            {
                (new Vector3(3f, 0.22f, -20f), 0.55f, 25f),
                (new Vector3(-4.5f, 0.18f, -8f), 0.45f, 70f),
                (new Vector3(2f, 0.26f, 7f), 0.6f, 130f),
                (new Vector3(-3f, 0.2f, 15f), 0.5f, 200f),
                (new Vector3(5f, 0.16f, 17f), 0.4f, 310f),
            };
            foreach (var (pos, size, yaw) in rubble)
            {
                var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = "Rubble";
                rock.transform.SetParent(arena);
                rock.transform.position = pos;
                rock.transform.localScale = new Vector3(size, size * 0.8f, size);
                rock.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), yaw, Random.Range(-8f, 8f));
                rock.GetComponent<Renderer>().material = rubbleMat;
            }

            // [BIOME] Mist in the Reach, spores in the Deep.
            style.buildAmbientParticles(Length);
        }

        private static void Divider(Transform parent, Material mat, float wallHeight, float z, float doorCenterX)
        {
            const float doorWidth = 4f;
            float halfW = Width / 2f;

            // Two segments leaving the gap: [-halfW .. doorLeft] and [doorRight .. halfW].
            float doorLeft = doorCenterX - doorWidth / 2f;
            float doorRight = doorCenterX + doorWidth / 2f;

            float leftLen = doorLeft - (-halfW);
            if (leftLen > 0.1f)
                Wall(parent, mat, new Vector3(-halfW + leftLen / 2f, wallHeight / 2f, z),
                     new Vector3(leftLen, wallHeight, 1f), "Divider");

            float rightLen = halfW - doorRight;
            if (rightLen > 0.1f)
                Wall(parent, mat, new Vector3(doorRight + rightLen / 2f, wallHeight / 2f, z),
                     new Vector3(rightLen, wallHeight, 1f), "Divider");
        }

        private static void Wall(Transform parent, Material mat, Vector3 pos, Vector3 scale, string name = "Wall")
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().material = mat;
        }
    }
}
