using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Linq;

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
    public async Task ProcessCharacterListResult(CharacterListResult result)
    {
        try
        {
            ClearPlayerListExceptSelected();

            foreach (CharacterData characterData in result.Characters)
            {
                // Check if character already loaded (avoid duplicates)
                if (CheckIfCharacterExists(characterData.CharID)) 
                { 
                    continue; 
                }

                // Load FamilyName ONLY if not already set
                if (string.IsNullOrEmpty(playerManager.FamilyName) && !string.IsNullOrEmpty(characterData.FamilyName))
                {
                    playerManager.FamilyName = characterData.FamilyName;
                }

                PlayerStatBlock newCharacter = InstantiateCharacter(characterData);
                if (newCharacter != null)
                {
                    playerManager.PlayerCharacters.Add(newCharacter);

                    //Sets the first character loaded as the selected character if none is selected
                    if (playerManager.SelectedPlayerCharacter == null)
                    {
                        playerManager.SelectedPlayerCharacter = newCharacter;
                    }
                }
            }
            EnsureSelectedCharacterInList();
        }
        catch (Exception ex)
        {
            Debug.LogError($"CharacterDataHandler: Exception during ProcessCharacterListResult: {ex.Message}\n{ex.StackTrace}");
        }
    }
    public async Task CreateCharacterAsync(string characterName, int charRace, int charGender, int charFace)
    {
        int charStartingZone = 1; // Use the player's starting zone
        charStartingZone = GetStartingZoneByRace(charRace);
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
        
        // Use CharactersManager directly for character creation
        bool created = await CharactersManager.Instance.CreateNewCharacterAsync(
            playerManager.AccountID, 
            playerManager.FamilyName, 
            characterName, 
            null,
            charStartingZone, 
            charRace, 
            charGender, 
            charFace
        );

        if (created)
        {
            await LoadCharactersAsync();
            
            // Only update UI on client side
            if (playerManager.SelectedPlayerCharacter != null && playerManager.UIManager != null && !playerManager.IsServer)
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
            if (!playerManager.PlayerCharacters.Contains(playerManager.SelectedPlayerCharacter))
            {
                playerManager.PlayerCharacters.Add(playerManager.SelectedPlayerCharacter);
            }
            return true;
        }
        else if (playerManager.PlayerCharacters.Any(pc => pc.GetCharacterID() == characterID))
        {
            return true;
        }
        return false;
    }
    private PlayerStatBlock InstantiateCharacter(CharacterData characterData)
    {
        PlayerStatBlock newCharacter = GameObject.Instantiate(playerManager.CharacterPrefab, playerManager.CharListParent.transform).GetComponent<PlayerStatBlock>();
        
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
            }
        }
    }
    private void EnsureSelectedCharacterInList()
    {
        if (playerManager.SelectedPlayerCharacter != null && !playerManager.PlayerCharacters.Contains(playerManager.SelectedPlayerCharacter))
        {
            //Debug.LogWarning("CharacterDataHandler: Selected character was not found in list during processing. Adding now.");
            playerManager.PlayerCharacters.Add(playerManager.SelectedPlayerCharacter);
        }
        else if (playerManager.SelectedPlayerCharacter == null && playerManager.PlayerCharacters.Count > 0)
        {
            //Debug.LogWarning("CharacterDataHandler: No character was selected during load, selecting first from list.");
            playerManager.SelectedPlayerCharacter = playerManager.PlayerCharacters[0];
        }
    }
    public int GetStartingZoneByRace(int race)
    {
        int toReturn = 1; // Default starting zone
        if (playerManager.SelectedPlayerCharacter != null)
        {
            switch (playerManager.SelectedPlayerCharacter.GetSpecies())
            {
                case 1: // Aelystian
                    toReturn = 1; // IthoriaSouth
                    break;
                case 2: // Anurian
                    toReturn = 2; // ShiftingWastes
                    break;
                case 3: // Getaii
                    toReturn = 3; // PurrgishWoodlands
                    break;
                case 4: // Hivernian
                    toReturn = 4; // HiverniaForestNorth
                    break;
                case 5: // Kasmiran
                    toReturn = 5; // CanaGrasslands
                    break;
                case 6: // Meliviaen
                    toReturn = 6; // GreatForestSouth
                    break;
                case 7: // Qadian
                    toReturn = 7; // QadianDelta
                    break;
                case 8: // Tkyan
                    toReturn = 8; // TkyanDepths
                    break;
                case 9: // Valahoran
                    toReturn = 9; // ValahorSouth
                    break;
                default:
                    toReturn = 1; // IthoriaSouth - can be default for now
                    break;
            }
        }

        return toReturn;
    }
    #endregion
} 