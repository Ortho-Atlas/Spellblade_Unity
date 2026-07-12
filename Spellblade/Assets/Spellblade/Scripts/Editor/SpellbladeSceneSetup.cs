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
    }
}
