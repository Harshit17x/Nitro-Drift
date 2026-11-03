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
        [SerializeField] private bool uploadedMountainRoad;

        private readonly List<Vector3> centers = new();
        private CarRaceVehicle player;
        private float raceTime;
        private float previousProgress;
        private int lap = 1;
        private bool finished;
        private GUIStyle titleStyle;
        private GUIStyle panelStyle;
        private GUIStyle textStyle;
        private GUIStyle driftTitleStyle;
        private GUIStyle driftScoreStyle;
        private GUIStyle driftBoostStyle;
        private GUIStyle driftValueStyle;
        private Material roadMaterial;
        private Material groundMaterial;
        private Material curbRedMaterial;
        private Material curbWhiteMaterial;
        private Material markerMaterial;
        private Material shoulderMaterial;
        private Material roadLineMaterial;
        private Material terrainMaterial;
        private Material rockMaterial;
        private Material snowMaterial;
        private Material guardrailMaterial;

        private const string UploadedEnvironmentName = "Mountain Road Environment";
        private const string UploadedEnvironment2Name = "Mountain Road Environment 2";
        private const string UploadedConnectorName = "Mountain Road Connector";
        private const string UploadedDriveSurfaceName = "Mountain Road Drive Surface";

        // Traced directly on the real asphalt mountain roads inside mountain_road.glb and mountain_road_2.glb.
        // Starts directly on the white start line road markings (-2, 19.28, 92),
        // drifts around the red-curbed North Hairpin, sprints down the touge straight,
        // sweeps the South Hairpin, curves through the yellow deceleration chicane,
        // crosses the high-speed highway connector onto Map 2, weaves through the panoramic
        // ridge passes and canyon sweeps, and finishes at the 170m Mountain Summit peak!
        private static readonly Vector3[] UploadedMountainRoadPath =
        {
            // Start Line: White painted road markings at the North Pass entrance
            new Vector3(-2.0f, 19.28f, 92.0f),
            new Vector3(2.0f, 20.47f, 112.0f),
            new Vector3(10.0f, 21.77f, 126.0f),
            new Vector3(20.0f, 22.37f, 126.0f),
            new Vector3(27.0f, 22.76f, 114.0f),
            new Vector3(29.0f, 26.99f, 96.0f),
            new Vector3(24.0f, 19.10f, 76.0f),
            new Vector3(16.0f, 22.00f, 54.0f),
            new Vector3(5.0f, 27.02f, 32.0f),
            new Vector3(-6.0f, 35.82f, 12.0f),
            new Vector3(-14.0f, 33.10f, -10.0f),
            new Vector3(-19.0f, 35.04f, -30.0f),
            new Vector3(-19.0f, 36.65f, -50.0f),
            new Vector3(-15.0f, 38.17f, -68.0f),
            new Vector3(-3.0f, 49.07f, -76.0f),
            new Vector3(8.0f, 46.31f, -72.0f),
            new Vector3(10.0f, 50.06f, -55.0f),
            new Vector3(9.0f, 48.27f, -35.0f),
            new Vector3(14.0f, 48.65f, -16.0f),
            new Vector3(25.0f, 48.89f, -6.0f),
            new Vector3(38.0f, 49.01f, -10.0f),
            new Vector3(58.0f, 49.41f, -9.0f),
            new Vector3(76.0f, 49.42f, -6.0f),
            new Vector3(88.0f, 47.76f, 4.0f),
            new Vector3(105.0f, 50.26f, 20.0f),
            new Vector3(118.0f, 51.41f, 38.0f),
            new Vector3(134.0f, 52.41f, 56.0f),
            new Vector3(150.0f, 53.36f, 72.0f),

            // Highway Connector from Map 1 into Map 2:
            new Vector3(156.6f, 57.09f, 75.8f),
            new Vector3(164.3f, 58.72f, 83.6f),
            new Vector3(172.1f, 60.22f, 91.4f),
            new Vector3(180.5f, 67.19f, 99.0f),
            new Vector3(188.8f, 87.48f, 106.6f),

            // Map 2: Elevated Ridge Pass & Vista Climbs:
            new Vector3(203.8f, 108.14f, 111.8f),
            new Vector3(219.2f, 121.17f, 116.8f),
            new Vector3(225.7f, 131.95f, 125.1f),
            new Vector3(231.8f, 146.38f, 133.5f),
            new Vector3(235.2f, 150.29f, 142.9f),
            new Vector3(238.4f, 150.86f, 152.4f),

            // Map 2: Panoramic Valley Drop & High-Speed Sweepers:
            new Vector3(252.5f, 126.12f, 157.9f),
            new Vector3(268.0f, 109.45f, 162.9f),
            new Vector3(281.0f, 93.35f, 168.8f),
            new Vector3(294.1f, 83.16f, 174.7f),
            new Vector3(301.9f, 76.59f, 182.5f),
            new Vector3(308.7f, 71.37f, 190.7f),
            new Vector3(310.6f, 69.21f, 200.6f),
            new Vector3(311.7f, 67.10f, 210.9f),

            // Map 2: Lower Canyon Hairpin & River Curves:
            new Vector3(294.0f, 82.40f, 228.0f),
            new Vector3(277.7f, 91.00f, 244.5f),
            new Vector3(272.6f, 98.82f, 257.0f),
            new Vector3(272.1f, 96.05f, 267.8f),
            new Vector3(277.6f, 94.87f, 276.5f),
            new Vector3(285.9f, 89.33f, 284.1f),
            new Vector3(292.9f, 89.96f, 292.2f),
            new Vector3(301.5f, 93.42f, 299.7f),

            // Map 2: High-Mountain Ascent to Summit Peak:
            new Vector3(312.9f, 103.74f, 306.2f),
            new Vector3(324.1f, 108.10f, 312.8f),
            new Vector3(328.2f, 113.02f, 321.9f),
            new Vector3(331.9f, 120.68f, 331.2f),
            new Vector3(344.4f, 130.82f, 337.3f),
            new Vector3(357.0f, 147.65f, 343.4f),
            new Vector3(363.5f, 158.41f, 351.6f),
            new Vector3(369.8f, 167.54f, 360.0f),
            new Vector3(380.7f, 173.85f, 366.7f),
            new Vector3(391.0f, 171.28f, 373.5f),
            new Vector3(396.9f, 171.86f, 382.0f),
            // Summit Finish Line (Elevated 170m Peak):
            new Vector3(402.1f, 169.87f, 390.8f)
        };

        public IReadOnlyList<Vector3> TrackCenters => centers;

        private void OnEnable()
        {
            BuildCenters();
        }

        private void Awake()
        {
            Application.runInBackground = true;
            Time.timeScale = 1f;
            BuildCenters();

            if (UsesUploadedMountainRoad())
            {
                CleanUploadedDriveSurface();
                if (Application.isPlaying && GameObject.Find(UploadedEnvironmentName) == null)
                {
                    if (GetComponent<MountainRoadRuntimeLoader>() == null)
                    {
                        gameObject.AddComponent<MountainRoadRuntimeLoader>();
                    }
                }

                ReconnectSavedScene();
                return;
            }

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

        public void RebuildGeneratedScene()
        {
            if (Application.isPlaying) return;

            ClearGeneratedScene();
            centers.Clear();
            BuildCenters();

            if (UsesUploadedMountainRoad())
            {
                BuildUploadedLighting();
                CleanUploadedDriveSurface();
                BuildRoadConnector();
                BuildPlayer();
                BuildCamera();
                return;
            }

            BuildMaterials();
            BuildTrack();
            BuildEnvironment();
            BuildPlayer();
            BuildCamera();
        }

        private void ClearGeneratedScene()
        {
            GameObject[] objects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (GameObject sceneObject in objects)
            {
                if (sceneObject == null) continue;
                if (sceneObject.transform.parent != null || !IsGeneratedObject(sceneObject.name)) continue;
                if (sceneObject.name == "Player Rally Car") continue;
                DestroyImmediate(sceneObject);
            }
        }

        private static bool IsGeneratedObject(string objectName)
        {
            return objectName == "Race Road" ||
                   objectName == "Road Shoulder" ||
                   objectName == "Mountain Ground" ||
                   objectName == "Race Sun" ||
                   objectName == "Player Rally Car" ||
                   objectName == "Race Camera" ||
                   objectName.StartsWith("Race Curb") ||
                   objectName.StartsWith("Start Grid") ||
                   objectName.StartsWith("Checkpoint") ||
                   objectName.StartsWith("Road Edge") ||
                   objectName.StartsWith("Road Center") ||
                   objectName.StartsWith("Road Guardrail") ||
                   objectName.StartsWith("Mountain Ridge") ||
                   objectName.StartsWith("Mountain Foothill") ||
                   objectName.StartsWith("Mountain Snow") ||
                   objectName == UploadedDriveSurfaceName ||
                   objectName == UploadedConnectorName;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (player == null || finished || Time.timeScale == 0f) return;

            raceTime += Time.deltaTime;
            float progress = FindProgress(player.transform.position);

            if (UsesUploadedMountainRoad())
            {
                if (progress >= 0.985f)
                {
                    finished = true;
                    player.SetFinished(true);
                }

                previousProgress = progress;
                return;
            }

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
            if (centers.Count == 0) BuildCenters();
            if (player == null || centers.Count == 0) return;
            Vector3 startPos = centers[0] + Vector3.up * 0.76f;
            GetRoadFrame(0, out Vector3 forward, out _, out Vector3 up);
            Quaternion startRot = Quaternion.LookRotation(forward, up);

            player.transform.SetPositionAndRotation(startPos, startRot);

            Rigidbody body = player.Body ?? player.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = startPos;
                body.rotation = startRot;
                body.WakeUp();
            }
            Physics.SyncTransforms();
            previousProgress = 0f;
            Camera.main?.GetComponent<CarRaceCamera>()?.SnapToTarget();
        }

        private void BuildMaterials()
        {
            roadMaterial = MakeMaterial(new Color(0.075f, 0.085f, 0.095f), 0.02f, 0.58f);
            shoulderMaterial = MakeMaterial(new Color(0.18f, 0.16f, 0.13f), 0.02f, 0.48f);
            groundMaterial = MakeMaterial(new Color(0.055f, 0.18f, 0.11f), 0f, 0.9f);
            curbRedMaterial = MakeMaterial(new Color(0.75f, 0.035f, 0.025f), 0.1f, 0.45f);
            curbWhiteMaterial = MakeMaterial(new Color(0.92f, 0.9f, 0.78f), 0.1f, 0.45f);
            markerMaterial = MakeMaterial(new Color(1f, 0.58f, 0.08f), 0.2f, 0.3f);
            roadLineMaterial = MakeMaterial(new Color(0.95f, 0.82f, 0.24f), 0.05f, 0.4f);
            terrainMaterial = MakeMaterial(new Color(0.07f, 0.24f, 0.12f), 0f, 0.86f);
            rockMaterial = MakeMaterial(new Color(0.22f, 0.27f, 0.3f), 0f, 0.9f);
            snowMaterial = MakeMaterial(new Color(0.9f, 0.94f, 0.96f), 0f, 0.55f);
            guardrailMaterial = MakeMaterial(new Color(0.56f, 0.62f, 0.66f), 0.72f, 0.42f);
        }

        private void BuildTrack()
        {
            GameObject shoulder = CreateRibbonObject("Road Shoulder", roadWidth * 0.5f + 1.5f, shoulderMaterial, -0.08f, true);
            GameObject road = CreateRibbonObject("Race Road", roadWidth * 0.5f, roadMaterial, 0f, true);
            road.GetComponent<MeshFilter>().sharedMesh.name = "MountainRoadMesh";

            CreateRibbonObject("Road Edge Left", -roadWidth * 0.5f + 0.18f, 0.16f, roadLineMaterial, 0.035f, false);
            CreateRibbonObject("Road Edge Right", roadWidth * 0.5f - 0.18f, 0.16f, roadLineMaterial, 0.035f, false);

            for (int i = 0; i < trackSegments; i += 4)
            {
                CreateRoadDash(i);
            }

            for (int i = 0; i < trackSegments; i += 2)
            {
                CreateCurb(i, -1f, i % 4 == 0 ? curbRedMaterial : curbWhiteMaterial);
                CreateCurb(i, 1f, i % 4 == 0 ? curbRedMaterial : curbWhiteMaterial);
            }

            BuildGuardrails();

            BuildStartLine();
            BuildCheckpoints();
        }

        private GameObject CreateRibbonObject(string name, float halfWidth, Material material, float yOffset, bool addCollider)
        {
            Mesh mesh = CreateRibbonMesh(halfWidth, yOffset);
            GameObject ribbon = new GameObject(name);
            ribbon.AddComponent<MeshFilter>().sharedMesh = mesh;
            ribbon.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (addCollider) ribbon.AddComponent<MeshCollider>().sharedMesh = mesh;
            return ribbon;
        }

        private GameObject CreateRibbonObject(string name, float centerOffset, float width, Material material, float yOffset, bool addCollider)
        {
            Mesh mesh = CreateOffsetRibbonMesh(centerOffset, width, yOffset);
            GameObject ribbon = new GameObject(name);
            ribbon.AddComponent<MeshFilter>().sharedMesh = mesh;
            ribbon.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (addCollider) ribbon.AddComponent<MeshCollider>().sharedMesh = mesh;
            return ribbon;
        }

        private Mesh CreateRibbonMesh(float halfWidth, float yOffset)
        {
            return CreateOffsetRibbonMesh(0f, halfWidth * 2f, yOffset);
        }

        private Mesh CreateOffsetRibbonMesh(float centerOffset, float width, float yOffset)
        {
            Mesh mesh = new Mesh { name = "MountainRoadRibbon" };
            Vector3[] vertices = new Vector3[trackSegments * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[trackSegments * 6];

            for (int i = 0; i < trackSegments; i++)
            {
                GetRoadFrame(i, out _, out Vector3 right, out Vector3 up);
                float left = centerOffset - width * 0.5f;
                float rightOffset = centerOffset + width * 0.5f;
                vertices[i * 2] = centers[i] + right * left + up * yOffset;
                vertices[i * 2 + 1] = centers[i] + right * rightOffset + up * yOffset;
                uv[i * 2] = new Vector2(0f, i / 4f);
                uv[i * 2 + 1] = new Vector2(1f, i / 4f);

                int next = (i + 1) % trackSegments;
                int ti = i * 6;
                triangles[ti] = i * 2;
                triangles[ti + 1] = next * 2;
                triangles[ti + 2] = i * 2 + 1;
                triangles[ti + 3] = i * 2 + 1;
                triangles[ti + 4] = next * 2;
                triangles[ti + 5] = next * 2 + 1;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }

        private void CreateRoadDash(int index)
        {
            GetRoadFrame(index, out Vector3 forward, out _, out Vector3 up);
            GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dash.name = "Road Center Dash";
            dash.transform.position = centers[index] + up * 0.055f;
            dash.transform.rotation = Quaternion.LookRotation(forward, up);
            dash.transform.localScale = new Vector3(0.16f, 0.035f, 2.4f);
            dash.GetComponent<Renderer>().sharedMaterial = roadLineMaterial;
            Collider dashCollider = dash.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(dashCollider);
            else DestroyImmediate(dashCollider);
        }

        private void BuildCenters()
        {
            if (centers.Count > 0) return;

            if (UsesUploadedMountainRoad())
            {
                int count = UploadedMountainRoadPath.Length;
                const int samplesPerSegment = 5;

                for (int i = 0; i < count - 1; i++)
                {
                    Vector3 p0 = UploadedMountainRoadPath[Mathf.Max(i - 1, 0)];
                    Vector3 p1 = UploadedMountainRoadPath[i];
                    Vector3 p2 = UploadedMountainRoadPath[i + 1];
                    Vector3 p3 = UploadedMountainRoadPath[Mathf.Min(i + 2, count - 1)];

                    for (int s = 0; s < samplesPerSegment; s++)
                    {
                        float t = s / (float)samplesPerSegment;
                        centers.Add(CatmullRom(p0, p1, p2, p3, t));
                    }
                }

                centers.Add(UploadedMountainRoadPath[count - 1]);
                return;
            }

            for (int i = 0; i < trackSegments; i++)
            {
                float t = i / (float)trackSegments * Mathf.PI * 2f;
                float x = Mathf.Cos(t) * (trackRadiusX + Mathf.Sin(t * 3f) * 4f);
                float z = Mathf.Sin(t) * (trackRadiusZ + Mathf.Cos(t * 2f) * 3f);
                float y = 1.2f + Mathf.Sin(t * 2f) * 1.2f + Mathf.Cos(t * 5f) * 0.35f;
                centers.Add(new Vector3(x, y, z));
            }
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private void ReconnectSavedScene()
        {
            BuildCenters();
            GameObject carObject = GameObject.Find("Player Rally Car");
            if (carObject != null)
            {
                player = carObject.GetComponent<CarRaceVehicle>();
                if (player == null) player = carObject.AddComponent<CarRaceVehicle>();
            }

            if (player != null)
            {
                player.Initialize();
                BuildCamera();
                RespawnPlayer();
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

        private void BuildGuardrails()
        {
            for (int i = 0; i < trackSegments; i += 3)
            {
                CreateGuardrail(i, -1f);
                CreateGuardrail(i, 1f);
            }
        }

        private void CreateGuardrail(int index, float side)
        {
            int next = (index + 3) % trackSegments;
            GetRoadFrame(index, out Vector3 forward, out Vector3 right, out Vector3 up);
            Vector3 offset = right * side * (roadWidth * 0.5f + 1.15f);
            Vector3 start = centers[index] + offset;
            Vector3 end = centers[next] + offset;
            Vector3 railDirection = (end - start).normalized;

            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Road Guardrail Post";
            post.transform.position = start + up * 0.55f;
            post.transform.localScale = new Vector3(0.13f, 0.55f, 0.13f);
            post.GetComponent<Renderer>().sharedMaterial = guardrailMaterial;

            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Road Guardrail Beam";
            beam.transform.position = Vector3.Lerp(start, end, 0.5f) + up * 1.02f;
            beam.transform.rotation = Quaternion.LookRotation(railDirection, up);
            beam.transform.localScale = new Vector3(0.16f, 0.16f, Vector3.Distance(start, end));
            beam.GetComponent<Renderer>().sharedMaterial = guardrailMaterial;
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
            ConfigureEnvironmentLighting();

            BuildTerrainMesh();
            BuildMountainRange();

            BuildSun();
        }

        private void BuildUploadedLighting()
        {
            ConfigureEnvironmentLighting();
            BuildSun();
        }

        private static void ConfigureEnvironmentLighting()
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.32f, 0.43f, 0.52f);
            RenderSettings.fogDensity = 0.0045f;
            RenderSettings.ambientLight = new Color(0.36f, 0.43f, 0.5f);
        }

        private static void BuildSun()
        {
            if (GameObject.Find("Race Sun") != null) return;

            GameObject sun = new GameObject("Race Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.84f, 0.66f);
            light.intensity = 1.9f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
        }

        private void BuildTerrainMesh()
        {
            const int gridSize = 35;
            const float terrainSize = 260f;
            Mesh terrainMesh = new Mesh { name = "ProceduralMountainTerrain" };
            Vector3[] vertices = new Vector3[gridSize * gridSize];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[(gridSize - 1) * (gridSize - 1) * 6];

            for (int z = 0; z < gridSize; z++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    float nx = x / (float)(gridSize - 1);
                    float nz = z / (float)(gridSize - 1);
                    float worldX = (nx - 0.5f) * terrainSize;
                    float worldZ = (nz - 0.5f) * terrainSize;
                    float noise = Mathf.PerlinNoise(worldX * 0.035f + 17.3f, worldZ * 0.035f + 4.8f);
                    float detail = Mathf.PerlinNoise(worldX * 0.09f + 2.1f, worldZ * 0.09f + 8.6f);
                    float height = -3.6f + noise * 1.8f + detail * 0.45f;
                    int vertex = z * gridSize + x;
                    vertices[vertex] = new Vector3(worldX, height, worldZ);
                    uv[vertex] = new Vector2(nx * 8f, nz * 8f);
                }
            }

            int triangle = 0;
            for (int z = 0; z < gridSize - 1; z++)
            {
                for (int x = 0; x < gridSize - 1; x++)
                {
                    int current = z * gridSize + x;
                    int next = current + gridSize;
                    triangles[triangle++] = current;
                    triangles[triangle++] = next;
                    triangles[triangle++] = current + 1;
                    triangles[triangle++] = current + 1;
                    triangles[triangle++] = next;
                    triangles[triangle++] = next + 1;
                }
            }

            terrainMesh.vertices = vertices;
            terrainMesh.uv = uv;
            terrainMesh.triangles = triangles;
            terrainMesh.RecalculateNormals();

            GameObject terrain = new GameObject("Mountain Ground");
            terrain.AddComponent<MeshFilter>().sharedMesh = terrainMesh;
            terrain.AddComponent<MeshRenderer>().sharedMaterial = terrainMaterial;
            terrain.AddComponent<MeshCollider>().sharedMesh = terrainMesh;
        }

        private void BuildMountainRange()
        {
            for (int i = 0; i < 18; i++)
            {
                float angle = i / 18f * Mathf.PI * 2f;
                float radius = 126f + (i % 4) * 14f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, -3.4f, Mathf.Sin(angle) * radius);
                float mountainRadius = 22f + (i % 3) * 5f;
                float mountainHeight = 34f + (i % 5) * 5f;
                CreateMountain("Mountain Ridge", position, mountainRadius, mountainHeight, i * 1.73f, rockMaterial);

                if (i % 2 == 0)
                {
                    CreateMountain("Mountain Snow", position + Vector3.up * (mountainHeight * 0.76f), mountainRadius * 0.22f, mountainHeight * 0.27f, i * 2.2f, snowMaterial);
                }
            }

            for (int i = 0; i < 12; i++)
            {
                float angle = i / 12f * Mathf.PI * 2f + 0.12f;
                float radius = 86f + (i % 3) * 5f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, -3.4f, Mathf.Sin(angle) * radius);
                CreateMountain("Mountain Foothill", position, 20f + (i % 3) * 4f, 11f + (i % 4) * 2.5f, i * 2.4f, rockMaterial);
            }
        }

        private void CreateMountain(string name, Vector3 position, float radius, float height, float seed, Material material)
        {
            const int sides = 12;
            const int rings = 4;
            Mesh mesh = new Mesh { name = name + " Mesh" };
            Vector3[] vertices = new Vector3[sides * rings + 1];
            int[] triangles = new int[sides * (rings - 1) * 6 + sides * 3];

            for (int ring = 0; ring < rings; ring++)
            {
                float ringT = ring / (float)(rings - 1);
                float ringRadius = radius * Mathf.Lerp(1f, 0.14f, Mathf.Pow(ringT, 0.9f));
                float ringHeight = height * ringT * 0.78f;
                for (int side = 0; side < sides; side++)
                {
                    float angle = side / (float)sides * Mathf.PI * 2f;
                    float variation = 0.84f + Mathf.PerlinNoise(seed + side * 0.31f, ring * 0.47f) * 0.3f;
                    int vertex = ring * sides + side;
                    vertices[vertex] = new Vector3(Mathf.Cos(angle) * ringRadius * variation, ringHeight, Mathf.Sin(angle) * ringRadius * variation);
                }
            }

            int apex = vertices.Length - 1;
            vertices[apex] = new Vector3(Mathf.Sin(seed) * radius * 0.12f, height, Mathf.Cos(seed * 1.4f) * radius * 0.12f);
            int triangle = 0;
            for (int ring = 0; ring < rings - 1; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int nextSide = (side + 1) % sides;
                    int current = ring * sides + side;
                    int next = ring * sides + nextSide;
                    int upper = (ring + 1) * sides + side;
                    int upperNext = (ring + 1) * sides + nextSide;
                    triangles[triangle++] = current;
                    triangles[triangle++] = upper;
                    triangles[triangle++] = next;
                    triangles[triangle++] = next;
                    triangles[triangle++] = upper;
                    triangles[triangle++] = upperNext;
                }
            }

            int topRing = (rings - 1) * sides;
            for (int side = 0; side < sides; side++)
            {
                triangles[triangle++] = topRing + side;
                triangles[triangle++] = apex;
                triangles[triangle++] = topRing + (side + 1) % sides;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            GameObject mountain = new GameObject(name);
            mountain.transform.position = position;
            mountain.AddComponent<MeshFilter>().sharedMesh = mesh;
            mountain.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void BuildPlayer()
        {
            GameObject carObject = GameObject.Find("Player Rally Car");
            if (carObject == null)
            {
                carObject = new GameObject("Player Rally Car");
                carObject.AddComponent<BoxCollider>().size = new Vector3(2f, 1.4f, 4.4f);
                player = carObject.AddComponent<CarRaceVehicle>();
            }
            else
            {
                player = carObject.GetComponent<CarRaceVehicle>();
                if (player == null) player = carObject.AddComponent<CarRaceVehicle>();
            }

            Vector3 startPos = centers[0] + Vector3.up * 0.76f;
            GetRoadFrame(0, out Vector3 forward, out _, out Vector3 up);
            Quaternion startRot = Quaternion.LookRotation(forward, up);

            carObject.transform.SetPositionAndRotation(startPos, startRot);

            Rigidbody body = player.Body ?? carObject.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = startPos;
                body.rotation = startRot;
            }
            Physics.SyncTransforms();
            player.Initialize();
        }

        private bool UsesUploadedMountainRoad()
        {
            return uploadedMountainRoad ||
                   GameObject.Find(UploadedEnvironmentName) != null ||
                   GameObject.Find(UploadedEnvironment2Name) != null;
        }

        private void CleanUploadedDriveSurface()
        {
            GameObject previous = GameObject.Find(UploadedDriveSurfaceName);
            if (previous != null)
            {
                if (Application.isPlaying) Destroy(previous);
                else DestroyImmediate(previous);
            }
        }

        private void BuildRoadConnector()
        {
            if (GameObject.Find(UploadedConnectorName) != null) return;
            if (GameObject.Find(UploadedEnvironment2Name) == null) return;

            GameObject connectorObj = new GameObject(UploadedConnectorName);
            MeshFilter filter = connectorObj.AddComponent<MeshFilter>();
            MeshRenderer renderer = connectorObj.AddComponent<MeshRenderer>();

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(shader) { color = new Color(0.12f, 0.13f, 0.14f) };
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
            renderer.sharedMaterial = mat;

            Vector3[] spline = {
                new Vector3(146.0f, 52.8f, 68.0f),
                new Vector3(150.0f, 53.36f, 72.0f),
                new Vector3(153.5f, 55.2f, 74.0f),
                new Vector3(158.0f, 57.1f, 77.0f),
                new Vector3(162.0f, 58.2f, 81.0f)
            };

            float width = 8.6f;
            int count = spline.Length;
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            for (int i = 0; i < count; i++)
            {
                Vector3 cur = spline[i];
                Vector3 fwd = (i < count - 1 ? spline[i + 1] - cur : cur - spline[i - 1]).normalized;
                Vector3 rgt = Vector3.Cross(Vector3.up, fwd).normalized;
                Vector3 up = Vector3.Cross(fwd, rgt).normalized;

                verts.Add(cur - rgt * (width * 0.5f));
                verts.Add(cur + rgt * (width * 0.5f));
                norms.Add(up);
                norms.Add(up);

                float v = i / (float)(count - 1) * 3f;
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(1f, v));

                if (i < count - 1)
                {
                    int baseIdx = i * 2;
                    tris.Add(baseIdx);
                    tris.Add(baseIdx + 1);
                    tris.Add(baseIdx + 2);
                    tris.Add(baseIdx + 1);
                    tris.Add(baseIdx + 3);
                    tris.Add(baseIdx + 2);
                }
            }

            Mesh mesh = new Mesh { name = "RoadConnectorMesh" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;

            MeshCollider collider = connectorObj.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            connectorObj.isStatic = true;
        }

        public void NotifyUploadedMountainRoadLoaded(GameObject environment)
        {
            if (environment == null || !Application.isPlaying) return;

            centers.Clear();
            BuildCenters();
            BuildUploadedLighting();
            CleanUploadedDriveSurface();
            BuildRoadConnector();

            if (player == null)
            {
                BuildPlayer();
            }

            BuildCamera();
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
            if (mainCamera.GetComponent<AudioListener>() == null)
            {
                mainCamera.gameObject.AddComponent<AudioListener>();
            }
            CarRaceCamera cameraRig = mainCamera.gameObject.GetComponent<CarRaceCamera>() ?? mainCamera.gameObject.AddComponent<CarRaceCamera>();
            cameraRig.Initialize(player.transform);
        }

        private Vector3 GetForward(int index)
        {
            Vector3 current = centers[index];
            if (UsesUploadedMountainRoad() && index == centers.Count - 1)
            {
                return (current - centers[index - 1]).normalized;
            }

            Vector3 next = centers[(index + 1) % centers.Count];
            return (next - current).normalized;
        }

        private void GetRoadFrame(int index, out Vector3 forward, out Vector3 right, out Vector3 up)
        {
            bool uploaded = UsesUploadedMountainRoad();
            Vector3 previous = uploaded && index == 0
                ? centers[0] - (centers[1] - centers[0])
                : centers[(index - 1 + centers.Count) % centers.Count];
            Vector3 next = uploaded && index == centers.Count - 1
                ? centers[index] + (centers[index] - centers[index - 1])
                : centers[(index + 1) % centers.Count];
            forward = (next - previous).normalized;
            right = Vector3.Cross(Vector3.up, forward).normalized;
            up = Vector3.Cross(forward, right).normalized;
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

            GUI.Box(new Rect(18f, 18f, 330f, 146f), GUIContent.none, panelStyle);
            GUI.Label(new Rect(34f, 27f, 300f, 32f), "SLOP-DRIFT", titleStyle);
            GUI.Label(new Rect(34f, 62f, 300f, 22f), $"SPEED   {player?.SpeedKph ?? 0f:000} km/h", textStyle);
            GUI.Label(new Rect(34f, 84f, 300f, 22f), $"DRIFT   {(int)(player?.DriftScore ?? 0f):N0} PTS", driftValueStyle);
            GUI.Label(new Rect(34f, 106f, 300f, 22f), $"LAP     {Mathf.Min(lap, lapsToWin)}/{lapsToWin}", textStyle);
            GUI.Label(new Rect(34f, 128f, 300f, 22f), $"TIME    {FormatTime(raceTime)}", textStyle);
            GUI.Label(new Rect(20f, Screen.height - 50f, 600f, 30f), "WASD / ARROWS Drive    SPACE Drift / Handbrake    R Respawn    P Pause", textStyle);

            if (player != null && player.IsDrifting)
            {
                GUI.Box(new Rect(Screen.width - 290f, 18f, 272f, 104f), GUIContent.none, panelStyle);
                GUI.Label(new Rect(Screen.width - 275f, 26f, 240f, 32f), $"DRIFT!  {player.DriftAngle:00}°", driftTitleStyle);
                GUI.Label(new Rect(Screen.width - 275f, 58f, 240f, 26f), $"+{(int)player.CurrentDriftPoints:N0}  x{player.DriftMultiplier} COMBO", driftScoreStyle);
                if (player.DriftBoostCharge > 0.15f)
                {
                    GUI.Label(new Rect(Screen.width - 275f, 84f, 240f, 20f), $"EXIT BOOST  {(int)(player.DriftBoostCharge * 100f)}%", driftBoostStyle);
                }
            }

            if (finished)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 190f, Screen.height * 0.5f - 78f, 380f, 156f), GUIContent.none, panelStyle);
                GUI.Label(new Rect(Screen.width * 0.5f - 160f, Screen.height * 0.5f - 55f, 320f, 44f), "FINISH!", titleStyle);
                GUI.Label(new Rect(Screen.width * 0.5f - 130f, Screen.height * 0.5f + 5f, 260f, 28f), $"TIME: {FormatTime(raceTime)}  DRIFT: {(int)(player?.DriftScore ?? 0f):N0}", textStyle);
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
            driftTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.65f, 0.1f) }
            };
            driftScoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.25f, 1f, 0.55f) }
            };
            driftBoostStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.2f, 0.85f, 1f) }
            };
            driftValueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.3f) }
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
