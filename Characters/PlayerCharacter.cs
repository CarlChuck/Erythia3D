using UnityEngine;
using UnityEngine.TextCore.Text;

public class PlayerCharacter : StatBlock
{
    [SerializeField] private EquipmentProfile equipment;
    [SerializeField] private Inventory inventory;
    private GameObject characterModel;
    private int encumberence;
    private int characterID;
    private string characterTitle;
    private int currentZone; 
    
    private Interactable currentInteractable; // Variable to store the interactable object


    #region Setup and Initialization
    public void AddModel(GameObject playerModel)
    {
        characterModel = playerModel;
        characterModel.transform.SetParent(transform);
        characterModel.SetActive(false);
    }
    public void ActivateModel(bool isActive)
    {
        if (characterModel != null)
        {
            characterModel.SetActive(isActive);
        }
    }
    public void SetUpCharacter(string newCharacterName, int newCharacterID, string title, int zoneID, int race, int face, int gender, int combatxp, int craftingxp, int arcaneexp, int spiritxp, int veilexp)
    {
        SetSpecies(CharactersManager.Instance.GetSpeciesByID(race));
        SetGender(gender);
        SetCharacterName(newCharacterName);
        characterID = newCharacterID;
        characterTitle = title;
        currentZone = zoneID;
        SetupStatBlock();
        SetUpInventory();
        SetUpEquipment();
        SetEncumberence();
    }
    private void SetUpInventory() 
    {
        inventory.SetupInventory();
    }
    private void SetUpEquipment() 
    { 
        equipment.SetupEquipmentProfile(inventory);
    }
    private void SetEncumberence()
    {
        encumberence = equipment.GetTotalWeight();
        encumberence += inventory.GetTotalWeight();
    }
    public int GetCharacterID()
    {
        return characterID;
    }
    #endregion

    public void InteractWithTarget()
    {
        // Define the ray starting point and direction
        Vector3 rayOrigin = transform.position + Vector3.up; // Slightly above the player's position
        Vector3 rayDirection = transform.forward;

        // Perform the raycast
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, 5f))
        {
            // Check if the hit object has an Interactable component
            Interactable interactable = hit.collider.GetComponent<Interactable>();
            if (interactable != null)
            {
                // Store the interactable object
                currentInteractable = interactable;
                Debug.Log($"Interactable found: {interactable.name}");
                currentInteractable.OnInteract(this); // Call the OnInteract method on the interactable object
            }
            else
            {
                Debug.Log("No interactable object found.");
                currentInteractable = null;
            }
        }
        else
        {
            Debug.Log("No object detected in front of the player.");
            currentInteractable = null;
        }
    }

    public void OnMiningHit()
    {
        //Fire the mining hit event
        //TODO Animation for Mining
    }
    public void OnWoodCuttingHit()
    {
        //Fire the woodcutting hit event
        //TODO Animation for Woodcutting
    }
    public void OnHarvestHit()
    {
        //Fire the harvesting hit event
        //TODO Animation for Harvesting
    }
    public EquipmentProfile GetEquipmentProfile()
    {
        return equipment;
    }
}
