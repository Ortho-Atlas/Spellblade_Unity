using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Spellblade
{
    /// <summary>
    /// SPELLBLADE PHASE 1 BOOTSTRAP
    /// Drop this on one empty GameObject in an empty scene, press Play, and it
    /// assembles the entire playground at runtime:
    ///   arena + walls/pillars → NavMesh bake → Shadow-Realm mood (fog, moonlight,
    ///   URP post volume) → player (Starter Assets rig if available, else capsule)
    ///   → training dummies → MOBA camera → four disciplines → HUD.
    ///
    /// (Or just use the menu: Spellblade → Create Playground Scene.)
    /// </summary>
    public class SpellbladeBootstrap : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Starter Assets PlayerArmature prefab. Leave empty — in the editor it auto-loads from Assets/StarterAssets if imported; otherwise a capsule is used.")]
        public GameObject playerRigPrefab;
        public float moveSpeed = 5.5f;

        [Header("Arena")]
        public float arenaSize = 30f;
        public float wallHeight = 3f;

        [Header("Camera")]
        public float cameraHeight = 12f;
        public float cameraDistance = 8f;
        [Range(20f, 80f)] public float cameraTilt = 52f;

        [Header("Biome")] // [BIOME] mood/palette/dressing now come from BiomeStyle
        [Tooltip("Playground preview only: biome to build when no arena node is loaded (\"shadow\", \"verdant\"). Arenas use their node's region.")]
        public string playgroundBiome = "shadow";

        [Header("Dummies")]
        public int dummyCount = 5;
        public float dummyHealth = 120f;
        public float dummyRespawnDelay = 4f;

        [Header("Mana")]
        public float maxMana = 100f;
        public float manaRegenPerSecond = 9f;

        private Transform _player;
        private BiomeStyle _style; // [BIOME]

        private void Start()
        {
            bool traversal = GameSession.CurrentNode != null &&
                             GameSession.CurrentNode.objective == ObjectiveType.Traversal; // [PHASE2-04]

            // [BIOME] Arena nodes bring their region's look; playground previews via the field.
            _style = BiomeStyle.For(GameSession.CurrentNode != null
                ? GameSession.CurrentNode.regionId : playgroundBiome);

            SetMood();
            if (traversal) TraversalArena.Build(wallHeight, _style); else BuildArena(); // [PHASE2-04] corridor layout, [BIOME] region palette
            BakeNavMesh();      // bake BEFORE dummies so they carve holes instead of becoming floor
            SpawnPlayer();
            if (GameSession.CurrentNode == null) SpawnDummies(); // [PHASE2-04] arena enemies come from the ObjectiveDirector
            SetupCamera();
            SetupPostProcessing();
            SetupHud();
            if (GameSession.CurrentNode != null) gameObject.AddComponent<ObjectiveDirector>(); // [PHASE2-04] ArenaFlow configures it below
            ArenaFlow.Begin(this); // [PHASE2-03] arena mode: node flow + Esc abandon; no-op in playground (CurrentNode == null)

            Debug.Log("[Spellblade] Playground assembled. WASD move · Space melee · hold RMB = spell wheel · scroll = discipline · LMB cast."); // [PHASE2-02]
        }

        // ---------------------------------------------------------------- Mood

        private void SetMood()
        {
            // [BIOME] Style-driven: Shadow = Scotland gloom, Verdant = gold through canopy.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = _style.ambient;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = _style.fogColor;
            RenderSettings.fogDensity = _style.fogDensity;

            // Silence any lights the scene came with — the mood is ours.
            foreach (var light in FindObjectsByType<Light>())
                light.enabled = false;

            var sky = new GameObject("Biome Skylight").AddComponent<Light>();
            sky.type = LightType.Directional;
            sky.color = _style.sunColor;
            sky.intensity = _style.sunIntensity;
            sky.shadows = LightShadows.Soft;
            sky.transform.rotation = Quaternion.Euler(_style.sunAngles);
        }

        // ---------------------------------------------------------------- Arena

        private void BuildArena()
        {
            var arena = new GameObject("Arena").transform;

            // Floor: deep desaturated green, wet with mist (high-ish smoothness = damp sheen).
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(arena);
            ground.transform.localScale = Vector3.one * (arenaSize / 10f); // plane is 10x10 at scale 1
            ground.GetComponent<Renderer>().material =
                SpellbladeFx.MakeLit(_style.groundColor, _style.groundSmoothness); // [BIOME]

            if (!_style.openField) // [BIOME] open-field biomes: treeline + NavMesh bound the arena, not stone
            {
                // Perimeter walls, palette per biome.
                var wallMat = SpellbladeFx.MakeLit(_style.wallColor, 0.2f); // [BIOME]
                float half = arenaSize / 2f;
                CreateWall(arena, wallMat, new Vector3(0, wallHeight / 2f, half), new Vector3(arenaSize + 1f, wallHeight, 1f));
                CreateWall(arena, wallMat, new Vector3(0, wallHeight / 2f, -half), new Vector3(arenaSize + 1f, wallHeight, 1f));
                CreateWall(arena, wallMat, new Vector3(half, wallHeight / 2f, 0), new Vector3(1f, wallHeight, arenaSize + 1f));
                CreateWall(arena, wallMat, new Vector3(-half, wallHeight / 2f, 0), new Vector3(1f, wallHeight, arenaSize + 1f));

                // Interior pillars — pushed to the arena edges so the corridor between
                // the spawn and the cultist arc has CLEAR firing lanes (Ryan's call).
                // They still give the NavMesh something to path around at the flanks.
                var pillarMat = SpellbladeFx.MakeLit(_style.pillarColor, 0.25f); // [BIOME]
                var obstacles = new (Vector3 pos, Vector3 scale)[]
                {
                    (new Vector3(-11f, 2f, -7f), new Vector3(1.4f, 4f, 1.4f)),
                    (new Vector3( 11f, 2f, -7f), new Vector3(1.4f, 4f, 1.4f)),
                    (new Vector3(-11.5f, 2f, 6f), new Vector3(1.4f, 4f, 1.4f)),
                    (new Vector3( 11.5f, 2f, 6f), new Vector3(1.4f, 4f, 1.4f)),
                    (new Vector3( 0f, 1.5f, -11.5f), new Vector3(6f, 3f, 1f)), // broken wall, south of spawn
                };
                foreach (var (pos, scale) in obstacles)
                    CreateWall(arena, pillarMat, pos, scale, "Pillar");
            }

            // [BIOME] Region set dressing (spires + mist for Shadow, roots + spores
            // for Verdant). Built before the NavMesh bake so big pieces block pathing.
            _style.buildDressing(arenaSize);
        }

        private void CreateWall(Transform parent, Material mat, Vector3 pos, Vector3 scale, string name = "Wall")
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().material = mat;
        }

        // ---------------------------------------------------------------- NavMesh

        private void BakeNavMesh()
        {
            var surface = new GameObject("NavMesh Surface").AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.BuildNavMesh(); // runtime bake — walls/pillars become unwalkable automatically
        }

        // ---------------------------------------------------------------- Player

        private void SpawnPlayer()
        {
            var player = new GameObject("Player");
            player.transform.position = GameSession.CurrentNode?.objective == ObjectiveType.Traversal
                ? new Vector3(0f, 0f, -26f)   // [PHASE2-04] traversal: enter at chamber 1's south end
                : new Vector3(0f, 0f, -6f);
            _player = player.transform;

            var agent = player.AddComponent<NavMeshAgent>();
            agent.speed = moveSpeed;
            agent.acceleration = 24f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 0.1f;
            agent.radius = 0.4f;
            agent.height = 2f;

            var controller = player.AddComponent<WasdController>(); // [PHASE2-01] WASD replaces click-to-move
            player.AddComponent<MeleeStrike>();                     // [PHASE2-01] Space melee — the "blade"
            var mana = player.AddComponent<ManaPool>();
            mana.maxMana = maxMana;
            mana.regenPerSecond = manaRegenPerSecond;

            var caster = player.AddComponent<SpellCaster>();
            caster.SetDisciplines(CreateDisciplines());

            var health = player.AddComponent<Health>(); // [PHASE2-04] the player is mortal now
            health.maxHealth = 120f;
            health.Revive(); // Awake ran before maxHealth was set — sync Current
            player.AddComponent<PlayerLife>();          // [PHASE2-04] vignette pulse + death flow

            var hitbox = player.AddComponent<CapsuleCollider>(); // [PHASE2-04] enemy projectiles sweep physics — they need something to hit
            hitbox.center = new Vector3(0f, 1f, 0f);
            hitbox.height = 2f;
            hitbox.radius = 0.4f;

            if (GameSession.CurrentNode != null) // [PHASE2-05] ranks + stat upgrades apply in arenas; playground stays baseline
                ProgressionMath.ApplyArenaLoadout(caster, player);

            AttachVisual(player, controller);

            // The "skin": orbiting soul-shard + glow + wisps, tinted per discipline.
            player.AddComponent<DisciplineAura>();
        }

        /// <summary>Starter Assets rig if available (animator wired), else a capsule.</summary>
        private void AttachVisual(GameObject player, WasdController controller) // [PHASE2-01]
        {
#if UNITY_EDITOR
            // Auto-find the rig so no manual Inspector wiring is ever needed.
            if (playerRigPrefab == null)
            {
                playerRigPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab");
            }
#endif
            if (playerRigPrefab != null)
            {
                // Instantiate deactivated so Starter Assets scripts never run a frame,
                // then strip everything except the visual rig + Animator. Movement is
                // 100% NavMeshAgent — their CharacterController would fight it.
                var holder = new GameObject("Rig Holder");
                holder.SetActive(false);

                var rig = Instantiate(playerRigPrefab, holder.transform);
                foreach (var script in rig.GetComponentsInChildren<MonoBehaviour>(true))
                    DestroyImmediate(script);
                var cc = rig.GetComponent<CharacterController>();
                if (cc != null) DestroyImmediate(cc);

                rig.transform.SetParent(player.transform, false);
                rig.transform.localPosition = Vector3.zero;
                rig.transform.localRotation = Quaternion.identity;
                rig.name = "Player Rig";
                rig.AddComponent<RigAnimationEvents>(); // absorbs OnFootstep/OnLand animation events
                Destroy(holder);

                controller.BindAnimator(rig.GetComponent<Animator>());
                WizardGear.Dress(player, rig.transform); // hat, staff, charcoal robes
                Debug.Log("[Spellblade] Starter Assets rig attached — walk/run animations live.");
            }
            else
            {
                // Capsule fallback: dark body with a violet emissive "core".
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Capsule Body";
                Destroy(body.GetComponent<Collider>()); // agent handles collision
                body.transform.SetParent(player.transform, false);
                body.transform.localPosition = new Vector3(0f, 1f, 0f);
                body.GetComponent<Renderer>().material =
                    SpellbladeFx.MakeLit(new Color(0.12f, 0.10f, 0.18f), 0.5f);

                var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                core.name = "Core";
                Destroy(core.GetComponent<Collider>());
                core.transform.SetParent(player.transform, false);
                core.transform.localPosition = new Vector3(0f, 1.2f, 0.28f);
                core.transform.localScale = Vector3.one * 0.3f;
                core.GetComponent<Renderer>().material = SpellbladeFx.MakeEmissive(
                    new Color(0.4f, 0.15f, 0.6f), new Color(0.55f, 0.15f, 1f), 2.5f);

                WizardGear.Dress(player, null); // hat + staff on static anchors
                Debug.Log("[Spellblade] No Starter Assets rig found — using capsule fallback.");
            }
        }

        // ---------------------------------------------------------------- Dummies

        private void SpawnDummies()
        {
            // Arc of dummies across the arena's north half, cycling through the
            // four elements so every attunement is represented (5th wraps to Umbra).
            for (int i = 0; i < dummyCount; i++)
            {
                float t = dummyCount > 1 ? i / (float)(dummyCount - 1) : 0.5f;
                float angle = Mathf.Lerp(-55f, 55f, t) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(angle) * 9f, 0f, 4f + Mathf.Cos(angle) * 5f);
                Dummy.Spawn(pos, dummyHealth, dummyRespawnDelay, (ElementType)(i % 4));
            }
        }

        // ---------------------------------------------------------------- Camera

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _style.cameraBackground; // [BIOME]

            var follow = cam.GetComponent<MobaCamera>();
            if (follow == null) follow = cam.gameObject.AddComponent<MobaCamera>();
            follow.target = _player;
            follow.height = cameraHeight;
            follow.distance = cameraDistance;
            follow.tilt = cameraTilt;
            follow.SnapToTarget();
        }

        // ---------------------------------------------------------------- Post FX

        private void SetupPostProcessing()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>();
            bloom.intensity.Override(1.3f);
            bloom.threshold.Override(0.9f);
            bloom.scatter.Override(0.65f);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(_style.vignetteColor); // [BIOME]

            var colors = profile.Add<ColorAdjustments>();
            colors.postExposure.Override(0.15f);
            colors.contrast.Override(_style.postContrast);     // [BIOME]
            colors.saturation.Override(_style.postSaturation); // [BIOME]
            colors.colorFilter.Override(_style.postFilter);    // [BIOME]

            var volumeGo = new GameObject("Biome Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;

            // Make sure the camera actually renders post-processing.
            var cam = Camera.main;
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            }
        }

        // ---------------------------------------------------------------- HUD

        private void SetupHud()
        {
            var caster = _player.GetComponent<SpellCaster>();
            var mana = _player.GetComponent<ManaPool>();
            HudController.Build(caster, mana, _player.GetComponent<Health>()); // [PHASE2-04] + HP bar
        }

        // ---------------------------------------------------------------- Spells

        /// <summary>Roster factory lives in SpellLibrary now ([PHASE2-05]) —
        /// the Sanctum reads the same 16 spells on the map scene.</summary>
        private List<Discipline> CreateDisciplines() => SpellLibrary.CreateDisciplines();
    }
}
