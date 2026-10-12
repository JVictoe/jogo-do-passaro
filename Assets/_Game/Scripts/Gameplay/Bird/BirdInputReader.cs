
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scripts.Gameplay.Bird
{
    [DisallowMultipleComponent]
    public sealed class BirdInputReader : MonoBehaviour
    {
        [Header("Input Actions")]
        [SerializeField]
        private InputActionReference moveAction;

        [SerializeField]
        private InputActionReference boostAction;

        [SerializeField]
        private InputActionReference takeoffLandAction;

        public Vector2 Move { get; private set; }

        public bool BoostHeld { get; private set; }

        public bool BrakeHeld => Move.y < -0.5f;

        // Armazena o pressionamento de Space ate
        // que a locomocao consuma o comando.
        private bool takeoffLandQueued;

        private void OnEnable()
        {
            if (moveAction?.action == null ||
                boostAction?.action == null ||
                takeoffLandAction?.action == null)
            {
                Debug.LogError(
                    "BirdInputReader: configure Move, " +
                    "Boost e TakeoffLand.",
                    this
                );

                enabled = false;
                return;
            }

            takeoffLandQueued = false;

            moveAction.action.Enable();
            boostAction.action.Enable();

            takeoffLandAction.action.performed +=
                OnTakeoffLandPerformed;

            takeoffLandAction.action.Enable();
        }

        private void Update()
        {
            Move = moveAction.action.ReadValue<Vector2>();

            BoostHeld = boostAction.action.IsPressed();
        }

        private void OnTakeoffLandPerformed(
            InputAction.CallbackContext context)
        {
            takeoffLandQueued = true;
        }

        // Sera chamado pelo controlador de locomocao
        // quando processarmos as transicoes fisicas.
        public bool ConsumeTakeoffLandPressed()
        {
            if (!takeoffLandQueued)
                return false;

            takeoffLandQueued = false;
            return true;
        }

        private void OnDisable()
        {
            if (takeoffLandAction?.action != null)
            {
                takeoffLandAction.action.performed -=
                    OnTakeoffLandPerformed;

                takeoffLandAction.action.Disable();
            }

            moveAction?.action?.Disable();
            boostAction?.action?.Disable();

            Move = Vector2.zero;
            BoostHeld = false;
            takeoffLandQueued = false;
        }
    }
}
