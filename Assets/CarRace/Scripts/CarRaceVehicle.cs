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
        private Transform[] wheels;
        private float throttle;
        private float brake;
        private float steer;
        private bool handbrake;
        private bool paused;
        private bool finished;

        public float SpeedKph => body == null ? 0f : body.linearVelocity.magnitude * 3.6f;
        public Rigidbody Body => body;

        public void Initialize()
        {
            body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.mass = 1120f;
            body.linearDamping = 0.18f;
            body.angularDamping = 4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.centerOfMass = new Vector3(0f, -0.45f, 0.1f);
            if (transform.Find("Body") == null)
            {
                BuildVisual();
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame)
            {
                paused = !paused;
                Time.timeScale = paused ? 0f : 1f;
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
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

            float steeringScale = Mathf.Clamp01(body.linearVelocity.magnitude / 3.5f);
            float torque = steer * steeringTorque * steeringScale * (handbrake ? 1.35f : 1f);
            body.AddTorque(Vector3.up * torque, ForceMode.Force);

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
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                throttle = 0f;
                brake = 0f;
                steer = 0f;
                handbrake = false;
                return;
            }

            throttle = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f;
            brake = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f;
            steer = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steer -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steer += 1f;
            handbrake = keyboard.spaceKey.isPressed;

            if (Gamepad.current != null)
            {
                Gamepad gamepad = Gamepad.current;
                throttle = Mathf.Max(throttle, gamepad.rightTrigger.ReadValue());
                brake = Mathf.Max(brake, gamepad.leftTrigger.ReadValue());
                if (Mathf.Abs(gamepad.leftStick.x.ReadValue()) > 0.1f) steer = gamepad.leftStick.x.ReadValue();
                handbrake |= gamepad.buttonSouth.isPressed;
            }
        }

        public void SetFinished(bool value)
        {
            finished = value;
            if (value) body.linearVelocity *= 0.25f;
        }

        private void BuildVisual()
        {
            Material bodyMaterial = MakeMaterial(new Color(0.82f, 0.08f, 0.055f), 0.45f, 0.28f);
            Material darkMaterial = MakeMaterial(new Color(0.025f, 0.03f, 0.04f), 0.05f, 0.8f);
            Material glassMaterial = MakeMaterial(new Color(0.04f, 0.16f, 0.23f), 0.2f, 0.18f);
            Material rimMaterial = MakeMaterial(new Color(0.62f, 0.67f, 0.72f), 0.8f, 0.25f);
            Material lampMaterial = MakeMaterial(new Color(1f, 0.78f, 0.25f), 0.15f, 0.2f);

            CreatePart(PrimitiveType.Cube, "Body", new Vector3(0f, 0.65f, 0f), new Vector3(1.9f, 0.48f, 4.2f), bodyMaterial);
            CreatePart(PrimitiveType.Cube, "Hood", new Vector3(0f, 0.94f, -1.05f), new Vector3(1.72f, 0.16f, 1.25f), bodyMaterial);
            CreatePart(PrimitiveType.Cube, "Cabin", new Vector3(0f, 1.1f, 0.35f), new Vector3(1.35f, 0.55f, 1.5f), darkMaterial);
            CreatePart(PrimitiveType.Cube, "Windshield", new Vector3(0f, 1.12f, -0.38f), new Vector3(1.18f, 0.34f, 0.06f), glassMaterial, Quaternion.Euler(18f, 0f, 0f));
            CreatePart(PrimitiveType.Cube, "Roof", new Vector3(0f, 1.42f, 0.35f), new Vector3(1.42f, 0.1f, 1.38f), bodyMaterial);
            CreatePart(PrimitiveType.Cube, "FrontBumper", new Vector3(0f, 0.48f, -2.12f), new Vector3(1.95f, 0.18f, 0.18f), darkMaterial);
            CreatePart(PrimitiveType.Cube, "RearSpoiler", new Vector3(0f, 1.35f, 1.78f), new Vector3(1.45f, 0.1f, 0.2f), darkMaterial);
            CreatePart(PrimitiveType.Cube, "Headlights", new Vector3(0f, 0.82f, -2.15f), new Vector3(1.4f, 0.16f, 0.06f), lampMaterial);

            wheels = new Transform[4];
            Vector3[] wheelPositions =
            {
                new Vector3(-1.02f, 0.42f, -1.25f), new Vector3(1.02f, 0.42f, -1.25f),
                new Vector3(-1.02f, 0.42f, 1.25f), new Vector3(1.02f, 0.42f, 1.25f),
            };
            for (int i = 0; i < wheelPositions.Length; i++)
            {
                GameObject wheel = CreatePart(PrimitiveType.Cylinder, $"Wheel_{i}", wheelPositions[i], new Vector3(0.48f, 0.18f, 0.48f), darkMaterial, Quaternion.Euler(0f, 0f, 90f));
                wheels[i] = wheel.transform;
                CreatePart(PrimitiveType.Cylinder, $"Rim_{i}", wheelPositions[i] + new Vector3(i % 2 == 0 ? -0.12f : 0.12f, 0f, 0f), new Vector3(0.25f, 0.2f, 0.25f), rimMaterial, Quaternion.Euler(0f, 0f, 90f));
            }
        }

        private GameObject CreatePart(PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale, Material material, Quaternion? rotation = null)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            if (rotation.HasValue) part.transform.localRotation = rotation.Value;
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
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
