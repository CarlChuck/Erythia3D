using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Simple singleton to manage the state of the item being dragged
public class UIDragDropManager : MonoBehaviour
{
    public static UIDragDropManager Instance { get; private set; }

    [SerializeField] private Image dragIconImage; // Assign a UI Image element in the Inspector

    private Item currentlyDraggedItem;
    private Component sourceSlot; // Where the item came from (UIInventorySlot or UIEquipmentSlot)
    private bool isDragging = false;
    private Canvas parentCanvas; // Canvas for position calculations

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // Optional: If needed across scenes
        }

        if (dragIconImage == null)
        {
            Debug.LogError("UIDragDropManager: Drag Icon Image is not assigned!");
            enabled = false;
            return;
        }
        parentCanvas = GetComponentInParent<Canvas>(); // Assumes manager is on the canvas or child
        if (parentCanvas == null)
        {
            Debug.LogError("UIDragDropManager: Cannot find parent Canvas!");
            enabled = false;
            return;
        }

        dragIconImage.gameObject.SetActive(false); // Start hidden
        dragIconImage.raycastTarget = false; // Prevent icon from blocking drops
    }

    public bool StartDragging(Item item, Component source, Sprite icon)
    {
        if (isDragging || item == null || source == null || icon == null)
        {
            return false; // Already dragging or invalid input
        }

        isDragging = true;
        currentlyDraggedItem = item;
        sourceSlot = source;

        dragIconImage.sprite = icon;
        dragIconImage.color = Color.white; // Ensure visible
        dragIconImage.gameObject.SetActive(true);
        UpdateDragIconPosition(Input.mousePosition); // Initial position

        Debug.Log($"Started dragging: {item.ItemName} from {source.GetType().Name}");
        return true;
    }

    public void UpdateDragIconPosition(Vector2 screenPosition)
    {
        if (!isDragging) return;

        // Convert screen position to Canvas local position
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            screenPosition,
            parentCanvas.worldCamera, // Use camera associated with canvas
            out Vector2 localPoint);

        dragIconImage.rectTransform.localPosition = localPoint;
    }

    // Called by the drop target (e.g., UIEquipmentSlot)
    public Item GetDraggedItem()
    {
        return isDragging ? currentlyDraggedItem : null;
    }

    public Component GetSourceSlot()
    {
        return isDragging ? sourceSlot : null;
    }

    // Called when dragging ends (either successful drop or cancelled)
    public void StopDragging(bool dropSuccessful)
    {
        if (!isDragging) return;

        Debug.Log($"Stopped dragging: {currentlyDraggedItem?.ItemName}. Success: {dropSuccessful}");

        // If drop wasn't successful, the source slot needs to handle reverting its display
        if (!dropSuccessful && sourceSlot != null)
        {
            // Tell the source slot the drag failed
            if (sourceSlot is UIInventorySlot invSlot) invSlot.OnDragCancelled();
            if (sourceSlot is UIEquipmentSlot equipSlot) equipSlot.OnDragCancelled();
        }


        isDragging = false;
        currentlyDraggedItem = null;
        sourceSlot = null;
        dragIconImage.gameObject.SetActive(false);
    }
}