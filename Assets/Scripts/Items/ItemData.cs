using System;
using UnityEngine;
using UnityEngine.Events;

public enum ItemType
{
    Scrap,
    Equipment,
    Consumable
}

[CreateAssetMenu(fileName = "ItemData", menuName = "Game/Items/Item Data")]
public sealed class ItemData : ScriptableObject
{
    [SerializeField, HideInInspector] private string itemDefinitionId;
    [SerializeField] private string displayName = "New Item";
    [SerializeField] private ItemType itemType;
    [SerializeField, Min(1)] private int maximumStackSize = 5;
    [SerializeField] private Sprite icon;
    [SerializeField] private GameObject worldPrefab;
    [SerializeField] private GameObject heldPrefab;
    [SerializeField] private UnityEvent onUse = new UnityEvent();

    public string DefinitionId => itemDefinitionId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public ItemType Type => itemType;
    public int MaximumStackSize => Mathf.Max(1, maximumStackSize);
    public Sprite Icon => icon;
    public GameObject WorldPrefab => worldPrefab;
    public GameObject HeldPrefab => heldPrefab;

    public void InvokeUse()
    {
        onUse?.Invoke();
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(itemDefinitionId))
            itemDefinitionId = Guid.NewGuid().ToString("N");

        maximumStackSize = Mathf.Max(1, maximumStackSize);
    }
}
