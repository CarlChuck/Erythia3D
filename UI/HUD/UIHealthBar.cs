using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIHealthBar : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image healthFillImage;
    [SerializeField] private Image manaFillImage;
    [SerializeField] private TMP_Text healthInfoText;
    [SerializeField] private TMP_Text manaInfoText;

    public void UpdateDisplay(float currentHealth, float maxHealth, float currentMana, float maxMana)
    {
        // Update Health Image Fill
        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = (maxHealth > 0) ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        }
        if (manaFillImage != null)
        {
            manaFillImage.fillAmount = (maxMana > 0) ? Mathf.Clamp01(currentMana / maxMana) : 0f;
        }

        // Update Health Text (as percentage)
        if (healthInfoText != null)
        {
            healthInfoText.text = (maxHealth > 0) ? $"{(currentHealth / maxHealth) * 100:F0}%" : "0%";
        }

        // Update Mana Text (as percentage)
        if (manaInfoText != null)
        {
            manaInfoText.text = (maxMana > 0) ? $"{(currentMana / maxMana) * 100:F0}%" : "0%";
        }
    }
}
