
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

        [Tooltip("Velocidade de reproducao das asas durante o voo.")]
        [SerializeField, Min(0f)]
        private float flyAnimationSpeed = 1.25f;

        [Header("Transitions")]

        [Tooltip(
            "Tempo de suavizacao da velocidade das asas, em segundos."
        )]
        [SerializeField, Min(0f)]
        private float speedSmoothTime = 0.25f;

        [Tooltip(
            "Velocidade fisica abaixo da qual o passaro " +
            "pode entrar no estado de hover."
        )]
        [SerializeField, Min(0f)]
        private float hoverSpeedThreshold = 0.35f;

        private BirdFlightMotor motor;
        private BirdInputReader input;

        private static readonly int FlightLoopHash =
            Animator.StringToHash("Base Layer.FlightLoop");

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

            if (!animator.HasState(0, FlightLoopHash))
            {
                Debug.LogError(
                    "BirdFlightAnimation: estado FlightLoop nao encontrado.",
                    this
                );

                enabled = false;
            }
        }

        private void Start()
        {
            if (!enabled)
                return;

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

            bool wantsToFly = input.Move.y > 0.5f;

            bool isMoving =
                motor.ActualSpeed > hoverSpeedThreshold;

            bool isFlying = wantsToFly || isMoving;

            float targetAnimationSpeed = isFlying
                ? flyAnimationSpeed
                : hoverAnimationSpeed;

            // Altera suavemente a velocidade das asas
            // sem reiniciar a animacao.
            animator.SetFloat(
                FlightCycleSpeedHash,
                targetAnimationSpeed,
                speedSmoothTime,
                Time.deltaTime
            );
        }
    }
}
