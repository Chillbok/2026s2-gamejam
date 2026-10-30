using System;
using UnityEngine;

[Serializable]
public sealed class ItemInstance
{
    [SerializeField] private string instanceId;
    [SerializeField] private ItemData definition;

    public string InstanceId => instanceId;
    public ItemData Definition => definition;

    public ItemInstance() { }

    public ItemInstance(ItemData definition)
    {
        this.definition = definition;
        instanceId = Guid.NewGuid().ToString("N");
    }

    public bool IsValid => definition != null && !string.IsNullOrWhiteSpace(instanceId);
}
