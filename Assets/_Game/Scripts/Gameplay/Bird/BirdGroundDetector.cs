
using UnityEngine;

namespace Scripts.Gameplay.Bird
{
    [DisallowMultipleComponent]
    public sealed class BirdGroundDetector : MonoBehaviour
    {
        [Header("References")]

        [Tooltip(
            "Transform localizado proximo da base " +
            "do CapsuleCollider do passaro."
        )]
        [SerializeField]
        private Transform groundCheck;

        [Header("Landing Detection")]

        [Tooltip(
            "Layers das superficies onde o passaro " +
            "pode pousar."
        )]
        [SerializeField]
        private LayerMask landableMask;

        [Tooltip(
            "Distancia maxima para detectar uma " +
            "superficie de pouso, em unidades."
        )]
        [SerializeField, Min(0.1f)]
        private float maxLandingDistance = 2f;

        [Tooltip(
            "Raio da esfera usada para detectar " +
            "superficies abaixo do passaro."
        )]
        [SerializeField, Min(0.01f)]
        private float checkRadius = 0.2f;

        [Tooltip(
            "Inclinacao maxima permitida para " +
            "considerar uma superficie pousavel."
        )]
        [SerializeField, Range(0f, 89f)]
        private float maxLandingSlope = 35f;

        [Tooltip(
            "Distancia usada para identificar " +
            "quando o passaro esta muito proximo " +
            "da superficie."
        )]
        [SerializeField, Min(0f)]
        private float groundContactDistance = 0.15f;

        [Header("Debug")]

        [SerializeField]
        private bool showGizmos = true;

        [SerializeField]
        private bool logDetectionChanges = false;

        private const float CastSkin = 0.05f;

        public bool HasLandingSurface { get; private set; }

        public bool IsCloseToSurface =>
            HasLandingSurface &&
            DistanceToSurface <= groundContactDistance;

        public float DistanceToSurface { get; private set; }

        public float SurfaceSlopeAngle { get; private set; }

        public Vector3 SurfacePoint { get; private set; }

        public Vector3 SurfaceNormal { get; private set; }

        public Collider SurfaceCollider { get; private set; }

        private void Awake()
        {
            if (groundCheck == null)
            {
                Debug.LogError(
                    "BirdGroundDetector: GroundCheck " +
                    "nao configurado.",
                    this
                );

                enabled = false;
                return;
            }

            if (landableMask.value == 0)
            {
                Debug.LogWarning(
                    "BirdGroundDetector: selecione " +
                    "a Layer Landable no Inspector.",
                    this
                );
            }
        }

        private void FixedUpdate()
        {
            DetectLandingSurface();
        }

        private void DetectLandingSurface()
        {
            bool previousDetection = HasLandingSurface;

            ResetDetection();

            Vector3 origin =
                groundCheck.position +
                Vector3.up * (checkRadius + CastSkin);

            float castDistance =
                maxLandingDistance + CastSkin;

            bool hasHit = Physics.SphereCast(
                origin,
                checkRadius,
                Vector3.down,
                out RaycastHit hit,
                castDistance,
                landableMask,
                QueryTriggerInteraction.Ignore
            );

            if (hasHit)
            {
                float slopeAngle = Vector3.Angle(
                    hit.normal,
                    Vector3.up
                );

                bool validSlope =
                    slopeAngle <= maxLandingSlope;

                if (validSlope)
                {
                    HasLandingSurface = true;

                    SurfaceCollider = hit.collider;
                    SurfacePoint = hit.point;
                    SurfaceNormal = hit.normal;
                    SurfaceSlopeAngle = slopeAngle;

                    // Distancia vertical aproximada
                    // entre GroundCheck e a superficie.
                    DistanceToSurface = Mathf.Max(
                        0f,
                        groundCheck.position.y -
                        hit.point.y
                    );
                }
            }

            if (logDetectionChanges &&
                previousDetection != HasLandingSurface)
            {
                Debug.Log(
                    HasLandingSurface
                        ? "[BIRD GROUND] Superficie detectada."
                        : "[BIRD GROUND] Sem superficie pousavel.",
                    this
                );
            }
        }

        private void ResetDetection()
        {
            HasLandingSurface = false;

            DistanceToSurface =
                float.PositiveInfinity;

            SurfaceSlopeAngle = 0f;
            SurfacePoint = Vector3.zero;
            SurfaceNormal = Vector3.up;
            SurfaceCollider = null;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos || groundCheck == null)
                return;

            float radius = Mathf.Max(0.01f, checkRadius);

            Vector3 origin =
                groundCheck.position +
                Vector3.up * (radius + CastSkin);

            Vector3 end =
                origin +
                Vector3.down *
                (maxLandingDistance + CastSkin);

            Gizmos.color = Application.isPlaying &&
                           HasLandingSurface
                ? Color.green
                : Color.yellow;

            Gizmos.DrawWireSphere(origin, radius);
            Gizmos.DrawLine(origin, end);
            Gizmos.DrawWireSphere(end, radius);

            if (Application.isPlaying &&
                HasLandingSurface)
            {
                Gizmos.color = Color.green;

                Gizmos.DrawSphere(
                    SurfacePoint,
                    0.08f
                );

                Gizmos.DrawLine(
                    SurfacePoint,
                    SurfacePoint + SurfaceNormal * 0.5f
                );
            }
        }
    }
}
