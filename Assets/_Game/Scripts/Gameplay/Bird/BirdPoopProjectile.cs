using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Scripts.Gameplay.Bird
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class BirdPoopProjectile : MonoBehaviour
    {
        [Header("Impact")]
        [SerializeField] private GameObject decalPrefab;
        [SerializeField] private LayerMask decalSurfaceMask = ~0;
        [SerializeField, Min(0.01f)] private float maxFlightLifetime = 8f;

        [Header("Cartoon Impact VFX")]
        [SerializeField] private GameObject impactVfxPrefab;
        [SerializeField, Min(0.2f)] private float impactVfxLifetime = 2f;

        [Header("Decal")]
        [SerializeField, Range(0.01f, 0.2f)] private float surfaceOffset = 0.06f;
        [SerializeField, Range(0.5f, 1f)] private float minSizeMultiplier = 0.85f;
        [SerializeField, Range(1f, 1.5f)] private float maxSizeMultiplier = 1.15f;
        [SerializeField, Min(1f)] private float decalLifetime = 60f;
        [SerializeField, Range(1, 200)] private int maxActiveDecals = 40;

        private Rigidbody projectileRigidbody;
        private SphereCollider projectileCollider;
        private bool initialized;
        private bool impacted;

        private static readonly Queue<GameObject> ActiveDecals = new Queue<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTrackedDecals()
        {
            ActiveDecals.Clear();
        }

        private void Awake()
        {
            projectileRigidbody = GetComponent<Rigidbody>();
            projectileCollider = GetComponent<SphereCollider>();

            projectileRigidbody.isKinematic = false;
            projectileRigidbody.useGravity = true;
            projectileRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            projectileRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            Destroy(gameObject, maxFlightLifetime);
        }

        public void Initialize(Vector3 initialVelocity, Collider[] birdColliders)
        {
            if (birdColliders != null)
            {
                foreach (Collider ownCollider in birdColliders)
                {
                    if (ownCollider != null && ownCollider != projectileCollider)
                        Physics.IgnoreCollision(projectileCollider, ownCollider, true);
                }
            }

            projectileRigidbody.linearVelocity = initialVelocity;
            initialized = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!initialized || impacted)
                return;

            impacted = true;

            if (collision.contactCount > 0 &&
                decalPrefab != null && collision.collider != null)
            {
                int layer = collision.collider.gameObject.layer;
                bool canLeaveDecal = (decalSurfaceMask.value & (1 << layer)) != 0;

                if (canLeaveDecal)
                {
                    ContactPoint contact = collision.GetContact(0);
                    SpawnDecal(contact.point, contact.normal);
                    SpawnImpactVfx(contact.point, contact.normal);
                }
            }

            Destroy(gameObject);
        }

        private void SpawnImpactVfx(Vector3 point, Vector3 surfaceNormal)
        {
            if (impactVfxPrefab == null)
                return;

            Vector3 normal = surfaceNormal.normalized;
            if (normal.sqrMagnitude < 0.001f)
                normal = Vector3.up;

            // Particle System Shape / Cone emite ao longo do Z local.
            // Orienta o prefab para projetar gotas para fora da superficie.
            Quaternion rotation = Quaternion.LookRotation(normal);

            GameObject effect = Instantiate(
                impactVfxPrefab,
                point + normal * 0.015f,
                rotation
            );

            // Suporta prefabs com Play On Awake desativado.
            ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in particles)
            {
                if (!particle.gameObject.activeInHierarchy)
                    continue;

                if (!particle.isPlaying)
                    particle.Play(true);
            }

            // Nao altera nem acumula efeitos no projétil.
            Destroy(effect, impactVfxLifetime);
        }

        private void SpawnDecal(Vector3 point, Vector3 surfaceNormal)
        {
            Vector3 normal = surfaceNormal.normalized;
            if (normal.sqrMagnitude < 0.001f)
                return;

            // URP Decal Projector projeta no sentido do +Z local.
            Vector3 projectionDirection = -normal;
            Vector3 tangentUp = Vector3.ProjectOnPlane(Vector3.up, projectionDirection);
            if (tangentUp.sqrMagnitude < 0.001f)
                tangentUp = Vector3.ProjectOnPlane(Vector3.forward, projectionDirection);

            Quaternion baseRotation = Quaternion.LookRotation(projectionDirection, tangentUp);
            Quaternion randomRotation = Quaternion.AngleAxis(Random.Range(0f, 360f), normal);

            GameObject decal = Instantiate(
                decalPrefab,
                point + normal * surfaceOffset,
                randomRotation * baseRotation
            );

            DecalProjector projector = decal.GetComponent<DecalProjector>();
            if (projector == null)
            {
                Debug.LogError("BirdPoopProjectile: prefab do decal sem Decal Projector.", decal);
                Destroy(decal);
                return;
            }

            float multiplier = Random.Range(minSizeMultiplier, maxSizeMultiplier);
            Vector3 originalSize = projector.size;
            projector.size = new Vector3(
                originalSize.x * multiplier,
                originalSize.y * multiplier,
                originalSize.z
            );

            // Mantem o limite de Decal Projectors simultaneos.
            while (ActiveDecals.Count > 0 && ActiveDecals.Peek() == null)
                ActiveDecals.Dequeue();

            while (ActiveDecals.Count >= maxActiveDecals)
            {
                GameObject oldest = ActiveDecals.Dequeue();
                if (oldest != null)
                    Destroy(oldest);
            }

            ActiveDecals.Enqueue(decal);
            Destroy(decal, decalLifetime);
        }
    }
}
