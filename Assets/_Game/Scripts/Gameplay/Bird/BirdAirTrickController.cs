using UnityEngine;
using Unity.Cinemachine;

namespace Scripts.Gameplay.Bird
{
    /// <summary>
    /// Double tap A/D: cartoon 360-degree visual barrel roll and a
    /// 90-degree Cinemachine orbit turn. Does not change Rigidbody physics.
    /// Designed for Cinemachine 3 Orbital Follow, just like v0.8.0.
    /// </summary>
    [DefaultExecutionOrder(150)]
    [RequireComponent(typeof(BirdInputReader))]
    [RequireComponent(typeof(BirdFlightMotor))]
    [RequireComponent(typeof(BirdLocomotionController))]
    [DisallowMultipleComponent]
    public sealed class BirdAirTrickController : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("Child of BirdVisualPivot and parent of BirdModel. Only this pivot rolls.")]
        [SerializeField] private Transform trickRollPivot;

        [Header("Cinemachine 3 - Orbital Follow")]
        [SerializeField] private CinemachineOrbitalFollow orbitalFollow;
        [Tooltip("Optional; if supplied, horizontal mouse orbit is paused during the trick.")]
        [SerializeField] private CinemachineInputAxisController cameraInputAxisController;
        [Tooltip("Keep the value that was correct in your working v0.8.0 setup.")]
        [SerializeField] private bool invertCameraYaw = true;

        [Header("Activation - preserved from v0.8.0")]
        [SerializeField, Range(0.12f, 0.5f)] private float doubleTapWindow = 0.28f;
        [SerializeField, Range(0.3f, 12f)] private float minimumFlightSpeed = 4f;
        [SerializeField] private bool requireForwardInput = true;
        [SerializeField, Min(0f)] private float cooldown = 0.35f;

        [Header("Trick - same duration and final heading")]
        [SerializeField, Range(0.25f, 1.5f)] private float duration = 0.65f;
        [SerializeField, Range(0f, 180f)] private float turnDegrees = 90f;
        [SerializeField, Range(0f, 720f)] private float rollDegrees = 360f;

        [Header("Cartoon Timing")]
        [Tooltip("Fraction spent preparing the roll in the opposite direction.")]
        [SerializeField, Range(0.02f, 0.25f)] private float windupFraction = 0.10f;
        [Tooltip("The roll and camera finish the exaggerated phase at this fraction.")]
        [SerializeField, Range(0.65f, 0.95f)] private float settleStartFraction = 0.84f;
        [Tooltip("Small opposite-direction wing/body roll just before the full spin.")]
        [SerializeField, Range(0f, 30f)] private float rollAnticipationDegrees = 12f;
        [Tooltip("How far the visual roll goes beyond 360 before settling back.")]
        [SerializeField, Range(0f, 35f)] private float rollOvershootDegrees = 14f;
        [Tooltip("Small camera overshoot. Final camera direction remains exactly Turn Degrees.")]
        [SerializeField, Range(0f, 12f)] private float cameraOvershootDegrees = 4f;

        [Header("Cartoon Squash & Stretch (visual-only)")]
        [SerializeField] private bool enableSquashStretch = true;
        [SerializeField, Range(0f, 0.12f)] private float squashStretchStrength = 0.035f;
        [SerializeField] private bool logTricks = true;

        private BirdInputReader input;
        private BirdFlightMotor motor;
        private BirdLocomotionController locomotion;

        private Quaternion baseRollRotation;
        private Vector3 baseRollScale;

        private int previousHorizontalSide;
        private int lastTapSide;
        private float lastTapTime = float.NegativeInfinity;
        private float nextAllowedTime;

        private bool isTricking;
        private int trickSide;
        private float trickElapsed;
        private float yawAtStart;

        private InputAxisControllerBase<CinemachineInputAxisController.Reader>.Controller horizontalInput;
        private bool horizontalInputWasEnabled;

        public bool IsTricking => isTricking;

        private void Awake()
        {
            input = GetComponent<BirdInputReader>();
            motor = GetComponent<BirdFlightMotor>();
            locomotion = GetComponent<BirdLocomotionController>();

            if (trickRollPivot == null || orbitalFollow == null)
            {
                Debug.LogError(
                    "BirdAirTrickController: assign Trick Roll Pivot and Cinemachine Orbital Follow.",
                    this);
                enabled = false;
                return;
            }

            baseRollRotation = trickRollPivot.localRotation;
            baseRollScale = trickRollPivot.localScale;

            if (cameraInputAxisController == null)
                cameraInputAxisController = orbitalFollow.GetComponent<CinemachineInputAxisController>();

            if (!orbitalFollow.HorizontalAxis.Wrap)
                Debug.LogWarning(
                    "BirdAirTrickController: enable Wrap on Orbital Follow Horizontal Axis.",
                    this);
        }

        private void OnValidate()
        {
            // Prevent the main animation interval from collapsing in Inspector.
            settleStartFraction = Mathf.Max(settleStartFraction, windupFraction + 0.1f);
        }

        private void Update()
        {
            int side = GetHorizontalSide(input.Move.x);

            if (side != 0 && side != previousHorizontalSide)
                RegisterSidePress(side);

            previousHorizontalSide = side;

            if (!isTricking)
                return;

            if (locomotion.State != BirdLocomotionState.Flying)
            {
                FinishTrick();
                return;
            }

            trickElapsed += Time.deltaTime;
        }

        private void LateUpdate()
        {
            if (!isTricking)
                return;

            float t = Mathf.Clamp01(trickElapsed / Mathf.Max(0.001f, duration));

            float rollAngle = CalculateRollAngle(t);
            float turnAngle = CalculateTurnAngle(t);

            // The camera remains the authority for steering; the existing
            // BirdFlightMotor keeps following camera.forward as before.
            float cameraDirection = invertCameraYaw ? -trickSide : trickSide;
            float desiredYaw = yawAtStart + cameraDirection * turnAngle;
            orbitalFollow.HorizontalAxis.Value =
                orbitalFollow.HorizontalAxis.ClampValue(desiredYaw);

            // An explicit growing angle guarantees a full 360-degree spin.
            trickRollPivot.localRotation = baseRollRotation *
                Quaternion.AngleAxis(-trickSide * rollAngle, Vector3.forward);

            if (enableSquashStretch)
            {
                // Gentle visual-only exaggeration; no effect on physics.
                float pulse = Mathf.Sin(Mathf.PI * t);
                float s = squashStretchStrength * pulse;
                trickRollPivot.localScale = Vector3.Scale(
                    baseRollScale,
                    new Vector3(1f + s, 1f - s * 0.7f, 1f + s * 0.35f));
            }

            if (t >= 1f)
                FinishTrick();
        }

        private float CalculateRollAngle(float t)
        {
            if (t < windupFraction)
            {
                float p = EaseSmoothStep(t / windupFraction);
                return -rollAnticipationDegrees * p;
            }

            if (t < settleStartFraction)
            {
                float p = (t - windupFraction) /
                          (settleStartFraction - windupFraction);
                // Fast and expressive but gently eased at both ends.
                float eased = EaseInOutCubic(p);
                return Mathf.LerpUnclamped(
                    -rollAnticipationDegrees,
                    rollDegrees + rollOvershootDegrees,
                    eased);
            }

            float settle = (t - settleStartFraction) /
                           (1f - settleStartFraction);
            return Mathf.LerpUnclamped(
                rollDegrees + rollOvershootDegrees,
                rollDegrees,
                EaseSmoothStep(settle));
        }

        private float CalculateTurnAngle(float t)
        {
            // Keep the camera still during the tiny windup.
            if (t <= windupFraction)
                return 0f;

            if (t < settleStartFraction)
            {
                float p = (t - windupFraction) /
                          (settleStartFraction - windupFraction);
                // Responsive start, softer approach to the corner.
                float eased = EaseOutCubic(p);
                return (turnDegrees + cameraOvershootDegrees) * eased;
            }

            float settle = (t - settleStartFraction) /
                           (1f - settleStartFraction);
            return Mathf.LerpUnclamped(
                turnDegrees + cameraOvershootDegrees,
                turnDegrees,
                EaseSmoothStep(settle));
        }

        private static float EaseSmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private void RegisterSidePress(int side)
        {
            if (isTricking || Time.time < nextAllowedTime)
                return;

            bool isSecondTap =
                lastTapSide == side && Time.time - lastTapTime <= doubleTapWindow;

            if (isSecondTap && CanStartTrick())
            {
                lastTapSide = 0;
                lastTapTime = float.NegativeInfinity;
                BeginTrick(side);
                return;
            }

            lastTapSide = side;
            lastTapTime = Time.time;
        }

        private bool CanStartTrick()
        {
            if (locomotion.State != BirdLocomotionState.Flying)
                return false;

            if (input.BrakeHeld || motor.ActualSpeed < minimumFlightSpeed)
                return false;

            if (requireForwardInput && input.Move.y <= 0.5f)
                return false;

            return true;
        }

        private void BeginTrick(int side)
        {
            isTricking = true;
            trickSide = side;
            trickElapsed = 0f;
            yawAtStart = orbitalFollow.HorizontalAxis.Value;

            horizontalInput = FindHorizontalInput();
            if (horizontalInput != null)
            {
                horizontalInputWasEnabled = horizontalInput.Enabled;
                horizontalInput.Enabled = false;
            }

            if (logTricks)
                Debug.Log(side < 0
                    ? "[BIRD TRICK] Cartoon left roll + 90 turn"
                    : "[BIRD TRICK] Cartoon right roll + 90 turn", this);
        }

        private InputAxisControllerBase<CinemachineInputAxisController.Reader>.Controller
            FindHorizontalInput()
        {
            if (cameraInputAxisController == null)
                return null;

            foreach (var controller in cameraInputAxisController.Controllers)
            {
                if (controller.Owner != orbitalFollow)
                    continue;

                string name = controller.Name ?? string.Empty;
                if (name.Contains("Orbit X") || name.Contains("Horizontal"))
                    return controller;
            }

            return null;
        }

        private void FinishTrick()
        {
            if (!isTricking)
                return;

            isTricking = false;
            trickRollPivot.localRotation = baseRollRotation;
            trickRollPivot.localScale = baseRollScale;

            if (horizontalInput != null)
            {
                horizontalInput.Enabled = horizontalInputWasEnabled;
                horizontalInput = null;
            }

            nextAllowedTime = Time.time + cooldown;
        }

        private void OnDisable()
        {
            FinishTrick();
            previousHorizontalSide = 0;
            lastTapSide = 0;
            lastTapTime = float.NegativeInfinity;
        }

        private static int GetHorizontalSide(float horizontal)
        {
            if (horizontal <= -0.5f) return -1;
            if (horizontal >= 0.5f) return 1;
            return 0;
        }
    }
}
