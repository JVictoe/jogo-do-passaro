
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
        private float diveMomentum;
        private float currentTurnRate;

        // Collision state
        private bool hasBlockingContact;
        private Vector3 blockingNormal;
        private bool isFinalBrakingPhase;
        private float brakeFinalElapsed;

        private BirdLocomotionController locomotion;

        // Indica ao Animator quando a fase final
        // de frenagem esta acontecendo.
        public bool IsFinalBrakingPhase => isFinalBrakingPhase && brakeFinalElapsed < settings.BrakeFinishDuration;
        public bool IsEmergencyBraking => input != null && input.BrakeHeld && input.BoostHeld;
        // Read-only gameplay data
        public BirdFlightSettings Settings => settings;
        public Quaternion FlightRotation => flightRotation;
        public float CurrentSpeed => currentSpeed;
        public float CurrentTurnRate => currentTurnRate;
        public float DiveMomentum => diveMomentum;
        public float ActualSpeed => rb != null ? rb.linearVelocity.magnitude : 0f;
        public bool IsBraking => input != null && input.BrakeHeld && ActualSpeed > 0.3f;


        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            input = GetComponent<BirdInputReader>();
            locomotion = GetComponent<BirdLocomotionController>();

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

            currentSpeed = 0f;

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

            rb.linearVelocity = Vector3.zero;
        }

        private void FixedUpdate()
        {
            // A locomocao assume o Rigidbody durante
            // pouso, decolagem e movimento terrestre.
            if (locomotion != null &&
                locomotion.State != BirdLocomotionState.Flying)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;

            UpdateSpeed(dt);
            UpdateFlightDirection(dt);
            ApplyFlightPhysics();
        }


        public void PrepareForFlight()
        {
            // Limpa os valores herdados do voo anterior.
            currentSpeed = 0f;
            diveMomentum = 0f;
            currentTurnRate = 0f;

            isFinalBrakingPhase = false;
            brakeFinalElapsed = 0f;

            hasBlockingContact = false;
            blockingNormal = Vector3.zero;

            // Alinha a proxima direcao de voo
            // com a camera.
            Vector3 direction =
                gameplayCamera.transform.forward;

            if (direction.sqrMagnitude > 0.001f)
            {
                flightRotation = Quaternion.LookRotation(
                    direction,
                    Vector3.up
                );
            }
        }


        private void UpdateSpeed(float dt)
        {
            bool wantsToBrake = input.BrakeHeld;
            bool emergencyBrake = IsEmergencyBraking;

            bool wantsToFly =
                input.Move.y > 0.5f &&
                !wantsToBrake;

            Vector3 flightDirection =
                flightRotation * Vector3.forward;

            float verticalDirection = Vector3.Dot(
                flightDirection,
                Vector3.up
            );

            UpdateDiveMomentum(
                dt,
                wantsToFly,
                wantsToBrake,
                verticalDirection
            );

            // Ao soltar S, cancela o controle
            // da frenagem fisica.
            if (!wantsToBrake)
            {
                isFinalBrakingPhase = false;
                brakeFinalElapsed = 0f;
            }

            // Detecta a entrada na fase final.
            if (wantsToBrake && !isFinalBrakingPhase)
            {
                bool reachedFinalSpeed =
                    ActualSpeed <=
                    settings.BrakeAnimationStartSpeed;

                bool isStillMoving =
                    ActualSpeed > 0.35f;

                if (reachedFinalSpeed && isStillMoving)
                {
                    isFinalBrakingPhase = true;
                    brakeFinalElapsed = 0f;
                }
            }

            // Fase final:
            // S usa a desaceleracao normal.
            // S + Shift usa uma desaceleracao mais forte.
            if (wantsToBrake && isFinalBrakingPhase)
            {
                float finishDuration = emergencyBrake
                    ? settings.FastBrakeFinishDuration
                    : settings.BrakeFinishDuration;

                float finalDeceleration =
                    settings.BrakeAnimationStartSpeed /
                    finishDuration;

                currentSpeed = Mathf.MoveTowards(
                    currentSpeed,
                    0f,
                    finalDeceleration * dt
                );

                brakeFinalElapsed += dt;

                return;
            }

            float targetSpeed = 0f;

            if (wantsToFly)
            {
                float baseSpeed = input.BoostHeld
                    ? settings.MaxSpeed
                    : settings.CruiseSpeed;

                float verticalModifier =
                    -verticalDirection *
                    settings.VerticalSpeedInfluence;

                targetSpeed = Mathf.Max(
                    0f,
                    baseSpeed +
                    verticalModifier +
                    diveMomentum
                );
            }

            float changeRate;

            if (wantsToBrake)
            {
                changeRate = emergencyBrake
                    ? settings.FastEmergencyBraking
                    : settings.EmergencyBraking;
            }
            else if (targetSpeed > currentSpeed)
            {
                changeRate = settings.Acceleration;
            }
            else
            {
                changeRate = settings.Braking;
            }

            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                targetSpeed,
                changeRate * dt
            );
        }



        private void UpdateDiveMomentum(
            float dt,
            bool wantsToFly,
            bool wantsToBrake,
            float verticalDirection)
        {
            // Sem voo, com freio ou com o sistema desativado,
            // descartamos o impulso armazenado.
            if (!settings.EnableDiveMomentum ||
                !wantsToFly ||
                wantsToBrake)
            {
                diveMomentum = 0f;
                return;
            }

            // 0 = voo horizontal
            // 1 = mergulho totalmente vertical
            float descentAmount = -verticalDirection;

            // Evita acumular impulso em descidas muito leves.
            float descentFactor = Mathf.Clamp01(
                (descentAmount - 0.25f) / 0.75f
            );

            // Confirma que o Rigidbody está realmente descendo.
            float actualDescentSpeed = Vector3.Dot(
                rb.linearVelocity,
                Vector3.down
            );

            bool isDiving =
                descentFactor > 0f &&
                actualDescentSpeed > 1.5f;

            if (isDiving)
            {
                // Mergulhos mais acentuados acumulam
                // impulso mais rapidamente.
                diveMomentum = Mathf.MoveTowards(
                    diveMomentum,
                    settings.MaxDiveMomentum,
                    settings.DiveMomentumGain *
                    descentFactor * dt
                );
            }
            else
            {
                // Ao subir, gastamos o impulso mais rapidamente.
                bool isClimbing = verticalDirection > 0.25f;

                float decayRate = isClimbing
                    ? settings.ClimbMomentumDecay
                    : settings.DiveMomentumDecay;

                diveMomentum = Mathf.MoveTowards(
                    diveMomentum,
                    0f,
                    decayRate * dt
                );
            }
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

            // Mantem a correcao de colisoes ja validada.
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

            hasBlockingContact = false;

            Vector3 velocityError =
                desiredVelocity - rb.linearVelocity;

            // Resposta fisica mais forte durante a frenagem.
            float brakeMultiplier = 1f;

            if (input.BrakeHeld)
            {
                brakeMultiplier = IsEmergencyBraking
                    ? settings.FastBrakeResponseMultiplier
                    : settings.BrakeResponseMultiplier;
            }

            float response =
                settings.VelocityResponse * brakeMultiplier;

            float maxAcceleration =
                settings.MaxFlightAcceleration * brakeMultiplier;

            Vector3 correction =
                velocityError * response;

            correction = Vector3.ClampMagnitude(
                correction,
                maxAcceleration
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
