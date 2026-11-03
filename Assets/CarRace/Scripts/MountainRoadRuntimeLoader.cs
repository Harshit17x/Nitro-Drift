using UnityEngine;

namespace CarRace
{
    public sealed class MountainRoadRuntimeLoader : MonoBehaviour
    {
        private const string ResourcePath = "mountain_road";

        private async void Start()
        {
            if (!Application.isPlaying || GameObject.Find("Mountain Road Environment") != null) return;

            GameObject modelAsset = Resources.Load<GameObject>(ResourcePath);
            if (modelAsset == null)
            {
                Debug.LogError($"Could not load Resources/{ResourcePath}.glb.");
                return;
            }

            GameObject environment = Instantiate(modelAsset);
            environment.name = "Mountain Road Environment";
            environment.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            environment.transform.localScale = Vector3.one;
            AddStaticMeshColliders(environment.transform);

            FindAnyObjectByType<CarRaceGame>()?.NotifyUploadedMountainRoadLoaded(environment);
        }

        private static void AddStaticMeshColliders(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                MeshFilter meshFilter = child.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                MeshCollider collider = child.GetComponent<MeshCollider>() ?? child.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = meshFilter.sharedMesh;
                collider.convex = false;
            }
        }
    }
}
