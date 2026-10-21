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
        private NavMeshPath path;
        private Vector3 homePosition;
        private float nextWalkTime;
        private float moveDeadline;
        private bool isWalking;

        public bool IsWalking => isWalking;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            path = new NavMeshPath();
        }

        private void Start()
        {
            homePosition = transform.position;

            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning(
                    $"NpcWanderAI: {name} nao esta sobre uma NavMesh. " +
                    "Posicione o NPC na area azul e refaca o Bake se necessario.", this);
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

            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
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
            for (int i = 0; i < destinationAttempts; i++)
            {
                Vector2 offset = Random.insideUnitCircle * wanderRadius;
                Vector3 candidate = homePosition +
                    new Vector3(offset.x, 0f, offset.y);

                if (!NavMesh.SamplePosition(
                        candidate, out NavMeshHit hit,
                        sampleDistance, agent.areaMask))
                    continue;

                if (Vector3.Distance(transform.position, hit.position) <
                    minimumTravelDistance)
                    continue;

                // Evita escolher um telhado ou outro andar proximo
                // da area de passeio deste NPC.
                if (Mathf.Abs(hit.position.y - homePosition.y) > 1.5f)
                    continue;

                if (!agent.CalculatePath(hit.position, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
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
