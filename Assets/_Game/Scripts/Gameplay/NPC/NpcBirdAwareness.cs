using System;
using UnityEngine;

namespace Scripts.Gameplay.NPC
{
    /// <summary>
    /// v0.9.4: observa o passaro sem comandar movimento, NavMeshAgent ou animacoes.
    /// A fase seguinte (reacoes) pode assinar OnAwarenessChanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcBirdAwareness : MonoBehaviour
    {
        public enum AwarenessState
        {
            Calm,
            Watching,
            Threatened
        }

        [Header("Referencias")]
        [Tooltip("Arraste o Rigidbody do objeto principal Bird (nao o pivot visual).")]
        [SerializeField] private Rigidbody birdRigidbody;

        [Header("Percepcao")]
        [Tooltip("Altura dos olhos do NPC em relacao ao seu Transform.")]
        [SerializeField, Min(0f)] private float eyeHeight = 1.4f;
        [SerializeField, Min(0.1f)] private float watchRadius = 8f;
        [SerializeField, Min(0.1f)] private float dangerRadius = 2.5f;
        [SerializeField, Min(0.1f)] private float minimumDangerSpeed = 5.5f;
        [Tooltip("Quanto tempo a frente usamos para prever um voo rasante (segundos).")]
        [SerializeField, Range(0f, 1f)] private float predictionSeconds = 0.45f;
        [SerializeField, Range(0.02f, 0.5f)] private float scanInterval = 0.08f;

        [Header("Estabilidade dos estados")]
        [Tooltip("Tempo minimo em Ameaçado depois de perceber um voo perigoso.")]
        [SerializeField, Min(0f)] private float threatHoldSeconds = 1.0f;
        [Tooltip("Tempo que continua observando depois que o passaro sai do alcance.")]
        [SerializeField, Min(0f)] private float watchHoldSeconds = 0.7f;

        [Header("Diagnostico (Play Mode)")]
        [SerializeField] private bool logStateChanges = true;
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private AwarenessState currentState = AwarenessState.Calm;
        [SerializeField] private float currentDistance;
        [SerializeField] private float currentSpeed;
        [SerializeField] private float predictedClosestDistance;
        [SerializeField] private bool incomingThreat;

        private float nextScanTime;
        private float lastNearbyTime = float.NegativeInfinity;
        private float lastThreatTime = float.NegativeInfinity;
        private bool warnedMissingBird;

        public AwarenessState CurrentState => currentState;
        public float CurrentDistance => currentDistance;
        public float CurrentSpeed => currentSpeed;
        public float PredictedClosestDistance => predictedClosestDistance;
        public bool IncomingThreat => incomingThreat;
        public Rigidbody BirdRigidbody => birdRigidbody;

        public event Action<AwarenessState> OnAwarenessChanged;

        private void OnEnable()
        {
            currentState = AwarenessState.Calm;
            lastNearbyTime = float.NegativeInfinity;
            lastThreatTime = float.NegativeInfinity;
            nextScanTime = 0f;
        }

        private void OnValidate()
        {
            watchRadius = Mathf.Max(0.1f, watchRadius);
            dangerRadius = Mathf.Clamp(dangerRadius, 0.1f, watchRadius);
            minimumDangerSpeed = Mathf.Max(0.1f, minimumDangerSpeed);
            scanInterval = Mathf.Clamp(scanInterval, 0.02f, 0.5f);
        }

        private void Update()
        {
            if (Time.time < nextScanTime)
                return;
            nextScanTime = Time.time + scanInterval;

            if (birdRigidbody == null)
            {
                if (!warnedMissingBird)
                {
                    Debug.LogWarning(
                        $"[NPC AWARENESS] {name}: atribua o Rigidbody do Bird no Inspector.", this);
                    warnedMissingBird = true;
                }
                return;
            }

            warnedMissingBird = false;
            EvaluateBird(Time.time);
        }

        private void EvaluateBird(float now)
        {
            Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
            Vector3 birdPosition = birdRigidbody.transform.position;
            Vector3 toEye = eyePosition - birdPosition;
            Vector3 birdVelocity = birdRigidbody.linearVelocity; // Unity 6

            currentDistance = toEye.magnitude;
            currentSpeed = birdVelocity.magnitude;
            predictedClosestDistance = currentDistance;
            incomingThreat = false;

            bool nearby = currentDistance <= watchRadius;
            if (nearby)
                lastNearbyTime = now;

            // Antecipacao: ponto mais proximo ao NPC ao longo da trajetoria
            // linear futura do passaro. Isso evita perder voos muito rapidos
            // entre dois scans e evita assustar NPCs fora da trajetoria.
            if (nearby && predictionSeconds > 0f && currentSpeed >= minimumDangerSpeed)
            {
                float speedSquared = birdVelocity.sqrMagnitude;
                float secondsToClosest = Vector3.Dot(toEye, birdVelocity) / speedSquared;

                // secondsToClosest > 0 significa que o passaro ainda se aproxima;
                // nao contamos uma passagem ja terminada como nova ameaca.
                if (secondsToClosest > 0f)
                {
                    float lookAhead = Mathf.Min(secondsToClosest, predictionSeconds);
                    Vector3 predictedPosition = birdPosition + birdVelocity * lookAhead;
                    predictedClosestDistance = Vector3.Distance(eyePosition, predictedPosition);

                    incomingThreat = predictedClosestDistance <= dangerRadius;
                }
            }

            if (incomingThreat)
                lastThreatTime = now;

            AwarenessState nextState;
            if (now - lastThreatTime <= threatHoldSeconds)
                nextState = AwarenessState.Threatened;
            else if (nearby || now - lastNearbyTime <= watchHoldSeconds)
                nextState = AwarenessState.Watching;
            else
                nextState = AwarenessState.Calm;

            ChangeState(nextState);
        }

        private void ChangeState(AwarenessState nextState)
        {
            if (currentState == nextState)
                return;

            currentState = nextState;
            if (logStateChanges)
            {
                Debug.Log(
                    $"[NPC AWARENESS] {name}: {currentState} | " +
                    $"dist={currentDistance:F1}m, velocidade={currentSpeed:F1}m/s, " +
                    $"aproximacao={predictedClosestDistance:F1}m", this);
            }

            OnAwarenessChanged?.Invoke(currentState);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
                return;

            Vector3 eyes = transform.position + Vector3.up * eyeHeight;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(eyes, watchRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(eyes, dangerRadius);

            if (Application.isPlaying && birdRigidbody != null)
            {
                Gizmos.color = incomingThreat ? Color.red : Color.cyan;
                Gizmos.DrawLine(eyes, birdRigidbody.transform.position);
            }
        }
    }
}
