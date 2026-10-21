using UnityEngine;
using UnityEngine.InputSystem;

namespace Scripts.Gameplay.Bird
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BirdPoopShooter : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference poopAction;

        [Header("Spawn")]
        [SerializeField] private Transform poopSpawnPoint;
        [SerializeField] private BirdPoopProjectile projectilePrefab;

        [Header("Flight Rules")]
        [SerializeField] private bool onlyWhileFlying = true;
        [SerializeField, Min(0f)] private float cooldown = 0.35f;
        [SerializeField, Range(0f, 1.5f)] private float inheritedVelocity = 0.9f;
        [SerializeField, Min(0f)] private float initialDownwardSpeed = 2f;

        private Rigidbody birdRigidbody;
        private BirdLocomotionController locomotion;
        private Collider[] birdColliders;
        private bool poopQueued;
        private float nextAllowedDrop;

        private void Awake()
        {
            birdRigidbody = GetComponent<Rigidbody>();
            locomotion = GetComponent<BirdLocomotionController>();
            birdColliders = GetComponentsInChildren<Collider>(true);

            if (poopSpawnPoint == null || projectilePrefab == null ||
                poopAction == null || poopAction.action == null)
            {
                Debug.LogError("BirdPoopShooter: configure Spawn Point, Projectile Prefab e Poop Action.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (poopAction == null || poopAction.action == null)
                return;

            poopQueued = false;
            poopAction.action.performed += OnPoopPerformed;
            poopAction.action.Enable();
        }

        private void OnDisable()
        {
            if (poopAction != null && poopAction.action != null)
            {
                poopAction.action.performed -= OnPoopPerformed;
                poopAction.action.Disable();
            }

            poopQueued = false;
        }

        private void OnPoopPerformed(InputAction.CallbackContext context)
        {
            poopQueued = true;
        }

        private void FixedUpdate()
        {
            if (!poopQueued)
                return;

            // Consumir o comando mesmo que esteja em cooldown / fora do estado Flying.
            poopQueued = false;

            if (onlyWhileFlying && locomotion != null &&
                locomotion.State != BirdLocomotionState.Flying)
                return;

            if (Time.fixedTime < nextAllowedDrop)
                return;

            nextAllowedDrop = Time.fixedTime + cooldown;

            BirdPoopProjectile projectile = Instantiate(
                projectilePrefab,
                poopSpawnPoint.position,
                Quaternion.identity
            );

            Vector3 initialVelocity =
                birdRigidbody.linearVelocity * inheritedVelocity +
                Vector3.down * initialDownwardSpeed;

            projectile.Initialize(initialVelocity, birdColliders);
        }
    }
}
