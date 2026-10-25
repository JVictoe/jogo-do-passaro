using UnityEngine;
using UnityEngine.AI;

namespace Scripts.Gameplay.NPC
{
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class NpcWanderAI : MonoBehaviour
    {
        [Header("Area de passeio")]
        [SerializeField, Min(1f)] private float wanderRadius = 8f;
        [SerializeField, Min(0.25f)] private float minimumTravelDistance = 1.5f;
        [SerializeField, Range(1, 20)] private int destinationAttempts = 10;
        [SerializeField, Min(0.1f)] private float sampleDistance = 1.5f;

        [Header("Pausas")]
        [SerializeField, Min(0f)] private float minimumIdleSeconds = 1.5f;
        [SerializeField, Min(0f)] private float maximumIdleSeconds = 4f;

        [Header("Seguranca")]
        [SerializeField, Min(1f)] private float movementTimeout = 12f;
        [SerializeField, Min(0.01f)] private float arrivalTolerance = 0.2f;
        [SerializeField] private bool showGizmos = true;

        private NavMeshAgent agent;
        private NpcNavigationProfile navigationProfile;
        private NavMeshPath candidatePath;
        private Vector3 homePosition;
        private float nextWalkTime;
        private float moveDeadline;
        private bool isWalking;

        public bool IsWalking => isWalking;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            navigationProfile = GetComponent<NpcNavigationProfile>();
            // Unity native NavMeshPath must be created in Awake, not in field initialization.
            candidatePath = new NavMeshPath();
        }

        private void Start()
        {
            homePosition = transform.position;

            if (navigationProfile != null && !navigationProfile.IsConfigured)
            {
                Debug.LogError($"NpcWanderAI ({name}): navigation areas are not configured.", this);
                enabled = false;
                return;
            }

            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning(
                    $"NpcWanderAI: {name} nao esta sobre uma NavMesh. " +
                    "Posicione o NPC sobre sua area e refaca o Bake se necessario.", this);
                enabled = false;
                return;
            }

            SchedulePause();
        }

        private void Update()
        {
            if (!agent.isOnNavMesh)
                return;

            if (!isWalking)
            {
                if (Time.time >= nextWalkTime && !TryStartWandering())
                    SchedulePause();
                return;
            }

            if (Time.time >= moveDeadline)
            {
                StopAndPause();
                return;
            }

            if (agent.pathPending)
                return;

            if (agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                StopAndPause();
                return;
            }

            bool nearDestination = agent.remainingDistance <=
                agent.stoppingDistance + arrivalTolerance;
            bool nearlyStopped = agent.velocity.sqrMagnitude <= 0.04f;

            if (nearDestination && nearlyStopped)
                StopAndPause();
        }

        private bool TryStartWandering()
        {
            // Explicit filter makes sampling respect both the agent type and the
            // active area mask (important when cars get a second NavMeshSurface).
            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };

            for (int i = 0; i < destinationAttempts; i++)
            {
                Vector2 offset = Random.insideUnitCircle * wanderRadius;
                Vector3 candidate = homePosition +
                    new Vector3(offset.x, 0f, offset.y);

                if (!NavMesh.SamplePosition(
                        candidate, out NavMeshHit hit,
                        sampleDistance, filter))
                    continue;

                if (Vector3.Distance(transform.position, hit.position) <
                    minimumTravelDistance)
                    continue;

                if (Mathf.Abs(hit.position.y - homePosition.y) > 1.5f)
                    continue;

                if (!agent.CalculatePath(hit.position, candidatePath) ||
                    candidatePath.status != NavMeshPathStatus.PathComplete)
                    continue;

                agent.isStopped = false;
                if (!agent.SetDestination(hit.position))
                    continue;

                isWalking = true;
                moveDeadline = Time.time + movementTimeout;
                return true;
            }

            return false;
        }

        // v0.9.5.1: explicit handoff after the reactions controller finishes.
        // Reanchors the wander area near the sidewalk where the NPC recovered,
        // so a long panic run does not leave every sampled destination far away.
        public void ResumeAfterReaction()
        {
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            {
                Debug.LogWarning($"NpcWanderAI ({name}): cannot resume outside the NavMesh.", this);
                return;
            }

            homePosition = agent.nextPosition;
            isWalking = false;
            agent.isStopped = true;
            agent.ResetPath();
            nextWalkTime = Time.time + 0.35f;
        }

        // Called on Normal/Chaos changes so a restricted agent does not
        // keep following an old path across a forbidden area.
        public void RefreshNavigationRules()
        {
            isWalking = false;

            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            nextWalkTime = Time.time + 0.25f;
        }

        private void StopAndPause()
        {
            agent.isStopped = true;
            agent.ResetPath();
            isWalking = false;
            SchedulePause();
        }

        private void SchedulePause()
        {
            float min = Mathf.Min(minimumIdleSeconds, maximumIdleSeconds);
            float max = Mathf.Max(minimumIdleSeconds, maximumIdleSeconds);
            nextWalkTime = Time.time + Random.Range(min, max);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos)
                return;

            Gizmos.color = Color.cyan;
            Vector3 center = Application.isPlaying ? homePosition : transform.position;
            Gizmos.DrawWireSphere(center, wanderRadius);
        }
    }
}
