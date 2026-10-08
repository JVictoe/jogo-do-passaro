
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
            bool isBraking = motor.IsBraking;

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


        private void ApplyVisualRotation(float deltaTime)
        {
            // Orientacao tridimensional do voo.
            Quaternion flightRotation =
                motor.FlightRotation;

            // Converte para o espaco local de Bird.
            Quaternion localFlightRotation =
                Quaternion.Inverse(transform.rotation) *
                flightRotation;

            // Aplica a inclinacao visual.

            Quaternion brakeAndBankRotation =
                Quaternion.Euler(
                    currentBrakePitch,
                    0f,
                    currentBankAngle
                );

            Quaternion targetRotation =
                localFlightRotation * brakeAndBankRotation;


            float smoothing = 1f - Mathf.Exp(
                -settings.VisualSmoothing * deltaTime
            );

            birdVisualPivot.localRotation =
                Quaternion.Slerp(
                    birdVisualPivot.localRotation,
                    targetRotation,
                    smoothing
                );
        }
    }
}
