using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldItemPickup : MonoBehaviour
{
    [SerializeField] private ItemData itemData;
    [SerializeField, Min(1)] private int initialQuantity = 1;
    [SerializeField] private List<ItemInstance> instances = new List<ItemInstance>();

    public ItemData ItemData => itemData;
    public int Quantity
    {
        get
        {
            EnsureInstances();
            return instances.Count;
        }
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
        EnsureInstances();
        if (itemData == null)
            Debug.LogError("WorldItemPickup requires an ItemData asset.", this);
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
