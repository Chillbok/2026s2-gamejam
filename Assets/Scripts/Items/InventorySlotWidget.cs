using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class InventorySlotWidget : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private PlayerInventoryUI owner;
    private InventoryListKind listKind;
    private int slotIndex;
    private Image background;
    private Text label;
    private Image icon;
    private CanvasGroup canvasGroup;

    public void Initialize(PlayerInventoryUI inventoryUI, InventoryListKind kind, int index)
    {
        owner = inventoryUI;
        listKind = kind;
        slotIndex = index;
        background = GetComponent<Image>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.62f);
        iconRect.anchorMax = new Vector2(0.5f, 0.62f);
        iconRect.sizeDelta = new Vector2(36f, 36f);
        icon = iconObject.GetComponent<Image>();
        icon.raycastTarget = false;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(3f, 2f);
        labelRect.offsetMax = new Vector2(-3f, -2f);
        label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 13;
        label.alignment = TextAnchor.LowerCenter;
        label.color = Color.white;
        label.raycastTarget = false;

        SetStack(null);
    }

    public void SetStack(InventoryStack stack)
    {
        if (label == null || icon == null || background == null)
            return;

        if (stack == null || stack.IsEmpty)
        {
            label.text = $"{slotIndex + 1}\n빈 슬롯";
            icon.enabled = false;
            background.color = new Color(0.2f, 0.24f, 0.29f, 1f);
            return;
        }

        label.text = $"{stack.Definition.DisplayName}\nx{stack.Count}";
        icon.sprite = stack.Definition.Icon;
        icon.enabled = icon.sprite != null;
        background.color = new Color(0.28f, 0.33f, 0.4f, 1f);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        owner?.BeginDrag(listKind, slotIndex);
        if (background != null)
            background.color = new Color(0.75f, 0.63f, 0.33f, 1f);
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (owner != null)
            owner.CompleteDrag(listKind, slotIndex, eventData);
        canvasGroup.alpha = 1f;
        owner?.Refresh();
    }
}
