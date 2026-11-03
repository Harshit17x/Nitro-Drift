using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarRace.Editor
{
    public static class ImportMountainRoadModel
    {
        private const string ModelPath1 = "Assets/Resources/mountain_road.glb";
        private const string ModelPath2 = "Assets/Resources/mountain_road_2.glb";
        private const string ScenePath = "Assets/Scenes/CarRace_SlopDrift.unity";
        private const string EnvironmentName1 = "Mountain Road Environment";
        private const string EnvironmentName2 = "Mountain Road Environment 2";

        [MenuItem("Car Race/Import Mountain Road")]
        [MenuItem("Car Race/Import Both Mountain Roads")]
        public static void ImportAndPlace()
        {
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(ModelPath1, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ModelPath2, ImportAssetOptions.ForceUpdate);

            GameObject modelAsset1 = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath1);
            GameObject modelAsset2 = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath2);
            if (modelAsset1 == null)
            {
                Debug.LogError($"Could not load mountain road 1 GLB at {ModelPath1}.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Setup Map 1
            GameObject prev1 = GameObject.Find(EnvironmentName1);
            if (prev1 != null) Object.DestroyImmediate(prev1);

            GameObject env1 = PrefabUtility.InstantiatePrefab(modelAsset1, scene) as GameObject;
            if (env1 != null)
            {
                env1.name = EnvironmentName1;
                env1.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f));
                env1.transform.localScale = Vector3.one;
                PrefabUtility.UnpackPrefabInstance(env1, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                AddStaticMeshColliders(env1.transform);
            }

            // Setup Map 2 (connected extended pass)
            if (modelAsset2 != null)
            {
                GameObject prev2 = GameObject.Find(EnvironmentName2);
                if (prev2 != null) Object.DestroyImmediate(prev2);

                GameObject env2 = PrefabUtility.InstantiatePrefab(modelAsset2, scene) as GameObject;
                if (env2 != null)
                {
                    env2.name = EnvironmentName2;
                    env2.transform.position = new Vector3(248.88f, 39.49f, 244.37f);
                    env2.transform.rotation = Quaternion.Euler(0f, 200f, 0f) * Quaternion.Euler(-90f, 0f, 0f);
                    env2.transform.localScale = Vector3.one;
                    PrefabUtility.UnpackPrefabInstance(env2, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    AddStaticMeshColliders(env2.transform);
                }
            }

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
            Debug.Log("Imported both mountain road GLB maps, connected their endpoints seamlessly with high-speed road ribbon, and saved scene.");
        }

        private static void AddStaticMeshColliders(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.isStatic = true;
                MeshFilter meshFilter = child.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                MeshCollider collider = child.GetComponent<MeshCollider>();
                if (collider == null) collider = child.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = meshFilter.sharedMesh;
                collider.convex = false;
            }
        }
    }
}
