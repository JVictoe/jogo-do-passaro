
using UnityEngine;

namespace Scripts.Gameplay.Bird
{
    [CreateAssetMenu(
        fileName = "BirdFlightSettings",
        menuName = "Bird/Flight Settings"
    )]
    public sealed class BirdFlightSettings : ScriptableObject
    {
        [Header("Speed")]
        [SerializeField] private float minSpeed = 5f;
        [SerializeField] private float startSpeed = 8f;
        [SerializeField] private float maxSpeed = 18f;

        [SerializeField] private float acceleration = 7f;
        [SerializeField] private float braking = 10f;

        [Header("Flight")]
        [SerializeField] private float turnSpeed = 180f;
        [SerializeField] private float velocityResponse = 6f;
        [SerializeField] private float maxFlightAcceleration = 80f;

        [Header("Visual Banking")]
        [SerializeField] private float maxBankAngle = 40f;
        [SerializeField] private float turnBankFactor = 0.25f;
        [SerializeField] private float keyboardBankAngle = 12f;
        [SerializeField] private float visualSmoothing = 10f;

        public float MinSpeed => minSpeed;
        public float StartSpeed => startSpeed;
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

        private void OnValidate()
        {
            minSpeed = Mathf.Max(0.1f, minSpeed);
            maxSpeed = Mathf.Max(minSpeed, maxSpeed);
            startSpeed = Mathf.Clamp(startSpeed, minSpeed, maxSpeed);

            acceleration = Mathf.Max(0f, acceleration);
            braking = Mathf.Max(0f, braking);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            velocityResponse = Mathf.Max(0.1f, velocityResponse);
            maxFlightAcceleration = Mathf.Max(0.1f, maxFlightAcceleration);

            maxBankAngle = Mathf.Clamp(maxBankAngle, 0f, 80f);
            turnBankFactor = Mathf.Max(0f, turnBankFactor);
            keyboardBankAngle = Mathf.Max(0f, keyboardBankAngle);
            visualSmoothing = Mathf.Max(0f, visualSmoothing);
        }
    }
}
