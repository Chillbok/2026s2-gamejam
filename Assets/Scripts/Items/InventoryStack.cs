using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public sealed class InventoryStack
{
    [SerializeField] private ItemData definition;
    [SerializeField] private List<ItemInstance> instances = new List<ItemInstance>();

    public ItemData Definition => definition;
    public IReadOnlyList<ItemInstance> Instances => instances;
    public int Count => instances.Count;
    public bool IsEmpty => definition == null || instances.Count == 0;

    public InventoryStack() { }

    public InventoryStack(ItemData definition)
    {
        this.definition = definition;
    }

    public int Add(IReadOnlyList<ItemInstance> items, int startIndex, int amount)
    {
        if (items == null || amount <= 0 || startIndex < 0 || startIndex >= items.Count)
            return 0;

        ItemData incomingDefinition = items[startIndex]?.Definition;
        if (incomingDefinition == null)
            return 0;

        if (IsEmpty)
            definition = incomingDefinition;
        else if (definition != incomingDefinition)
            return 0;

        int capacity = definition.MaximumStackSize - instances.Count;
        int toAdd = Mathf.Min(Mathf.Min(amount, capacity), items.Count - startIndex);
        int added = 0;
        for (int i = 0; i < toAdd; i++)
        {
            ItemInstance instance = items[startIndex + i];
            if (instance == null || instance.Definition != definition)
                break;

            instances.Add(instance);
            added++;
        }

        if (instances.Count == 0)
            definition = null;

        return added;
    }

    public ItemInstance RemoveLast()
    {
        if (instances.Count == 0)
            return null;

        int lastIndex = instances.Count - 1;
        ItemInstance removed = instances[lastIndex];
        instances.RemoveAt(lastIndex);
        if (instances.Count == 0)
            definition = null;
        return removed;
    }

    public List<ItemInstance> TakeAll()
    {
        List<ItemInstance> taken = instances.ToList();
        instances.Clear();
        definition = null;
        return taken;
    }

    public void Clear()
    {
        instances.Clear();
        definition = null;
    }
}
