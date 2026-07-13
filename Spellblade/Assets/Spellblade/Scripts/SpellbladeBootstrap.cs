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

        [Header("Shadow Realm Mood")]
        [Range(0f, 0.1f)] public float fogDensity = 0.028f;
        public float moonIntensity = 0.55f;

        [Header("Dummies")]
        public int dummyCount = 5;
        public float dummyHealth = 120f;
        public float dummyRespawnDelay = 4f;

        [Header("Mana")]
        public float maxMana = 100f;
        public float manaRegenPerSecond = 9f;

        private Transform _player;

        private void Start()
        {
            bool traversal = GameSession.CurrentNode != null &&
                             GameSession.CurrentNode.objective == ObjectiveType.Traversal; // [PHASE2-04]

            SetMood();
            if (traversal) TraversalArena.Build(wallHeight); else BuildArena(); // [PHASE2-04] corridor layout for Traversal nodes
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
            // Shadow Biome lore: "Scotland gloom" — overcast, ancient, war-worn.
            // Flat diffuse light under heavy low clouds, not supernatural blackness.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.11f, 0.12f, 0.13f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.10f, 0.115f, 0.11f); // gray-green mist
            RenderSettings.fogDensity = fogDensity;

            // Silence any lights the scene came with — the mood is ours.
            foreach (var light in FindObjectsByType<Light>())
                light.enabled = false;

            // Cold silver skylight — the ambient light of a sky a few hours from rain.
            var sky = new GameObject("Overcast Skylight").AddComponent<Light>();
            sky.type = LightType.Directional;
            sky.color = new Color(0.72f, 0.76f, 0.82f);
            sky.intensity = moonIntensity;
            sky.shadows = LightShadows.Soft;
            sky.transform.rotation = Quaternion.Euler(58f, -25f, 0f);
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
                SpellbladeFx.MakeLit(new Color(0.085f, 0.125f, 0.09f), 0.55f);

            // Perimeter walls: weathered charcoal stone.
            var wallMat = SpellbladeFx.MakeLit(new Color(0.115f, 0.115f, 0.135f), 0.2f);
            float half = arenaSize / 2f;
            CreateWall(arena, wallMat, new Vector3(0, wallHeight / 2f, half), new Vector3(arenaSize + 1f, wallHeight, 1f));
            CreateWall(arena, wallMat, new Vector3(0, wallHeight / 2f, -half), new Vector3(arenaSize + 1f, wallHeight, 1f));
            CreateWall(arena, wallMat, new Vector3(half, wallHeight / 2f, 0), new Vector3(1f, wallHeight, arenaSize + 1f));
            CreateWall(arena, wallMat, new Vector3(-half, wallHeight / 2f, 0), new Vector3(1f, wallHeight, arenaSize + 1f));

            // Interior pillars — pushed to the arena edges so the corridor between
            // the spawn and the cultist arc has CLEAR firing lanes (Ryan's call).
            // They still give the NavMesh something to path around at the flanks.
            var pillarMat = SpellbladeFx.MakeLit(new Color(0.15f, 0.15f, 0.17f), 0.25f);
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

            // Shadow Biome dressing: spires, arches, iron gate, standing stones,
            // rubble, ground mist. Built before the NavMesh bake so stone blocks pathing.
            ShadowBiomeArt.Build(arenaSize);
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
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);

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
            vignette.color.Override(new Color(0.01f, 0.01f, 0.03f));

            var colors = profile.Add<ColorAdjustments>();
            colors.postExposure.Override(0.15f);
            colors.contrast.Override(16f);                                   // heavy but not void-black
            colors.saturation.Override(-22f);                                // "color pulled out by centuries of overcast"
            colors.colorFilter.Override(new Color(0.86f, 0.92f, 0.94f));     // cold silver-gray cast

            var volumeGo = new GameObject("Shadow Realm Volume");
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

        /// <summary>
        /// The four disciplines, each with a FULL 4-slot wheel roster
        /// ([PHASE2-02] — was 1 spell each). Generated in code so nothing
        /// depends on .asset files; every spell carries a stable spellId
        /// ("umbra_1".."blood_4") that progression and saves key off.
        /// Wheel slot order: index 0 top, then clockwise right/bottom/left.
        /// </summary>
        private List<Discipline> CreateDisciplines()
        {
            var umbra = new Color(0.55f, 0.20f, 0.95f);
            var frost = new Color(0.30f, 0.85f, 0.95f);
            var storm = new Color(0.75f, 0.85f, 1.00f);
            var blood = new Color(0.85f, 0.10f, 0.20f);

            // ---------------- UMBRA — burst / assassin ----------------

            var umbralLance = SpellSO.Create("Umbral Lance", umbra, AimType.LineSkillshot);
            umbralLance.spellId = "umbra_1";
            umbralLance.element = ElementType.Umbra;
            umbralLance.manaCost = 18f;
            umbralLance.cooldown = 2.5f;
            umbralLance.damage = 32f;
            umbralLance.projectileSpeed = 26f;
            umbralLance.range = 16f;
            umbralLance.projectileRadius = 0.35f;

            var shadowstep = SpellSO.Create("Shadowstep", umbra, AimType.Blink);
            shadowstep.spellId = "umbra_2";
            shadowstep.element = ElementType.Umbra;
            shadowstep.manaCost = 20f;
            shadowstep.cooldown = 6f;
            shadowstep.blinkDistance = 8f;
            shadowstep.damage = 12f;      // the shadow-burst left at the origin
            shadowstep.selfRadius = 2.2f;

            var creepingDark = SpellSO.Create("Creeping Dark", umbra, AimType.PointAoE);
            creepingDark.spellId = "umbra_3";
            creepingDark.element = ElementType.Umbra;
            creepingDark.manaCost = 26f;
            creepingDark.cooldown = 7f;
            creepingDark.damage = 10f;
            creepingDark.range = 12f;
            creepingDark.aoeRadius = 3.5f;
            creepingDark.dotDamagePerSecond = 10f;
            creepingDark.dotDuration = 3f;

            var nightNova = SpellSO.Create("Night Nova", umbra, AimType.SelfNova);
            nightNova.spellId = "umbra_4";
            nightNova.element = ElementType.Umbra;
            nightNova.manaCost = 32f;
            nightNova.cooldown = 9f;
            nightNova.damage = 34f;
            nightNova.selfRadius = 3.2f;

            // ---------------- FROST — control ----------------

            var rimeblast = SpellSO.Create("Rimeblast", frost, AimType.LineSkillshot);
            rimeblast.spellId = "frost_1";
            rimeblast.element = ElementType.Frost;
            rimeblast.manaCost = 22f;
            rimeblast.cooldown = 5f;
            rimeblast.damage = 24f;
            rimeblast.projectileSpeed = 18f;
            rimeblast.range = 9f;
            rimeblast.projectileRadius = 0.5f;
            rimeblast.slowPercent = 0.35f;
            rimeblast.slowDuration = 2.5f;

            var glacialSpike = SpellSO.Create("Glacial Spike", frost, AimType.LineSkillshot);
            glacialSpike.spellId = "frost_2";
            glacialSpike.element = ElementType.Frost;
            glacialSpike.manaCost = 30f;
            glacialSpike.cooldown = 8f;
            glacialSpike.damage = 46f;
            glacialSpike.projectileSpeed = 14f;
            glacialSpike.range = 14f;
            glacialSpike.projectileRadius = 0.6f;

            var frostNova = SpellSO.Create("Frost Nova", frost, AimType.SelfNova);
            frostNova.spellId = "frost_3";
            frostNova.element = ElementType.Frost;
            frostNova.manaCost = 28f;
            frostNova.cooldown = 9f;
            frostNova.damage = 18f;
            frostNova.selfRadius = 4f;
            frostNova.slowPercent = 0.45f;
            frostNova.slowDuration = 3f;

            var iceWard = SpellSO.Create("Ice Ward", frost, AimType.SelfNova);
            iceWard.spellId = "frost_4";
            iceWard.element = ElementType.Frost;
            iceWard.manaCost = 26f;
            iceWard.cooldown = 14f;
            iceWard.damage = 0f;
            iceWard.shieldAmount = 40f;
            iceWard.shieldDuration = 6f;

            // ---------------- STORM — speed / chain ----------------

            var tempestBolt = SpellSO.Create("Tempest Bolt", storm, AimType.LineSkillshot);
            tempestBolt.spellId = "storm_1";
            tempestBolt.element = ElementType.Storm;
            tempestBolt.manaCost = 20f;
            tempestBolt.cooldown = 3.5f;
            tempestBolt.damage = 26f;
            tempestBolt.projectileSpeed = 34f;
            tempestBolt.range = 18f;
            tempestBolt.projectileRadius = 0.25f;

            var chainSpark = SpellSO.Create("Chain Spark", storm, AimType.LineSkillshot);
            chainSpark.spellId = "storm_2";
            chainSpark.element = ElementType.Storm;
            chainSpark.manaCost = 24f;
            chainSpark.cooldown = 5f;
            chainSpark.damage = 20f;
            chainSpark.projectileSpeed = 26f;
            chainSpark.range = 16f;
            chainSpark.projectileRadius = 0.3f;
            chainSpark.chainCount = 3;
            chainSpark.chainRadius = 5f;

            var zephyrRush = SpellSO.Create("Zephyr Rush", storm, AimType.SelfNova);
            zephyrRush.spellId = "storm_3";
            zephyrRush.element = ElementType.Storm;
            zephyrRush.manaCost = 18f;
            zephyrRush.cooldown = 10f;
            zephyrRush.damage = 0f;
            zephyrRush.selfRadius = 1.6f; // wind burst visual only
            zephyrRush.hasteMultiplier = 1.45f;
            zephyrRush.hasteDuration = 3.5f;

            var thunderhead = SpellSO.Create("Thunderhead", storm, AimType.PointAoE);
            thunderhead.spellId = "storm_4";
            thunderhead.element = ElementType.Storm;
            thunderhead.manaCost = 30f;
            thunderhead.cooldown = 8f;
            thunderhead.damage = 44f;
            thunderhead.range = 12f;
            thunderhead.aoeRadius = 2.5f;
            thunderhead.aoeDelaySeconds = 0.8f; // telegraphed strike

            // ---------------- BLOOD — DoT / lifesteal ----------------

            var hemorrhage = SpellSO.Create("Hemorrhage", blood, AimType.PointAoE);
            hemorrhage.spellId = "blood_1";
            hemorrhage.element = ElementType.Blood;
            hemorrhage.manaCost = 30f;
            hemorrhage.cooldown = 7f;
            hemorrhage.damage = 20f;
            hemorrhage.range = 12f;
            hemorrhage.aoeRadius = 2.75f;
            hemorrhage.dotDamagePerSecond = 8f;
            hemorrhage.dotDuration = 4f;

            var leechBolt = SpellSO.Create("Leech Bolt", blood, AimType.LineSkillshot);
            leechBolt.spellId = "blood_2";
            leechBolt.element = ElementType.Blood;
            leechBolt.manaCost = 22f;
            leechBolt.cooldown = 4f;
            leechBolt.damage = 24f;
            leechBolt.projectileSpeed = 20f;
            leechBolt.range = 14f;
            leechBolt.projectileRadius = 0.35f;
            leechBolt.lifestealPercent = 0.4f;

            var crimsonPact = SpellSO.Create("Crimson Pact", blood, AimType.SelfNova);
            crimsonPact.spellId = "blood_3";
            crimsonPact.element = ElementType.Blood;
            crimsonPact.manaCost = 0f;
            crimsonPact.cooldown = 12f;
            crimsonPact.damage = 0f;
            crimsonPact.selfRadius = 1.4f; // blood-rite visual only
            crimsonPact.healthCost = 15f;
            crimsonPact.manaRestore = 40f;

            var bloodNova = SpellSO.Create("Blood Nova", blood, AimType.SelfNova);
            bloodNova.spellId = "blood_4";
            bloodNova.element = ElementType.Blood;
            bloodNova.manaCost = 34f;
            bloodNova.cooldown = 11f;
            bloodNova.damage = 16f;
            bloodNova.selfRadius = 3.5f;
            bloodNova.dotDamagePerSecond = 9f;
            bloodNova.dotDuration = 4f;
            bloodNova.sustainHealPerSecond = 3f;

            return new List<Discipline>
            {
                Discipline.Create("Umbra", umbra, "1", umbralLance, shadowstep, creepingDark, nightNova),
                Discipline.Create("Frost", frost, "2", rimeblast, glacialSpike, frostNova, iceWard),
                Discipline.Create("Storm", storm, "3", tempestBolt, chainSpark, zephyrRush, thunderhead),
                Discipline.Create("Blood", blood, "4", hemorrhage, leechBolt, crimsonPact, bloodNova),
            };
        }
    }
}
