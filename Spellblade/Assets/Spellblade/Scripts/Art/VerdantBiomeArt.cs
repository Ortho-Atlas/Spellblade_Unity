using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// VERDANT DEEP set dressing ([BIOME]) — the earth-magic biome. An ancient
    /// courtyard the forest took back: mossy boulders, fallen logs, root arches,
    /// rune-carved standing stones gone green, glowing mushroom clusters, and
    /// spore motes drifting through gold light. Built from primitives before the
    /// NavMesh bake, same as ShadowBiomeArt — big pieces block pathing, visual
    /// moss/mushrooms don't (colliders stripped).
    /// </summary>
    public static class VerdantBiomeArt
    {
        private static readonly Color Moss = new(0.22f, 0.34f, 0.14f);
        private static readonly Color Bark = new(0.19f, 0.14f, 0.09f);
        private static readonly Color OldStone = new(0.24f, 0.25f, 0.20f);
        private static readonly Color SporeGlow = new(0.65f, 0.85f, 0.35f); // living green-gold

        public static void Build(float arenaSize)
        {
            var root = new GameObject("Verdant Dressing").transform;
            float half = arenaSize / 2f;

            var mossMat = SpellbladeFx.MakeLit(Moss, 0.25f);
            var barkMat = SpellbladeFx.MakeLit(Bark, 0.2f);
            var stoneMat = SpellbladeFx.MakeLit(OldStone, 0.3f);

            BuildBoulders(root, mossMat, stoneMat);
            BuildFallenLogs(root, barkMat, mossMat);
            BuildRootArch(root, barkMat);
            BuildRuneStones(root, stoneMat);
            BuildMossPatches(root, mossMat);
            BuildMushroomClusters(root);
            SporeMotes(arenaSize);
        }

        // -- Giant mossy boulders at the flanks (block pathing) ------------------

        private static void BuildBoulders(Transform root, Material moss, Material stone)
        {
            var boulders = new (Vector3 pos, float size, float yaw)[]
            {
                (new Vector3(-9.5f, 0f, 10.5f), 2.6f, 20f),
                (new Vector3(10f, 0f, 11f), 3.1f, 130f),
                (new Vector3(-12f, 0f, -1f), 2.2f, 260f),
                (new Vector3(12.5f, 0f, -3f), 2.4f, 75f),
            };
            foreach (var (pos, size, yaw) in boulders)
            {
                // Stone core with a moss cap slightly offset upward — reads as growth.
                var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                core.name = "Boulder";
                core.transform.SetParent(root);
                core.transform.position = pos + Vector3.up * (size * 0.32f);
                core.transform.localScale = new Vector3(size, size * 0.75f, size * 0.9f);
                core.transform.rotation = Quaternion.Euler(0f, yaw, Random.Range(-6f, 6f));
                core.GetComponent<Renderer>().material = stone;

                var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.name = "Boulder Moss";
                Object.DestroyImmediate(cap.GetComponent<Collider>()); // visual only
                cap.transform.SetParent(core.transform, false);
                cap.transform.localPosition = new Vector3(0f, 0.18f, 0f);
                cap.transform.localScale = Vector3.one * 0.96f;
                cap.GetComponent<Renderer>().material = moss;
            }
        }

        // -- Fallen logs along the edges ------------------------------------------

        private static void BuildFallenLogs(Transform root, Material bark, Material moss)
        {
            var logs = new (Vector3 pos, float length, float yaw)[]
            {
                (new Vector3(-4f, 0.45f, 12.5f), 7f, 78f),
                (new Vector3(8.5f, 0.4f, -11f), 5.5f, 15f),
                (new Vector3(-11f, 0.4f, -9f), 4.5f, 305f),
            };
            foreach (var (pos, length, yaw) in logs)
            {
                var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                log.name = "Fallen Log";
                log.transform.SetParent(root);
                log.transform.position = pos;
                log.transform.localScale = new Vector3(0.9f, length / 2f, 0.9f);
                log.transform.rotation = Quaternion.Euler(90f, yaw, 0f); // lying down
                log.GetComponent<Renderer>().material = bark;

                var mossStrip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mossStrip.name = "Log Moss";
                Object.DestroyImmediate(mossStrip.GetComponent<Collider>());
                mossStrip.transform.SetParent(log.transform, false);
                mossStrip.transform.localPosition = new Vector3(0f, 0f, -0.14f); // upper side when lying
                mossStrip.transform.localScale = new Vector3(0.92f, 0.98f, 0.7f);
                mossStrip.GetComponent<Renderer>().material = moss;
            }
        }

        // -- A great root arch over the north approach ------------------------------

        private static void BuildRootArch(Transform root, Material bark)
        {
            var arch = new GameObject("Root Arch").transform;
            arch.SetParent(root);
            arch.position = new Vector3(0f, 0f, 12f);

            var pieces = new (Vector3 pos, Vector3 scale, Vector3 euler)[]
            {
                (new Vector3(-3.2f, 1.6f, 0f), new Vector3(0.8f, 3.4f, 0.8f), new Vector3(0f, 0f, 14f)),
                (new Vector3(3.2f, 1.6f, 0f), new Vector3(0.8f, 3.4f, 0.8f), new Vector3(0f, 0f, -14f)),
                (new Vector3(0f, 3.5f, 0f), new Vector3(0.65f, 3.6f, 0.65f), new Vector3(0f, 0f, 90f)), // spanning root
                (new Vector3(-2.2f, 3.0f, 0.3f), new Vector3(0.35f, 1.4f, 0.35f), new Vector3(20f, 0f, 55f)), // tendril
                (new Vector3(2.4f, 2.9f, -0.3f), new Vector3(0.3f, 1.2f, 0.3f), new Vector3(-15f, 0f, -60f)),
            };
            foreach (var (pos, scale, euler) in pieces)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                piece.name = "Root";
                piece.transform.SetParent(arch, false);
                piece.transform.localPosition = pos;
                piece.transform.localScale = scale;
                piece.transform.localRotation = Quaternion.Euler(euler);
                piece.GetComponent<Renderer>().material = bark;
            }
        }

        // -- Standing stones, runes gone green ---------------------------------------

        private static void BuildRuneStones(Transform root, Material stone)
        {
            var runeMat = SpellbladeFx.MakeEmissive(SporeGlow * 0.4f, SporeGlow, 2f);
            var stones = new (Vector3 pos, float yaw)[]
            {
                (new Vector3(-13f, 0f, 6f), 35f),
                (new Vector3(13f, 0f, 4f), 210f),
                (new Vector3(6f, 0f, -13f), 120f),
            };
            foreach (var (pos, yaw) in stones)
            {
                var monolith = GameObject.CreatePrimitive(PrimitiveType.Cube);
                monolith.name = "Rune Stone";
                monolith.transform.SetParent(root);
                monolith.transform.position = pos + Vector3.up * 1.3f;
                monolith.transform.localScale = new Vector3(0.9f, 2.6f, 0.5f);
                monolith.transform.rotation = Quaternion.Euler(Random.Range(-4f, 4f), yaw, Random.Range(-3f, 3f));
                monolith.GetComponent<Renderer>().material = stone;

                for (int i = 0; i < 3; i++)
                {
                    var rune = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rune.name = "Rune";
                    Object.DestroyImmediate(rune.GetComponent<Collider>());
                    rune.transform.SetParent(monolith.transform, false);
                    rune.transform.localPosition = new Vector3(0f, 0.28f - i * 0.24f, -0.52f);
                    rune.transform.localScale = new Vector3(0.22f, 0.1f, 0.05f);
                    rune.GetComponent<Renderer>().material = runeMat;
                    rune.AddComponent<RunePulse>(); // same slow breath as the Shadow runes
                }
            }
        }

        // -- Moss patches on the loam (visual only — no pathing bumps) ----------------

        private static void BuildMossPatches(Transform root, Material moss)
        {
            var patches = new (Vector3 pos, float size)[]
            {
                (new Vector3(-5f, 0f, 3f), 3.4f),
                (new Vector3(6f, 0f, 7f), 2.6f),
                (new Vector3(2f, 0f, -7f), 3.8f),
                (new Vector3(-8f, 0f, -5f), 2.2f),
                (new Vector3(9f, 0f, 0f), 2.0f),
            };
            foreach (var (pos, size) in patches)
            {
                var patch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                patch.name = "Moss Patch";
                Object.DestroyImmediate(patch.GetComponent<Collider>());
                patch.transform.SetParent(root);
                patch.transform.position = pos + Vector3.up * 0.012f;
                patch.transform.localScale = new Vector3(size, 0.012f, size * Random.Range(0.7f, 1f));
                patch.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                patch.GetComponent<Renderer>().material = moss;
            }
        }

        // -- Glowing mushroom clusters (the biome's night-lights) ----------------------

        private static void BuildMushroomClusters(Transform root)
        {
            var stemMat = SpellbladeFx.MakeLit(new Color(0.55f, 0.52f, 0.45f), 0.3f);
            var capMat = SpellbladeFx.MakeEmissive(SporeGlow * 0.35f, SporeGlow, 1.8f);

            var clusters = new Vector3[]
            {
                new(-9.2f, 0f, 10f), new(11f, 0f, -4.2f), new(-11.5f, 0f, -8.2f),
                new(3.5f, 0f, 12.8f), new(7.8f, 0f, -11.8f),
            };
            foreach (var basePos in clusters)
            {
                for (int i = 0; i < 3; i++)
                {
                    float scale = Random.Range(0.14f, 0.3f);
                    var offset = new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));

                    var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    stem.name = "Mushroom Stem";
                    Object.DestroyImmediate(stem.GetComponent<Collider>());
                    stem.transform.SetParent(root);
                    stem.transform.position = basePos + offset + Vector3.up * (scale * 0.8f);
                    stem.transform.localScale = new Vector3(scale * 0.35f, scale * 0.8f, scale * 0.35f);
                    stem.GetComponent<Renderer>().material = stemMat;

                    var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    cap.name = "Mushroom Cap";
                    Object.DestroyImmediate(cap.GetComponent<Collider>());
                    cap.transform.SetParent(root);
                    cap.transform.position = basePos + offset + Vector3.up * (scale * 1.7f);
                    cap.transform.localScale = new Vector3(scale * 1.4f, scale * 0.8f, scale * 1.4f);
                    cap.GetComponent<Renderer>().material = capMat;
                }

                // One soft light per cluster — cheap, and the gloom-to-glow contrast sells it.
                var light = new GameObject("Mushroom Glow").AddComponent<Light>();
                light.transform.SetParent(root);
                light.transform.position = basePos + Vector3.up * 0.6f;
                light.type = LightType.Point;
                light.color = SporeGlow;
                light.intensity = 1.6f;
                light.range = 4.5f;
            }
        }

        // -- Spore motes: the Verdant answer to the Shadow ground mist ------------------

        private static void SporeMotes(float areaSize)
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
