using UnityEngine;
using UnityEngine.AI;

namespace Scripts.Gameplay.NPC
{
    // v0.9.5: adds independent local panic without changing the existing
    // global CityChaosManager.SetChaosActive(...) interface.
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class NpcNavigationProfile : MonoBehaviour
    {
        public enum AgentCategory
        {
            Pedestrian,
            Vehicle
        }

        [Header("City Navigation")]
        [SerializeField] private AgentCategory category = AgentCategory.Pedestrian;
        [SerializeField] private bool startInChaos;

        private NavMeshAgent agent;
        private NpcWanderAI wanderAI;
        private int normalMask;
        private int chaosMask;
        private bool isConfigured;
        private bool isChaosActive;
        private bool globalChaosActive;
        private bool localChaosActive;

        public AgentCategory Category => category;
        public bool IsConfigured => isConfigured;
        // Effective permissions (global OR local).
        public bool IsChaosActive => isChaosActive;
        public bool IsLocalChaosActive => localChaosActive;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            wanderAI = GetComponent<NpcWanderAI>();

            int sidewalk = NavMesh.GetAreaFromName("Sidewalk");
            int crosswalk = NavMesh.GetAreaFromName("Crosswalk");
            int road = NavMesh.GetAreaFromName("Road");

            if (sidewalk < 0 || crosswalk < 0 || road < 0)
            {
                Debug.LogError(
                    $"NpcNavigationProfile ({name}): create NavMesh Areas " +
                    "Sidewalk, Crosswalk and Road, then bake the NavMesh.", this);
                isConfigured = false;
                return;
            }

            int sidewalkMask = 1 << sidewalk;
            int crosswalkMask = 1 << crosswalk;
            int roadMask = 1 << road;

            normalMask = category == AgentCategory.Pedestrian
                ? sidewalkMask | crosswalkMask
                : roadMask | crosswalkMask;
            chaosMask = sidewalkMask | crosswalkMask | roadMask;
            isConfigured = true;

            globalChaosActive = startInChaos;
            localChaosActive = false;
            ApplyMask(requestNewPath: false, force: true);
        }

        // Existing CityChaosManager uses this as the GLOBAL switch.
        public void SetChaosActive(bool enabled)
        {
            if (!isConfigured || globalChaosActive == enabled)
                return;

            globalChaosActive = enabled;
            ApplyMask(requestNewPath: true, force: false);
        }

        // New: only this one NPC may use Road/Sidewalk/Crosswalk while panicked.
        public void SetLocalChaosActive(bool enabled)
        {
            if (!isConfigured || localChaosActive == enabled)
                return;

            localChaosActive = enabled;
            ApplyMask(requestNewPath: true, force: false);
        }

        private void ApplyMask(bool requestNewPath, bool force)
        {
            bool effectiveChaos = globalChaosActive || localChaosActive;
            if (!force && isChaosActive == effectiveChaos)
                return;

            isChaosActive = effectiveChaos;
            agent.areaMask = effectiveChaos ? chaosMask : normalMask;

            // The previous route may use areas that are forbidden now.
            if (requestNewPath && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                if (wanderAI != null && wanderAI.isActiveAndEnabled)
                    wanderAI.RefreshNavigationRules();
                else
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }
            }
        }
    }
}
