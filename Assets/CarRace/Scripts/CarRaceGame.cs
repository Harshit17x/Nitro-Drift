using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarRace
{
    [ExecuteAlways]
    public sealed class CarRaceGame : MonoBehaviour
    {
        [Header("Track")]
        [SerializeField] private int trackSegments = 72;
        [SerializeField] private float trackRadiusX = 58f;
        [SerializeField] private float trackRadiusZ = 44f;
        [SerializeField] private float roadWidth = 8f;
        [SerializeField] private int lapsToWin = 1;

        private readonly List<Vector3> centers = new();
        private CarRaceVehicle player;
        private float raceTime;
        private float previousProgress;
        private int lap = 1;
        private bool finished;
        private GUIStyle titleStyle;
        private GUIStyle panelStyle;
        private GUIStyle textStyle;
        private Material roadMaterial;
        private Material groundMaterial;
        private Material curbRedMaterial;
        private Material curbWhiteMaterial;
        private Material markerMaterial;

        public IReadOnlyList<Vector3> TrackCenters => centers;

        private void Awake()
        {
            Time.timeScale = 1f;
            BuildCenters();

            // The scene is authored in edit mode so it is visible in the Scene view.
            // When Unity reloads or enters Play mode, reconnect to the saved objects
            // instead of generating a second track.
            if (GameObject.Find("Race Road") != null)
            {
                ReconnectSavedScene();
                return;
            }

            BuildMaterials();
            BuildTrack();
            BuildEnvironment();
            BuildPlayer();
            BuildCamera();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (player == null || finished || Time.timeScale == 0f) return;

            raceTime += Time.deltaTime;
            float progress = FindProgress(player.transform.position);
            if (previousProgress > 0.82f && progress < 0.18f)
            {
                lap++;
                if (lap > lapsToWin)
                {
                    finished = true;
                    player.SetFinished(true);
                }
            }
            previousProgress = progress;
        }

        public void RespawnPlayer()
        {
            if (player == null || centers.Count == 0) return;
            Rigidbody body = player.Body;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            player.transform.position = centers[0] + Vector3.up * 1.8f;
            player.transform.rotation = Quaternion.LookRotation(GetForward(0), Vector3.up);
            previousProgress = 0f;
        }

        private void BuildMaterials()
        {
            roadMaterial = MakeMaterial(new Color(0.045f, 0.055f, 0.065f), 0.05f, 0.72f);
            groundMaterial = MakeMaterial(new Color(0.055f, 0.18f, 0.11f), 0f, 0.9f);
            curbRedMaterial = MakeMaterial(new Color(0.75f, 0.035f, 0.025f), 0.1f, 0.45f);
            curbWhiteMaterial = MakeMaterial(new Color(0.92f, 0.9f, 0.78f), 0.1f, 0.45f);
            markerMaterial = MakeMaterial(new Color(1f, 0.58f, 0.08f), 0.2f, 0.3f);
        }

        private void BuildTrack()
        {
            Mesh roadMesh = new Mesh { name = "MountainRoadMesh" };
            Vector3[] vertices = new Vector3[trackSegments * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[trackSegments * 6];

            for (int i = 0; i < trackSegments; i++)
            {
                Vector3 tangent = GetForward(i);
                Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                vertices[i * 2] = centers[i] - right * (roadWidth * 0.5f);
                vertices[i * 2 + 1] = centers[i] + right * (roadWidth * 0.5f);
                uv[i * 2] = new Vector2(0f, i / 4f);
                uv[i * 2 + 1] = new Vector2(1f, i / 4f);

                int next = (i + 1) % trackSegments;
                int ti = i * 6;
                triangles[ti] = i * 2;
                triangles[ti + 1] = i * 2 + 1;
                triangles[ti + 2] = next * 2;
                triangles[ti + 3] = i * 2 + 1;
                triangles[ti + 4] = next * 2 + 1;
                triangles[ti + 5] = next * 2;
            }

            roadMesh.vertices = vertices;
            roadMesh.uv = uv;
            roadMesh.triangles = triangles;
            roadMesh.RecalculateNormals();

            GameObject road = new GameObject("Race Road");
            MeshFilter filter = road.AddComponent<MeshFilter>();
            filter.sharedMesh = roadMesh;
            MeshRenderer renderer = road.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = roadMaterial;
            MeshCollider collider = road.AddComponent<MeshCollider>();
            collider.sharedMesh = roadMesh;

            for (int i = 0; i < trackSegments; i += 2)
            {
                CreateCurb(i, -1f, i % 4 == 0 ? curbRedMaterial : curbWhiteMaterial);
                CreateCurb(i, 1f, i % 4 == 0 ? curbRedMaterial : curbWhiteMaterial);
            }

            BuildStartLine();
            BuildCheckpoints();
        }

        private void BuildCenters()
        {
            if (centers.Count > 0) return;

            for (int i = 0; i < trackSegments; i++)
            {
                float t = i / (float)trackSegments * Mathf.PI * 2f;
                float x = Mathf.Cos(t) * (trackRadiusX + Mathf.Sin(t * 3f) * 4f);
                float z = Mathf.Sin(t) * (trackRadiusZ + Mathf.Cos(t * 2f) * 3f);
                float y = 1.2f + Mathf.Sin(t * 2f) * 1.2f + Mathf.Cos(t * 5f) * 0.35f;
                centers.Add(new Vector3(x, y, z));
            }
        }

        private void ReconnectSavedScene()
        {
            GameObject carObject = GameObject.Find("Player Rally Car");
            if (carObject != null)
            {
                player = carObject.GetComponent<CarRaceVehicle>();
            }

            if (player != null)
            {
                player.Initialize();
                BuildCamera();
            }
        }

        private void CreateCurb(int index, float side, Material material)
        {
            Vector3 tangent = GetForward(index);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            GameObject curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curb.name = "Race Curb";
            curb.transform.position = centers[index] + right * side * (roadWidth * 0.5f + 0.38f) + Vector3.up * 0.16f;
            curb.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            curb.transform.localScale = new Vector3(0.72f, 0.28f, 1.7f);
            curb.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void BuildStartLine()
        {
            Vector3 tangent = GetForward(0);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            for (int i = -4; i < 4; i++)
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "Start Grid";
                tile.transform.position = centers[0] + right * (i + 0.5f) + Vector3.up * 0.04f;
                tile.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
                tile.transform.localScale = new Vector3(0.95f, 0.035f, 1.15f);
                tile.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? curbWhiteMaterial : roadMaterial;
            }
        }

        private void BuildCheckpoints()
        {
            for (int i = 8; i < trackSegments; i += 8)
            {
                Vector3 tangent = GetForward(i);
                Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                Vector3 position = centers[i];
                CreateCheckpointPost(position - right * (roadWidth * 0.52f), tangent, "Checkpoint Left");
                CreateCheckpointPost(position + right * (roadWidth * 0.52f), tangent, "Checkpoint Right");
                GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beam.name = "Checkpoint Beam";
                beam.transform.position = position + Vector3.up * 4f;
                beam.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
                beam.transform.localScale = new Vector3(roadWidth * 1.15f, 0.28f, 0.28f);
                beam.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            }
        }

        private void CreateCheckpointPost(Vector3 position, Vector3 tangent, string name)
        {
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = name;
            post.transform.position = position + Vector3.up * 2f;
            post.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            post.transform.localScale = new Vector3(0.28f, 4f, 0.28f);
            post.GetComponent<Renderer>().sharedMaterial = markerMaterial;
        }

        private void BuildEnvironment()
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.18f, 0.25f, 0.31f);
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.ambientLight = new Color(0.28f, 0.34f, 0.4f);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Mountain Ground";
            ground.transform.position = new Vector3(0f, -2.1f, 0f);
            ground.transform.localScale = new Vector3(45f, 1f, 45f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            Material rockMaterial = MakeMaterial(new Color(0.13f, 0.16f, 0.18f), 0f, 0.95f);
            for (int i = 0; i < 24; i++)
            {
                float angle = i / 24f * Mathf.PI * 2f;
                float radius = 105f + (i % 3) * 14f;
                GameObject mountain = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mountain.name = "Mountain Ridge";
                mountain.transform.position = new Vector3(Mathf.Cos(angle) * radius, 12f, Mathf.Sin(angle) * radius);
                mountain.transform.localScale = new Vector3(12f + (i % 4) * 4f, 24f + (i % 5) * 5f, 12f + (i % 4) * 4f);
                mountain.GetComponent<Renderer>().sharedMaterial = rockMaterial;
            }

            GameObject sun = new GameObject("Race Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.78f, 0.58f);
            light.intensity = 1.7f;
            sun.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
        }

        private void BuildPlayer()
        {
            GameObject carObject = new GameObject("Player Rally Car");
            carObject.transform.position = centers[0] + Vector3.up * 1.8f;
            carObject.transform.rotation = Quaternion.LookRotation(GetForward(0), Vector3.up);
            carObject.AddComponent<BoxCollider>().size = new Vector3(2f, 1.4f, 4.4f);
            player = carObject.AddComponent<CarRaceVehicle>();
            player.Initialize();
        }

        private void BuildCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObject = new GameObject("Race Camera");
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            mainCamera.fieldOfView = 66f;
            CarRaceCamera cameraRig = mainCamera.gameObject.GetComponent<CarRaceCamera>() ?? mainCamera.gameObject.AddComponent<CarRaceCamera>();
            cameraRig.Initialize(player.transform);
        }

        private Vector3 GetForward(int index)
        {
            Vector3 current = centers[index];
            Vector3 next = centers[(index + 1) % centers.Count];
            return (next - current).normalized;
        }

        private float FindProgress(Vector3 position)
        {
            float bestDistance = float.MaxValue;
            int bestIndex = 0;
            for (int i = 0; i < centers.Count; i++)
            {
                float distance = (position - centers[i]).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex / (float)centers.Count;
        }

        private void OnGUI()
        {
            if (titleStyle == null) BuildStyles();

            GUI.Box(new Rect(18f, 18f, 330f, 122f), GUIContent.none, panelStyle);
            GUI.Label(new Rect(34f, 29f, 300f, 34f), "SLOP-DRIFT", titleStyle);
            GUI.Label(new Rect(34f, 67f, 300f, 24f), $"SPEED   {player?.SpeedKph ?? 0f:000} km/h", textStyle);
            GUI.Label(new Rect(34f, 91f, 300f, 24f), $"LAP     {Mathf.Min(lap, lapsToWin)}/{lapsToWin}", textStyle);
            GUI.Label(new Rect(34f, 115f, 300f, 24f), $"TIME    {FormatTime(raceTime)}", textStyle);
            GUI.Label(new Rect(20f, Screen.height - 50f, 560f, 30f), "WASD / ARROWS Drive    SPACE Drift    R Respawn    P Pause", textStyle);

            if (finished)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 190f, Screen.height * 0.5f - 78f, 380f, 156f), GUIContent.none, panelStyle);
                GUI.Label(new Rect(Screen.width * 0.5f - 160f, Screen.height * 0.5f - 55f, 320f, 44f), "FINISH!", titleStyle);
                GUI.Label(new Rect(Screen.width * 0.5f - 130f, Screen.height * 0.5f + 5f, 260f, 28f), FormatTime(raceTime), textStyle);
            }
        }

        private void BuildStyles()
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.74f, 0.22f) }
            };
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakePanelTexture(new Color(0.015f, 0.025f, 0.04f, 0.84f)) }
            };
            textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        private static Texture2D MakePanelTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static string FormatTime(float seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return $"{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds / 10:00}";
        }

        private static Material MakeMaterial(Color color, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { color = color };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return material;
        }
    }
}
