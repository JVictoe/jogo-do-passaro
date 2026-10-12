
using UnityEngine;

namespace Scripts.Gameplay.Bird
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BirdInputReader))]
    [RequireComponent(typeof(BirdFlightMotor))]
    [RequireComponent(typeof(BirdGroundDetector))]
    [DisallowMultipleComponent]
    public sealed class BirdLocomotionController : MonoBehaviour
    {
        [Header("Starting State")]

        [Tooltip(
            "Inicia o jogo no chao se houver uma " +
            "superficie Landable proxima."
        )]
        [SerializeField]
        private bool startGrounded = false;

        [Header("Landing")]

        [Tooltip(
            "Velocidade maxima da descida durante o pouso."
        )]
        [SerializeField, Min(0.1f)]
        private float landingDescentSpeed = 2.5f;

        [Tooltip(
            "Rapidez com que a velocidade horizontal " +
            "e reduzida durante o pouso."
        )]
        [SerializeField, Min(0.1f)]
        private float landingHorizontalDeceleration = 20f;

        [Tooltip(
            "Rapidez com que a velocidade vertical " +
            "se aproxima da velocidade de pouso."
        )]
        [SerializeField, Min(0.1f)]
        private float landingVerticalAcceleration = 12f;

        [Tooltip(
            "Distancia na qual a descida comeca " +
            "a ficar mais lenta."
        )]
        [SerializeField, Min(0.1f)]
        private float landingSlowdownDistance = 0.8f;

        [Header("Takeoff")]

        [Tooltip(
            "Velocidade vertical durante a decolagem."
        )]
        [SerializeField, Min(0.1f)]
        private float takeoffSpeed = 3.5f;

        [Tooltip(
            "Altura que o passaro deve subir antes " +
            "de recuperar o controle do voo."
        )]
        [SerializeField, Min(0.1f)]
        private float takeoffHeight = 1.4f;

        [Tooltip(
            "Tempo maximo permitido para a decolagem."
        )]
        [SerializeField, Min(0.1f)]
        private float takeoffMaxDuration = 1.2f;

        [Tooltip(
            "Intervalo apos a decolagem durante " +
            "o qual nao e permitido pousar novamente."
        )]
        [SerializeField, Min(0f)]
        private float landingCooldownAfterTakeoff = 0.6f;

        [Header("Debug")]

        [SerializeField]
        private bool logStateChanges = true;

        private Rigidbody rb;
        private BirdInputReader input;
        private BirdFlightMotor flightMotor;
        private BirdGroundDetector groundDetector;

        private float takeoffStartY;
        private float takeoffElapsed;
        private float landingUnlockTime;
        private float groundLostElapsed;
        private float nextDebugTime;


        private float lastValidSupportContactTime =
            float.NegativeInfinity;

        private bool HasRecentSupportContact =>
            Time.fixedTime - lastValidSupportContactTime <=
            Time.fixedDeltaTime * 2.5f;

        private void OnCollisionEnter(Collision collision)
        {
            RegisterSupportContact(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            RegisterSupportContact(collision);
        }

        private void RegisterSupportContact(Collision collision)
        {
            if (State != BirdLocomotionState.Landing &&
                State != BirdLocomotionState.Grounded)
            {
                return;
            }

            if (!groundDetector.IsValidLandingContact(collision))
                return;

            lastValidSupportContactTime = Time.fixedTime;
        }

        public BirdLocomotionState State { get; private set; } =
            BirdLocomotionState.Flying;

        public bool IsGrounded =>
            State == BirdLocomotionState.Grounded;

        public bool IsFlying =>
            State == BirdLocomotionState.Flying;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            input = GetComponent<BirdInputReader>();
            flightMotor = GetComponent<BirdFlightMotor>();
            groundDetector = GetComponent<BirdGroundDetector>();
        }

        private void Start()
        {
            if (!startGrounded)
                return;

            groundDetector.RefreshDetection();

            if (groundDetector.IsCloseToSurface)
            {
                EnterGrounded();
            }
            else
            {
                Debug.LogWarning(
                    "BirdLocomotionController: Start Grounded " +
                    "ativado, mas nenhuma superficie proxima " +
                    "foi encontrada. Iniciando em Flying.",
                    this
                );
            }
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // Atualiza a deteccao antes de tomar
            // qualquer decisao de locomocao.
            groundDetector.RefreshDetection();

            // Cada pressionamento de Space
            // e consumido apenas uma vez.
            bool togglePressed =
                input.ConsumeTakeoffLandPressed();

            switch (State)
            {
                case BirdLocomotionState.Flying:
                    UpdateFlying(togglePressed);
                    break;

                case BirdLocomotionState.Landing:

                    if (togglePressed)
                    {
                        // Cancela o pouso e inicia uma subida.
                        BeginTakingOff();
                    }
                    else
                    {
                        UpdateLanding(dt);
                    }

                    break;

                case BirdLocomotionState.Grounded:
                    UpdateGrounded(dt, togglePressed);
                    break;

                case BirdLocomotionState.TakingOff:
                    UpdateTakingOff(dt);
                    break;
            }

            if (Time.time >= nextDebugTime)
            {
                Debug.Log(
                    $"[BIRD LOCOMOTION] " +
                    $"State: {State} | " +
                    $"Ground: {groundDetector.HasLandingSurface} | " +
                    $"Close: {groundDetector.IsCloseToSurface} | " +
                    $"Distance: {groundDetector.DistanceToSurface:F2} | " +
                    $"Velocity: {rb.linearVelocity} | " +
                    $"Gravity: {rb.useGravity}"
                );

                nextDebugTime = Time.time + 0.25f;
            }
        }

        private void UpdateFlying(bool togglePressed)
        {
            if (!togglePressed)
                return;

            // Impede pousar novamente imediatamente
            // depois de uma decolagem.
            if (Time.time < landingUnlockTime)
                return;

            if (!groundDetector.HasLandingSurface)
                return;

            BeginLanding();
        }

        private void BeginLanding()
        {
            rb.useGravity = false;
            lastValidSupportContactTime =float.NegativeInfinity;
            ChangeState(BirdLocomotionState.Landing);
        }


        private void UpdateLanding(float dt)
        {
            // So termina o pouso depois do contato
            // fisico com uma superficie valida.
            if (HasRecentSupportContact)
            {
                EnterGrounded();
                return;
            }

            // Se perdemos completamente a superficie,
            // abortamos o pouso.
            if (!groundDetector.HasLandingSurface)
            {
                AbortLanding();
                return;
            }

            Vector3 velocity = rb.linearVelocity;

            // Reduz suavemente a velocidade horizontal.
            Vector3 horizontalVelocity =
                Vector3.ProjectOnPlane(
                    velocity,
                    Vector3.up
                );

            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                Vector3.zero,
                landingHorizontalDeceleration * dt
            );

            float distance =
                groundDetector.DistanceToSurface;

            float descentSpeed = landingDescentSpeed;

            // Desacelera a descida perto da superficie.
            if (distance < landingSlowdownDistance)
            {
                float normalizedDistance = Mathf.Clamp01(
                    distance / landingSlowdownDistance
                );

                descentSpeed = Mathf.Lerp(
                    0.35f,
                    landingDescentSpeed,
                    normalizedDistance
                );
            }

            float verticalVelocity = Mathf.MoveTowards(
                velocity.y,
                -descentSpeed,
                landingVerticalAcceleration * dt
            );

            rb.linearVelocity =
                horizontalVelocity +
                Vector3.up * verticalVelocity;

            // Nao usamos mais IsCloseToSurface
            // para concluir o pouso.
            // A colisao real define o estado Grounded.
        }


        private void EnterGrounded()
        {
            rb.linearVelocity = Vector3.zero;
            rb.useGravity = true;

            flightMotor.PrepareForFlight();

            groundLostElapsed = 0f;

            ChangeState(BirdLocomotionState.Grounded);
        }

        private void UpdateGrounded(
            float dt,
            bool togglePressed)
        {
            if (togglePressed)
            {
                BeginTakingOff();
                return;
            }

            // Na v0.7.2 ainda nao temos caminhada.
            // A gravidade e as colisoes fisicas
            // continuam atuando verticalmente.
            Vector3 velocity = rb.linearVelocity;

            rb.linearVelocity = new Vector3(
                0f,
                Mathf.Min(velocity.y, 0f),
                0f
            );

            // Se o suporte desaparecer, o passaro
            // volta ao modo aereo.
            if (!HasRecentSupportContact)
            {
                groundLostElapsed += dt;

                if (groundLostElapsed >= 0.15f)
                {
                    rb.useGravity = false;
                    flightMotor.PrepareForFlight();

                    ChangeState(BirdLocomotionState.Flying);
                }
            }
            else
            {
                groundLostElapsed = 0f;
            }
        }

        private void BeginTakingOff()
        {
            flightMotor.PrepareForFlight();

            rb.useGravity = false;
            rb.linearVelocity =
                Vector3.up * takeoffSpeed;

            takeoffStartY = rb.position.y;
            takeoffElapsed = 0f;

            // Um novo pressionamento de Space
            // sera necessario para pousar.
            landingUnlockTime =
                Time.time + landingCooldownAfterTakeoff;
            lastValidSupportContactTime = float.NegativeInfinity;
            ChangeState(BirdLocomotionState.TakingOff);
        }

        private void UpdateTakingOff(float dt)
        {
            takeoffElapsed += dt;

            rb.linearVelocity =
                Vector3.up * takeoffSpeed;

            float heightGained =
                rb.position.y - takeoffStartY;

            bool reachedHeight =
                heightGained >= takeoffHeight;

            bool timedOut =
                takeoffElapsed >= takeoffMaxDuration;

            if (reachedHeight || timedOut)
            {
                ChangeState(BirdLocomotionState.Flying);
            }
        }

        private void AbortLanding()
        {
            rb.useGravity = false;

            flightMotor.PrepareForFlight();
            lastValidSupportContactTime = float.NegativeInfinity;
            ChangeState(BirdLocomotionState.Flying);
        }

        private void ChangeState(BirdLocomotionState newState)
        {
            if (State == newState)
                return;

            BirdLocomotionState previous = State;
            State = newState;

            if (logStateChanges)
            {
                Debug.Log(
                    $"[BIRD LOCOMOTION] " +
                    $"{previous} -> {newState}",
                    this
                );
            }
        }
    }
}
