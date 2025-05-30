using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Linq;

/// <summary>
/// Helper class for PlayerManager to handle character-related operations
/// Uses PlayerManager's RPC methods for server communication
/// </summary>
public class CharacterDataHandler
{
    private PlayerManager playerManager;

    public CharacterDataHandler(PlayerManager manager)
    {
        playerManager = manager;
    }

    #region Login Operations
    public async Task<bool> LoginAsync()
    {
        try
        {
            Debug.Log("CharacterDataHandler: Requesting login from server...");
            
            // Use NetworkRequestManager for cleaner request handling
            LoginResult result = await playerManager.requestManager.SendLoginRequestAsync(
                playerManager.SteamID,
                playerManager.AccountID,
                playerManager.AccountName,
                playerManager.Email,
                playerManager.IPAddress,
                playerManager.Language
            );
            
            if (result.Success)
            {
                return ProcessLoginResult(result);
            }
            else
            {
                Debug.LogError($"CharacterDataHandler: Login failed: {result.ErrorMessage}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"CharacterDataHandler: Exception during LoginAsync: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    private bool ProcessLoginResult(LoginResult result)
    {
        try
        {
            // Update PlayerManager's account info
            playerManager.AccountID = result.AccountID;
            playerManager.AccountName = result.AccountName;
            playerManager.SteamID = result.SteamID;
            
            Debug.Log($"CharacterDataHandler: Login successful. AccountID: {playerManager.AccountID}, AccountName: {playerManager.AccountName}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"CharacterDataHandler: Exception during ProcessLoginResult: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }
    #endregion

    #region Character Management
    public async Task LoadCharactersAsync()
    {
        if (playerManager.AccountID <= 0)
        {
            Debug.LogError("CharacterDataHandler: Cannot load characters: Invalid AccountID.");
            return;
        }

        Debug.Log("CharacterDataHandler: Requesting character list from server...");
        
        // Use NetworkRequestManager for cleaner request handling
        CharacterListResult result = await playerManager.requestManager.SendCharacterListRequestAsync(playerManager.AccountID);
        
        if (result.Success)
        {
            await ProcessCharacterListResult(result);
        }
        else
        {
            Debug.LogError($"CharacterDataHandler: Character list request failed: {result.ErrorMessage}");
        }
    }

    private async Task ProcessCharacterListResult(CharacterListResult result)
    {
        try
        {
            ClearPlayerListExceptSelected();

            Debug.Log($"CharacterDataHandler: Processing {result.Characters.Length} character data entries from server.");

            foreach (CharacterData characterData in result.Characters)
            {
                // Check if character already loaded (avoid duplicates)
                bool alreadyExists = CheckIfCharacterExists(characterData.CharID);
                if (alreadyExists) continue;

                // Load FamilyName ONLY if not already set
                if (string.IsNullOrEmpty(playerManager.FamilyName) && !string.IsNullOrEmpty(characterData.FamilyName))
                {
                    playerManager.FamilyName = characterData.FamilyName;
                }

                // Instantiate and setup character
                PlayerStatBlock newCharacter = InstantiateCharacter(characterData);
                if (newCharacter != null)
                {
                    playerManager.PlayerCharacters.Add(newCharacter);

                    // Set as selected if none currently selected
                    if (playerManager.SelectedPlayerCharacter == null)
                    {
                        Debug.Log($"CharacterDataHandler: Setting first loaded character as selected: {characterData.Name}");
                        playerManager.SelectedPlayerCharacter = newCharacter;
                    }
                }
            }

            // Ensure selected character is in list
            EnsureSelectedCharacterInList();

            Debug.Log($"CharacterDataHandler: Finished processing character list. Final count: {playerManager.PlayerCharacters.Count}. Selected: {playerManager.SelectedPlayerCharacter?.GetCharacterName() ?? "None"}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"CharacterDataHandler: Exception during ProcessCharacterListResult: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public async Task CreateCharacterAsync(string characterName, int charRace, int charGender, int charFace)
    {
        if (string.IsNullOrEmpty(playerManager.FamilyName) || string.IsNullOrEmpty(characterName))
        {
            Debug.LogError("CharacterDataHandler: Character or Family Name cannot be empty");
            return;
        }

        if (playerManager.AccountID <= 0)
        {
            Debug.LogError("CharacterDataHandler: Cannot create character: Invalid AccountID.");
            return;
        }

        Debug.Log($"CharacterDataHandler: Attempting to create character: {characterName}");
        
        // Use CharactersManager directly for character creation
        bool created = await CharactersManager.Instance.CreateNewCharacterAsync(
            playerManager.AccountID, 
            playerManager.FamilyName, 
            characterName, 
            null, 
            1, 
            charRace, 
            charGender, 
            charFace
        );

        if (created)
        {
            Debug.Log("CharacterDataHandler: Character creation successful, reloading character list...");
            await LoadCharactersAsync();
            
            if (playerManager.SelectedPlayerCharacter != null && playerManager.UIManager != null)
            {
                playerManager.UIManager.SetupUI(playerManager.SelectedPlayerCharacter);
            }
        }
        else
        {
            Debug.LogError($"CharacterDataHandler: Failed to create character: {characterName}");
        }
    }
    #endregion

    #region Helper Methods
    private bool CheckIfCharacterExists(int characterID)
    {
        if (playerManager.SelectedPlayerCharacter != null && playerManager.SelectedPlayerCharacter.GetCharacterID() == characterID)
        {
            Debug.Log($"CharacterDataHandler: Character ID {characterID} is already the selected character.");
            if (!playerManager.PlayerCharacters.Contains(playerManager.SelectedPlayerCharacter))
            {
                playerManager.PlayerCharacters.Add(playerManager.SelectedPlayerCharacter);
            }
            return true;
        }
        else if (playerManager.PlayerCharacters.Any(pc => pc.GetCharacterID() == characterID))
        {
            Debug.Log($"CharacterDataHandler: Character ID {characterID} already exists in the loaded list.");
            return true;
        }
        return false;
    }

    private PlayerStatBlock InstantiateCharacter(CharacterData characterData)
    {
        if (playerManager.CharacterPrefab == null || playerManager.CharListParent == null)
        {
            Debug.LogError("CharacterDataHandler: Missing prefabs or parent references for character instantiation!");
            return null;
        }

        Debug.Log($"CharacterDataHandler: Instantiating new character object for ID: {characterData.CharID}, Name: {characterData.Name}");
        PlayerStatBlock newCharacter = GameObject.Instantiate(playerManager.CharacterPrefab, playerManager.CharListParent.transform).GetComponent<PlayerStatBlock>();
        
        if (newCharacter == null)
        {
            Debug.LogError("CharacterDataHandler: Failed to get PlayerStatBlock component from prefab!");
            return null;
        }

        newCharacter.SetUpCharacter(
            characterData.Name, 
            characterData.CharID, 
            characterData.Title, 
            characterData.ZoneID, 
            characterData.Race, 
            characterData.Face, 
            characterData.Gender, 
            characterData.CombatExp, 
            characterData.CraftingExp, 
            characterData.ArcaneExp, 
            characterData.SpiritExp, 
            characterData.VeilExp,
            characterData.SpeciesStrength,
            characterData.SpeciesDexterity,
            characterData.SpeciesConstitution,
            characterData.SpeciesIntelligence,
            characterData.SpeciesSpirit
        );

        return newCharacter;
    }

    private void ClearPlayerListExceptSelected()
    {
        if (playerManager.PlayerCharacters == null) 
        { 
            // This shouldn't happen with property access, but safe fallback
            Debug.LogError("CharacterDataHandler: PlayerCharacters list is null");
            return;
        }

        List<PlayerStatBlock> toRemove = new List<PlayerStatBlock>();
        foreach (PlayerStatBlock character in playerManager.PlayerCharacters)
        {
            if (character == null) continue;
            if (playerManager.SelectedPlayerCharacter == null || character.GetInstanceID() != playerManager.SelectedPlayerCharacter.GetInstanceID())
            {
                toRemove.Add(character);
            }
        }

        foreach (PlayerStatBlock characterToRemove in toRemove)
        {
            playerManager.PlayerCharacters.Remove(characterToRemove);
            if (characterToRemove.gameObject != null)
            {
                GameObject.Destroy(characterToRemove.gameObject);
                Debug.Log($"CharacterDataHandler: Destroyed non-selected character object: {characterToRemove.GetCharacterName()}");
            }
        }
    }

    private void EnsureSelectedCharacterInList()
    {
        if (playerManager.SelectedPlayerCharacter != null && !playerManager.PlayerCharacters.Contains(playerManager.SelectedPlayerCharacter))
        {
            Debug.LogWarning("CharacterDataHandler: Selected character was not found in list during processing. Adding now.");
            playerManager.PlayerCharacters.Add(playerManager.SelectedPlayerCharacter);
        }
        else if (playerManager.SelectedPlayerCharacter == null && playerManager.PlayerCharacters.Count > 0)
        {
            Debug.LogWarning("CharacterDataHandler: No character was selected during load, selecting first from list.");
            playerManager.SelectedPlayerCharacter = playerManager.PlayerCharacters[0];
        }
    }
    #endregion
} 