using UnityEngine;

namespace Scripts.Gameplay.NPC
{
    [DisallowMultipleComponent]
    public sealed class CityChaosManager : MonoBehaviour
    {
        [Header("Test configuration")]
        [SerializeField] private bool startInChaos;
        [SerializeField] private bool logChanges = true;

        public bool IsChaosActive { get; private set; }

        private void Start()
        {
            SetChaosActive(startInChaos);
        }

        // Integrate this public method with bird events in a later version.
        public void SetChaosActive(bool enabled)
        {
            IsChaosActive = enabled;
            RefreshProfiles();

            if (logChanges)
                Debug.Log($"[CITY CHAOS] {(enabled ? "ACTIVE" : "NORMAL")}", this);
        }

        // Call again if NPCs have been spawned at runtime.
        public void RefreshProfiles()
        {
            NpcNavigationProfile[] profiles =
                FindObjectsByType<NpcNavigationProfile>();

            foreach (NpcNavigationProfile profile in profiles)
                profile.SetChaosActive(IsChaosActive);
        }

        [ContextMenu("TEST - Enable Chaos (Play Mode)")]
        private void DebugEnableChaos()
        {
            if (Application.isPlaying)
                SetChaosActive(true);
        }

        [ContextMenu("TEST - Disable Chaos (Play Mode)")]
        private void DebugDisableChaos()
        {
            if (Application.isPlaying)
                SetChaosActive(false);
        }
    }
}
