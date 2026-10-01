using PewPewPew.Networking;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PewPewPew.Editor
{
    [InitializeOnLoad]
    internal static class SteamLobbySceneUIInstaller
    {
        private const string m_ScenePath = "Assets/Scenes/SplashScreen.unity";
        private const string m_MenuUITypeName = "PewPewPew.Networking.SteamLobbyMenuUI";

        static SteamLobbySceneUIInstaller()
        {
            EditorApplication.delayCall += BuildIfReady;
        }

        [MenuItem("PewPewPew/Build Steam Lobby UI in Splash Screen")]
        private static void BuildFromMenu()
        {
            Scene scene = SceneManager.GetSceneByPath(m_ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(m_ScenePath, OpenSceneMode.Single);
            }

            BuildAndSave(scene, allowDirtyScene: true);
        }

        private static void BuildIfReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

            Scene scene = SceneManager.GetSceneByPath(m_ScenePath);
            if (!scene.IsValid() || !scene.isLoaded || scene.isDirty) return;

            BuildAndSave(scene, allowDirtyScene: false);
        }

        private static void BuildAndSave(Scene scene, bool allowDirtyScene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (scene.isDirty && !allowDirtyScene)
            {
                Debug.LogWarning("SplashScreen has unsaved edits. Save them, then run PewPewPew/Build Steam Lobby UI in Splash Screen.");
                return;
            }

            Component menuUI = FindMenuUI(scene);
            if (menuUI == null || HasSceneObjects(menuUI)) return;

            Undo.RegisterFullObjectHierarchyUndo(menuUI.gameObject, "Build Steam Lobby UI");
            MethodInfo buildMethod = menuUI.GetType().GetMethod("BuildSceneObjects", BindingFlags.Instance | BindingFlags.Public);
            if (buildMethod == null)
            {
                Debug.LogError($"{m_MenuUITypeName} does not expose BuildSceneObjects().", menuUI);
                return;
            }

            buildMethod.Invoke(menuUI, null);
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene))
            {
                Debug.Log("Steam lobby UI objects were added to SplashScreen.", menuUI);
            }
        }

        private static Component FindMenuUI(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component != null && component.GetType().FullName == m_MenuUITypeName) return component;
                }
            }

            return null;
        }

        private static bool HasSceneObjects(Component menuUI)
        {
            PropertyInfo property = menuUI.GetType().GetProperty("HasSceneObjects", BindingFlags.Instance | BindingFlags.Public);
            return property != null && (bool)property.GetValue(menuUI);
        }
    }
}