using System.Linq;
using Accumulator.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Accumulator.Editor
{
    public static class PrototypeSceneTools
    {
        public const string ScenePath = "Assets/Scenes/Prototype01.unity";

        [MenuItem("Accumulator/Open Prototype 0.1")]
        public static void OpenPrototype()
        {
            // Preserve the user's unsaved scene instead of replacing it during tooling calls.
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new System.InvalidOperationException("Save the open scene before opening the prototype.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 0, -10);
            var view = camera.GetComponent<Camera>(); view.orthographic = true;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(0.035f, 0.065f, 0.095f);
            new GameObject("Prototype", typeof(PrototypeController));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
        }
    }
}
