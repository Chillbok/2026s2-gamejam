using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public enum InventoryListKind
{
    EquipmentQuickSlots,
    ConsumableQuickSlots,
    General
}

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(ThirdPersonController))]
public sealed class PlayerInventory : MonoBehaviour
{
    [Header("Slot Counts")]
    [SerializeField, Min(0)] private int equipmentQuickSlotCount = 5;
    [SerializeField, Min(0)] private int consumableQuickSlotCount = 5;
    [SerializeField, Min(0)] private int generalSlotCount = 18;

    [Header("Pickup")]
    [SerializeField, Min(0.1f)] private float pickupRadius = 2.5f;

    [Header("Held Item")]
    [SerializeField] private Transform heldItemAnchor;

    [SerializeField] private List<InventoryStack> equipmentQuickSlots = new List<InventoryStack>();
    [SerializeField] private List<InventoryStack> consumableQuickSlots = new List<InventoryStack>();
    [SerializeField] private List<InventoryStack> generalSlots = new List<InventoryStack>();

    private PlayerInput playerInput;
    private ThirdPersonController playerController;
    private PlayerInventoryUI inventoryUI;
    private InputAction inventoryToggleAction;
    private InputAction equipmentWheelAction;
    private InputAction consumableWheelAction;
    private ItemData heldItemData;
    private InventoryListKind heldItemList;
    private GameObject heldItemVisual;
    private bool inventoryOpen;
    private bool wheelOpen;
    private bool cursorStateSaved;
    private InventoryListKind activeWheelList;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;

    public event Action InventoryChanged;

    public bool IsInventoryOpen => inventoryOpen;
    public bool HasHeldItem => heldItemData != null;
    public ItemData HeldItemData => heldItemData;
    public float PickupRadius => pickupRadius;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        playerController = GetComponent<ThirdPersonController>();

        EnsureListCapacity(equipmentQuickSlots, equipmentQuickSlotCount);
        EnsureListCapacity(consumableQuickSlots, consumableQuickSlotCount);
        EnsureListCapacity(generalSlots, generalSlotCount);

