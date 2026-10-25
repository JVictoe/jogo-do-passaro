using UnityEngine;
using UnityEngine.AI;

namespace Scripts.Gameplay.NPC
{
    /// <summary>
    /// Bird Game v0.9.5.4: simple return destination on Sidewalk; traverse ALL NavMesh areas during return.
    /// Uses v0.9.4 awareness; requires the additive ResumeAfterReaction() method
    /// supplied in NpcWanderAI v0.9.5.1. Does not change any bird code.
    /// Designed for one pedestrian first, with a NavMeshAgent + the three city areas.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NpcBirdAwareness))]
    [RequireComponent(typeof(NpcNavigationProfile))]
    [DisallowMultipleComponent]
    public sealed class NpcPedestrianReactions : MonoBehaviour
    {
        public enum ReactionState
        {
            Wandering,
            Watching,
            Fleeing,
            Recovering,
            ReturningToSidewalk
        }

        [Header("Watching")]
        [SerializeField, Min(0f)] private float watchTurnSpeed = 300f;

        [Header("Flee")]
        [SerializeField, Min(0.1f)] private float fleeSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float fleeAcceleration = 12f;
        [SerializeField, Min(0.5f)] private float fleeDistance = 7f;
        [SerializeField, Range(0f, 120f)] private float fleeSpreadDegrees = 65f;
        [SerializeField, Range(1, 30)] private int fleeDestinationAttempts = 14;
        [SerializeField, Min(0.1f)] private float fleeSampleDistance = 2f;
        [Tooltip("Tempo minimo que o NPC continua correndo apos deixar de perceber a ameaca.")]
        [SerializeField, Min(1f)] private float panicAfterThreatMinSeconds = 15f;
        [Tooltip("Tempo maximo que o NPC continua correndo apos deixar de perceber a ameaca.")]
        [SerializeField, Min(1f)] private float panicAfterThreatMaxSeconds = 30f;

        [Header("Recovery")]
        [SerializeField, Min(0f)] private float recoverPauseSeconds = 1f;
        [SerializeField, Min(1f)] private float sidewalkSearchRadius = 14f;
        [SerializeField, Range(1, 50)] private int sidewalkSearchAttempts = 24;
        [SerializeField, Min(0.1f)] private float sidewalkSampleDistance = 2f;
        [SerializeField, Min(0.1f)] private float sidewalkReturnRetrySeconds = 1f;
        [SerializeField, Min(0.1f)] private float returnSpeed = 2f;
        [SerializeField, Min(2f)] private float returnRouteTimeout = 18f;
        [SerializeField, Range(0.05f, 0.4f)] private float sidewalkArrivalSampleRadius = 0.15f;
        [Tooltip("Margem minima desejada para escolher um ponto dentro da calçada, longe das bordas.")]
        [SerializeField, Min(0.1f)] private float sidewalkInteriorClearance = 0.4f;

        [Header("Retorno livre pela NavMesh")]
        [Tooltip("Distancia maxima (m) para procurar calçadas apos fugas longas.")]
        [SerializeField, Min(10f)] private float maximumSidewalkSearchRadius = 160f;
        [Tooltip("Quanto perto do destino basta chegar para retomar o passeio.")]
        [SerializeField, Range(0.25f, 1f)] private float returnArrivalTolerance = 0.55f;

        [Header("Debug (Play Mode)")]
        [SerializeField] private bool logStateChanges = true;
        [SerializeField] private bool drawDestinationGizmo = true;
        [SerializeField] private ReactionState currentState = ReactionState.Wandering;
        [SerializeField] private Vector3 currentReactionDestination;
        [SerializeField] private bool hasReactionDestination;
        [SerializeField] private float panicSecondsRemaining;
        [SerializeField] private bool waitForCalmBeforeWatching;
        [SerializeField] private bool hasRememberedSidewalk;
        [SerializeField] private Vector3 rememberedSidewalkPosition;
        [SerializeField] private string returnDiagnostic = "Not returning";
        [SerializeField] private int returnRouteFailures;
        [SerializeField] private int returnStallCount;
        [SerializeField] private string currentNavArea = "Unknown";
        [SerializeField] private float remainingReturnDistance = -1f;
        [SerializeField] private float sidewalkDistanceAtFeet = -1f;
        [SerializeField] private float returnSecondsWithoutProgress;
        [SerializeField] private float selectedSidewalkClearance;
        [SerializeField] private bool returnAgentStopped;
        [SerializeField] private float returnAgentSpeed;
        [SerializeField] private string returnPathStatus = "None";

