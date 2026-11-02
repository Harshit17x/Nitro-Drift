using UnityEngine;
using UnityEngine.InputSystem;

namespace CarRace
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class CarRaceVehicle : MonoBehaviour
    {
        [Header("Handling")]
        [SerializeField] private float engineForce = 9200f;
        [SerializeField] private float reverseForce = 3600f;
        [SerializeField] private float brakeForce = 12000f;
        [SerializeField] private float steeringTorque = 420f;
        [SerializeField] private float lateralGrip = 8.5f;
        [SerializeField] private float maxSpeed = 58f;

        private Rigidbody body;
        private Transform visualRoot;
        private Transform[] wheels;
        private float throttle;
        private float brake;
        private float steer;
        private bool handbrake;
        private bool paused;
        private bool finished;
        private InputAction throttleAction;
        private InputAction brakeAction;
        private InputAction steerAction;
        private InputAction handbrakeAction;
        private InputAction pauseAction;
        private InputAction respawnAction;

        public float SpeedKph => body == null ? 0f : body.linearVelocity.magnitude * 3.6f;
        public Rigidbody Body => body;

        private void OnEnable()
        {
            if (Application.isPlaying) CreateInputActions();
        }

        private void OnDisable()
        {
            DisposeInputActions();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!Application.isPlaying || !hasFocus) return;

            paused = false;
            Time.timeScale = 1f;
            CreateInputActions();
        }

        public void Initialize()
        {
            body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.mass = 1120f;
            body.isKinematic = false;
            body.detectCollisions = true;
            body.useGravity = true;
            body.linearDamping = 0.18f;
            body.angularDamping = 4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.centerOfMass = new Vector3(0f, -0.45f, 0.1f);
            body.WakeUp();
            visualRoot = transform.Find("ClassicMuscleVisual");
            if (visualRoot == null)
            {
                ClearVisual();
                BuildVisual();
            }
            else
            {
                CacheWheels();
            }

            // The vehicle drives along +Z; the model's hood and headlights are authored on -Z.
            // Flip only the visual root so physics, steering, and the chase camera keep +Z forward.
            visualRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        private void Update()
        {
            ReadInput();

            bool pausePressed = pauseAction != null && pauseAction.WasPressedThisFrame();
            bool respawnPressed = respawnAction != null && respawnAction.WasPressedThisFrame();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                pausePressed |= keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame;
                respawnPressed |= keyboard.rKey.wasPressedThisFrame;
            }

            if (pausePressed)
            {
                paused = !paused;
                Time.timeScale = paused ? 0f : 1f;
            }

            if (respawnPressed)
            {
                FindAnyObjectByType<CarRaceGame>()?.RespawnPlayer();
            }
        }

        private void FixedUpdate()
        {
            if (body == null || paused || finished) return;

            ReadInput();
            Vector3 localVelocity = transform.InverseTransformDirection(body.linearVelocity);
            float forwardSpeed = localVelocity.z;
            float speedRatio = Mathf.Clamp01(body.linearVelocity.magnitude / maxSpeed);

            if (throttle > 0f)
            {
                body.AddForce(transform.forward * (engineForce * throttle * (1f - speedRatio * 0.55f)), ForceMode.Force);
            }

            if (brake > 0f)
            {
                if (forwardSpeed > 1f)
                {
                    body.AddForce(-transform.forward * brakeForce * brake, ForceMode.Force);
                }
                else
                {
                    body.AddForce(-transform.forward * reverseForce * brake, ForceMode.Force);
                }
            }

            if (throttle > 0f || brake > 0f)
            {
                float targetSpeed = throttle > 0f ? maxSpeed * throttle : (forwardSpeed > 1f ? 0f : -maxSpeed * 0.35f);
                float acceleration = (throttle > 0f ? engineForce : brakeForce) / body.mass * 2.2f;
                localVelocity.z = Mathf.MoveTowards(forwardSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);
                body.linearVelocity = transform.TransformDirection(localVelocity);
            }

            float steeringScale = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(body.linearVelocity.magnitude / 10f));
            float steeringRate = steeringTorque * 0.18f * steeringScale * (handbrake ? 1.25f : 1f);
            if (Mathf.Abs(steer) > 0.01f)
            {
                Quaternion steeringRotation = Quaternion.AngleAxis(steer * steeringRate * Time.fixedDeltaTime, Vector3.up);
                body.MoveRotation(steeringRotation * body.rotation);
            }

            float torque = steer * steeringTorque * steeringScale * (handbrake ? 1.35f : 1f);
            body.AddTorque(Vector3.up * torque * 0.15f, ForceMode.Force);

            Vector3 lateralVelocity = Vector3.Project(body.linearVelocity, transform.right);
            float grip = handbrake ? lateralGrip * 0.2f : lateralGrip;
            body.AddForce(-lateralVelocity * grip, ForceMode.Force);
            body.AddForce(Vector3.down * (body.linearVelocity.sqrMagnitude * 0.018f), ForceMode.Force);

            if (body.linearVelocity.magnitude > maxSpeed)
            {
                body.linearVelocity = body.linearVelocity.normalized * maxSpeed;
            }

            AnimateWheels(forwardSpeed);
        }

        private void ReadInput()
        {
            throttle = throttleAction == null ? 0f : throttleAction.ReadValue<float>();
            brake = brakeAction == null ? 0f : brakeAction.ReadValue<float>();
            steer = steerAction == null ? 0f : steerAction.ReadValue<float>();
            handbrake = handbrakeAction != null && handbrakeAction.IsPressed();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                throttle = Mathf.Max(throttle, keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f);
                brake = Mathf.Max(brake, keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steer = Mathf.Min(steer, -1f);
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steer = Mathf.Max(steer, 1f);
                handbrake |= keyboard.spaceKey.isPressed;
            }

            if (Gamepad.current != null)
            {
                Gamepad gamepad = Gamepad.current;
                throttle = Mathf.Max(throttle, gamepad.rightTrigger.ReadValue());
                brake = Mathf.Max(brake, gamepad.leftTrigger.ReadValue());
                if (Mathf.Abs(gamepad.leftStick.x.ReadValue()) > 0.1f) steer = gamepad.leftStick.x.ReadValue();
                handbrake |= gamepad.buttonSouth.isPressed;
            }
        }

        private void CreateInputActions()
        {
            DisposeInputActions();

            throttleAction = new InputAction("Throttle", InputActionType.Button);
            throttleAction.AddBinding("<Keyboard>/w");
            throttleAction.AddBinding("<Keyboard>/upArrow");
            throttleAction.AddBinding("<Gamepad>/rightTrigger");

            brakeAction = new InputAction("Brake", InputActionType.Button);
            brakeAction.AddBinding("<Keyboard>/s");
            brakeAction.AddBinding("<Keyboard>/downArrow");
            brakeAction.AddBinding("<Gamepad>/leftTrigger");

            steerAction = new InputAction("Steer", InputActionType.Value);
            steerAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            steerAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");
            steerAction.AddBinding("<Gamepad>/leftStick/x");

            handbrakeAction = new InputAction("Handbrake", InputActionType.Button);
            handbrakeAction.AddBinding("<Keyboard>/space");
            handbrakeAction.AddBinding("<Gamepad>/buttonSouth");

            pauseAction = new InputAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<Keyboard>/p");
            pauseAction.AddBinding("<Keyboard>/escape");

            respawnAction = new InputAction("Respawn", InputActionType.Button);
            respawnAction.AddBinding("<Keyboard>/r");

            throttleAction.Enable();
            brakeAction.Enable();
            steerAction.Enable();
            handbrakeAction.Enable();
            pauseAction.Enable();
            respawnAction.Enable();
        }

        private void DisposeInputActions()
        {
            throttleAction?.Dispose();
            brakeAction?.Dispose();
            steerAction?.Dispose();
            handbrakeAction?.Dispose();
            pauseAction?.Dispose();
            respawnAction?.Dispose();
            throttleAction = null;
            brakeAction = null;
            steerAction = null;
            handbrakeAction = null;
            pauseAction = null;
            respawnAction = null;
        }

        public void SetFinished(bool value)
        {
            finished = value;
            if (value) body.linearVelocity *= 0.25f;
        }

        private void BuildVisual()
        {
            Material bodyMaterial = MakeMaterial(new Color(0.48f, 0.018f, 0.012f), 0.72f, 0.34f);
            Material bodyAccentMaterial = MakeMaterial(new Color(0.2f, 0.012f, 0.01f), 0.55f, 0.26f);
            Material silverMaterial = MakeMaterial(new Color(0.5f, 0.53f, 0.56f), 0.88f, 0.34f);
            Material chromeMaterial = MakeMaterial(new Color(0.82f, 0.86f, 0.9f), 0.95f, 0.38f);
            Material carbonMaterial = MakeMaterial(new Color(0.012f, 0.014f, 0.018f), 0.2f, 0.65f);
            Material glassMaterial = MakeMaterial(new Color(0.018f, 0.065f, 0.09f), 0.38f, 0.2f);
            Material tyreMaterial = MakeMaterial(new Color(0.012f, 0.014f, 0.016f), 0.02f, 0.72f);
            Material tyreSidewallMaterial = MakeMaterial(new Color(0.045f, 0.05f, 0.055f), 0.03f, 0.5f);
            Material rimMaterial = MakeMaterial(new Color(0.62f, 0.68f, 0.74f), 0.88f, 0.3f);
            Material brakeMaterial = MakeMaterial(new Color(0.24f, 0.26f, 0.28f), 0.8f, 0.42f);
            Material caliperMaterial = MakeMaterial(new Color(0.95f, 0.18f, 0.025f), 0.35f, 0.24f);
            Material headlightMaterial = MakeMaterial(new Color(1f, 0.88f, 0.5f), 0.1f, 0.2f);
            Material tailLightMaterial = MakeMaterial(new Color(1f, 0.018f, 0.008f), 0.1f, 0.2f);

            visualRoot = new GameObject("ClassicMuscleVisual").transform;
            visualRoot.SetParent(transform, false);
            visualRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);

            CreatePart(PrimitiveType.Cube, "BodyShell", new Vector3(0f, 0.58f, 0f), new Vector3(2.12f, 0.42f, 4.5f), bodyMaterial);
            CreatePart(PrimitiveType.Cube, "LowerBody", new Vector3(0f, 0.39f, 0.02f), new Vector3(2.18f, 0.2f, 4.3f), bodyAccentMaterial);
            CreatePart(PrimitiveType.Cube, "FrontNose", new Vector3(0f, 0.63f, -2.02f), new Vector3(2.16f, 0.36f, 0.62f), bodyMaterial);
            CreatePart(PrimitiveType.Cube, "LongHood", new Vector3(0f, 0.86f, -1.12f), new Vector3(1.86f, 0.16f, 1.7f), bodyMaterial);
            CreatePart(PrimitiveType.Cube, "HoodScoop", new Vector3(0f, 0.975f, -0.82f), new Vector3(0.48f, 0.09f, 0.52f), carbonMaterial);
            CreatePart(PrimitiveType.Cube, "RearDeck", new Vector3(0f, 0.86f, 1.48f), new Vector3(1.88f, 0.16f, 0.78f), bodyMaterial);

            CreatePart(PrimitiveType.Cube, "CabinFrame", new Vector3(0f, 1.1f, 0.34f), new Vector3(1.5f, 0.48f, 1.62f), bodyMaterial);
            CreatePart(PrimitiveType.Cube, "Windshield", new Vector3(0f, 1.16f, -0.45f), new Vector3(1.3f, 0.34f, 0.055f), glassMaterial, Quaternion.Euler(20f, 0f, 0f));
            CreatePart(PrimitiveType.Cube, "RearGlass", new Vector3(0f, 1.14f, 1.06f), new Vector3(1.3f, 0.33f, 0.055f), glassMaterial, Quaternion.Euler(-18f, 0f, 0f));
            CreatePart(PrimitiveType.Cube, "LeftWindow", new Vector3(-0.765f, 1.15f, 0.3f), new Vector3(0.055f, 0.3f, 1.08f), glassMaterial, Quaternion.Euler(0f, 0f, 9f));
            CreatePart(PrimitiveType.Cube, "RightWindow", new Vector3(0.765f, 1.15f, 0.3f), new Vector3(0.055f, 0.3f, 1.08f), glassMaterial, Quaternion.Euler(0f, 0f, -9f));
            CreatePart(PrimitiveType.Cube, "BlackRoof", new Vector3(0f, 1.39f, 0.3f), new Vector3(1.48f, 0.1f, 1.48f), carbonMaterial);

            CreatePart(PrimitiveType.Cube, "ChromeSideTrimLeft", new Vector3(-1.08f, 0.56f, 0.18f), new Vector3(0.07f, 0.13f, 3.45f), chromeMaterial);
            CreatePart(PrimitiveType.Cube, "ChromeSideTrimRight", new Vector3(1.08f, 0.56f, 0.18f), new Vector3(0.07f, 0.13f, 3.45f), chromeMaterial);
            CreatePart(PrimitiveType.Cube, "FrontBumper", new Vector3(0f, 0.46f, -2.32f), new Vector3(2.2f, 0.18f, 0.18f), chromeMaterial);
            CreatePart(PrimitiveType.Cube, "RearBumper", new Vector3(0f, 0.46f, 2.3f), new Vector3(2.2f, 0.18f, 0.18f), chromeMaterial);
            CreatePart(PrimitiveType.Cube, "FrontGrille", new Vector3(0f, 0.64f, -2.34f), new Vector3(1.55f, 0.26f, 0.05f), carbonMaterial);
            CreatePart(PrimitiveType.Cube, "GrilleBarTop", new Vector3(0f, 0.76f, -2.37f), new Vector3(1.58f, 0.035f, 0.035f), chromeMaterial);
            CreatePart(PrimitiveType.Cube, "GrilleBarBottom", new Vector3(0f, 0.52f, -2.37f), new Vector3(1.58f, 0.035f, 0.035f), chromeMaterial);
            CreatePart(PrimitiveType.Cube, "FrontSplitter", new Vector3(0f, 0.33f, -2.34f), new Vector3(2.18f, 0.07f, 0.28f), bodyAccentMaterial);
            CreatePart(PrimitiveType.Cube, "RearPanel", new Vector3(0f, 0.72f, 2.31f), new Vector3(1.72f, 0.28f, 0.05f), bodyAccentMaterial);
            CreatePart(PrimitiveType.Cube, "TailLightLeft", new Vector3(-0.62f, 0.79f, 2.34f), new Vector3(0.5f, 0.15f, 0.05f), tailLightMaterial);
            CreatePart(PrimitiveType.Cube, "TailLightRight", new Vector3(0.62f, 0.79f, 2.34f), new Vector3(0.5f, 0.15f, 0.05f), tailLightMaterial);
            CreatePart(PrimitiveType.Cube, "RearLip", new Vector3(0f, 1.04f, 1.94f), new Vector3(1.7f, 0.08f, 0.16f), bodyAccentMaterial);
            CreatePart(PrimitiveType.Cube, "MirrorLeft", new Vector3(-0.92f, 1.15f, -0.12f), new Vector3(0.18f, 0.14f, 0.3f), bodyAccentMaterial, Quaternion.Euler(0f, -12f, 0f));
            CreatePart(PrimitiveType.Cube, "MirrorRight", new Vector3(0.92f, 1.15f, -0.12f), new Vector3(0.18f, 0.14f, 0.3f), bodyAccentMaterial, Quaternion.Euler(0f, 12f, 0f));
            CreatePart(PrimitiveType.Cylinder, "HeadlightRingLeft", new Vector3(-0.72f, 0.84f, -2.35f), new Vector3(0.25f, 0.045f, 0.25f), chromeMaterial, Quaternion.Euler(90f, 0f, 0f));
            CreatePart(PrimitiveType.Cylinder, "HeadlightRingRight", new Vector3(0.72f, 0.84f, -2.35f), new Vector3(0.25f, 0.045f, 0.25f), chromeMaterial, Quaternion.Euler(90f, 0f, 0f));
            CreatePart(PrimitiveType.Cylinder, "HeadlightLeft", new Vector3(-0.72f, 0.84f, -2.39f), new Vector3(0.17f, 0.035f, 0.17f), headlightMaterial, Quaternion.Euler(90f, 0f, 0f));
            CreatePart(PrimitiveType.Cylinder, "HeadlightRight", new Vector3(0.72f, 0.84f, -2.39f), new Vector3(0.17f, 0.035f, 0.17f), headlightMaterial, Quaternion.Euler(90f, 0f, 0f));
            CreatePart(PrimitiveType.Cylinder, "ExhaustLeft", new Vector3(-0.62f, 0.47f, 2.4f), new Vector3(0.13f, 0.2f, 0.13f), chromeMaterial, Quaternion.Euler(90f, 0f, 0f));
            CreatePart(PrimitiveType.Cylinder, "ExhaustRight", new Vector3(0.62f, 0.47f, 2.4f), new Vector3(0.13f, 0.2f, 0.13f), chromeMaterial, Quaternion.Euler(90f, 0f, 0f));

            wheels = new Transform[4];
            Vector3[] wheelPositions =
            {
                new Vector3(-1.1f, 0.43f, -1.35f), new Vector3(1.1f, 0.43f, -1.35f),
                new Vector3(-1.1f, 0.43f, 1.35f), new Vector3(1.1f, 0.43f, 1.35f),
            };
            for (int i = 0; i < wheelPositions.Length; i++)
            {
                CreatePart(PrimitiveType.Cube, $"Fender_{i}", wheelPositions[i] + Vector3.up * 0.27f, new Vector3(0.22f, 0.28f, 1.08f), bodyMaterial);
                CreateWheel(i, wheelPositions[i], tyreMaterial, tyreSidewallMaterial, rimMaterial, brakeMaterial, caliperMaterial);
            }
        }

        private void CreateWheel(int index, Vector3 position, Material tyreMaterial, Material sidewallMaterial, Material rimMaterial, Material brakeMaterial, Material caliperMaterial)
        {
            Quaternion axleRotation = Quaternion.Euler(0f, 0f, 90f);
            GameObject tyre = CreatePart(PrimitiveType.Cylinder, $"Wheel_{index}", position, new Vector3(0.58f, 0.23f, 0.58f), tyreMaterial, axleRotation);
            wheels[index] = tyre.transform;

            float outside = position.x < 0f ? -0.255f : 0.255f;
            Vector3 facePosition = position + Vector3.right * outside;
            CreatePart(PrimitiveType.Cylinder, $"TyreSidewall_{index}", facePosition, new Vector3(0.49f, 0.035f, 0.49f), sidewallMaterial, axleRotation);
            CreatePart(PrimitiveType.Cylinder, $"BrakeDisc_{index}", facePosition + Vector3.right * outside * 0.12f, new Vector3(0.37f, 0.045f, 0.37f), brakeMaterial, axleRotation);
            CreatePart(PrimitiveType.Cylinder, $"Rim_{index}", facePosition + Vector3.right * outside * 0.2f, new Vector3(0.31f, 0.065f, 0.31f), rimMaterial, axleRotation);
            CreatePart(PrimitiveType.Cylinder, $"Hub_{index}", facePosition + Vector3.right * outside * 0.28f, new Vector3(0.12f, 0.08f, 0.12f), brakeMaterial, axleRotation);

            for (int spoke = 0; spoke < 5; spoke++)
            {
                float angle = spoke * 72f;
                Vector3 radial = Quaternion.AngleAxis(angle, Vector3.right) * Vector3.up;
                Vector3 spokePosition = facePosition + Vector3.right * outside * 0.3f + radial * 0.13f;
                CreatePart(PrimitiveType.Cube, $"Spoke_{index}_{spoke}", spokePosition, new Vector3(0.035f, 0.045f, 0.23f), rimMaterial, Quaternion.LookRotation(radial, Vector3.right));
            }

            Vector3 caliperPosition = facePosition + Vector3.right * outside * 0.33f + Vector3.up * 0.2f;
            CreatePart(PrimitiveType.Cube, $"Caliper_{index}", caliperPosition, new Vector3(0.045f, 0.16f, 0.1f), caliperMaterial);
        }

        private void CacheWheels()
        {
            wheels = new Transform[4];
            for (int i = 0; i < wheels.Length; i++)
            {
                wheels[i] = visualRoot.Find($"Wheel_{i}");
            }
        }

        private void ClearVisual()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            visualRoot = null;
            wheels = null;
        }

        private GameObject CreatePart(PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale, Material material, Quaternion? rotation = null)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(visualRoot == null ? transform : visualRoot, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            if (rotation.HasValue) part.transform.localRotation = rotation.Value;
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
            return part;
        }

        private void AnimateWheels(float forwardSpeed)
        {
            if (wheels == null) return;
            float spin = forwardSpeed * 0.08f;
            foreach (Transform wheel in wheels) wheel.Rotate(Vector3.right, spin, Space.Self);
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
