using UnityEngine;

namespace Scripts.Gameplay.Vehicles
{
    /// <summary>
    /// Buzina ao entrar na zona de proximidade do carro.
    /// Nao modifica a simulacao ITHappy, o Transform ou o Compute Shader.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ITHappy.Car))]
    public sealed class VehicleBirdHorn : MonoBehaviour
    {
        [Header("Deteccao")]
        [SerializeField, Min(0.5f)] private float detectionRadius = 5f;
        [SerializeField, Min(0f)] private float minimumBirdSpeed = 0f;
        [SerializeField, Min(0.1f)] private float resetDistanceExtra = 1.5f;
        [SerializeField, Min(0f)] private float startGraceSeconds = 1f;
        [SerializeField, Min(0f)] private float hornCooldown = 4f;

        [Header("Audio 3D")]
        [SerializeField] private AudioClip[] hornClip;
        [SerializeField, Range(0f, 1f)] private float hornVolume = 0.8f;
        [SerializeField, Min(0.1f)] private float audioMinDistance = 2f;
        [SerializeField, Min(1f)] private float audioMaxDistance = 25f;

        [Header("Diagnostico (Play Mode)")]
        [SerializeField] private bool logHorn = false;
        [SerializeField] private bool drawGizmo = true;
        [SerializeField] private float currentBirdDistance = -1f;
        [SerializeField] private float currentBirdSpeed;
        [SerializeField] private bool armed;
        [SerializeField] private bool initialObservationComplete;
        [SerializeField] private int hornCount;

        private AudioSource hornSource;
        private static Rigidbody sharedBirdRigidbody;
        private static float nextSharedSearchTime;

        private float enabledAt;
        private float nextHornTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sharedBirdRigidbody = null;
            nextSharedSearchTime = 0f;
        }

        private void Awake()
        {
            if (hornSource == null)
                hornSource = GetComponent<AudioSource>();

            if (hornSource == null)
                hornSource = gameObject.AddComponent<AudioSource>();

            // AudioSource dedicado a buzina: somente tocar quando PlayHorn for chamado.
            hornSource.playOnAwake = false;
            hornSource.loop = false;
            hornSource.spatialBlend = 1f;
            hornSource.rolloffMode = AudioRolloffMode.Logarithmic;
            hornSource.minDistance = audioMinDistance;
            hornSource.maxDistance = Mathf.Max(audioMinDistance + 1f, audioMaxDistance);
        }

        private void OnEnable()
        {
            enabledAt = Time.time;
            nextHornTime = 0f;
            armed = false;
            initialObservationComplete = false;
            hornCount = 0;
        }

        private void Start()
        {
            // Protege contra buzina que ja estava tocando no prefab por Play On Awake.
            // Use um AudioSource exclusivo para buzina (nao compartilhe com o motor).
            if (hornSource != null && hornSource.isPlaying)
                hornSource.Stop();
        }

        private void Update()
        {
            if (sharedBirdRigidbody == null)
            {
                FindBirdWhenNeeded();
                currentBirdDistance = -1f;
                currentBirdSpeed = 0f;
                return;
            }

            Vector3 difference = sharedBirdRigidbody.position - transform.position;
            float distanceSquared = difference.sqrMagnitude;
            currentBirdDistance = Mathf.Sqrt(distanceSquared);
            currentBirdSpeed = sharedBirdRigidbody.linearVelocity.magnitude;

            float rearmRadius = detectionRadius + resetDistanceExtra;
            bool isOutsideResetZone = distanceSquared >= rearmRadius * rearmRadius;

            // Uma instancia nova NAO buzina por ja ter nascido perto do passaro.
            // Se nasceu longe, fica pronta para a primeira aproximacao.
            if (!initialObservationComplete)
            {
                if (Time.time - enabledAt < startGraceSeconds)
                    return;

                initialObservationComplete = true;
                armed = isOutsideResetZone;
                return;
            }

            // So rearma ao sair da zona ampliada, evitando repeticoes na borda.
            if (isOutsideResetZone)
            {
                armed = true;
                return;
            }

            if (!armed || distanceSquared > detectionRadius * detectionRadius ||
                currentBirdSpeed < minimumBirdSpeed || Time.time < nextHornTime)
                return;

            armed = false;
            nextHornTime = Time.time + hornCooldown;
            hornCount++;
            PlayHorn();
        }

        private static void FindBirdWhenNeeded()
        {
            if (Time.time < nextSharedSearchTime)
                return;

            nextSharedSearchTime = Time.time + 1f;
            // A tag Player deve estar no GameObject que contem o Rigidbody do Bird.
            GameObject bird = GameObject.FindGameObjectWithTag("Player");
            if (bird != null)
                sharedBirdRigidbody = bird.GetComponent<Rigidbody>();
        }

        private void PlayHorn()
        {
            if (hornSource != null)
            {
                AudioClip clip = hornClip.Length > 0 ? hornClip[Random.Range(0, hornClip.Length)] : hornSource.clip;
                if (clip != null)
                    hornSource.PlayOneShot(clip, hornVolume);
            }

            if (logHorn)
                Debug.Log($"[VEHICLE HORN] {name} - buzina #{hornCount}, passaro a {currentBirdDistance:F1}m", this);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, detectionRadius + resetDistanceExtra);
        }
    }
}