        private NavMeshAgent agent;
        private NpcBirdAwareness awareness;
        private NpcNavigationProfile navigationProfile;
        private NpcWanderAI wanderAI;
        private NavMeshPath reusablePath;
        private float originalSpeed;
        private float originalAcceleration;
        private float originalAngularSpeed;
        private float originalStoppingDistance;
        private bool originalUpdateRotation;
        private float fleeUntil;
        private float selectedPanicDuration;
        private float recoverUntil;
        private float nextRouteAttempt;
        private float returnRouteDeadline;
        private float lastReturnProgressTime;
        private Vector3 lastReturnProgressPosition;
        private float nextNoSidewalkWarning;
        private int sidewalkMask;
        private Vector3 lastSafeSidewalkPosition;
        private bool hasLastSafeSidewalk;
        private bool initialized;
        private bool warnedOffMesh;
        private Vector3 lastFailedReturnDestination;
        private bool hasFailedReturnDestination;
        private float nextDebugUpdate;

        public ReactionState CurrentState => currentState;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            awareness = GetComponent<NpcBirdAwareness>();
            navigationProfile = GetComponent<NpcNavigationProfile>();
            wanderAI = GetComponent<NpcWanderAI>();

            // Unity native NavMeshPath MUST be created in Awake, not field initialization.
            reusablePath = new NavMeshPath();
        }

        private void OnEnable()
        {
            if (awareness != null)
                awareness.OnAwarenessChanged += OnAwarenessChanged;
        }

        private void Start()
        {
            if (wanderAI == null || !wanderAI.enabled ||
                !navigationProfile.IsConfigured ||
                navigationProfile.Category != NpcNavigationProfile.AgentCategory.Pedestrian)
            {
                Debug.LogError(
                    $"[NPC REACTIONS] {name}: requires enabled NpcWanderAI " +
                    "and configured Pedestrian navigation profile.", this);
                enabled = false;
                return;
            }

            originalSpeed = agent.speed;
            originalAcceleration = agent.acceleration;
            originalAngularSpeed = agent.angularSpeed;
            originalStoppingDistance = agent.stoppingDistance;
            originalUpdateRotation = agent.updateRotation;
            int sidewalkArea = NavMesh.GetAreaFromName("Sidewalk");
            if (sidewalkArea < 0)
            {
                Debug.LogError($"[NPC REACTIONS] {name}: NavMesh Area Sidewalk is missing.", this);
                enabled = false;
                return;
            }
            sidewalkMask = 1 << sidewalkArea;
            initialized = true;
            currentState = ReactionState.Wandering;
            RememberSidewalkUnderAgent();
        }

