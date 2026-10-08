
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

        [SerializeField]
        private Transform birdVisual;

        private Rigidbody rb;
        private BirdInputReader input;

        private Quaternion flightRotation;
        private float currentSpeed;
        private float currentTurnRate;

        private bool hasBlockingContact;
        private Vector3 blockingNormal;


        public float CurrentSpeed => currentSpeed;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            input = GetComponent<BirdInputReader>();

            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            if (settings == null ||
                gameplayCamera == null ||
                birdVisual == null ||
                birdVisual == transform)
            {
                Debug.LogError(
                    "BirdFlightMotor: referencias invalidas.",
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

            rb.isKinematic = false;
            rb.useGravity = false;
            rb.linearDamping = 0f;

            rb.interpolation =
                RigidbodyInterpolation.Interpolate;


            rb.constraints |= RigidbodyConstraints.FreezeRotation;

        }

        private void Start()
        {
            if (!enabled)
                return;

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

            flightRotation = Quaternion.RotateTowards(
                flightRotation,
                targetRotation,
                settings.TurnSpeed * dt
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

            // Nao rotacionamos mais o Rigidbody.
            // A rotacao visual continua no LateUpdate.
        }



        private void ApplyFlightPhysics()
        {
            Vector3 flightDirection =
                flightRotation * Vector3.forward;

            Vector3 desiredVelocity =
                flightDirection * currentSpeed;

            // Impede aceleracao para dentro de obstaculos.
            if (hasBlockingContact)
            {
                if (Vector3.Dot(
                    desiredVelocity,
                    blockingNormal
                ) < 0f)
                {
                    desiredVelocity = Vector3.ProjectOnPlane(
                        desiredVelocity,
                        blockingNormal
                    );
                }
            }

            // Contatos sao atualizados pela proxima
            // etapa de simulacao fisica.
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

                // O passaro esta voando contra a superficie.
                if (dot < -0.1f)
                {
                    blockingNormal = normal;
                    hasBlockingContact = true;
                    break;
                }
            }
        }


        private void LateUpdate()
        {
            if (!enabled)
                return;

            float turnBank =
                -currentTurnRate * settings.TurnBankFactor;

            float keyboardBank =
                -input.Move.x * settings.KeyboardBankAngle;

            float bank = Mathf.Clamp(
                turnBank + keyboardBank,
                -settings.MaxBankAngle,
                settings.MaxBankAngle
            );

            // Converte a direcao de voo para o espaco local.
            Quaternion localFlightRotation =
                Quaternion.Inverse(transform.rotation) *
                flightRotation;

            Quaternion targetVisualRotation =
                localFlightRotation *
                Quaternion.Euler(0f, 0f, bank);

            float smoothing = 1f - Mathf.Exp(
                -settings.VisualSmoothing * Time.deltaTime
            );

            birdVisual.localRotation = Quaternion.Slerp(
                birdVisual.localRotation,
                targetVisualRotation,
                smoothing
            );
        }
    }
}
