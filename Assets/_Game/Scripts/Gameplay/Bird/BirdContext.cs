using UnityEngine;

namespace Scripts.Gameplay.Bird
{
    /// <summary>
    /// Referencia central do passaro controlado pelo jogador.
    /// O proprio passaro se registra em Awake/OnEnable. Nenhum consumidor
    /// precisa executar Find, procurar tags ou percorrer a Hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BirdContext : MonoBehaviour
    {
        /// <summary>Passaro ativo. Null quando ainda nao existe ou esta desativado.</summary>
        public static BirdContext Instance { get; private set; }

        public Rigidbody Rigidbody { get; private set; }
        public Transform BirdTransform { get; private set; }
        public BirdFlightMotor FlightMotor { get; private set; }
        public BirdLocomotionController Locomotion { get; private set; }

        // Os valores sao lidos dos componentes atuais, sem sincronizacao
        // paralela nem custo de GetComponent por frame.
        public Vector3 Position => Rigidbody != null ? Rigidbody.position : transform.position;
        public Vector3 Velocity => Rigidbody != null ? Rigidbody.linearVelocity : Vector3.zero;
        public float Speed => Velocity.magnitude;
        public Vector3 Forward => BirdTransform != null ? BirdTransform.forward : Vector3.forward;
        public bool IsFlying => Locomotion != null && Locomotion.State == BirdLocomotionState.Flying;

        // Importante ao usar Enter Play Mode Options com Domain Reload desligado.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();
            BirdTransform = transform;
            FlightMotor = GetComponent<BirdFlightMotor>();
            Locomotion = GetComponent<BirdLocomotionController>();
            Register();
        }

        private void OnEnable()
        {
            Register();
        }

        private void Register()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"[BIRD CONTEXT] Dois passaros ativos encontrados: '{Instance.name}' e '{name}'. " +
                    "Mantenha BirdContext apenas no passaro do jogador.", this);
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
