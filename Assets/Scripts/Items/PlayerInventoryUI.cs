using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class PlayerInventoryUI : MonoBehaviour
{
    private static Sprite whiteSprite;
    private readonly Dictionary<InventoryListKind, List<InventorySlotWidget>> slotWidgets =
        new Dictionary<InventoryListKind, List<InventorySlotWidget>>();
    private readonly List<Image> wheelSlotImages = new List<Image>();
    private readonly List<Text> wheelSlotLabels = new List<Text>();

    private PlayerInventory inventory;
    private Canvas inventoryCanvas;
    private GameObject inventoryPanel;
    private GameObject quickWheelPanel;
    private Text statusText;
    private Text hudStatusText;
    private Text heldItemText;
    private RectTransform quickWheelRect;
    private IReadOnlyList<InventoryStack> activeWheelSlots;
    private int highlightedQuickSlot = -1;
    private InventoryListKind activeWheelKind;
    private float statusExpiresAt;

    public void Initialize(PlayerInventory playerInventory)
    {
        inventory = playerInventory;
        EnsureCanvasAndEventSystem();
        BuildInventoryPanel();
        BuildQuickWheel();
        Refresh();
    }

    public void SetInventoryOpen(bool isOpen)
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(isOpen);

        if (isOpen)
            Refresh();
    }

    public void ShowStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        if (hudStatusText != null)
        {
            hudStatusText.text = message;
            hudStatusText.gameObject.SetActive(true);
            statusExpiresAt = Time.unscaledTime + 2.5f;
        }
    }

    private void Update()
    {
        if (hudStatusText != null && hudStatusText.gameObject.activeSelf && Time.unscaledTime >= statusExpiresAt)
            hudStatusText.gameObject.SetActive(false);
    }

    public void Refresh()
    {
        if (inventory == null)
            return;

        foreach (KeyValuePair<InventoryListKind, List<InventorySlotWidget>> entry in slotWidgets)
        {
            IReadOnlyList<InventoryStack> slots = inventory.GetSlots(entry.Key);
            for (int i = 0; i < entry.Value.Count; i++)
                entry.Value[i].SetStack(i < slots.Count ? slots[i] : null);
        }

        if (quickWheelPanel != null && quickWheelPanel.activeSelf)
            UpdateQuickSlotWheel(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);

        if (heldItemText != null)
        {
            heldItemText.gameObject.SetActive(inventory.HasHeldItem);
            heldItemText.text = inventory.HasHeldItem
                ? $"손에 든 아이템: {inventory.HeldItemData.DisplayName} (E 짧게: 넣기 / 2초 이상: 사용)"
                : string.Empty;
        }
    }

    public void BeginDrag(InventoryListKind sourceKind, int sourceIndex)
    {
        IReadOnlyList<InventoryStack> slots = inventory.GetSlots(sourceKind);
        if (sourceIndex < 0 || sourceIndex >= slots.Count || slots[sourceIndex] == null || slots[sourceIndex].IsEmpty)
            return;

        ShowStatus("스택 전체를 다른 목록으로 옮기거나 목록 밖에 놓아 버리세요.");
    }

    public void CompleteDrag(InventoryListKind sourceKind, int sourceIndex, PointerEventData eventData)
    {
        List<RaycastResult> raycastResults = new List<RaycastResult>();
        if (EventSystem.current != null)
            EventSystem.current.RaycastAll(eventData, raycastResults);

        InventoryListDropTarget target = null;
        foreach (RaycastResult result in raycastResults)
        {
            target = result.gameObject.GetComponentInParent<InventoryListDropTarget>();
            if (target != null)
                break;
        }

        if (target != null)
        {
            bool moved = inventory.TryMoveWholeStack(sourceKind, sourceIndex, target.Kind);
            ShowStatus(moved ? "스택을 이동했습니다." : "이동할 수 없습니다. 원래 위치를 유지합니다.");
        }
        else
        {
            bool dropped = inventory.DropWholeStack(sourceKind, sourceIndex);
            ShowStatus(dropped ? "아이템을 플레이어 위치에 버렸습니다." : "버릴 아이템이 없습니다.");
        }
    }

    public void ShowQuickSlotWheel(InventoryListKind kind, IReadOnlyList<InventoryStack> slots)
    {
        activeWheelKind = kind;
        activeWheelSlots = slots;
        highlightedQuickSlot = -1;
        quickWheelPanel.SetActive(true);
        RefreshWheelContents();
        UpdateQuickSlotWheel(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);
    }

    public void UpdateQuickSlotWheel(Vector2 screenPosition)
    {
        if (quickWheelPanel == null || !quickWheelPanel.activeSelf || activeWheelSlots == null)
            return;

        Vector2 center = RectTransformUtility.WorldToScreenPoint(null, quickWheelRect.position);
        Vector2 direction = screenPosition - center;
        int nextIndex = -1;

        if (direction.sqrMagnitude >= 36f && activeWheelSlots.Count > 0)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
            angle = (angle % 360f + 360f) % 360f;
            nextIndex = Mathf.Clamp(Mathf.FloorToInt(angle / 360f * activeWheelSlots.Count), 0,
                activeWheelSlots.Count - 1);
        }

        highlightedQuickSlot = nextIndex;
        for (int i = 0; i < wheelSlotImages.Count; i++)
        {
            bool selected = i == highlightedQuickSlot;
            wheelSlotImages[i].color = selected
                ? new Color(0.95f, 0.68f, 0.18f, 1f)
                : new Color(0.18f, 0.21f, 0.27f, 0.96f);
        }
    }

    public int GetHighlightedQuickSlot()
    {
        return highlightedQuickSlot;
    }

    public void HideQuickSlotWheel()
    {
        if (quickWheelPanel != null)
            quickWheelPanel.SetActive(false);
        activeWheelSlots = null;
        highlightedQuickSlot = -1;
    }

    private void EnsureCanvasAndEventSystem()
    {
        GameObject canvasObject = new GameObject("RuntimeInventoryCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        inventoryCanvas = canvasObject.GetComponent<Canvas>();
        inventoryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        inventoryCanvas.overrideSorting = true;
        inventoryCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("InventoryEventSystem", typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().actionsAsset =
                GetComponent<PlayerInput>()?.actions;
        }

    }

    private void BuildInventoryPanel()
    {
        inventoryPanel = CreatePanel("InventoryPanel", inventoryCanvas.transform, new Vector2(1180f, 690f),
            new Color(0.055f, 0.07f, 0.09f, 0.96f));
        inventoryPanel.SetActive(false);

        CreateText("InventoryTitle", inventoryPanel.transform, "인벤토리  |  Tab: 닫기", 30,
            new Vector2(0f, 300f), new Vector2(1080f, 55f), TextAnchor.MiddleCenter, Color.white);

        CreateColumn(InventoryListKind.EquipmentQuickSlots, "사용 장비 퀵슬롯 (1)", -425f, 260f, 2);
        CreateColumn(InventoryListKind.ConsumableQuickSlots, "소모품 퀵슬롯 (2)", -145f, 260f, 2);
        CreateColumn(InventoryListKind.General, "일반 인벤토리", 250f, 510f, 6);

        statusText = CreateText("InventoryStatus", inventoryPanel.transform,
            "아이템은 기본적으로 일반 인벤토리에 들어갑니다.", 17, new Vector2(0f, -305f),
            new Vector2(1080f, 35f), TextAnchor.MiddleCenter, new Color(0.82f, 0.86f, 0.9f));

        hudStatusText = CreateText("HUDStatus", inventoryCanvas.transform, string.Empty, 20,
            new Vector2(0f, 28f), new Vector2(700f, 40f), TextAnchor.MiddleCenter, Color.white);
        SetBottomAnchored(hudStatusText.rectTransform, new Vector2(0f, 28f), new Vector2(700f, 40f));
        hudStatusText.gameObject.SetActive(false);

        heldItemText = CreateText("HeldItemStatus", inventoryCanvas.transform, string.Empty, 18,
            new Vector2(0f, 76f), new Vector2(700f, 36f), TextAnchor.MiddleCenter, Color.white);
        SetBottomAnchored(heldItemText.rectTransform, new Vector2(0f, 76f), new Vector2(700f, 36f));
        heldItemText.gameObject.SetActive(false);
    }

    private void CreateColumn(InventoryListKind kind, string title, float x, float width, int columns)
    {
        GameObject column = CreatePanel($"{kind}List", inventoryPanel.transform,
            new Vector2(width, 535f), new Color(0.12f, 0.15f, 0.19f, 0.94f));
        RectTransform columnRect = column.GetComponent<RectTransform>();
        columnRect.anchoredPosition = new Vector2(x, -15f);

        InventoryListDropTarget dropTarget = column.AddComponent<InventoryListDropTarget>();
        dropTarget.Initialize(kind);

        CreateText("ListTitle", column.transform, title, 19, new Vector2(0f, 235f),
            new Vector2(width - 20f, 40f), TextAnchor.MiddleCenter, Color.white);

        GameObject slotsRoot = new GameObject("Slots", typeof(RectTransform));
        slotsRoot.transform.SetParent(column.transform, false);
        RectTransform slotsRect = slotsRoot.GetComponent<RectTransform>();
        slotsRect.anchorMin = new Vector2(0f, 0f);
        slotsRect.anchorMax = new Vector2(1f, 1f);
        slotsRect.offsetMin = new Vector2(12f, 12f);
        slotsRect.offsetMax = new Vector2(-12f, -58f);

        GridLayoutGroup grid = slotsRoot.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(columns == 6 ? 74f : 102f, 82f);
        grid.spacing = new Vector2(7f, 8f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;

        IReadOnlyList<InventoryStack> slots = inventory.GetSlots(kind);
        List<InventorySlotWidget> widgets = new List<InventorySlotWidget>(slots.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            GameObject slotObject = CreatePanel($"Slot_{i + 1}", slotsRoot.transform,
                new Vector2(100f, 80f), new Color(0.2f, 0.24f, 0.29f, 1f));
            InventorySlotWidget widget = slotObject.AddComponent<InventorySlotWidget>();
            widget.Initialize(this, kind, i);
            widgets.Add(widget);
        }
        slotWidgets[kind] = widgets;
    }

    private void BuildQuickWheel()
    {
        quickWheelPanel = CreatePanel("QuickSlotWheel", inventoryCanvas.transform, new Vector2(380f, 380f),
            new Color(0.035f, 0.045f, 0.06f, 0.92f));
        quickWheelRect = quickWheelPanel.GetComponent<RectTransform>();
        quickWheelPanel.SetActive(false);

        CreateText("WheelTitle", quickWheelPanel.transform, "퀵슬롯", 22, new Vector2(0f, 0f),
            new Vector2(160f, 42f), TextAnchor.MiddleCenter, Color.white);

        int slotCount = Mathf.Max(inventory.GetSlots(InventoryListKind.EquipmentQuickSlots).Count,
            inventory.GetSlots(InventoryListKind.ConsumableQuickSlots).Count);
        for (int i = 0; i < slotCount; i++)
        {
            float angle = (90f - i * 360f / Mathf.Max(1, slotCount)) * Mathf.Deg2Rad;
            Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 138f;
            GameObject button = CreatePanel($"WheelSlot_{i + 1}", quickWheelPanel.transform,
                new Vector2(92f, 68f), new Color(0.18f, 0.21f, 0.27f, 0.96f));
            button.GetComponent<RectTransform>().anchoredPosition = position;
            wheelSlotImages.Add(button.GetComponent<Image>());
            wheelSlotLabels.Add(CreateText("Label", button.transform, "빈 슬롯", 15,
                Vector2.zero, new Vector2(86f, 62f), TextAnchor.MiddleCenter, Color.white));
        }
    }

    private void RefreshWheelContents()
    {
        for (int i = 0; i < wheelSlotLabels.Count; i++)
        {
            InventoryStack stack = activeWheelSlots != null && i < activeWheelSlots.Count
                ? activeWheelSlots[i]
                : null;
            wheelSlotLabels[i].text = stack == null || stack.IsEmpty
                ? $"{i + 1}\n빈 슬롯"
                : $"{i + 1}\n{stack.Definition.DisplayName} x{stack.Count}";
        }
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        Image image = panel.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = color;
        return panel;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        return whiteSprite;
    }

    private static Text CreateText(string name, Transform parent, string value, int fontSize,
        Vector2 anchoredPosition, Vector2 size, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static void SetBottomAnchored(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
