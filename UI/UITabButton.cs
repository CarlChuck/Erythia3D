using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public enum TabWindowType 
{
    None,
    Character,
    Skills,
    Crafting,
    Roster,
    Social,
    Chat
}

public class UITabButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler
{
    [SerializeField] private GameObject Glow;
    [SerializeField] private GameObject RPart;
    [SerializeField] private GameObject LPart;
    [SerializeField] private TabWindowType windowToToggle = TabWindowType.None;
    [SerializeField] private UIManager uiManagerInstance;

    private static List<UITabButton> allRegisteredTabs = new List<UITabButton>();
    public static IReadOnlyList<UITabButton> AllRegisteredTabs => allRegisteredTabs.AsReadOnly();

    public TabWindowType AssociatedWindowType => windowToToggle;

    void Awake()
    {
        if (!allRegisteredTabs.Contains(this))
        {
            allRegisteredTabs.Add(this);
        }
        RPart.SetActive(false);
        LPart.SetActive(false);
        Glow.SetActive(false);
    }

    void OnDestroy()
    {
        if (allRegisteredTabs.Contains(this))
        {
            allRegisteredTabs.Remove(this);
        }
    }

    public void SetSelectedLook(bool isSelected)
    {
        if (Glow != null) 
        {
            Glow.SetActive(isSelected);
        }
        if (RPart != null) 
        {
            RPart.SetActive(isSelected);
        }
        if (LPart != null) 
        {
            LPart.SetActive(isSelected);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (RPart != null) 
        {
            RPart.SetActive(true);
        }
        if (LPart != null) 
        {
            LPart.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (RPart != null) 
        {
            RPart.SetActive(false);
        }
        if (LPart != null) 
        {
            LPart.SetActive(false);
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (uiManagerInstance != null)
        {
            uiManagerInstance.RequestWindowToggle(windowToToggle, this);
        }
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SetSelectedLook(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {            
            return;
        }
        EventSystem.current.SetSelectedGameObject(gameObject, eventData);
    }
}
