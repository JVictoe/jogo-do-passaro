
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


        [Header("Glide Animation")]

        [Tooltip(
            "Velocidade fisica minima para entrar no Glide, em unidades/s."
        )]
        [SerializeField, Min(0f)]
        private float glideEnterSpeed = 9.5f;

        [Tooltip(
            "Velocidade abaixo da qual o Glide termina, em unidades/s. " +
            "Deve ser menor que Glide Enter Speed."
        )]
        [SerializeField, Min(0f)]
        private float glideExitSpeed = 7.5f;

        [Tooltip(
    "Inclinacao minima de mergulho para entrar no Glide. " +
    "0.12 corresponde a aproximadamente 7 graus para baixo."
)]
        [SerializeField, Range(0f, 1f)]
        private float glideEnterDescent = 0.12f;

        [Tooltip(
            "Inclinacao minima para manter o Glide. " +
            "Valores negativos permitem uma pequena subida " +
            "sem interromper imediatamente a animacao."
        )]
        [SerializeField, Range(-1f, 1f)]
        private float glideExitDescent = -0.08f;

        [Tooltip(
            "Duracao da transicao de entrada ou saida do Glide, " +
            "em segundos."
        )]
        [SerializeField, Min(0f)]
        private float glideTransitionDuration = 0.35f;


        [Tooltip(
            "Tempo durante o qual o passaro continua planando " +
            "depois de iniciar uma subida, em segundos."
        )]
        [SerializeField, Min(0f)]
        private float glideClimbHoldDuration = 2f;

        // Evita considerar pequenas oscilacoes da camera
        // como uma subida intencional.
        private const float GlideClimbThreshold = 0.05f;

        private float glideClimbElapsed;
        private bool glideClimbStarted;


        private float brakeAnimationTimer;
        private bool brakeActivatedForPress;

        private BirdFlightMotor motor;
        private BirdInputReader input;

        private enum FlightAnimationState
        {
            FlightLoop,
            Boost,
            Glide,
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

        private static readonly int GlideHash =
            Animator.StringToHash("Base Layer.Glide");

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
                !animator.HasState(0, BrakeHash) || !animator.HasState(0, GlideHash))
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

            UpdateGlideClimbTimer(Time.deltaTime);

            FlightAnimationState targetState =
                DetermineAnimationState();

            if (targetState != currentState)
            {
                ChangeAnimation(targetState);
            }
        }


        private void UpdateGlideClimbTimer(float dt)
        {
            if (currentState != FlightAnimationState.Glide)
                return;

            Vector3 flightDirection =
                motor.FlightRotation * Vector3.forward;

            float verticalDirection = Vector3.Dot(
                flightDirection,
                Vector3.up
            );

            // Um novo mergulho significativo permite
            // preparar uma nova subida.
            if (verticalDirection <= -glideEnterDescent)
            {
                glideClimbStarted = false;
                glideClimbElapsed = 0f;
                return;
            }

            // Inicia a contagem quando o passaro
            // comeca realmente a subir.
            if (verticalDirection >= GlideClimbThreshold)
            {
                glideClimbStarted = true;
            }

            // Depois que a subida comecou, o tempo
            // continua passando mesmo se nivelar o voo.
            if (glideClimbStarted)
            {
                glideClimbElapsed += dt;
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



        private bool ShouldGlide(bool wantsToFly)
        {
            if (!wantsToFly)
                return false;

            float actualSpeed = motor.ActualSpeed;

            Vector3 flightDirection =
                motor.FlightRotation * Vector3.forward;

            float verticalDirection = Vector3.Dot(
                flightDirection,
                Vector3.up
            );

            bool isAlreadyGliding =
                currentState == FlightAnimationState.Glide;

            // Se o passaro iniciou uma subida enquanto
            // planava, conserva a animacao por alguns
            // segundos, mesmo com velocidade diminuindo.
            if (isAlreadyGliding && glideClimbStarted)
            {
                bool hasRemainingTime =
                    glideClimbElapsed < glideClimbHoldDuration;

                bool isStillMoving =
                    actualSpeed > hoverSpeedThreshold;

                return hasRemainingTime && isStillMoving;
            }

            // Histerese de velocidade.
            float requiredSpeed = isAlreadyGliding
                ? Mathf.Min(glideExitSpeed, glideEnterSpeed)
                : glideEnterSpeed;

            // Histerese de inclinacao.
            float requiredDescent = isAlreadyGliding
                ? Mathf.Min(glideExitDescent, glideEnterDescent)
                : glideEnterDescent;

            float descentAmount = -verticalDirection;

            bool hasEnoughSpeed =
                actualSpeed >= requiredSpeed;

            bool hasCorrectInclination =
                descentAmount >= requiredDescent;

            return hasEnoughSpeed && hasCorrectInclination;
        }




        private FlightAnimationState DetermineAnimationState()
        {
            bool wantsToFly =
                input.Move.y > 0.5f &&
                !input.BrakeHeld;

            // 1. Frenagem em andamento.
            // Permite que a animacao continue ate sua
            // duracao terminar ou o jogador retomar o voo.
            if (currentState == FlightAnimationState.Brake)
            {
                if (!wantsToFly &&
                    brakeAnimationTimer <
                    motor.Settings.BrakeFinishDuration)
                {
                    return FlightAnimationState.Brake;
                }
            }

            // 2. Nova frenagem final.
            if (motor.IsFinalBrakingPhase && motor.IsEmergencyBraking && !brakeActivatedForPress)
            {
                return FlightAnimationState.Brake;
            }

            // 3. Boost tem prioridade sobre Glide.
            if (wantsToFly && input.BoostHeld)
            {
                return FlightAnimationState.Boost;
            }

            // 4. Glide durante mergulhos rapidos.
            if (ShouldGlide(wantsToFly))
            {
                return FlightAnimationState.Glide;
            }

            // 5. Hover e voo normal.
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

                    // Transicao mais suave quando
                    // estamos saindo da pose de planar.
                    transitionDuration =
                        currentState == FlightAnimationState.Glide
                            ? glideTransitionDuration
                            : boostTransitionDuration;

                    break;

                case FlightAnimationState.Brake:

                    stateHash = BrakeHash;
                    transitionDuration =
                        brakeTransitionDuration;

                    brakeAnimationTimer = 0f;
                    brakeActivatedForPress = true;

                    break;

                case FlightAnimationState.Glide:

                    stateHash = GlideHash;
                    transitionDuration =
                        glideTransitionDuration;

                    break;

                default:

                    stateHash = FlightLoopHash;

                    // Preserva a transicao especial
                    // ao sair da animacao Glide.
                    transitionDuration =
                        currentState == FlightAnimationState.Glide
                            ? glideTransitionDuration
                            : flightTransitionDuration;

                    break;
            }

            animator.CrossFadeInFixedTime(
                stateHash,
                transitionDuration,
                0
            );

            if (newState == FlightAnimationState.Glide || currentState == FlightAnimationState.Glide)
            {
                glideClimbElapsed = 0f;
                glideClimbStarted = false;
            }

            currentState = newState;
        }

    }
}
