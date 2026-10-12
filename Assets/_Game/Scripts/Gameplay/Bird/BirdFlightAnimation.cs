
using UnityEngine;

namespace Scripts.Gameplay.Bird
{
    [RequireComponent(typeof(BirdFlightMotor))]
    [RequireComponent(typeof(BirdInputReader))]
    [DisallowMultipleComponent]
    public sealed class BirdFlightAnimation : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Animator animator;

        [Header("Flight Animation Speed")]

        [Tooltip("Velocidade de reproducao das asas durante o hover.")]
        [SerializeField, Min(0f)]
        private float hoverAnimationSpeed = 0.8f;

        [Tooltip("Velocidade de reproducao das asas durante o voo normal.")]
        [SerializeField, Min(0f)]
        private float flyAnimationSpeed = 1.25f;

        [Header("Flight Loop")]

        [Tooltip(
            "Tempo de suavizacao para alterar a velocidade das asas."
        )]
        [SerializeField, Min(0f)]
        private float speedSmoothTime = 0.25f;

        [Tooltip(
            "Velocidade fisica abaixo da qual o passaro " +
            "e considerado praticamente parado."
        )]
        [SerializeField, Min(0f)]
        private float hoverSpeedThreshold = 0.35f;

        [Header("Animation Transitions")]

        [Tooltip(
            "Duracao da transicao ao entrar na animacao de Boost."
        )]
        [SerializeField, Min(0f)]
        private float boostTransitionDuration = 0.15f;

        [Tooltip(
            "Duracao da transicao ao entrar na animacao de Brake."
        )]
        [SerializeField, Min(0f)]
        private float brakeTransitionDuration = 0.12f;

        [Tooltip(
            "Duracao da transicao de retorno ao FlightLoop."
        )]
        [SerializeField, Min(0f)]
        private float flightTransitionDuration = 0.20f;

        private float brakeAnimationTimer;
        private bool brakeActivatedForPress;

        private BirdFlightMotor motor;
        private BirdInputReader input;

        private enum FlightAnimationState
        {
            FlightLoop,
            Boost,
            Brake
        }

        private FlightAnimationState currentState;

        private static readonly int FlightLoopHash =
            Animator.StringToHash("Base Layer.FlightLoop");

        private static readonly int BoostHash =
            Animator.StringToHash("Base Layer.Boost");

        private static readonly int BrakeHash =
            Animator.StringToHash("Base Layer.Brake");

        private static readonly int FlightCycleSpeedHash =
            Animator.StringToHash("FlightCycleSpeed");

        private void Awake()
        {
            motor = GetComponent<BirdFlightMotor>();
            input = GetComponent<BirdInputReader>();

            if (animator == null)
            {
                Debug.LogError(
                    "BirdFlightAnimation: Animator nao configurado.",
                    this
                );

                enabled = false;
                return;
            }

            if (!animator.HasState(0, FlightLoopHash) ||
                !animator.HasState(0, BoostHash) ||
                !animator.HasState(0, BrakeHash))
            {
                Debug.LogError(
                    "BirdFlightAnimation: configure FlightLoop, " +
                    "Boost e Brake na Base Layer do Animator.",
                    this
                );

                enabled = false;
            }
        }

        private void Start()
        {
            if (!enabled)
                return;

            currentState = FlightAnimationState.FlightLoop;

            animator.SetFloat(
                FlightCycleSpeedHash,
                hoverAnimationSpeed
            );

            animator.Play(FlightLoopHash, 0, 0f);
        }


        private void Update()
        {
            if (!motor.isActiveAndEnabled ||
                !input.isActiveAndEnabled)
            {
                return;
            }

            UpdateFlightCycleSpeed();

            // Permite uma nova frenagem quando S for
            // solto e pressionado novamente.
            if (!input.BrakeHeld)
            {
                brakeActivatedForPress = false;
            }

            // Conta o tempo da animacao de frenagem.
            if (currentState == FlightAnimationState.Brake)
            {
                brakeAnimationTimer += Time.deltaTime;
            }

            FlightAnimationState targetState =
                DetermineAnimationState();

            if (targetState != currentState)
            {
                ChangeAnimation(targetState);
            }
        }


        private void UpdateFlightCycleSpeed()
        {
            bool wantsToFly =
                input.Move.y > 0.5f &&
                !input.BrakeHeld;

            bool isMoving =
                motor.ActualSpeed > hoverSpeedThreshold;

            bool isFlying = wantsToFly || isMoving;

            float targetSpeed = isFlying
                ? flyAnimationSpeed
                : hoverAnimationSpeed;

            // Continua atualizando o parametro mesmo
            // quando Boost ou Brake estiverem ativos.
            // Assim, FlightLoop recebe o valor adequado
            // quando voltar a ser reproduzido.
            animator.SetFloat(
                FlightCycleSpeedHash,
                targetSpeed,
                speedSmoothTime,
                Time.deltaTime
            );
        }


        private FlightAnimationState DetermineAnimationState()
        {
            bool wantsToFly =
                input.Move.y > 0.5f &&
                !input.BrakeHeld;

            // A animacao de freio ja comecou?
            if (currentState == FlightAnimationState.Brake)
            {
                // Se o jogador voltar a voar,
                // pode interromper a frenagem.
                if (!wantsToFly &&
                    brakeAnimationTimer < motor.Settings.BrakeFinishDuration)
                {
                    return FlightAnimationState.Brake;
                }
            }

            // Inicia a frenagem somente uma vez
            // por acionamento de S.
            if (motor.IsFinalBrakingPhase && input.BrakeHeld && !brakeActivatedForPress)
            {
                return FlightAnimationState.Brake;
            }

            // Boost somente durante o voo.
            if (wantsToFly && input.BoostHeld)
            {
                return FlightAnimationState.Boost;
            }

            return FlightAnimationState.FlightLoop;
        }


        private void ChangeAnimation(
            FlightAnimationState newState)
        {
            int stateHash;
            float transitionDuration;

            switch (newState)
            {
                case FlightAnimationState.Boost:
                    stateHash = BoostHash;
                    transitionDuration =
                        boostTransitionDuration;
                    break;


                case FlightAnimationState.Brake:
                    stateHash = BrakeHash;

                    transitionDuration =
                        brakeTransitionDuration;

                    // Reinicia o tempo somente quando
                    // uma nova frenagem for iniciada.
                    brakeAnimationTimer = 0f;
                    brakeActivatedForPress = true;

                    break;


                default:
                    stateHash = FlightLoopHash;
                    transitionDuration =
                        flightTransitionDuration;
                    break;
            }

            animator.CrossFadeInFixedTime(
                stateHash,
                transitionDuration,
                0
            );

            currentState = newState;
        }
    }
}
