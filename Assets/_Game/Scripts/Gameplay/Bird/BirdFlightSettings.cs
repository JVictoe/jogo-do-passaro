
using UnityEngine;
using UnityEngine.Serialization;

namespace Scripts.Gameplay.Bird
{
    [CreateAssetMenu(
        fileName = "BirdFlightSettings",
        menuName = "Bird/Flight Settings"
    )]

    public sealed class BirdFlightSettings : ScriptableObject
    {
        [Header("Speed")]

        [SerializeField, Min(0f)] private float minSpeed = 0f;
        [FormerlySerializedAs("startSpeed")]
        [SerializeField, Min(0f)] private float cruiseSpeed = 8f;
        [SerializeField, Min(0f)] private float maxSpeed = 18f;

        [SerializeField] private float acceleration = 7f;
        [SerializeField] private float braking = 10f;

        [Header("Active Braking")]

        [SerializeField, Min(0f)] private float emergencyBraking = 36f;

        [SerializeField, Min(1f)] private float brakeResponseMultiplier = 1.8f;

        [Header("Braking Visual")]

        [SerializeField, Range(0f, 50f)] private float brakePitchAngle = 30f;

        [SerializeField, Min(0f)] private float brakeTiltEnterResponse = 14f;

        [SerializeField, Min(0f)] private float brakeTiltExitResponse = 8f;

        [Header("Flight")]
        [SerializeField] private float turnSpeed = 180f;
        [SerializeField] private float velocityResponse = 6f;
        [SerializeField] private float maxFlightAcceleration = 80f;

        [Header("Visual Banking")]
        [SerializeField] private float maxBankAngle = 40f;
        [SerializeField] private float turnBankFactor = 0.25f;
        [SerializeField] private float keyboardBankAngle = 12f;
        [SerializeField] private float visualSmoothing = 10f;

        [Header("Banking Feel")]
        [SerializeField, Min(0.01f)] private float bankEnterSmoothTime = 0.12f;


        [Header("Turn Agility")]
        [SerializeField, Range(0.4f, 1.4f)] private float highSpeedTurnMultiplier = 1f;

        [SerializeField, Min(0.01f)] private float bankExitSmoothTime = 0.32f;

        [SerializeField, Range(0f, 1f)] private float bankSpeedInfluence = 0.35f;

        public float MinSpeed => minSpeed;
        public float CruiseSpeed => cruiseSpeed;
        public float MaxSpeed => maxSpeed;
        public float Acceleration => acceleration;
        public float Braking => braking;

        public float TurnSpeed => turnSpeed;
        public float VelocityResponse => velocityResponse;
        public float MaxFlightAcceleration => maxFlightAcceleration;

        public float MaxBankAngle => maxBankAngle;
        public float TurnBankFactor => turnBankFactor;
        public float KeyboardBankAngle => keyboardBankAngle;
        public float VisualSmoothing => visualSmoothing;

        public float BankEnterSmoothTime => bankEnterSmoothTime;
        public float BankExitSmoothTime => bankExitSmoothTime;
        public float BankSpeedInfluence => bankSpeedInfluence;
        public float HighSpeedTurnMultiplier => highSpeedTurnMultiplier;

        public float EmergencyBraking => emergencyBraking;

        public float BrakeResponseMultiplier =>
            brakeResponseMultiplier;

        public float BrakePitchAngle => brakePitchAngle;

        public float BrakeTiltEnterResponse =>
            brakeTiltEnterResponse;

        public float BrakeTiltExitResponse =>
            brakeTiltExitResponse;


        private void OnValidate()
        {
            minSpeed = Mathf.Max(0f, minSpeed);
            maxSpeed = Mathf.Max(minSpeed, maxSpeed);
            cruiseSpeed = Mathf.Clamp(cruiseSpeed, minSpeed, maxSpeed);

            acceleration = Mathf.Max(0f, acceleration);
            braking = Mathf.Max(0f, braking);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            velocityResponse = Mathf.Max(0.1f, velocityResponse);
            maxFlightAcceleration = Mathf.Max(0.1f, maxFlightAcceleration);

            emergencyBraking = Mathf.Max(braking,emergencyBraking);
            brakeResponseMultiplier = Mathf.Max( 1f, brakeResponseMultiplier);
            brakeTiltEnterResponse = Mathf.Max(0f,brakeTiltEnterResponse);
            brakeTiltExitResponse = Mathf.Max(0f,brakeTiltExitResponse);

            maxBankAngle = Mathf.Clamp(maxBankAngle, 0f, 80f);
            turnBankFactor = Mathf.Max(0f, turnBankFactor);
            keyboardBankAngle = Mathf.Max(0f, keyboardBankAngle);
            visualSmoothing = Mathf.Max(0f, visualSmoothing);

            bankEnterSmoothTime =Mathf.Max(0.01f, bankEnterSmoothTime);
            bankExitSmoothTime =Mathf.Max(0.01f, bankExitSmoothTime);
            bankSpeedInfluence = Mathf.Clamp01(bankSpeedInfluence);
        }
    }
}
