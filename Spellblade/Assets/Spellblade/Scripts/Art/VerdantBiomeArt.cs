using Unity.AI.Navigation;
using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// VERDANT DEEP set dressing ([BIOME], v2 per Ryan: "trees and open in a
    /// field in nature") — an open sunlit meadow ringed by procedural forest.
    /// No walls: the treeline is the visual boundary and the NavMesh is the
    /// real one (the outer field is marked NotWalkable, so player and enemies
    /// stay in the meadow while the eye wanders into the trees).
    ///
    /// Every tree is primitives: tapered trunk, angled branches, 3 layered
    /// canopy spheres, per-tree random height/lean/green — no two alike, no
    /// imported assets. Canopies sit high enough to never carve the NavMesh.
    /// </summary>
    public static class VerdantBiomeArt
    {
        private static readonly Color Bark = new(0.21f, 0.15f, 0.10f);
        private static readonly Color DeepLeaf = new(0.12f, 0.30f, 0.10f);
        private static readonly Color SunLeaf = new(0.36f, 0.48f, 0.13f);
        private static readonly Color Grass = new(0.30f, 0.42f, 0.14f);
        private static readonly Color SporeGlow = new(0.65f, 0.85f, 0.35f);

        public static void Build(float arenaSize)
        {
            var root = new GameObject("Verdant Dressing").transform;
            float half = arenaSize / 2f;

            BuildOuterField(root, arenaSize);
            BuildTreeline(root, half);
            BuildFieldTrees(root);
            BuildGrassAndFlowers(root, half);
            BuildNatureClaimedRuin(root);
            SporeMotes(arenaSize);
        }

        // -- The world beyond the meadow: visible grass, unwalkable ----------------

        private static void BuildOuterField(Transform root, float arenaSize)
        {
            var outer = GameObject.CreatePrimitive(PrimitiveType.Plane);
            outer.name = "Outer Field";
            outer.transform.SetParent(root);
            outer.transform.position = new Vector3(0f, -0.03f, 0f); // under the meadow, no z-fight
            outer.transform.localScale = Vector3.one * (arenaSize * 3f / 10f);
            outer.GetComponent<Renderer>().material =
                SpellbladeFx.MakeLit(new Color(0.10f, 0.16f, 0.07f), 0.3f); // darker under-canopy grass

            // FULLY invisible to the bake. (v2 used a NotWalkable override, but
            // 3cm under the meadow the voxelizer merged the two planes and poisoned
            // real floor — the player spawned onto a canopy island. The meadow
            // plane's own EDGE is the boundary now: agents can't leave the mesh.)
            MarkIgnoredByNavMesh(outer);
        }

        /// <summary>Dressing must never shape the NavMesh — the bake reads render
        /// meshes, so canopies/grass/flowers would otherwise grow walkable islands
        /// or punch holes in the floor. Applies to this object and its children.</summary>
        private static void MarkIgnoredByNavMesh(GameObject go)
        {
            var modifier = go.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
        }

        // -- The forest wall: two staggered rings of procedural trees ---------------

        private static void BuildTreeline(Transform root, float half)
        {
            var ring = new (int count, float radius, float scaleMin, float scaleMax)[]
            {
                (12, half + 2.0f, 0.9f, 1.15f),  // inner row — the edge of the clearing (thinned per Ryan)
                (9, half + 5.5f, 1.1f, 1.45f),   // outer row — bigger, fading into fog
            };
            foreach (var (count, radius, scaleMin, scaleMax) in ring)
            {
                float phase = Random.Range(0f, 360f);
                for (int i = 0; i < count; i++)
                {
                    float angle = (phase + i * (360f / count) + Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
                    float r = radius + Random.Range(-0.8f, 1.2f);
                    var pos = new Vector3(Mathf.Sin(angle) * r, 0f, Mathf.Cos(angle) * r);
                    BuildTree(root, pos, Random.Range(scaleMin, scaleMax));
                }
            }
        }

        // -- A few big trees IN the meadow (cover to duck bolts behind) -------------

        private static void BuildFieldTrees(Transform root)
        {
            var spots = new Vector3[] // thinned per Ryan — the meadow stays open
            {
                new(-8.5f, 0f, 7.5f),
                new(9f, 0f, 5f),
                new(7.5f, 0f, -9.5f),
            };
            foreach (var pos in spots)
                BuildTree(root, pos, Random.Range(1.05f, 1.3f));
        }

        /// <summary>One procedural tree: tapered trunk (collider kept — enemies path
        /// around it), 2 angled branches, 3 layered canopy spheres. Canopy bottoms
        /// stay ~3m up so the NavMesh bake never reads them as ceilings.</summary>
        private static void BuildTree(Transform root, Vector3 pos, float scale)
        {
            var tree = new GameObject("Tree").transform;
            tree.SetParent(root);
            tree.position = pos;
            tree.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            float height = Random.Range(3.6f, 4.8f) * scale;
            var barkMat = SpellbladeFx.MakeLit(
                Color.Lerp(Bark, new Color(0.28f, 0.22f, 0.15f), Random.value * 0.5f), 0.2f);

            // Trunk — the one piece that keeps its collider.
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree, false);
            trunk.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            trunk.transform.localScale = new Vector3(0.34f * scale, height / 2f, 0.34f * scale);
            trunk.transform.localRotation = Quaternion.Euler(Random.Range(-3.5f, 3.5f), 0f, Random.Range(-3.5f, 3.5f));
            trunk.GetComponent<Renderer>().material = barkMat;

            // A couple of bare branches reaching out of the canopy line.
            for (int i = 0; i < 2; i++)
            {
                var branch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                branch.name = "Branch";
                Object.DestroyImmediate(branch.GetComponent<Collider>());
                MarkIgnoredByNavMesh(branch); // never bakes
                branch.transform.SetParent(tree, false);
                float yaw = Random.Range(0f, 360f);
                branch.transform.localPosition = Quaternion.Euler(0f, yaw, 0f) *
                    new Vector3(0.55f * scale, height * Random.Range(0.62f, 0.78f), 0f);
                branch.transform.localScale = new Vector3(0.1f * scale, 0.55f * scale, 0.1f * scale);
                branch.transform.localRotation = Quaternion.Euler(0f, yaw, Random.Range(38f, 58f));
                branch.GetComponent<Renderer>().material = barkMat;
            }

            // Canopy: one crown + two offset lobes, each its own green.
            float baseGreen = Random.value;
            for (int i = 0; i < 3; i++)
            {
                var lobe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lobe.name = "Canopy";
                Object.DestroyImmediate(lobe.GetComponent<Collider>());
                MarkIgnoredByNavMesh(lobe); // THE tree-top bug: canopy tops baked as walkable islands
                lobe.transform.SetParent(tree, false);

                float size = (i == 0 ? Random.Range(2.9f, 3.5f) : Random.Range(1.9f, 2.5f)) * scale;
                var offset = i == 0
                    ? Vector3.zero
                    : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * new Vector3(size * 0.42f, -0.25f * scale, 0f);
                lobe.transform.localPosition = new Vector3(0f, height + 0.85f * scale, 0f) + offset;
                lobe.transform.localScale = new Vector3(size, size * 0.72f, size);

                var green = Color.Lerp(DeepLeaf, SunLeaf, Mathf.Clamp01(baseGreen + Random.Range(-0.18f, 0.18f)));
                lobe.GetComponent<Renderer>().material = SpellbladeFx.MakeLit(green, 0.15f);
            }
        }

        // -- Grass tufts + wildflowers scattered through the meadow ------------------

        private static void BuildGrassAndFlowers(Transform root, float half)
        {
            var grassMat = SpellbladeFx.MakeLit(Grass, 0.15f);
            var grassDry = SpellbladeFx.MakeLit(Color.Lerp(Grass, new Color(0.55f, 0.52f, 0.2f), 0.4f), 0.15f);

            for (int i = 0; i < 44; i++)
            {
                var pos = new Vector3(Random.Range(-half + 2f, half - 2f), 0f, Random.Range(-half + 2f, half - 2f));
                if (pos.magnitude < 3f) continue; // keep the player spawn readable

                // A tuft: two thin crossed blades, slight lean.
                var tuft = new GameObject("Grass Tuft").transform;
                MarkIgnoredByNavMesh(tuft.gameObject); // covers both blades
                tuft.SetParent(root);
                tuft.position = pos;
                tuft.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                float h = Random.Range(0.28f, 0.55f);
                for (int b = 0; b < 2; b++)
                {
                    var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    blade.name = "Blade";
                    Object.DestroyImmediate(blade.GetComponent<Collider>()); // never bumps the bake
                    blade.transform.SetParent(tuft, false);
                    blade.transform.localPosition = new Vector3(0f, h / 2f, 0f);
                    blade.transform.localScale = new Vector3(0.05f, h, 0.28f);
                    blade.transform.localRotation = Quaternion.Euler(Random.Range(-10f, 10f), b * 90f, Random.Range(-8f, 8f));
                    blade.GetComponent<Renderer>().material = Random.value < 0.7f ? grassMat : grassDry;
                }
            }

            // Wildflowers: stem + soft-glow head, sparse.
            var flowerColors = new[]
            {
                new Color(0.95f, 0.9f, 0.75f),  // meadow white
                new Color(0.95f, 0.75f, 0.3f),  // gold
                new Color(0.7f, 0.5f, 0.9f),    // violet
            };
            var stemMat = SpellbladeFx.MakeLit(new Color(0.2f, 0.32f, 0.12f), 0.2f);
            for (int i = 0; i < 16; i++)
            {
                var pos = new Vector3(Random.Range(-half + 2.5f, half - 2.5f), 0f, Random.Range(-half + 2.5f, half - 2.5f));
                if (pos.magnitude < 3.5f) continue;

                float h = Random.Range(0.3f, 0.5f);
                var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stem.name = "Flower Stem";
                Object.DestroyImmediate(stem.GetComponent<Collider>());
                MarkIgnoredByNavMesh(stem);
                stem.transform.SetParent(root);
                stem.transform.position = pos + Vector3.up * (h / 2f);
                stem.transform.localScale = new Vector3(0.025f, h / 2f, 0.025f);
                stem.GetComponent<Renderer>().material = stemMat;

                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Flower";
                Object.DestroyImmediate(head.GetComponent<Collider>());
                MarkIgnoredByNavMesh(head);
                head.transform.SetParent(root);
                head.transform.position = pos + Vector3.up * (h + 0.04f);
                head.transform.localScale = Vector3.one * Random.Range(0.09f, 0.15f);
                var color = flowerColors[Random.Range(0, flowerColors.Length)];
                head.GetComponent<Renderer>().material = SpellbladeFx.MakeEmissive(color * 0.6f, color, 0.9f);
            }
        }

        // -- One mossy monolith + mushrooms: the forest remembers the ruin ------------

        private static void BuildNatureClaimedRuin(Transform root)
        {
            var stoneMat = SpellbladeFx.MakeLit(new Color(0.24f, 0.25f, 0.20f), 0.3f);
            var runeMat = SpellbladeFx.MakeEmissive(SporeGlow * 0.4f, SporeGlow, 2f);

            var monolith = GameObject.CreatePrimitive(PrimitiveType.Cube);
            monolith.name = "Rune Stone";
            monolith.transform.SetParent(root);
            monolith.transform.position = new Vector3(-11f, 1.2f, 2.5f);
            monolith.transform.localScale = new Vector3(0.9f, 2.5f, 0.5f);
            monolith.transform.rotation = Quaternion.Euler(-4f, 40f, 3f); // sinking into the earth
            monolith.GetComponent<Renderer>().material = stoneMat;

            for (int i = 0; i < 3; i++)
            {
                var rune = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rune.name = "Rune";
                Object.DestroyImmediate(rune.GetComponent<Collider>());
                rune.transform.SetParent(monolith.transform, false);
                rune.transform.localPosition = new Vector3(0f, 0.28f - i * 0.24f, -0.52f);
                rune.transform.localScale = new Vector3(0.22f, 0.1f, 0.05f);
                rune.GetComponent<Renderer>().material = runeMat;
                rune.AddComponent<RunePulse>();
            }

            // Glowing mushrooms at the monolith's foot and one treeline root.
            var capMat = SpellbladeFx.MakeEmissive(SporeGlow * 0.35f, SporeGlow, 1.8f);
            var stemMat = SpellbladeFx.MakeLit(new Color(0.55f, 0.52f, 0.45f), 0.3f);
            var clusters = new Vector3[] { new(-10.4f, 0f, 3.4f), new(9.4f, 0f, 5.8f) };
            foreach (var basePos in clusters)
            {
                for (int i = 0; i < 3; i++)
                {
                    float s = Random.Range(0.12f, 0.26f);
                    var offset = new Vector3(Random.Range(-0.45f, 0.45f), 0f, Random.Range(-0.45f, 0.45f));

                    var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    stem.name = "Mushroom Stem";
                    Object.DestroyImmediate(stem.GetComponent<Collider>());
                    MarkIgnoredByNavMesh(stem);
                    stem.transform.SetParent(root);
                    stem.transform.position = basePos + offset + Vector3.up * (s * 0.8f);
                    stem.transform.localScale = new Vector3(s * 0.35f, s * 0.8f, s * 0.35f);
                    stem.GetComponent<Renderer>().material = stemMat;

                    var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    cap.name = "Mushroom Cap";
                    Object.DestroyImmediate(cap.GetComponent<Collider>());
                    MarkIgnoredByNavMesh(cap);
                    cap.transform.SetParent(root);
                    cap.transform.position = basePos + offset + Vector3.up * (s * 1.7f);
                    cap.transform.localScale = new Vector3(s * 1.4f, s * 0.8f, s * 1.4f);
                    cap.GetComponent<Renderer>().material = capMat;
                }

                var light = new GameObject("Mushroom Glow").AddComponent<Light>();
                light.transform.SetParent(root);
                light.transform.position = basePos + Vector3.up * 0.6f;
                light.type = LightType.Point;
                light.color = SporeGlow;
                light.intensity = 1.4f;
                light.range = 4f;
            }
        }

        // -- Spore motes: the Verdant answer to the Shadow ground mist ------------------
        // Public: the traversal corridor calls this via BiomeStyle.buildAmbientParticles.

        public static void SporeMotes(float areaSize)
        {
            var go = new GameObject("Ambient Spore Motes");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            go.GetComponent<ParticleSystemRenderer>().material = SpellbladeParticles.MakeParticleMat(Color.white);
            go.transform.position = new Vector3(0f, 1.6f, 0f);

            var main = ps.main;
            main.startLifetime = 12f;
            main.startSpeed = 0.12f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
            main.startColor = new Color(SporeGlow.r, SporeGlow.g, SporeGlow.b, 0.35f);
            main.maxParticles = 90;
            main.gravityModifier = -0.008f; // spores rise, mist sinks — opposite souls
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            main.loop = true;

            var emission = ps.emission;
            emission.rateOverTime = 7f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(areaSize * 0.9f, 2.6f, areaSize * 0.9f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.22f;
            noise.frequency = 0.2f;

            ps.Play();
        }
    }
}
