using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public sealed class ExplosionHitbox : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float radius = 3.5f;
    [SerializeField, Min(0f)] private float damage = 35f;
    [SerializeField, Min(0.01f)] private float activeDuration = 0.2f;
    [SerializeField] private LayerMask damageLayers = Physics.AllLayers;

    private SphereCollider sphereCollider;
    private Renderer hitboxRenderer;
    private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
    private IDamageable owner;
    private Transform ownerRoot;
    private bool isInitialized;
    private bool hasDetonated;

    public float Radius => Mathf.Max(0.1f, radius);
    public float ActiveDuration => Mathf.Max(0.01f, activeDuration);

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized)
            return;

        isInitialized = true;
        sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.center = Vector3.zero;
        sphereCollider.radius = 0.5f;
        sphereCollider.enabled = false;

        hitboxRenderer = GetComponent<Renderer>();
        if (hitboxRenderer != null)
            hitboxRenderer.enabled = false;

        ApplyRadius();
    }

    public void Activate(IDamageable explosionOwner, Transform explosionOwnerRoot)
    {
        if (hasDetonated)
            return;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        Initialize();
        hasDetonated = true;
        owner = explosionOwner;
        ownerRoot = explosionOwnerRoot;
        damagedTargets.Clear();
        ApplyRadius();

        sphereCollider.enabled = true;
        if (hitboxRenderer != null)
            hitboxRenderer.enabled = true;

        Physics.SyncTransforms();
        Vector3 center = transform.TransformPoint(sphereCollider.center);
        float worldRadius = sphereCollider.radius * MaxAbsComponent(transform.lossyScale);
        Collider[] overlaps = Physics.OverlapSphere(
            center,
            worldRadius,
            damageLayers,
            QueryTriggerInteraction.Collide);

        foreach (Collider overlap in overlaps)
            TryDamage(overlap);

        StartCoroutine(DisableAfterActiveWindow());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasDetonated)
            TryDamage(other);
    }

    private void TryDamage(Collider overlap)
    {
        if (overlap == null)
            return;

        IDamageable damageable = overlap.GetComponentInParent<IDamageable>();
        if (damageable == null || damageable == owner || !damageable.IsAlive)
            return;

        Component damageableComponent = damageable as Component;
        if (damageableComponent == null)
            return;

        Transform damageableTransform = damageableComponent.transform;
        if (ownerRoot != null &&
            (damageableTransform == ownerRoot || damageableTransform.IsChildOf(ownerRoot)))
        {
            return;
        }

        if (damagedTargets.Add(damageable))
        {
            damageable.TakeDamage(damage);
            Debug.Log($"ExplosionHitbox hit '{damageableTransform.name}' for {damage:0.##} damage.", this);
        }
    }

    private IEnumerator DisableAfterActiveWindow()
    {
        yield return new WaitForSeconds(ActiveDuration);

        if (sphereCollider != null)
            sphereCollider.enabled = false;
        if (hitboxRenderer != null)
            hitboxRenderer.enabled = false;

        gameObject.SetActive(false);
    }

    private void ApplyRadius()
    {
        if (sphereCollider == null)
            return;

        transform.localPosition = Vector3.zero;
        transform.localScale = Vector3.one * (Radius * 2f);
    }

    private static float MaxAbsComponent(Vector3 value)
    {
        return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.05f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, Radius);
    }
}
