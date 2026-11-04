using UnityEngine;

namespace CarRace
{
    public sealed class MountainRoadRuntimeLoader : MonoBehaviour
    {
        private const string ResourcePath1 = "mountain_road";
        private const string ResourcePath2 = "mountain_road_2";

        private void Start()
        {
            if (!Application.isPlaying) return;

            GameObject env1 = GameObject.Find("Mountain Road Environment");
            if (env1 == null)
            {
                GameObject asset1 = Resources.Load<GameObject>(ResourcePath1);
                if (asset1 != null)
                {
                    env1 = Instantiate(asset1);
                    env1.name = "Mountain Road Environment";
                    env1.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f));
                    env1.transform.localScale = Vector3.one;
                    AddStaticMeshColliders(env1.transform);
                }
            }

            GameObject env2 = GameObject.Find("Mountain Road Environment 2");
            if (env2 == null)
            {
                GameObject asset2 = Resources.Load<GameObject>(ResourcePath2);
                if (asset2 != null)
                {
                    env2 = Instantiate(asset2);
                    env2.name = "Mountain Road Environment 2";
                    env2.transform.position = new Vector3(347f, 42f, 503f);
                    env2.transform.rotation = new Quaternion(-0.44340926f, 0.5508069f, 0.5508069f, 0.44340926f);
                    env2.transform.localScale = Vector3.one;
                    AddStaticMeshColliders(env2.transform);
                }
            }

            FindAnyObjectByType<CarRaceGame>()?.NotifyUploadedMountainRoadLoaded(env1 ?? env2);
        }

        private static void AddStaticMeshColliders(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
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
