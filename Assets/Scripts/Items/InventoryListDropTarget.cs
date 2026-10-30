using UnityEngine;

public sealed class InventoryListDropTarget : MonoBehaviour
{
    public InventoryListKind Kind { get; private set; }

    public void Initialize(InventoryListKind kind)
    {
        Kind = kind;
    }
}