        private void Update()
        {
            if (!initialized || !agent.isActiveAndEnabled)
                return;

            if (!agent.isOnNavMesh)
            {
                if (!warnedOffMesh)
                {
                    Debug.LogWarning($"[NPC REACTIONS] {name} is not on a NavMesh.", this);
                    warnedOffMesh = true;
                }
                return;
            }

            warnedOffMesh = false;
            // Remember the last real sidewalk polygon visited. During panic
            // this becomes a reliable fallback if the NPC runs far into a road.
            if (currentState != ReactionState.ReturningToSidewalk)
                RememberSidewalkUnderAgent();

            if (Time.time >= nextDebugUpdate)
            {
                UpdateReturnDebug();
                nextDebugUpdate = Time.time + 0.2f;
            }
            panicSecondsRemaining = currentState == ReactionState.Fleeing
                ? Mathf.Max(0f, fleeUntil - Time.time) : 0f;

            // After panic, do not immediately stop again just because the bird
            // remains within the passive Watching radius. A new Threatened state
            // still interrupts and triggers another panic normally.
            if (waitForCalmBeforeWatching &&
                awareness.CurrentState == NpcBirdAwareness.AwarenessState.Calm)
                waitForCalmBeforeWatching = false;

            // Synchronizes when the sensor was already in a state before Start.
            if (currentState == ReactionState.Wandering)
            {
                if (awareness.CurrentState == NpcBirdAwareness.AwarenessState.Threatened)
                    BeginFlee();
                else if (!waitForCalmBeforeWatching &&
                         awareness.CurrentState == NpcBirdAwareness.AwarenessState.Watching)
                    BeginWatching();
                return;
            }

            switch (currentState)
            {
                case ReactionState.Watching:
                    if (awareness.CurrentState == NpcBirdAwareness.AwarenessState.Threatened)
                        BeginFlee();
                    else if (awareness.CurrentState == NpcBirdAwareness.AwarenessState.Calm)
                        ResumeWander();
                    else
                        FaceBird();
                    break;

                case ReactionState.Fleeing:
                    // Each moment of continued threat moves the panic end time
                    // forward; once the bird departs, run for 15-30 more seconds.
                    if (awareness.CurrentState == NpcBirdAwareness.AwarenessState.Threatened)
                        ExtendPanicFromNow();

                    if (Time.time >= fleeUntil)
                    {
                        BeginRecovery();
                        break;
                    }

                    if (Time.time >= nextRouteAttempt && !agent.pathPending &&
                        (!agent.hasPath ||
                         agent.pathStatus != NavMeshPathStatus.PathComplete ||
                         HasArrived()))
                    {
                        TrySetFleeDestination();
                        nextRouteAttempt = Time.time + 0.65f;
                    }
                    break;

                case ReactionState.Recovering:
                    if (Time.time >= recoverUntil)
                        BeginReturnToSidewalk();
                    break;

                case ReactionState.ReturningToSidewalk:
                    // Durante TODO o retorno, o NPC pode passar por qualquer AREA
                    // navegavel do mesmo Agent Type (Road, Crosswalk, Sidewalk etc.).
                    // Se o caos global mudar, preserve a liberdade temporaria.
                    if (agent.areaMask != NavMesh.AllAreas)
                    {
                        agent.areaMask = NavMesh.AllAreas;
                        if (hasReactionDestination)
                            MoveTo(currentReactionDestination);
                    }

                    // O destino ja foi validado como Sidewalk antes de SetDestination.
                    // Nao exija IsOnSidewalk() a cada frame: nas bordas a leitura
                    // da area do poligono pode alternar e impedir a conclusao.
                    if (ReachedReturnDestination())
                    {
                        returnDiagnostic = "Reached Sidewalk target - resuming Wander";
                        ResumeWander();
                        break;
                    }

                    if (Vector3.Distance(agent.nextPosition, lastReturnProgressPosition) > 0.4f)
                    {
                        lastReturnProgressPosition = agent.nextPosition;
                        lastReturnProgressTime = Time.time;
                    }

                    bool stalled = Time.time - lastReturnProgressTime > 5f;
                    bool invalidPath = !agent.pathPending &&
                        (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete);
                    bool expired = Time.time >= returnRouteDeadline;
                    if (Time.time >= nextRouteAttempt && !agent.pathPending &&
                        (invalidPath || stalled || expired))
                    {
                        if (hasReactionDestination && stalled)
                        {
                            hasFailedReturnDestination = true;
                            lastFailedReturnDestination = currentReactionDestination;
                            returnStallCount++;
                        }
                        TrySetReturnDestination();
                    }
                    break;
            }
        }

        private void OnAwarenessChanged(NpcBirdAwareness.AwarenessState state)
        {
            if (!initialized || !isActiveAndEnabled)
                return;

            // Threat can interrupt every other state, including returning to sidewalk.
            if (state == NpcBirdAwareness.AwarenessState.Threatened)
            {
                if (currentState != ReactionState.Fleeing)
                    BeginFlee();
                else
                    ExtendPanicFromNow();
            }
            else if (state == NpcBirdAwareness.AwarenessState.Watching &&
                     currentState == ReactionState.Wandering &&
                     !waitForCalmBeforeWatching)
            {
                BeginWatching();
            }
            else if (state == NpcBirdAwareness.AwarenessState.Calm &&
                     currentState == ReactionState.Watching)
            {
                ResumeWander();
            }
        }

        private void BeginWatching()
        {
            if (!IsReady()) return;
            TakeControl();
            agent.isStopped = true;
            agent.ResetPath();
            agent.updateRotation = false;
            SetState(ReactionState.Watching);
        }

        private void FaceBird()
        {
            Rigidbody bird = awareness.BirdRigidbody;
            if (bird == null) return;
            Vector3 delta = bird.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= 0.001f) return;

