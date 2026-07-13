using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spellblade.EditorTools
{
    /// <summary>
    /// One-click scene setup: Spellblade → Create Playground Scene.
    /// Makes a fresh empty scene containing only the bootstrap GameObject and
    /// saves it, so "press Play and it works" needs zero manual steps.
    /// </summary>
    public static class SpellbladeSceneSetup
    {
        private const string ScenePath = "Assets/Spellblade/Spellblade Playground.unity";

        [MenuItem("Spellblade/Create Playground Scene")]
        public static void CreatePlaygroundScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrapGo = new GameObject("Spellblade Bootstrap");
            bootstrapGo.AddComponent<SpellbladeBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Spellblade] Playground scene created at {ScenePath} — press Play.");
        }

        // ------------------------------------------------------------ [PHASE2-03]

        private const string WorldMapScenePath = "Assets/Scenes/WorldMap.unity";
        private const string ArenaScenePath = "Assets/Scenes/Arena.unity";

        /// <summary>
        /// Generates the two-scene game structure: WorldMap (build index 0,
        /// WorldMapBootstrap) and Arena (index 1, SpellbladeBootstrap), and
        /// registers both in Build Settings. Rerunnable — overwrites in place.
        /// </summary>
        [MenuItem("Spellblade/Create Game Scenes")]
        public static void CreateGameScenes()
        {
            // The user-prompt save guard has no UI in batchmode (CI/CLI runs).
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            CreateBootstrapScene(WorldMapScenePath, "WorldMap Bootstrap", typeof(WorldMapBootstrap));
            CreateBootstrapScene(ArenaScenePath, "Spellblade Bootstrap", typeof(SpellbladeBootstrap));

            // Build Settings: WorldMap = 0, Arena = 1, any other registered scenes after.
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(WorldMapScenePath, true),
                new EditorBuildSettingsScene(ArenaScenePath, true),
            };
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path == WorldMapScenePath || existing.path == ArenaScenePath) continue;
                scenes.Add(existing);
            }
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("[Spellblade] Game scenes created — WorldMap (index 0) + Arena (index 1) in Build Settings.");
        }

        private static void CreateBootstrapScene(string path, string goName, System.Type bootstrapType)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject(goName);
            go.AddComponent(bootstrapType);
            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
