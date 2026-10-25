using UnityEngine;
using UnityEngine.AI;

namespace Scripts.Gameplay.NPC
{
    // Applied before wander scripts make their first navigation request.
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

        public AgentCategory Category => category;
        public bool IsConfigured => isConfigured;
        public bool IsChaosActive => isChaosActive;

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

            ApplyMask(startInChaos, false);
        }

        public void SetChaosActive(bool enabled)
        {
            if (!isConfigured)
                return;

            if (isChaosActive == enabled)
                return;

            ApplyMask(enabled, true);
        }

        private void ApplyMask(bool enabled, bool requestNewPath)
        {
            isChaosActive = enabled;
            agent.areaMask = enabled ? chaosMask : normalMask;

            // A mask change may invalidate the previous path.
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
