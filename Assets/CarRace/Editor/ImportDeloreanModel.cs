using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarRace.Editor
{
    public static class ImportDeloreanModel
    {
        private const string ModelPath = "Assets/CarRace/Models/DeloreanTimeMachine.glb";
        private const string ScenePath = "Assets/Scenes/CarRace_SlopDrift.unity";

        public static void ImportAndPlace()
        {
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                Debug.LogError("Could not load imported DeLorean GLB asset.");
                EditorApplication.Exit(1);
                return;
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset == null)
            {
                Debug.LogError("Could not load the race scene.");
                EditorApplication.Exit(1);
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.Find("Player Rally Car");
            if (player == null)
            {
                Debug.LogError("Player Rally Car was not found in the race scene.");
                EditorApplication.Exit(1);
                return;
            }

            Transform previous = player.transform.Find("ImportedDelorean");
            if (previous != null)
            {
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
            }

            Transform procedural = player.transform.Find("ClassicMuscleVisual");
            if (procedural != null)
            {
                UnityEngine.Object.DestroyImmediate(procedural.gameObject);
            }

            GameObject imported = PrefabUtility.InstantiatePrefab(modelAsset, scene) as GameObject;
            if (imported == null)
            {
                imported = UnityEngine.Object.Instantiate(modelAsset);
            }

            imported.name = "ImportedDelorean";
            imported.transform.SetParent(player.transform, false);
            // glTFast exposes the source FBX conversion as +90 degrees on X. Cancel that
            // conversion at the visual root so the car lies on its wheels and keeps +Z
            // as its driving direction.
            imported.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            imported.transform.localPosition = Vector3.zero;
            imported.transform.localScale = Vector3.one;

            Bounds bounds;
            if (TryGetBounds(imported, out bounds))
            {
                float length = Mathf.Max(bounds.size.x, bounds.size.z);
                float scale = length > 0.001f ? 4.85f / length : 1f;
            imported.transform.localScale = Vector3.one * scale;

                Vector3 localCenter = player.transform.InverseTransformPoint(bounds.center);
                Vector3 localBottomPoint = player.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                imported.transform.localPosition = new Vector3(
                    -localCenter.x * scale,
                    -0.68f - localBottomPoint.y * scale,
                    -localCenter.z * scale);
            }

            // The road at the start line is y=1.55 and the vehicle collider is 1.4m tall.
            // Keep the root low enough for the imported wheels/body to sit on that surface.
            player.transform.position = new Vector3(player.transform.position.x, 2.27f, player.transform.position.z);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Imported and placed DeLoreanTimeMachine.glb on Player Rally Car.");
            EditorApplication.Exit(0);
        }

        public static void ValidateScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.Find("Player Rally Car");
            Transform imported = player == null ? null : player.transform.Find("ImportedDelorean");
            Transform procedural = player == null ? null : player.transform.Find("ClassicMuscleVisual");
            Renderer[] renderers = imported == null ? Array.Empty<Renderer>() : imported.GetComponentsInChildren<Renderer>(true);
            bool valid = player != null && imported != null && procedural == null && renderers.Length > 0;
            Debug.Log($"DeLorean scene validation: valid={valid}, renderers={renderers.Length}, imported={imported != null}, procedural={procedural != null}");
            if (imported != null)
            {
                Debug.Log($"DeLorean pose: rootUp={imported.up}, rootForward={imported.forward}, rootRotation={imported.localEulerAngles}, rootPosition={imported.localPosition}");
                Transform[] hierarchy = imported.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < Mathf.Min(hierarchy.Length, 8); i++)
                {
                    Debug.Log($"DeLorean node {i}: {hierarchy[i].name}, localRotation={hierarchy[i].localEulerAngles}, localPosition={hierarchy[i].localPosition}, worldUp={hierarchy[i].up}, worldForward={hierarchy[i].forward}");
                }

                for (int i = 0; i < hierarchy.Length; i++)
                {
                    if (hierarchy[i].name.IndexOf("Wheel", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Debug.Log($"DeLorean wheel node: {hierarchy[i].name}, worldPosition={hierarchy[i].position}");
                    }
                }
            }
            EditorApplication.Exit(valid ? 0 : 1);
        }

        private static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.size.sqrMagnitude > 0.000001f;
        }
    }
}