            Quaternion target = Quaternion.LookRotation(delta.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, watchTurnSpeed * Time.deltaTime);
        }

        private void BeginFlee()
        {
            if (!IsReady()) return;
            TakeControl();
            navigationProfile.SetLocalChaosActive(true);
            // Se um novo susto interromper o retorno, nao mantenha AllAreas
            // na fuga: volte as 3 areas oficiais do modo caos.
            agent.areaMask = GetProfileAreaMask();
            agent.speed = fleeSpeed;
            agent.acceleration = fleeAcceleration;
            agent.angularSpeed = Mathf.Max(720f, originalAngularSpeed);
            agent.updateRotation = true;
            // Sample ONCE per panic episode. Re-detecting an active threat
            // extends the deadline with this same duration (no re-roll each frame).
            float min = Mathf.Min(panicAfterThreatMinSeconds, panicAfterThreatMaxSeconds);
            float max = Mathf.Max(panicAfterThreatMinSeconds, panicAfterThreatMaxSeconds);
            selectedPanicDuration = Random.Range(min, max);
            fleeUntil = Time.time + selectedPanicDuration;
            panicSecondsRemaining = selectedPanicDuration;
            nextRouteAttempt = Time.time;
            SetState(ReactionState.Fleeing);
            TrySetFleeDestination();
            nextRouteAttempt = Time.time + 0.65f;
        }

        private void ExtendPanicFromNow()
        {
            fleeUntil = Mathf.Max(fleeUntil, Time.time + selectedPanicDuration);
        }

        private bool TrySetFleeDestination()
        {
            Rigidbody bird = awareness.BirdRigidbody;
            Vector3 origin = transform.position;
            Vector3 away = bird != null ? origin - bird.transform.position : transform.forward;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = -transform.forward;
            away.Normalize();

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };

            bool found = false;
            float bestScore = float.NegativeInfinity;
            Vector3 bestPoint = origin;
            for (int i = 0; i < fleeDestinationAttempts; i++)
            {
                float angle = Random.Range(-fleeSpreadDegrees, fleeSpreadDegrees);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * away;
                float distance = Random.Range(fleeDistance * 0.7f, fleeDistance * 1.3f);
                Vector3 candidate = origin + direction * distance;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit,
                        fleeSampleDistance, filter))
                    continue;

                Vector3 displacement = hit.position - origin;
                displacement.y = 0f;
                if (displacement.magnitude < 1.5f ||
                    Vector3.Dot(displacement.normalized, away) < 0.1f)
                    continue;

                if (!agent.CalculatePath(hit.position, reusablePath) ||
                    reusablePath.status != NavMeshPathStatus.PathComplete)
                    continue;

                float score = Vector3.Dot(displacement, away) + displacement.magnitude * 0.25f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPoint = hit.position;
                    found = true;
                }
            }

            if (!found)
            {
                StopReactionMovement();
                return false;
            }

            return MoveTo(bestPoint);
        }

        private void BeginRecovery()
        {
            if (!IsReady()) return;
            StopReactionMovement();
            recoverUntil = Time.time + recoverPauseSeconds;
            SetState(ReactionState.Recovering);
        }

        private void BeginReturnToSidewalk()
        {
            if (!IsReady()) return;
            agent.speed = returnSpeed;
            agent.acceleration = originalAcceleration;
            agent.angularSpeed = originalAngularSpeed;
            agent.updateRotation = true;
            // Sidewalk continua sendo o unico DESTINO valido, mas o TRAJETO
            // pode atravessar qualquer area navegavel do mesmo Agent Type.
            agent.stoppingDistance = 0.12f;
            agent.areaMask = NavMesh.AllAreas;
            SetState(ReactionState.ReturningToSidewalk);

            if (IsOnSidewalk())
            {
                ResumeWander();
                return;
            }

            lastReturnProgressPosition = agent.nextPosition;
            lastReturnProgressTime = Time.time;
            nextRouteAttempt = Time.time;
            returnDiagnostic = "Searching Sidewalk destination (traverse ALL areas)...";
            hasFailedReturnDestination = false;
            returnStallCount = 0;
            TrySetReturnDestination();
        }

        // Busca somente o DESTINO na area Sidewalk; a rota entre o NPC
        // e esse destino usa agent.areaMask = NavMesh.AllAreas.
        private bool TrySetReturnDestination()
        {
            nextRouteAttempt = Time.time + sidewalkReturnRetrySeconds;
            NavMeshQueryFilter sidewalkFilter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = sidewalkMask
            };

            Vector3 origin = agent.nextPosition;
            bool foundInterior = false;
            bool foundBorder = false;
            float bestInteriorScore = float.PositiveInfinity;
            float bestBorderScore = float.PositiveInfinity;
            Vector3 bestInterior = origin, bestBorder = origin;
            float interiorClearance = 0f, borderClearance = 0f;

            // A fuga de 15-30s pode levar muito longe da calçada.
            // Ampliamos progressivamente a busca se necessario.
            float radius = Mathf.Max(2f, sidewalkSearchRadius);
            float maxRadius = Mathf.Max(radius, maximumSidewalkSearchRadius);
            while (radius <= maxRadius + 0.01f)
            {
                if (NavMesh.SamplePosition(origin, out NavMeshHit nearest,
                    radius, sidewalkFilter))
                {
                    EvaluateReturnCandidate(nearest.position, origin, sidewalkFilter,
                        ref foundInterior, ref bestInteriorScore, ref bestInterior,
                        ref interiorClearance, ref foundBorder, ref bestBorderScore,
                        ref bestBorder, ref borderClearance);

                    // Se o ponto mais proximo estiver na BORDA, exploramos
                    // outras posicoes ao redor DELE, dentro da calçada.
                    for (int i = 0; i < sidewalkSearchAttempts; i++)
                    {
                        Vector2 offset = Random.insideUnitCircle * 3.2f;
                        Vector3 guess = nearest.position + new Vector3(offset.x, 0f, offset.y);
                        if (NavMesh.SamplePosition(guess, out NavMeshHit hit,
                            Mathf.Max(0.4f, sidewalkSampleDistance), sidewalkFilter))
                        {
                            EvaluateReturnCandidate(hit.position, origin, sidewalkFilter,
                                ref foundInterior, ref bestInteriorScore, ref bestInterior,
                                ref interiorClearance, ref foundBorder, ref bestBorderScore,
                                ref bestBorder, ref borderClearance);
                        }
                    }
                }

                if (foundInterior) break;
                if (radius >= maxRadius) break;
                radius = Mathf.Min(maxRadius, radius * 2f);
            }

            if (hasLastSafeSidewalk &&
                NavMesh.SamplePosition(lastSafeSidewalkPosition, out NavMeshHit remembered,
                    1f, sidewalkFilter))
            {
                EvaluateReturnCandidate(remembered.position, origin, sidewalkFilter,
                    ref foundInterior, ref bestInteriorScore, ref bestInterior,
                    ref interiorClearance, ref foundBorder, ref bestBorderScore,
                    ref bestBorder, ref borderClearance);
            }

            if (foundInterior || foundBorder)
            {
                Vector3 target = foundInterior ? bestInterior : bestBorder;
                selectedSidewalkClearance = foundInterior ? interiorClearance : borderClearance;
                if (MoveTo(target))
                {
                    // Tempo baseado na distancia real da rota, nao so na linha reta.
                    float pathLength = Vector3.Distance(origin, target);
                    if (agent.CalculatePath(target, reusablePath) &&
                        reusablePath.status == NavMeshPathStatus.PathComplete)
                    {
                        pathLength = 0f;
                        Vector3 p = origin;
                        foreach (Vector3 corner in reusablePath.corners)
                        {
                            pathLength += Vector3.Distance(p, corner);
                            p = corner;
                        }
                    }
                    returnRouteDeadline = Time.time + Mathf.Max(returnRouteTimeout,
                        pathLength / Mathf.Max(0.1f, returnSpeed) * 2f + 5f);
                    lastReturnProgressPosition = agent.nextPosition;
                    lastReturnProgressTime = Time.time;
                    returnDiagnostic = $"Heading to Sidewalk via ALL NavMesh areas ({pathLength:F1} m route)";
                    return true;
                }
            }

            StopReactionMovement();
            returnRouteFailures++;
            returnDiagnostic = "No COMPLETE path to a Sidewalk destination, even using ALL areas";
            if (Time.time >= nextNoSidewalkWarning)
            {
                Debug.LogWarning($"[NPC REACTIONS] {name}: {returnDiagnostic}. " +
                    "Check NavMesh connections and Agent Type.", this);
                nextNoSidewalkWarning = Time.time + 8f;
            }
            return false;
        }

        private void EvaluateReturnCandidate(Vector3 point, Vector3 origin,
            NavMeshQueryFilter sidewalkFilter,
            ref bool foundInterior, ref float bestInteriorScore, ref Vector3 bestInterior,
            ref float interiorClearance,
            ref bool foundBorder, ref float bestBorderScore, ref Vector3 bestBorder,
            ref float borderClearance)
        {
            // Evita atingir um andar superior/inferior da cidade por engano.
            if (Mathf.Abs(point.y - origin.y) > 3f)
                return;
            if (hasFailedReturnDestination &&
                (point - lastFailedReturnDestination).sqrMagnitude < 1.2f * 1.2f)
                return;
            if (!agent.CalculatePath(point, reusablePath) ||
                reusablePath.status != NavMeshPathStatus.PathComplete)
                return;

            float clearance = NavMesh.FindClosestEdge(point, out NavMeshHit edge,
                sidewalkFilter) ? edge.distance : 0f;
            float score = Vector3.Distance(origin, point);
            float requiredClearance = Mathf.Max(sidewalkInteriorClearance,
                agent.radius * 0.8f);
            if (clearance >= requiredClearance)
            {
                if (score < bestInteriorScore)
                {
                    foundInterior = true;
                    bestInteriorScore = score;
                    bestInterior = point;
                    interiorClearance = clearance;
                }
            }
            else if (score < bestBorderScore)
            {
                foundBorder = true;
                bestBorderScore = score;
                bestBorder = point;
                borderClearance = clearance;
            }
        }

        private bool ReachedReturnDestination()
        {
            if (!hasReactionDestination || agent.pathPending) return false;
            float directDistance = Vector3.Distance(agent.nextPosition, currentReactionDestination);
            // Uma rota so e aceita se antes foi calculada como completa.
            // Mesmo que o agente limpe seu hasPath ao finalizar, a distancia
            // ao DESTINO Sidewalk continua sendo o criterio de chegada.
            if (directDistance <= returnArrivalTolerance) return true;
            return agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete &&
                   agent.remainingDistance <= Mathf.Max(returnArrivalTolerance,
                       agent.stoppingDistance + 0.25f);
        }

        private void UpdateReturnDebug()
        {
            if (!agent.isOnNavMesh) return;
            returnAgentStopped = agent.isStopped;
            returnAgentSpeed = agent.velocity.magnitude;
            returnPathStatus = agent.pathPending ? "PathPending" :
                !agent.hasPath ? "NoPath" : agent.pathStatus.ToString();
            remainingReturnDistance = agent.pathPending || !agent.hasPath ||
                float.IsInfinity(agent.remainingDistance)
                ? -1f : agent.remainingDistance;
            returnSecondsWithoutProgress = currentState == ReactionState.ReturningToSidewalk
                ? Time.time - lastReturnProgressTime : 0f;

            NavMeshQueryFilter fullFilter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };
            if (NavMesh.SamplePosition(agent.nextPosition, out NavMeshHit current,
                0.18f, fullFilter))
            {
                int area = NavMesh.GetAreaFromName("Sidewalk");
                int cross = NavMesh.GetAreaFromName("Crosswalk");
                int road = NavMesh.GetAreaFromName("Road");
                currentNavArea = (current.mask & (1 << area)) != 0 ? "Sidewalk" :
                    cross >= 0 && (current.mask & (1 << cross)) != 0 ? "Crosswalk" :
                    road >= 0 && (current.mask & (1 << road)) != 0 ? "Road" :
                    "Other";
            }
            else currentNavArea = "Unknown (no polygon nearby)";

            NavMeshQueryFilter sidewalkFilter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = sidewalkMask
            };
            sidewalkDistanceAtFeet = NavMesh.SamplePosition(agent.nextPosition,
                out NavMeshHit closest, 2.5f, sidewalkFilter)
                ? Vector3.Distance(agent.nextPosition, closest.position) : -1f;
        }

        private bool IsOnSidewalk()
        {
            if (!agent.isOnNavMesh) return false;
            // Sample every currently allowed area at the agent's actual
            // position. A filtered Sidewalk query may find a neighbouring
            // polygon without the agent really standing on it.
            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };
            return NavMesh.SamplePosition(agent.nextPosition, out NavMeshHit hit,
                       Mathf.Max(0.2f, sidewalkArrivalSampleRadius), filter) &&
                   (hit.mask & sidewalkMask) != 0;
        }

        private void RememberSidewalkUnderAgent()
        {
            if (!agent.isOnNavMesh || !IsOnSidewalk()) return;
            hasLastSafeSidewalk = true;
            lastSafeSidewalkPosition = agent.nextPosition;
            hasRememberedSidewalk = true;
            rememberedSidewalkPosition = lastSafeSidewalkPosition;
        }

        private bool HasArrived()
        {
            if (!agent.hasPath || agent.pathPending)
                return false;
            return agent.remainingDistance <= agent.stoppingDistance + 0.3f &&
                   agent.velocity.sqrMagnitude <= 0.25f;
        }

        private bool MoveTo(Vector3 position)
        {
            if (!IsReady()) return false;
            agent.isStopped = false;
            agent.ResetPath();
            if (!agent.SetDestination(position))
            {
                StopReactionMovement();
                return false;
            }
            currentReactionDestination = position;
            hasReactionDestination = true;
            return true;
        }

        private void StopReactionMovement()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            hasReactionDestination = false;
        }

        private void TakeControl()
        {
            if (wanderAI != null)
                wanderAI.enabled = false;
            StopReactionMovement();
        }

        private void ResumeWander()
        {
            if (!IsReady()) return;
            StopReactionMovement();
            navigationProfile.SetLocalChaosActive(false);
            // SetLocalChaosActive pode nao trocar a mascara se o caos GLOBAL
            // continuar ligado. Restauramos explicitamente a permissao oficial.
            agent.areaMask = GetProfileAreaMask();
            agent.speed = originalSpeed;
            agent.acceleration = originalAcceleration;
            agent.angularSpeed = originalAngularSpeed;
            agent.stoppingDistance = originalStoppingDistance;
            agent.updateRotation = originalUpdateRotation;
            hasReactionDestination = false;
            hasFailedReturnDestination = false;
            panicSecondsRemaining = 0f;
            returnDiagnostic = "Recovered - Wander resumed";
            // Avoid a Watching->Wandering->Watching loop at the end of recovery.
            waitForCalmBeforeWatching =
                awareness.CurrentState != NpcBirdAwareness.AwarenessState.Calm;
            SetState(ReactionState.Wandering);
            if (wanderAI != null)
            {
                wanderAI.enabled = true;
                wanderAI.ResumeAfterReaction();
            }
        }

        private int GetProfileAreaMask()
        {
            // Mesmo que o CityChaosManager esteja ligado, restaurar apenas
            // Sidewalk/Crosswalk/Road, sem deixar NavMesh.AllAreas permanente.
            int crosswalk = NavMesh.GetAreaFromName("Crosswalk");
            int road = NavMesh.GetAreaFromName("Road");
            int mask = sidewalkMask | (crosswalk >= 0 ? 1 << crosswalk : 0);
            if (navigationProfile.IsChaosActive && road >= 0)
                mask |= 1 << road;
            return mask;
        }

        private bool IsReady() => initialized && agent != null &&
                                  agent.isActiveAndEnabled && agent.isOnNavMesh;

        private void SetState(ReactionState next)
        {
            if (currentState == next) return;
            currentState = next;
            if (logStateChanges)
                Debug.Log($"[NPC REACTIONS] {name}: {currentState}", this);
        }

        private void OnDisable()
        {
            if (awareness != null)
                awareness.OnAwarenessChanged -= OnAwarenessChanged;

            if (!Application.isPlaying || !initialized)
                return;

            if (agent != null)
            {
                agent.speed = originalSpeed;
                agent.acceleration = originalAcceleration;
                agent.angularSpeed = originalAngularSpeed;
                agent.stoppingDistance = originalStoppingDistance;
                agent.updateRotation = originalUpdateRotation;
            }
            if (navigationProfile != null)
            {
                navigationProfile.SetLocalChaosActive(false);
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                    agent.areaMask = GetProfileAreaMask();
            }
            hasReactionDestination = false;
            currentState = ReactionState.Wandering;
            if (wanderAI != null && agent != null && agent.isOnNavMesh)
            {
                wanderAI.enabled = true;
                wanderAI.ResumeAfterReaction();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDestinationGizmo || !Application.isPlaying ||
                !hasReactionDestination)
                return;
            Gizmos.color = currentState == ReactionState.Fleeing ? Color.red : Color.green;
            Gizmos.DrawWireSphere(currentReactionDestination, 0.35f);
            Gizmos.DrawLine(transform.position, currentReactionDestination);
        }
    }
}
