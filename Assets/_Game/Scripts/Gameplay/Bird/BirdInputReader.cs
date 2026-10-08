
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scripts.Gameplay.Bird
{
    [DisallowMultipleComponent]
    public sealed class BirdInputReader : MonoBehaviour
    {
        [SerializeField]
        private InputActionReference moveAction;

        public Vector2 Move { get; private set; }

        private void OnEnable()
        {
            if (moveAction?.action == null)
            {
                Debug.LogError(
                    "BirdInputReader: Move Action nao configurada.",
                    this
                );

                enabled = false;
                return;
            }

            moveAction.action.Enable();
        }

        private void Update()
        {
            Move = moveAction.action.ReadValue<Vector2>();
        }

        private void OnDisable()
        {
            moveAction?.action?.Disable();
            Move = Vector2.zero;
        }
    }
}
