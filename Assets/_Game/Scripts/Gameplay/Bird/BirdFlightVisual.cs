
using UnityEngine;

namespace Scripts.Gameplay.Bird
{
    [RequireComponent(typeof(BirdFlightMotor))]
    [RequireComponent(typeof(BirdInputReader))]
    [DisallowMultipleComponent]
    public sealed class BirdFlightVisual : MonoBehaviour
    {
        [Header("Visual References")]
        [SerializeField]
        private Transform birdVisualPivot;

        private BirdFlightMotor motor;
        private BirdInputReader input;
        private BirdFlightSettings settings;
        private float currentBrakePitch;
        private float currentBankAngle;
        private float bankAngularVelocity;
        private const float HoverSpeedThreshold = 0.35f;
        public float CurrentBankAngle => currentBankAngle;

        private void Awake()
        {
            motor = GetComponent<BirdFlightMotor>();
            input = GetComponent<BirdInputReader>();

            settings = motor.Settings;

            if (settings == null ||
                birdVisualPivot == null ||
                birdVisualPivot.parent != transform)
            {
                Debug.LogError(
                    "BirdFlightVisual: referencias invalidas.",
                    this
                );

                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            currentBankAngle = 0f;
            bankAngularVelocity = 0f;
            currentBrakePitch = 0f;
        }


        private void LateUpdate()
        {
            if (!motor.isActiveAndEnabled)
                return;

            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
                return;

            float targetBank = CalculateTargetBank();

            UpdateBankAngle(targetBank, deltaTime);

            UpdateBrakeTilt(deltaTime);

            ApplyVisualRotation(deltaTime);
        }

        private Quaternion GetVisualFlightRotation()
        {
            Quaternion flightRotation = motor.FlightRotation;

            bool noForwardMovementInput =
                Mathf.Abs(input.Move.y) < 0.1f;

            bool isPracticallyStopped =
                motor.ActualSpeed <= HoverSpeedThreshold;

            bool isHovering =
                noForwardMovementInput && isPracticallyStopped;

            // Durante o voo, mantemos a orientacao 3D completa.
            if (!isHovering)
                return flightRotation;

            // Durante o hover, removemos a inclinacao vertical.
            // Mantemos apenas a direcao horizontal.
            Vector3 horizontalForward = Vector3.ProjectOnPlane(
                flightRotation * Vector3.forward,
                Vector3.up
            );

            // Evita problemas quando a camera aponta
            // praticamente na vertical.
            if (horizontalForward.sqrMagnitude < 0.0001f)
            {
                horizontalForward = Vector3.ProjectOnPlane(
                    birdVisualPivot.forward,
                    Vector3.up
                );
            }

            if (horizontalForward.sqrMagnitude < 0.0001f)
            {
                horizontalForward = Vector3.forward;
            }

            return Quaternion.LookRotation(
                horizontalForward.normalized,
                Vector3.up
            );
        }

        private float CalculateTargetBank()
        {
            // Curva detectada pelo motor.
            float turnRate = motor.CurrentTurnRate;

            // Velocidade real do Rigidbody.
            float currentSpeed = motor.ActualSpeed;

            float speedNormalized = Mathf.InverseLerp(
                settings.MinSpeed,
                settings.MaxSpeed,
                currentSpeed
            );

            // Curvas ficam visualmente mais intensas
            // conforme a velocidade aumenta.
            float speedMultiplier = Mathf.Lerp(
                1f - settings.BankSpeedInfluence,
                1f + settings.BankSpeedInfluence,
                speedNormalized
            );

            float turnBank =
                -turnRate *
                settings.TurnBankFactor *
                speedMultiplier;

            // A/D complementam a inclinacao.
            float keyboardBank =
                -input.Move.x *
                settings.KeyboardBankAngle;

            float targetBank = Mathf.Clamp(
                turnBank + keyboardBank,
                -settings.MaxBankAngle,
                settings.MaxBankAngle
            );

            return targetBank;
        }

        private void UpdateBankAngle(
            float targetBank,
            float deltaTime)
        {
            bool changingDirection =
                Mathf.Abs(targetBank) > 0.01f &&
                Mathf.Sign(targetBank) !=
                Mathf.Sign(currentBankAngle);

            bool enteringBank =
                Mathf.Abs(targetBank) >
                Mathf.Abs(currentBankAngle) ||
                changingDirection;

            float smoothTime = enteringBank
                ? settings.BankEnterSmoothTime
                : settings.BankExitSmoothTime;

            currentBankAngle = Mathf.SmoothDampAngle(
                currentBankAngle,
                targetBank,
                ref bankAngularVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime
            );

            currentBankAngle = Mathf.Clamp(
                currentBankAngle,
                -settings.MaxBankAngle,
                settings.MaxBankAngle
            );
        }


        private void UpdateBrakeTilt(float deltaTime)
        {
            bool isBraking = motor.IsBraking || (input.BrakeHeld && motor.IsFinalBrakingPhase);

            // Angulo negativo levanta o nariz do passaro.
            float targetPitch = isBraking
                ? -settings.BrakePitchAngle
                : 0f;

            float response = isBraking
                ? settings.BrakeTiltEnterResponse
                : settings.BrakeTiltExitResponse;

            float smoothing = 1f - Mathf.Exp(
                -response * deltaTime
            );

            currentBrakePitch = Mathf.Lerp(
                currentBrakePitch,
                targetPitch,
                smoothing
            );
        }



        private void ApplyVisualRotation(float dt)
        {
            // Obtem a orientacao visual apropriada:
            // completa durante o voo e nivelada no hover.
            Quaternion visualFlightRotation =
                GetVisualFlightRotation();

            // Converte a orientacao para o espaco local
            // do objeto Bird.
            Quaternion localFlightRotation =
                Quaternion.Inverse(transform.rotation) *
                visualFlightRotation;

            // Mantem a inclinacao de frenagem
            // e o banking lateral.
            Quaternion brakeAndBankRotation =
                Quaternion.Euler(
                    currentBrakePitch,
                    0f,
                    currentBankAngle
                );

            Quaternion targetRotation =
                localFlightRotation * brakeAndBankRotation;

            // Mantem nossa suavizacao visual.
            float smoothing = 1f - Mathf.Exp(
                -settings.VisualSmoothing * dt
            );

            birdVisualPivot.localRotation = Quaternion.Slerp(
                birdVisualPivot.localRotation,
                targetRotation,
                smoothing
            );
        }

    }
}
