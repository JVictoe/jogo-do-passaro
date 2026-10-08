
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

        public Vector2 Move { get; private set; }

        public bool BoostHeld { get; private set; }

        public bool BrakeHeld => Move.y < -0.5f;

        private void OnEnable()
        {
            if (moveAction?.action == null ||
                boostAction?.action == null)
            {
                Debug.LogError(
                    "BirdInputReader: Input Actions ausentes.",
                    this
                );

                enabled = false;
                return;
            }

            moveAction.action.Enable();
            boostAction.action.Enable();
        }

        private void Update()
        {
            Move = moveAction.action.ReadValue<Vector2>();

            BoostHeld = boostAction.action.IsPressed();
        }

        private void OnDisable()
        {
            moveAction?.action?.Disable();
            boostAction?.action?.Disable();

            Move = Vector2.zero;
            BoostHeld = false;
        }
    }
}
