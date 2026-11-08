using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Blackout.Experience.Editor
{
    /// <summary>Creates the Track 1 test scene: XR rig, interaction manager, event system and the Experience bootstrap.</summary>
    public static class ExperienceSceneBuilder
    {
        public const string ScenePath = "Assets/_Experience/Scenes/Experience_Test.unity";
        const string RigPath = "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        [MenuItem("Blackout/Experience/Create Test Scene")]
        public static void Create()
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (rigPrefab == null)
            {
                Debug.LogError($"[Blackout] XR rig prefab not found at '{RigPath}'. Import the XR Interaction Toolkit Starter Assets sample.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<XRUIInputModule>();

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.name = "XR Origin (XR Rig)";
            var cam = rig.GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }

            // Simple reference floor and prop so the player has something to orient against in the test scene.
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Test Floor";
            floor.transform.localScale = new Vector3(10f, 0.1f, 10f);
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            if (lit != null) floor.GetComponent<Renderer>().sharedMaterial = new Material(lit) { color = new Color(0.15f, 0.15f, 0.17f) };

            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = "Test Prop";
            prop.transform.position = new Vector3(0f, 0.5f, 3f);
            if (lit != null) prop.GetComponent<Renderer>().sharedMaterial = new Material(lit) { color = new Color(1f, 0.85f, 0.1f) };

            new GameObject("Experience").AddComponent<ExperienceBootstrap>();

            System.IO.Directory.CreateDirectory("Assets/_Experience/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"[Blackout] Created {ScenePath} and added it to Build Settings.");
        }

        public static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
