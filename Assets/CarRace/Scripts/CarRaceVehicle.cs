using UnityEngine;
using UnityEngine.InputSystem;

namespace CarRace
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class CarRaceVehicle : MonoBehaviour
    {
        [Header("Suspension & Dynamics")]
        [SerializeField] private float suspensionRestDist = 0.55f;
        [SerializeField] private float suspensionTravel = 0.22f;
        [SerializeField] private float suspensionStrength = 36000f;
        [SerializeField] private float suspensionDamping = 3800f;
        [SerializeField] private float antiRollStrength = 4000f;
        [SerializeField] private float gravityMultiplier = 1.0f;
        [SerializeField] private float airborneGravityMultiplier = 1.6f;
        [SerializeField] private float groundAlignmentStrength = 1200f;
        [SerializeField] private float groundAlignmentDamping = 800f;
        [SerializeField] private float downforceCoefficient = 0.8f;
        [SerializeField] private float staticDownforce = 500f;
        [SerializeField] private float minimumGroundNormal = 0.25f;

        [Header("Handling")]
        [SerializeField] private float engineForce = 8500f;
        [SerializeField] private float reverseForce = 4500f;
        [SerializeField] private float brakeForce = 16000f;
        [SerializeField] private float steerAngle = 32f;
        [SerializeField] private float lateralGrip = 13f;
        [SerializeField] private float driftGrip = 3.6f;
        [SerializeField] private float tireFriction = 1.4f;
        [SerializeField] private float driftFriction = 0.55f;
        [SerializeField] private float rollingResistance = 15f;
        [SerializeField] private float aerodynamicDrag = 0.6f;
        [SerializeField] private float steeringGain = 5.0f;
        [SerializeField] private float maxYawRate = 2.8f;
        [SerializeField] private float maxSpeed = 38f;
        [SerializeField] private float maxReverseSpeed = 12f;
        [SerializeField] private float throttleResponse = 7f;
        [SerializeField] private float steeringResponse = 10f;

        // DeLorean wheel axle positions relative to the car root:
        // Front axles: local Y = -0.31, Z = 1.29, X = +/-0.79, tire bottom = -0.74
        // Rear axles: local Y = -0.27, Z = -1.24, X = +/-0.79, tire bottom = -0.76
        private static readonly Vector3[] WheelAxles =
        {
            new Vector3(-0.79f, -0.31f, 1.29f),  // 0: Front Left
            new Vector3(0.79f, -0.31f, 1.29f),   // 1: Front Right
            new Vector3(-0.79f, -0.27f, -1.24f), // 2: Rear Left
            new Vector3(0.79f, -0.27f, -1.24f)   // 3: Rear Right
        };

        private Rigidbody body;
        private Transform visualRoot;
        private bool importedModel;
        private Transform[] wheels;
        private Vector3[] baseWheelPositions;
        private Quaternion[] baseWheelRotations;
        private float wheelRollAngle;
        private int roadLayerMask;
        private BoxCollider vehicleCollider;
        private int lastCollisionFilterFrame = -1;
        private readonly RaycastHit[] wheelRaycastHits = new RaycastHit[16];

        private float throttle;
        private float brake;
        private float steer;
        private float targetThrottle;
        private float targetBrake;
        private float targetSteer;
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
        public bool DebugOverride { get; set; }
        public float DebugThrottle { get; set; }
        public float DebugBrake { get; set; }
        public float DebugSteer { get; set; }
        public bool DebugHandbrake { get; set; }

        private void Awake()
        {
            Initialize();
        }

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

            body.mass = 1250f;
            body.isKinematic = false;
            body.detectCollisions = true;
            body.useGravity = true;
            body.linearDamping = 0.05f;
            body.angularDamping = 3.0f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = 8f;
            body.solverIterations = 14;
            body.solverVelocityIterations = 10;
            body.centerOfMass = new Vector3(0f, -0.32f, 0.02f);
            body.WakeUp();

            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
            gameObject.layer = ignoreRaycast;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = ignoreRaycast;
            }
            roadLayerMask = Physics.AllLayers;
            if (ignoreRaycast >= 0)
            {
                roadLayerMask &= ~(1 << ignoreRaycast);
            }

            BoxCollider col = GetComponent<BoxCollider>();
            if (col != null)
            {
                // Collider covers upper cabin (bottom at Y = +0.125), keeping it well above the road (Y = -0.75)
                col.size = new Vector3(1.95f, 0.65f, 4.3f);
                col.center = new Vector3(0f, 0.45f, 0f);
                col.material = new PhysicsMaterial("VehiclePhysicMat")
                {
                    staticFriction = 0.45f,
                    dynamicFriction = 0.35f,
                    frictionCombine = PhysicsMaterialCombine.Average
                };
                vehicleCollider = col;
            }

            ApplyRoadCollisionFiltering();

            visualRoot = transform.Find("ImportedDelorean");
            importedModel = visualRoot != null;
            if (!importedModel)
            {
                visualRoot = transform.Find("ClassicMuscleVisual");
            }

            if (visualRoot == null)
            {
                ClearVisual();
                BuildVisual();
            }
            else
            {
                CacheWheels();
            }

            visualRoot.localRotation = importedModel ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.Euler(0f, 180f, 0f);
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

            if (respawnPressed || (body != null && transform.position.y < -20f))
            {
                CarRaceGame game = FindAnyObjectByType<CarRaceGame>();
                game?.RespawnPlayer();
            }

            float forwardSpeed = body != null ? Vector3.Dot(body.linearVelocity, transform.forward) : 0f;
            AnimateWheels(forwardSpeed);
        }

        private void FixedUpdate()
        {
            if (body == null || paused || finished) return;
            if (body.IsSleeping()) body.WakeUp();

            if (Time.frameCount - lastCollisionFilterFrame > 30)
            {
                ApplyRoadCollisionFiltering();
            }

            ReadInput();
            SmoothInput();

            // Unity's default gravity is the baseline. A small multiplier gives the
            // vehicle a grounded, road-car feel without changing gravity for the rest
            // of the project.
            if (gravityMultiplier > 1f)
            {
                body.AddForce(Physics.gravity * body.mass * (gravityMultiplier - 1f), ForceMode.Force);
            }

            Vector3 groundNormalSum = Vector3.zero;
            int groundedCount = 0;
            float[] compressions = new float[4];
            bool[] wheelGrounded = new bool[4];
            Vector3[] groundNormals = new Vector3[4];
            Vector3[] axleWorlds = new Vector3[4];

            const float rayStartHeight = 0.35f;
            float rayTotalLength = rayStartHeight + suspensionRestDist + suspensionTravel;

            // Find contacts first. This lets the suspension force be shared across
            // the wheels that are actually supporting the car and prevents a single
            // curb or mesh edge from launching the whole vehicle.
            for (int i = 0; i < 4; i++)
            {
                Vector3 axle = WheelAxles[i];
                Vector3 axleWorld = transform.TransformPoint(axle);
                axleWorlds[i] = axleWorld;
                Vector3 rayOrigin = axleWorld + transform.up * rayStartHeight;

                if (TryGetGroundHit(rayOrigin, -transform.up, rayTotalLength, out RaycastHit roadHit))
                {
                    groundedCount++;
                    wheelGrounded[i] = true;
                    Vector3 groundNormal = roadHit.normal.normalized;
                    groundNormals[i] = groundNormal;
                    groundNormalSum += groundNormal;

                    // Suspension length is measured from the axle to the road. At
                    // rest length the spring is neutral; compression then builds
                    // smoothly as the car settles onto the surface.
                    float distFromAxle = roadHit.distance - rayStartHeight;
                    float compression = Mathf.Clamp(suspensionRestDist - distFromAxle, 0f, suspensionTravel);
                    compressions[i] = compression;
                }
            }

            float gravityMagnitude = Mathf.Max(0.1f, Physics.gravity.magnitude);
            float maxWheelSupportForce = body.mass * gravityMagnitude * 1.5f;

            for (int i = 0; i < 4; i++)
            {
                if (!wheelGrounded[i]) continue;

                Vector3 axleWorld = axleWorlds[i];
                Vector3 groundNormal = groundNormals[i];
                Vector3 suspDir = Vector3.Lerp(transform.up, groundNormal, 0.35f).normalized;
                Vector3 wheelVel = body.GetPointVelocity(axleWorld);
                float springVel = Vector3.Dot(wheelVel, suspDir);
                float springF = Mathf.Clamp(
                    (compressions[i] * suspensionStrength) - (springVel * suspensionDamping),
                    0f,
                    maxWheelSupportForce);
                body.AddForceAtPosition(suspDir * springF, axleWorld, ForceMode.Force);

                // Keep tire forces in the road plane so a steep slope cannot turn
                // engine power into unwanted lift.
                bool isFront = i < 2;
                float currentSteer = isFront ? steer * steerAngle : 0f;
                Vector3 wheelFwd = Vector3.ProjectOnPlane(transform.forward, groundNormal);
                if (wheelFwd.sqrMagnitude < 0.01f) continue;
                wheelFwd = Quaternion.AngleAxis(currentSteer, groundNormal) * wheelFwd.normalized;
                wheelFwd = Vector3.ProjectOnPlane(wheelFwd, groundNormal).normalized;
                Vector3 wheelRgt = Vector3.Cross(groundNormal, wheelFwd).normalized;
                float wheelForwardSpeed = Vector3.Dot(wheelVel, wheelFwd);

                // Lateral tire grip
                float latVel = Vector3.Dot(wheelVel, wheelRgt);
                float gripCoeff = isFront ? lateralGrip : (handbrake ? driftGrip : lateralGrip);
                float desiredLatForce = latVel * gripCoeff * (body.mass * 0.25f);
                float frictionCoeff = handbrake && !isFront ? driftFriction : tireFriction;
                float maxLatForce = Mathf.Max(springF * frictionCoeff, body.mass * 0.25f * gravityMagnitude * frictionCoeff);
                Vector3 latForce = -wheelRgt * Mathf.Clamp(desiredLatForce, -maxLatForce, maxLatForce);
                body.AddForceAtPosition(latForce, axleWorld, ForceMode.Force);

                // Rear-biased drive with torque curve
                if (throttle > 0f && wheelForwardSpeed < maxSpeed)
                {
                    float driveBias = isFront ? 0.35f : 0.65f;
                    float speedRatio = Mathf.Clamp01(Mathf.Max(0f, wheelForwardSpeed) / maxSpeed);
                    float torqueFactor = Mathf.Lerp(1f, 0.3f, speedRatio);
                    Vector3 driveF = wheelFwd * (engineForce * throttle * driveBias * torqueFactor);
                    body.AddForceAtPosition(driveF, axleWorld, ForceMode.Force);
                }

                if (brake > 0f)
                {
                    if (wheelForwardSpeed > 0.5f)
                    {
                        Vector3 brakeF = -wheelFwd * (brakeForce * brake * 0.25f);
                        body.AddForceAtPosition(brakeF, axleWorld, ForceMode.Force);
                    }
                    else if (wheelForwardSpeed > -maxReverseSpeed)
                    {
                        Vector3 revF = -wheelFwd * (reverseForce * brake * 0.25f);
                        body.AddForceAtPosition(revF, axleWorld, ForceMode.Force);
                    }
                }

                // Rolling resistance when coasting
                if (throttle < 0.01f && brake < 0.01f && Mathf.Abs(wheelForwardSpeed) > 0.1f)
                {
                    Vector3 rollF = -wheelFwd * (wheelForwardSpeed * rollingResistance);
                    body.AddForceAtPosition(rollF, axleWorld, ForceMode.Force);
                }
            }

            // Anti-roll bars transferring spring load across left and right axles
            if (wheelGrounded[0] && wheelGrounded[1])
            {
                float frontAntiRoll = (compressions[0] - compressions[1]) * antiRollStrength;
                body.AddForceAtPosition(-transform.up * frontAntiRoll, axleWorlds[0], ForceMode.Force);
                body.AddForceAtPosition(transform.up * frontAntiRoll, axleWorlds[1], ForceMode.Force);
            }
            if (wheelGrounded[2] && wheelGrounded[3])
            {
                float rearAntiRoll = (compressions[2] - compressions[3]) * antiRollStrength;
                body.AddForceAtPosition(-transform.up * rearAntiRoll, axleWorlds[2], ForceMode.Force);
                body.AddForceAtPosition(transform.up * rearAntiRoll, axleWorlds[3], ForceMode.Force);
            }

            if (groundedCount > 0)
            {
                Vector3 avgNormal = (groundNormalSum / groundedCount).normalized;
                Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, avgNormal);

                if (groundedCount >= 2)
                {
                    Vector3 tiltAxis = Vector3.Cross(transform.up, avgNormal);
                    Vector3 rollPitchVelocity = body.angularVelocity - avgNormal * Vector3.Dot(body.angularVelocity, avgNormal);
                    body.AddTorque(
                        tiltAxis * groundAlignmentStrength - rollPitchVelocity * groundAlignmentDamping,
                        ForceMode.Force);

                    body.AddForce(-avgNormal * (planarVelocity.sqrMagnitude * downforceCoefficient + staticDownforce), ForceMode.Force);
                    body.AddForce(-planarVelocity * planarVelocity.magnitude * aerodynamicDrag, ForceMode.Force);

                    // Drive steering from a target yaw rate
                    Vector3 surfaceForward = Vector3.ProjectOnPlane(transform.forward, avgNormal).normalized;
                    float forwardSpeed = Vector3.Dot(body.linearVelocity, surfaceForward);
                    float steerSpeed = Mathf.Max(Mathf.Abs(forwardSpeed), 2f);
                    float wheelBase = Mathf.Abs(WheelAxles[0].z - WheelAxles[2].z);
                    float targetYawRate = steer * Mathf.Tan(steerAngle * Mathf.Deg2Rad) * steerSpeed / Mathf.Max(wheelBase, 0.1f);
                    if (forwardSpeed < -0.5f) targetYawRate = -targetYawRate;
                    targetYawRate = Mathf.Clamp(targetYawRate, -maxYawRate, maxYawRate);
                    float currentYawRate = Vector3.Dot(body.angularVelocity, avgNormal);
                    body.AddTorque(avgNormal * ((targetYawRate - currentYawRate) * steeringGain), ForceMode.Acceleration);

                    // Hill-hold when stopped on slope
                    if (throttle < 0.01f && brake < 0.01f && Mathf.Abs(forwardSpeed) < 0.4f)
                    {
                        body.linearVelocity = Vector3.MoveTowards(body.linearVelocity, Vector3.zero, 15f * Time.fixedDeltaTime);
                    }
                }
            }
            else if (groundedCount == 0 && airborneGravityMultiplier > 1f)
            {
                // Pull the car back down quickly if it briefly loses all four contacts
                body.AddForce(Physics.gravity * body.mass * (airborneGravityMultiplier - 1f), ForceMode.Force);
            }

            LimitForwardSpeed(groundNormalSum, groundedCount);
        }

        private void ReadInput()
        {
            if (DebugOverride)
            {
                targetThrottle = Mathf.Clamp01(DebugThrottle);
                targetBrake = Mathf.Clamp01(DebugBrake);
                targetSteer = Mathf.Clamp(DebugSteer, -1f, 1f);
                handbrake = DebugHandbrake;
                return;
            }

            targetThrottle = throttleAction == null ? 0f : throttleAction.ReadValue<float>();
            targetBrake = brakeAction == null ? 0f : brakeAction.ReadValue<float>();
            targetSteer = steerAction == null ? 0f : steerAction.ReadValue<float>();
            handbrake = handbrakeAction != null && handbrakeAction.IsPressed();

            Keyboard keyboard = Keyboard.current;
            bool keyboardSteering = false;
            if (keyboard != null)
            {
                targetThrottle = Mathf.Max(targetThrottle, keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f);
                targetBrake = Mathf.Max(targetBrake, keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);

                bool steerLeft = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
                bool steerRight = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
                if (steerLeft || steerRight)
                {
                    keyboardSteering = true;
                    targetSteer = steerLeft == steerRight ? 0f : (steerLeft ? -1f : 1f);
                }
                handbrake |= keyboard.spaceKey.isPressed;
            }

            if (!keyboardSteering && Gamepad.current != null)
            {
                Gamepad gamepad = Gamepad.current;
                targetThrottle = Mathf.Max(targetThrottle, gamepad.rightTrigger.ReadValue());
                targetBrake = Mathf.Max(targetBrake, gamepad.leftTrigger.ReadValue());
                float stickSteer = gamepad.leftStick.x.ReadValue();
                if (Mathf.Abs(stickSteer) > 0.1f) targetSteer = stickSteer;
                handbrake |= gamepad.buttonSouth.isPressed;
            }

            targetThrottle = Mathf.Clamp01(targetThrottle);
            targetBrake = Mathf.Clamp01(targetBrake);
            targetSteer = Mathf.Clamp(targetSteer, -1f, 1f);
        }

        private void SmoothInput()
        {
            throttle = Mathf.MoveTowards(throttle, targetThrottle, throttleResponse * Time.fixedDeltaTime);
            brake = Mathf.MoveTowards(brake, targetBrake, throttleResponse * Time.fixedDeltaTime);
            steer = Mathf.MoveTowards(steer, targetSteer, steeringResponse * Time.fixedDeltaTime);
        }

        private void LimitForwardSpeed(Vector3 groundNormalSum, int groundedCount)
        {
            Vector3 surfaceNormal = groundedCount > 0
                ? (groundNormalSum / groundedCount).normalized
                : Vector3.up;
            Vector3 surfaceForward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal).normalized;
            if (surfaceForward.sqrMagnitude < 0.01f) return;

            float forwardSpeed = Vector3.Dot(body.linearVelocity, surfaceForward);
            float limitedSpeed = Mathf.Clamp(forwardSpeed, -maxReverseSpeed, maxSpeed);
            if (!Mathf.Approximately(forwardSpeed, limitedSpeed))
            {
                body.linearVelocity += surfaceForward * (limitedSpeed - forwardSpeed);
            }
        }

        private bool TryGetGroundHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit groundHit)
        {
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                wheelRaycastHits,
                distance,
                roadLayerMask,
                QueryTriggerInteraction.Ignore);

            int closestIndex = -1;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = wheelRaycastHits[i];
                if (hit.collider == null || hit.collider.transform.root == transform.root) continue;

                // Ignore walls, guardrails, and the sides of the road. A wheel
                // ray should only support the car from a reasonably upward-facing
                // driving surface.
                Vector3 normal = hit.normal.normalized;
                if (Vector3.Dot(normal, Vector3.up) < minimumGroundNormal) continue;

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestIndex = i;
                }
            }

            if (closestIndex >= 0)
            {
                groundHit = wheelRaycastHits[closestIndex];
                return true;
            }

            groundHit = default;
            return false;
        }

        private void ApplyRoadCollisionFiltering()
        {
            // Destroy any lingering Mountain Road Drive Surface so no invisible barriers exist
            GameObject driveSurface = GameObject.Find("Mountain Road Drive Surface");
            if (driveSurface != null)
            {
                if (Application.isPlaying) Destroy(driveSurface);
                else DestroyImmediate(driveSurface);
            }

            lastCollisionFilterFrame = Time.frameCount;
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
            if (value && body != null) body.linearVelocity *= 0.25f;
        }

        private void CacheWheels()
        {
            wheels = new Transform[4];
            baseWheelPositions = new Vector3[4];
            baseWheelRotations = new Quaternion[4];

            if (importedModel)
            {
                wheels[0] = transform.Find("ImportedDelorean/FINAL_MODEL_BTTF.fbx/RootNode/Object_3/_rootJoint/Wheel1A_3D_00/3DWheel Front L");
                wheels[1] = transform.Find("ImportedDelorean/FINAL_MODEL_BTTF.fbx/RootNode/Object_3/_rootJoint/Wheel1A_3D_00/3DWheel Front R");
                wheels[2] = transform.Find("ImportedDelorean/FINAL_MODEL_BTTF.fbx/RootNode/Object_3/_rootJoint/Wheel1A_3D_00/3DWheel Rear L");
                wheels[3] = transform.Find("ImportedDelorean/FINAL_MODEL_BTTF.fbx/RootNode/Object_3/_rootJoint/Wheel1A_3D_00/3DWheel Rear R");
            }
            else if (visualRoot != null)
            {
                for (int i = 0; i < wheels.Length; i++)
                {
                    wheels[i] = visualRoot.Find($"Wheel_{i}");
                }
            }

            for (int i = 0; i < 4; i++)
            {
                if (wheels[i] != null)
                {
                    baseWheelPositions[i] = wheels[i].localPosition;
                    baseWheelRotations[i] = wheels[i].localRotation;
                }
            }
        }

        private void AnimateWheels(float forwardSpeed)
        {
            if (wheels == null) return;
            wheelRollAngle += forwardSpeed * (Time.deltaTime / 0.35f) * Mathf.Rad2Deg;

            for (int i = 0; i < 4; i++)
            {
                if (wheels[i] == null) continue;

                if (importedModel && baseWheelRotations != null && i < baseWheelRotations.Length)
                {
                    bool isFront = i < 2;
                    float steerYaw = isFront ? steer * steerAngle : 0f;
                    // In FBX hierarchy, local X is the rolling axle, local Y is steering yaw
                    Quaternion steerRot = Quaternion.Euler(0f, steerYaw, 0f);
                    Quaternion rollRot = Quaternion.Euler(wheelRollAngle, 0f, 0f);
                    wheels[i].localRotation = steerRot * rollRot * baseWheelRotations[i];

                    if (baseWheelPositions != null && i < baseWheelPositions.Length)
                    {
                        wheels[i].localPosition = baseWheelPositions[i];
                    }
                }
                else
                {
                    wheels[i].Rotate(Vector3.right, forwardSpeed * 0.08f, Space.Self);
                }
            }
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
            baseWheelPositions = new Vector3[4];
            baseWheelRotations = new Quaternion[4];
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
            baseWheelPositions[index] = tyre.transform.localPosition;
            baseWheelRotations[index] = tyre.transform.localRotation;

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
            baseWheelPositions = null;
            baseWheelRotations = null;
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
