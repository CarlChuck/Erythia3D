using UnityEngine;
using TMPro;

public class UITooltip : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI headerText; // For item name
    [SerializeField] private TextMeshProUGUI contentText; // For description, stats, etc.
    [SerializeField] private RectTransform rectTransform; // To get dimensions for positioning
    [SerializeField] private CanvasGroup canvasGroup; // Optional: for fading

    void Awake()
    {
        if (rectTransform == null) 
        {
            rectTransform = GetComponent<RectTransform>();
        }
        if (canvasGroup == null) 
        {
            canvasGroup = GetComponent<CanvasGroup>(); // Optional
        }
        Hide(); // Start hidden
    }

    public void SetText(string header, string content)
    {
        if (headerText != null) 
        {
            headerText.text = header;
        }
        if (contentText != null) 
        {
            contentText.text = content;
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        if(canvasGroup != null) 
        {
            canvasGroup.alpha = 1f;
        } 
    }

    public void Hide()
    {
         if(canvasGroup != null) 
         {
            canvasGroup.alpha = 0f;
         } 
        gameObject.SetActive(false);
    }

    public Vector2 GetSize()
    {
        return rectTransform != null ? rectTransform.sizeDelta : Vector2.zero;
    }
} 