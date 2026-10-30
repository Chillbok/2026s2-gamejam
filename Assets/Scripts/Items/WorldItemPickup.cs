using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public sealed class WorldItemPickup : MonoBehaviour
{
    private const float PickupMass = 50f;
    private const float PickupLinearDamping = 5f;
    private const float PickupAngularDamping = 8f;
    private const float PickupMaximumSpeed = 4f;
    private const float PickupColliderRadius = 0.45f;

    [SerializeField] private ItemData itemData;
    [SerializeField, Min(1)] private int initialQuantity = 1;
    [SerializeField] private List<ItemInstance> instances = new List<ItemInstance>();
    [SerializeField] private Transform visualRoot;

    private GameObject spawnedVisual;
    private Rigidbody body;
    private SphereCollider pickupCollider;

    public ItemData ItemData => itemData;
    public IReadOnlyList<ItemInstance> Instances
    {
        get
        {
            EnsureInstances();
            return instances;
        }
    }
    public int Quantity
    {
        get
        {
            EnsureInstances();
            return instances.Count;
        }
    }

    private void Awake()
    {
        ConfigurePhysics();
    }

    private void FixedUpdate()
    {
        if (body == null || body.isKinematic)
            return;

        Vector3 velocity = body.linearVelocity;
        if (velocity.sqrMagnitude > PickupMaximumSpeed * PickupMaximumSpeed)
            body.linearVelocity = velocity.normalized * PickupMaximumSpeed;
    }

    public bool TryCollect(PlayerInventory inventory)
    {
        if (inventory == null)
            return false;

        EnsureInstances();
        if (instances.Count == 0)
            return false;

        int collected = inventory.TryAddToGeneral(instances);
        if (collected <= 0)
            return false;

        instances.RemoveRange(0, collected);
        initialQuantity = instances.Count;
        if (instances.Count == 0)
            Destroy(gameObject);
        return true;
    }

    public void Initialize(ItemData data, IReadOnlyList<ItemInstance> transferredInstances)
    {
        itemData = data;
        instances = transferredInstances == null
            ? new List<ItemInstance>()
            : new List<ItemInstance>(transferredInstances);
        initialQuantity = Mathf.Max(1, instances.Count);
    }

    private void EnsureInstances()
    {
        if (instances == null)
            instances = new List<ItemInstance>();

        if (itemData == null && instances.Count > 0)
            itemData = instances[0]?.Definition;

        if (itemData == null)
            return;

        while (instances.Count < initialQuantity)
            instances.Add(new ItemInstance(itemData));
    }

    private void Start()
    {
        ConfigurePhysics();
        EnsureInstances();
        if (itemData == null)
        {
            Debug.LogError("WorldItemPickup requires an ItemData asset.", this);
            return;
        }

        EnsureWorldVisual();
    }

    private void ConfigurePhysics()
    {
        if (body == null)
            body = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
        if (pickupCollider == null)
            pickupCollider = GetComponent<SphereCollider>() ?? gameObject.AddComponent<SphereCollider>();

        pickupCollider.enabled = true;
        pickupCollider.isTrigger = false;
        pickupCollider.center = Vector3.zero;
        pickupCollider.radius = PickupColliderRadius;

        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            if (collider != pickupCollider)
                collider.enabled = false;
        }

        body.isKinematic = false;
        body.useGravity = true;
        body.mass = PickupMass;
        body.linearDamping = PickupLinearDamping;
        body.angularDamping = PickupAngularDamping;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void EnsureWorldVisual()
    {
        if (itemData == null || itemData.WorldPrefab == null)
            return;

        // A dropped visual prefab is already the root GameObject; do not nest it inside itself.
        if (GetComponent<Renderer>() != null)
            return;

        if (visualRoot == null)
        {
            Transform existingRoot = transform.Find("VisualRoot");
            if (existingRoot != null)
            {
                visualRoot = existingRoot;
            }
            else
            {
                GameObject root = new GameObject("VisualRoot");
                visualRoot = root.transform;
                visualRoot.SetParent(transform, false);
            }
        }

        Transform placeholder = visualRoot.Find("VisualPlaceholder");
        foreach (Transform child in visualRoot)
        {
            if (child == placeholder)
                continue;

            if (child.GetComponentInChildren<Renderer>(true) != null)
            {
                if (placeholder != null)
                    placeholder.gameObject.SetActive(false);
                return;
            }
        }

        if (spawnedVisual != null)
            return;

        if (placeholder != null)
            placeholder.gameObject.SetActive(false);

        spawnedVisual = Instantiate(itemData.WorldPrefab, visualRoot, false);
        spawnedVisual.name = "ItemModel";
        spawnedVisual.transform.localPosition = Vector3.zero;
        spawnedVisual.transform.localRotation = Quaternion.identity;

        // The pickup root owns interaction and physics. Model-prefab colliders/scripts must not
        // create duplicate pickup behavior when the model is nested under an existing pickup.
        foreach (Collider modelCollider in spawnedVisual.GetComponentsInChildren<Collider>(true))
            modelCollider.enabled = false;

        foreach (WorldItemPickup nestedPickup in spawnedVisual.GetComponentsInChildren<WorldItemPickup>(true))
        {
            if (nestedPickup != this)
                nestedPickup.enabled = false;
        }
    }

    private void OnValidate()
    {
        initialQuantity = Mathf.Max(1, initialQuantity);
    }

    public static WorldItemPickup Spawn(ItemData itemData, IReadOnlyList<ItemInstance> transferredInstances, Vector3 position)
    {
        if (itemData == null || transferredInstances == null || transferredInstances.Count == 0)
            return null;

        GameObject worldObject;
        if (itemData.WorldPrefab != null)
        {
            worldObject = Instantiate(itemData.WorldPrefab, position, Quaternion.identity);
        }
        else
        {
            worldObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            worldObject.name = itemData.DisplayName;
            worldObject.transform.position = position;
            worldObject.transform.localScale = Vector3.one * 0.3f;
        }

        WorldItemPickup pickup = worldObject.GetComponent<WorldItemPickup>();
        if (pickup == null)
            pickup = worldObject.AddComponent<WorldItemPickup>();

        if (worldObject.GetComponent<Collider>() == null)
            worldObject.AddComponent<SphereCollider>();

        pickup.Initialize(itemData, transferredInstances);
        return pickup;
    }
}
