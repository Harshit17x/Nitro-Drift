using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarRace.Editor
{
    public static class ImportMountainRoadModel
    {
        private const string ModelPath = "Assets/Resources/mountain_road.glb";
        private const string ScenePath = "Assets/Scenes/CarRace_SlopDrift.unity";
        private const string EnvironmentName = "Mountain Road Environment";

        [MenuItem("Car Race/Import Mountain Road")]
        public static void ImportAndPlace()
        {
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"Could not load the mountain road GLB at {ModelPath}.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject previous = GameObject.Find(EnvironmentName);
            if (previous != null)
            {
                Object.DestroyImmediate(previous);
            }

            GameObject environment = PrefabUtility.InstantiatePrefab(modelAsset, scene) as GameObject;
            if (environment == null)
            {
                Debug.LogError("Could not instantiate the mountain road GLB.");
                return;
            }

            environment.name = EnvironmentName;
            environment.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            environment.transform.localScale = Vector3.one;
            PrefabUtility.UnpackPrefabInstance(environment, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            AddStaticMeshColliders(environment.transform);

            GameObject raceObject = GameObject.Find("Slop-Drift Mountain Race");
            CarRaceGame race = raceObject == null ? null : raceObject.GetComponent<CarRaceGame>();
            if (race == null)
            {
                Debug.LogError("Could not find the CarRaceGame component in the race scene.");
                return;
            }

            SerializedObject serializedRace = new SerializedObject(race);
            serializedRace.FindProperty("uploadedMountainRoad").boolValue = true;
            serializedRace.ApplyModifiedPropertiesWithoutUndo();
            race.RebuildGeneratedScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Imported mountain_road.glb, added static mesh collision, and placed the car on its driving route.");
        }

        private static void AddStaticMeshColliders(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.isStatic = true;
                MeshFilter meshFilter = child.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                MeshCollider collider = child.GetComponent<MeshCollider>() ?? child.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = meshFilter.sharedMesh;
                collider.convex = false;
            }
        }
    }
}