        if (heldItemAnchor == null)
        {
            GameObject anchor = new GameObject("HeldItemAnchor");
            anchor.transform.SetParent(transform, false);
            anchor.transform.localPosition = new Vector3(0.35f, 1.1f, 0.4f);
            heldItemAnchor = anchor.transform;
        }
    }

    private void Start()
    {
        inventoryUI = GetComponent<PlayerInventoryUI>();
        if (inventoryUI == null)
            inventoryUI = gameObject.AddComponent<PlayerInventoryUI>();
        inventoryUI.Initialize(this);

        playerController.onInteract.AddListener(HandleShortInteract);
        playerController.onInteractLong.AddListener(HandleLongInteract);

        inventoryToggleAction = new InputAction("Toggle Inventory", InputActionType.Button, "<Keyboard>/tab");
        inventoryToggleAction.performed += _ => ToggleInventory();
        inventoryToggleAction.Enable();

        equipmentWheelAction = new InputAction("Equipment Quick Slot Wheel", InputActionType.Button, "<Keyboard>/1");
        equipmentWheelAction.started += _ => BeginQuickSlotWheel(InventoryListKind.EquipmentQuickSlots);
        equipmentWheelAction.canceled += _ => EndQuickSlotWheel(InventoryListKind.EquipmentQuickSlots);
        equipmentWheelAction.Enable();

        consumableWheelAction = new InputAction("Consumable Quick Slot Wheel", InputActionType.Button, "<Keyboard>/2");
        consumableWheelAction.started += _ => BeginQuickSlotWheel(InventoryListKind.ConsumableQuickSlots);
        consumableWheelAction.canceled += _ => EndQuickSlotWheel(InventoryListKind.ConsumableQuickSlots);
        consumableWheelAction.Enable();

        NotifyChanged();
    }

    private void Update()
    {
        if (wheelOpen && inventoryUI != null && Mouse.current != null)
            inventoryUI.UpdateQuickSlotWheel(Mouse.current.position.ReadValue());
    }

    private void OnDestroy()
    {
        if (playerController != null)
        {
            playerController.onInteract.RemoveListener(HandleShortInteract);
            playerController.onInteractLong.RemoveListener(HandleLongInteract);
        }

        DisposeAction(inventoryToggleAction);
        DisposeAction(equipmentWheelAction);
        DisposeAction(consumableWheelAction);
        RestoreCursor();
    }

    private void OnValidate()
    {
        equipmentQuickSlotCount = Mathf.Max(0, equipmentQuickSlotCount);
        consumableQuickSlotCount = Mathf.Max(0, consumableQuickSlotCount);
        generalSlotCount = Mathf.Max(0, generalSlotCount);
        pickupRadius = Mathf.Max(0.1f, pickupRadius);

        if (Application.isPlaying)
        {
            equipmentQuickSlotCount = ResizeOrKeep(equipmentQuickSlots, equipmentQuickSlotCount);
            consumableQuickSlotCount = ResizeOrKeep(consumableQuickSlots, consumableQuickSlotCount);
            generalSlotCount = ResizeOrKeep(generalSlots, generalSlotCount);
            NotifyChanged();
        }
    }

    public IReadOnlyList<InventoryStack> GetSlots(InventoryListKind kind)
    {
        return GetMutableSlots(kind);
    }

    public int TryAddToGeneral(List<ItemInstance> incomingInstances)
    {
        if (incomingInstances == null || incomingInstances.Count == 0)
            return 0;

        int added = AddInstances(generalSlots, InventoryListKind.General, incomingInstances, incomingInstances.Count);
        if (added > 0)
        {
            SortSlots(generalSlots);
            NotifyChanged();
        }
        return added;
    }

    public bool TryMoveWholeStack(InventoryListKind sourceKind, int sourceIndex, InventoryListKind destinationKind)
    {
        if (sourceKind == destinationKind)
            return false;

        List<InventoryStack> sourceSlots = GetMutableSlots(sourceKind);
        List<InventoryStack> destinationSlots = GetMutableSlots(destinationKind);
        if (sourceIndex < 0 || sourceIndex >= sourceSlots.Count)
            return false;

        InventoryStack sourceStack = sourceSlots[sourceIndex];
        if (sourceStack == null || sourceStack.IsEmpty)
            return false;

        ItemData definition = sourceStack.Definition;
        if (!CanStoreType(destinationKind, definition.Type))
            return false;

        List<ItemInstance> instances = sourceStack.Instances.ToList();
        if (GetAvailableCapacity(destinationSlots, definition) < instances.Count)
            return false;

        int added = AddInstances(destinationSlots, destinationKind, instances, instances.Count);
        if (added != instances.Count)
            return false;

        sourceStack.Clear();
        SortSlots(sourceSlots);
        SortSlots(destinationSlots);

        if (heldItemData == definition && heldItemList == sourceKind)
            ClearHeldItem();

        NotifyChanged();
        return true;
    }

    public bool DropWholeStack(InventoryListKind sourceKind, int sourceIndex)
    {
        List<InventoryStack> sourceSlots = GetMutableSlots(sourceKind);
        if (sourceIndex < 0 || sourceIndex >= sourceSlots.Count)
            return false;

        InventoryStack stack = sourceSlots[sourceIndex];
        if (stack == null || stack.IsEmpty)
            return false;

        ItemData definition = stack.Definition;
        List<ItemInstance> instances = stack.TakeAll();
        SortSlots(sourceSlots);

        if (heldItemData == definition && heldItemList == sourceKind)
            ClearHeldItem();

        WorldItemPickup.Spawn(definition, instances, transform.position);
        NotifyChanged();
        return true;
    }

    public bool SelectQuickSlot(InventoryListKind kind, int slotIndex)
    {
        if (kind == InventoryListKind.General)
            return false;

        List<InventoryStack> slots = GetMutableSlots(kind);
        if (slotIndex < 0 || slotIndex >= slots.Count || slots[slotIndex] == null || slots[slotIndex].IsEmpty)
            return false;

        heldItemData = slots[slotIndex].Definition;
        heldItemList = kind;
        ShowHeldItemVisual();
        inventoryUI?.ShowStatus($"들고 있음: {heldItemData.DisplayName}");
        NotifyChanged();
        return true;
    }

    private void HandleShortInteract()
    {
        if (HasHeldItem)
        {
            StowHeldItem();
            return;
        }

        TryPickupNearestWorldItem();
    }

    private void HandleLongInteract()
    {
        if (!HasHeldItem)
            return;

        ItemData usedItem = heldItemData;
        usedItem.InvokeUse();

        if (usedItem.Type == ItemType.Consumable)
        {
            List<InventoryStack> sourceSlots = GetMutableSlots(heldItemList);
            InventoryStack lastMatchingStack = sourceSlots
                .Where(slot => slot != null && !slot.IsEmpty && slot.Definition == usedItem)
                .LastOrDefault();

            if (lastMatchingStack != null)
                lastMatchingStack.RemoveLast();

            SortSlots(sourceSlots);
            if (!sourceSlots.Any(slot => slot != null && !slot.IsEmpty && slot.Definition == usedItem))
                ClearHeldItem();
        }

        NotifyChanged();
    }

    private void StowHeldItem()
    {
        if (!HasHeldItem)
            return;

        ClearHeldItem();
        inventoryUI?.ShowStatus("아이템을 퀵슬롯에 다시 넣었습니다.");
        NotifyChanged();
    }

    private void TryPickupNearestWorldItem()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, pickupRadius, Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        WorldItemPickup nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Collider hit in colliders)
        {
            WorldItemPickup pickup = hit.GetComponentInParent<WorldItemPickup>();
            if (pickup == null)
                continue;

            float distance = (pickup.transform.position - transform.position).sqrMagnitude;
            if (distance >= nearestDistance)
                continue;

            nearest = pickup;
            nearestDistance = distance;
        }

        if (nearest != null && nearest.TryCollect(this))
            inventoryUI?.ShowStatus("아이템을 주웠습니다.");
        else
            inventoryUI?.ShowStatus("주울 수 있는 아이템이 없습니다.");
    }

    private void ToggleInventory()
    {
        if (wheelOpen)
            return;

        if (inventoryOpen)
            CloseInventory();
        else
            OpenInventory();
    }

    private void OpenInventory()
    {
        inventoryOpen = true;
        playerController.CancelPendingInteraction();
        equipmentWheelAction?.Disable();
        consumableWheelAction?.Disable();
        SaveCursor();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SwitchActionMap("UI");
        inventoryUI?.SetInventoryOpen(true);
    }

    private void CloseInventory()
    {
        inventoryOpen = false;
        inventoryUI?.SetInventoryOpen(false);
        SwitchActionMap("Player");
        equipmentWheelAction?.Enable();
        consumableWheelAction?.Enable();
        RestoreCursor();
    }

    private void BeginQuickSlotWheel(InventoryListKind kind)
    {
        if (inventoryOpen || wheelOpen)
            return;

        wheelOpen = true;
        activeWheelList = kind;
        SaveCursor();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SwitchActionMap("UI");
        inventoryUI?.ShowQuickSlotWheel(kind, GetSlots(kind));
    }

    private void EndQuickSlotWheel(InventoryListKind kind)
    {
        if (!wheelOpen || activeWheelList != kind)
            return;

        int selectedIndex = inventoryUI != null ? inventoryUI.GetHighlightedQuickSlot() : -1;
        inventoryUI?.HideQuickSlotWheel();
        wheelOpen = false;

        if (selectedIndex >= 0)
            SelectQuickSlot(kind, selectedIndex);

        SwitchActionMap("Player");
        RestoreCursor();
    }

    private void SwitchActionMap(string mapName)
    {
        if (playerInput == null || playerInput.actions == null || playerInput.actions.FindActionMap(mapName, false) == null)
            return;

        playerInput.SwitchCurrentActionMap(mapName);
    }

    private void SaveCursor()
    {
        if (cursorStateSaved)
            return;

        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        cursorStateSaved = true;
    }

    private void RestoreCursor()
    {
        if (!cursorStateSaved)
            return;

        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;
        cursorStateSaved = false;
    }

    private void ShowHeldItemVisual()
    {
        if (heldItemVisual != null)
            Destroy(heldItemVisual);

        if (heldItemAnchor != null && heldItemData != null && heldItemData.HeldPrefab != null)
        {
            heldItemVisual = Instantiate(heldItemData.HeldPrefab, heldItemAnchor);
            heldItemVisual.transform.localPosition = Vector3.zero;
            heldItemVisual.transform.localRotation = Quaternion.identity;
        }
    }

    private void ClearHeldItem()
    {
        heldItemData = null;
        if (heldItemVisual != null)
            Destroy(heldItemVisual);
        heldItemVisual = null;
    }

    private int AddInstances(List<InventoryStack> destination, InventoryListKind kind,
        IReadOnlyList<ItemInstance> instances, int requestedCount)
    {
        if (instances == null || instances.Count == 0 || requestedCount <= 0)
            return 0;

        ItemData definition = instances[0]?.Definition;
        if (definition == null || !CanStoreType(kind, definition.Type))
            return 0;

        int count = Mathf.Min(requestedCount, instances.Count);
        for (int i = 0; i < count; i++)
        {
            if (instances[i] == null || instances[i].Definition != definition)
                return 0;
        }

        int available = GetAvailableCapacity(destination, definition);
        count = Mathf.Min(count, available);

        int cursor = 0;
        for (int i = 0; i < destination.Count && cursor < count; i++)
        {
            InventoryStack slot = destination[i];
            if (slot == null || slot.IsEmpty || slot.Definition != definition)
                continue;

            cursor += slot.Add(instances, cursor, count - cursor);
        }

        for (int i = 0; i < destination.Count && cursor < count; i++)
        {
            if (destination[i] != null && !destination[i].IsEmpty)
                continue;

            InventoryStack slot = new InventoryStack(definition);
            cursor += slot.Add(instances, cursor, count - cursor);
            destination[i] = slot;
        }

        return cursor;
    }

    private static int GetAvailableCapacity(List<InventoryStack> slots, ItemData definition)
    {
        int available = 0;
        foreach (InventoryStack slot in slots)
        {
            if (slot == null || slot.IsEmpty)
                available += definition.MaximumStackSize;
            else if (slot.Definition == definition)
                available += definition.MaximumStackSize - slot.Count;
        }
        return available;
    }

    private static bool CanStoreType(InventoryListKind kind, ItemType itemType)
    {
        return kind switch
        {
            InventoryListKind.EquipmentQuickSlots => itemType == ItemType.Equipment,
            InventoryListKind.ConsumableQuickSlots => itemType == ItemType.Consumable,
            InventoryListKind.General => true,
            _ => false
        };
    }

    private List<InventoryStack> GetMutableSlots(InventoryListKind kind)
    {
        return kind switch
        {
            InventoryListKind.EquipmentQuickSlots => equipmentQuickSlots,
            InventoryListKind.ConsumableQuickSlots => consumableQuickSlots,
            _ => generalSlots
        };
    }

    private static void SortSlots(List<InventoryStack> slots)
    {
        if (slots == null || slots.Count == 0)
            return;

        int capacity = slots.Count;

        List<ItemInstance> allInstances = slots
            .Where(slot => slot != null && !slot.IsEmpty)
            .SelectMany(slot => slot.Instances)
            .Where(instance => instance != null && instance.Definition != null)
            .ToList();

        List<InventoryStack> sortedStacks = new List<InventoryStack>();
        var groups = allInstances
            .GroupBy(instance => instance.Definition.DefinitionId)
            .OrderBy(group => group.First().Definition.Type)
            .ThenBy(group => group.Key, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            ItemData definition = group.First().Definition;
            List<ItemInstance> instances = group.ToList();
            for (int offset = 0; offset < instances.Count; offset += definition.MaximumStackSize)
            {
                int length = Mathf.Min(definition.MaximumStackSize, instances.Count - offset);
                InventoryStack stack = new InventoryStack(definition);
                stack.Add(instances, offset, length);
                sortedStacks.Add(stack);
            }
        }

        slots.Clear();
        slots.AddRange(sortedStacks);
        while (slots.Count < capacity)
            slots.Add(new InventoryStack());
    }

    private static void EnsureListCapacity(List<InventoryStack> slots, int capacity)
    {
        while (slots.Count < capacity)
            slots.Add(new InventoryStack());
    }

    private static int ResizeOrKeep(List<InventoryStack> slots, int requestedCapacity)
    {
        if (slots.Count > requestedCapacity)
        {
            for (int i = requestedCapacity; i < slots.Count; i++)
            {
                if (slots[i] != null && !slots[i].IsEmpty)
                    return slots.Count;
            }
            slots.RemoveRange(requestedCapacity, slots.Count - requestedCapacity);
        }
        else
        {
            EnsureListCapacity(slots, requestedCapacity);
        }

        return requestedCapacity;
    }

    private void NotifyChanged()
    {
        InventoryChanged?.Invoke();
        inventoryUI?.Refresh();
    }

    private static void DisposeAction(InputAction action)
    {
        if (action == null)
            return;
        action.Disable();
        action.Dispose();
    }
}
