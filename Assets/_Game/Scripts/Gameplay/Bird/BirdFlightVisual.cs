
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
                    "BirdFlightVisual: configure um " +
                    "BirdVisualPivot filho direto de Bird.",
                    this
                );

                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (!motor.isActiveAndEnabled)
                return;

            UpdateVisualRotation();
        }

        private void UpdateVisualRotation()
        {
            Quaternion flightRotation =
                motor.FlightRotation;

            float turnRate =
                motor.CurrentTurnRate;

            float keyboardInput =
                input.Move.x;

            // Banking caused by flight direction changes.
            float turnBank =
                -turnRate * settings.TurnBankFactor;

            // Additional banking from A / D.
            float keyboardBank =
                -keyboardInput * settings.KeyboardBankAngle;

            float finalBank = Mathf.Clamp(
                turnBank + keyboardBank,
                -settings.MaxBankAngle,
                settings.MaxBankAngle
            );

            // Convert world flight rotation to local space.
            Quaternion localFlightRotation =
                Quaternion.Inverse(transform.rotation) *
                flightRotation;

            Quaternion bankRotation =
                Quaternion.Euler(0f, 0f, finalBank);

            Quaternion targetRotation =
                localFlightRotation * bankRotation;

            // Framerate-independent visual smoothing.
            float smoothing = 1f - Mathf.Exp(
                -settings.VisualSmoothing *
                Time.deltaTime
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
