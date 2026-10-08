
using UnityEngine;

namespace Scripts.Gameplay.Bird
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BirdInputReader))]
    [DisallowMultipleComponent]
    public sealed class BirdFlightMotor : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private BirdFlightSettings settings;

        [Header("References")]
        [SerializeField]
        private Camera gameplayCamera;

        private Rigidbody rb;
        private BirdInputReader input;

        private Quaternion flightRotation;
        private float currentSpeed;
        private float currentTurnRate;

        // Collision state
        private bool hasBlockingContact;
        private Vector3 blockingNormal;

        // Read-only gameplay data
        public BirdFlightSettings Settings => settings;
        public Quaternion FlightRotation => flightRotation;
        public float CurrentSpeed => currentSpeed;
        public float CurrentTurnRate => currentTurnRate;

        public float ActualSpeed =>
            rb != null ? rb.linearVelocity.magnitude : 0f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            input = GetComponent<BirdInputReader>();

            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            if (settings == null || gameplayCamera == null)
            {
                Debug.LogError(
                    "BirdFlightMotor: referencias ausentes.",
                    this
                );

                enabled = false;
                return;
            }

            currentSpeed = Mathf.Clamp(
                settings.StartSpeed,
                settings.MinSpeed,
                settings.MaxSpeed
            );

            flightRotation = transform.rotation;

            ConfigureRigidbody();
        }

        private void ConfigureRigidbody()
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.linearDamping = 0f;

            rb.interpolation =
                RigidbodyInterpolation.Interpolate;

            // Collision physics cannot rotate the capsule.
            rb.constraints |=
                RigidbodyConstraints.FreezeRotation;
        }

        private void Start()
        {
            if (!enabled)
                return;

            if (!input.isActiveAndEnabled)
            {
                Debug.LogError(
                    "BirdFlightMotor: InputReader inativo.",
                    this
                );

                enabled = false;
                return;
            }

            rb.linearVelocity =
                transform.forward * currentSpeed;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            UpdateSpeed(dt);
            UpdateFlightDirection(dt);
            ApplyFlightPhysics();
        }

        private void UpdateSpeed(float dt)
        {
            float throttle = input.Move.y;

            if (throttle > 0f)
            {
                currentSpeed +=
                    throttle * settings.Acceleration * dt;
            }
            else if (throttle < 0f)
            {
                currentSpeed +=
                    throttle * settings.Braking * dt;
            }

            currentSpeed = Mathf.Clamp(
                currentSpeed,
                settings.MinSpeed,
                settings.MaxSpeed
            );
        }


        private void UpdateFlightDirection(float dt)
        {
            Vector3 cameraForward =
                gameplayCamera.transform.forward;

            Quaternion targetRotation =
                Quaternion.LookRotation(
                    cameraForward,
                    Vector3.up
                );

            Vector3 previousForward =
                flightRotation * Vector3.forward;

            // Agilidade proporcional a velocidade comandada.
            float normalizedSpeed = Mathf.InverseLerp(
                settings.MinSpeed,
                settings.MaxSpeed,
                currentSpeed
            );

            float speedMultiplier = Mathf.Lerp(
                1f,
                settings.HighSpeedTurnMultiplier,
                normalizedSpeed
            );

            float effectiveTurnSpeed =
                settings.TurnSpeed * speedMultiplier;

            // Aproxima a direcao de voo da direcao da camera.
            flightRotation = Quaternion.RotateTowards(
                flightRotation,
                targetRotation,
                effectiveTurnSpeed * dt
            );

            Vector3 newForward =
                flightRotation * Vector3.forward;

            Vector3 previousFlat =
                Vector3.ProjectOnPlane(
                    previousForward,
                    Vector3.up
                );

            Vector3 newFlat =
                Vector3.ProjectOnPlane(
                    newForward,
                    Vector3.up
                );

            if (previousFlat.sqrMagnitude > 0.001f &&
                newFlat.sqrMagnitude > 0.001f)
            {
                float angle = Vector3.SignedAngle(
                    previousFlat,
                    newFlat,
                    Vector3.up
                );

                currentTurnRate = angle / dt;
            }
            else
            {
                currentTurnRate = 0f;
            }
        }


        private void ApplyFlightPhysics()
        {
            Vector3 flightDirection =
                flightRotation * Vector3.forward;

            Vector3 desiredVelocity =
                flightDirection * currentSpeed;

            // Preserve the previously validated
            // collision response.
            if (hasBlockingContact)
            {
                if (Vector3.Dot(
                    desiredVelocity,
                    blockingNormal
                ) < 0f)
                {
                    desiredVelocity =
                        Vector3.ProjectOnPlane(
                            desiredVelocity,
                            blockingNormal
                        );
                }
            }

            hasBlockingContact = false;

            Vector3 velocityError =
                desiredVelocity - rb.linearVelocity;

            Vector3 correction =
                velocityError * settings.VelocityResponse;

            correction = Vector3.ClampMagnitude(
                correction,
                settings.MaxFlightAcceleration
            );

            rb.AddForce(
                correction,
                ForceMode.Acceleration
            );
        }

        private void OnCollisionEnter(Collision collision)
        {
            RegisterCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            RegisterCollision(collision);
        }

        private void RegisterCollision(Collision collision)
        {
            Vector3 flightDirection =
                flightRotation * Vector3.forward;

            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector3 normal =
                    collision.GetContact(i).normal;

                float dot = Vector3.Dot(
                    flightDirection,
                    normal
                );

                if (dot < -0.1f)
                {
                    blockingNormal = normal;
                    hasBlockingContact = true;
                    break;
                }
            }
        }
    }
}
