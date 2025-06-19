using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Threading;
using Unity.Netcode;

public class CharactersManager : BaseManager
{
    private const string CharacterDataTableName = "CharacterData";

    [Header("Species Templates")]
    [SerializeField] private SpeciesTemplate raceAelystian;
    [SerializeField] private SpeciesTemplate raceAnurian;
    [SerializeField] private SpeciesTemplate raceGetaii;
    [SerializeField] private SpeciesTemplate raceHivernian;
    [SerializeField] private SpeciesTemplate raceKasmiran;
    [SerializeField] private SpeciesTemplate raceMeliviaen;
    [SerializeField] private SpeciesTemplate raceQadian;
    [SerializeField] private SpeciesTemplate raceTkyan;
    [SerializeField] private SpeciesTemplate raceValahoran;

    #region Singleton
    public static CharactersManager Instance;
    protected void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Duplicate CharactersManager detected. Destroying self.");
            Destroy(gameObject);
            return;
        }        
    }
    #endregion

    #region Initialize
    protected override async Task InitializeAsync()
    {
        try
        {
            // 1. Ensure Table Exists
            await EnsureCharacterTableExistsAsync();

            // 2. Mark as Initialized
            isInitialized = true;
            NotifyDataLoaded();
        }
        catch (Exception ex)
        {
            LogError("CharactersManager Initialization Failed", ex);
            isInitialized = false;
        }
    }
    private async Task EnsureCharacterTableExistsAsync()
    {
        bool tableOK = await EnsureTableExistsAsync(CharacterDataTableName, GetCharacterTableDefinition());

        if (!tableOK)
        {
            throw new Exception("Failed to initialize character database table async.");
        }
    }
    #endregion

    public async Task<bool> CreateNewCharacterAsync(int accountID, string familyName, string characterName, int race = 1, int gender = 1, int face = 1)
    {
        string title = "";
        int startingArea = GetStartingZoneByRace(race);
        
        int xLoc = 0; 
        int yLoc = 0;
        int zLoc = 0;
        
        // TODO: Get actual starting location based on startingArea
        
        Dictionary<string, object> values = new Dictionary<string, object>
        {
            {"AccountID", accountID},
            {"FamilyName", familyName},
            {"Name", characterName},
            {"Title", title},
            {"ZoneID", startingArea},
            {"XLoc", xLoc},
            {"YLoc", yLoc},
            {"ZLoc", zLoc},
            {"Race", race},
            {"Gender", gender},
            {"Face", face},
            {"CombatExp", 0},
            {"CraftingExp", 0},
            {"ArcaneExp", 0},
            {"SpiritExp", 0},
            {"VeilExp", 0}
        };

        try
        {
            bool characterAdded = await SaveDataAsync(CharacterDataTableName, values);
            if (characterAdded != false)
            {
                return true;
            }

            LogWarning($"Failed to create new character '{characterName}'. Check DatabaseManager logs or constraints (e.g., duplicate Familyname?).");
            return false;
        }
        catch (Exception ex)
        {
            LogError($"Exception during character creation for '{characterName}'", ex);
            return false;
        }
    }

    public async Task<bool> UpdateCharacterAsync(int characterID, string title, int xLoc, int yLoc, int zLoc, int combatExp, int craftingExp, int arcaneExp, int spiritExp, int veilExp)
    {
        if (characterID <= 0)
        {
            LogError("Invalid CharacterID provided for update.");
            return false;
        }

        Dictionary<string, object> valuesToUpdate = new Dictionary<string, object>
        {
            { "Title", title }, // Allow null/empty titles if needed
            { "XLoc", xLoc },
            { "YLoc", yLoc },
            { "ZLoc", zLoc },
            { "CombatExp", combatExp },
            { "CraftingExp", craftingExp },
            { "ArcaneExp", arcaneExp },
            { "SpiritExp", spiritExp },
            { "VeilExp", veilExp }
        };

        string whereCondition = "`CharID` = @where_CharID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            { "@where_CharID", characterID }
        };

        try
        {
            bool success = await UpdateDataAsync(CharacterDataTableName, valuesToUpdate, whereCondition, whereParams);
            if (success == false)
            {
                LogWarning($"Failed to update character data for CharID: {characterID}. Character might not exist?");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception updating character data for CharID {characterID}", ex);
            return false;
        }
    }

    #region Getters
    public async Task<List<Dictionary<string, object>>> GetCharactersByAccountIDAsync(int accountId)
    {
        string query = $"SELECT * FROM `{CharacterDataTableName}` WHERE AccountID = @AccountID";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@AccountID", accountId } };
        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query, parameters);
            return results;
        }
        catch (Exception ex)
        {
            LogError($"Exception retrieving characters by Account ID {accountId}", ex);
            return new List<Dictionary<string, object>>();
        }
    }
    private static Dictionary<string, string> GetCharacterTableDefinition()
    {
        return new Dictionary<string, string>
        {
            {"CharID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"AccountID", "INT"},
            {"FamilyName", "VARCHAR(255) UNIQUE"},
            {"Name", "VARCHAR(255)"},
            {"Title", "VARCHAR(255)"},
            {"ZoneID", "INT"},
            {"XLoc", "INT"},
            {"YLoc", "INT"},
            {"ZLoc", "INT"},
            {"Race", "INT"},
            {"Gender", "INT"},
            {"Face", "INT"},
            {"CombatExp", "INT"},
            {"CraftingExp", "INT"},
            {"ArcaneExp", "INT"},
            {"SpiritExp", "INT"},
            {"VeilExp", "INT"},
            {"CreationDate", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP"}
        };
    }
    public SpeciesTemplate GetSpeciesByID(int id)
    {
        return id switch
        {
            1 => raceAelystian,
            2 => raceAnurian,
            3 => raceGetaii,
            4 => raceHivernian,
            5 => raceKasmiran,
            6 => raceMeliviaen,
            7 => raceQadian,
            8 => raceTkyan,
            9 => raceValahoran,
            _ => raceAelystian,
        };
    }
    #endregion

    #region Zone and Position Management
    public async Task<Dictionary<string, object>> GetCharacterLocationAsync(int characterID)
    {
        string query = $"SELECT ZoneID, XLoc, YLoc, ZLoc FROM `{CharacterDataTableName}` WHERE CharID = @CharID";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@CharID", characterID } };
        
        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query, parameters);
            
            if (results.Count > 0)
            {
                return results[0]; 
            }
            else
            {
                LogWarning($"No location data found for CharID: {characterID}");
                return null;
            }
        }
        catch (Exception ex)
        {
            LogError($"Exception retrieving location for CharID {characterID}", ex);
            return null;
        }
    }
    public async Task<bool> UpdateCharacterLocationAsync(int characterID, int zoneID, int xLoc, int yLoc, int zLoc)
    {
        if (characterID <= 0)
        {
            LogError("Invalid CharacterID provided for location update.");
            return false;
        }

        Dictionary<string, object> valuesToUpdate = new Dictionary<string, object>
        {
            { "ZoneID", zoneID },
            { "XLoc", xLoc },
            { "YLoc", yLoc },
            { "ZLoc", zLoc }
        };

        string whereCondition = "`CharID` = @where_CharID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            { "@where_CharID", characterID }
        };

        try
        {
            bool success = await UpdateDataAsync(CharacterDataTableName, valuesToUpdate, whereCondition, whereParams);
            if (success == false)
            {
                LogWarning($"Failed to update character location for CharID: {characterID}. Character might not exist?");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception updating character location for CharID {characterID}", ex);
            return false;
        }
    }
    public async Task<bool> UpdateCharacterZoneAsync(int characterID, int zoneID)
    {
        if (characterID <= 0)
        {
            LogError("Invalid CharacterID provided for zone update.");
            return false;
        }

        Dictionary<string, object> valuesToUpdate = new Dictionary<string, object>
        {
            { "ZoneID", zoneID }
        };

        string whereCondition = "`CharID` = @where_CharID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            { "@where_CharID", characterID }
        };

        try
        {
            bool success = await UpdateDataAsync(CharacterDataTableName, valuesToUpdate, whereCondition, whereParams);
            if (success == false)
            {
                LogWarning($"Failed to update character zone for CharID: {characterID}. Character might not exist?");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception updating character zone for CharID {characterID}", ex);
            return false;
        }
    }
    public Dictionary<string, object> GetDefaultCharacterLocation()
    {
        return new Dictionary<string, object>
        {
            { "ZoneID", 1 },
            { "XLoc", 0 }, 
            { "YLoc", 0 },    
            { "ZLoc", 0 } 
        };
    }
    public bool IsDefaultLocation(int xLoc, int yLoc, int zLoc)
    {
        return xLoc == 0 && yLoc == 0 && zLoc == 0;
    }
    private static int GetStartingZoneByRace(int race)
    {
        int toReturn = 1; // Default starting zone
        switch (race)
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
        return toReturn; 
    }
    #endregion
}

[System.Serializable]
public struct CharacterData : INetworkSerializable
{
    public int CharID;
    public int AccountID;
    public string FamilyName;
    public string Name;
    public string Title;
    public int ZoneID;
    public int XLoc;
    public int YLoc;
    public int ZLoc;
    public int Race;
    public int Gender;
    public int Face;
    public int CombatExp;
    public int CraftingExp;
    public int ArcaneExp;
    public int SpiritExp;
    public int VeilExp;
    
    // Species stats - populated from SpeciesTemplate
    public int SpeciesStrength;
    public int SpeciesDexterity;
    public int SpeciesConstitution;
    public int SpeciesIntelligence;
    public int SpeciesSpirit;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref CharID);
        serializer.SerializeValue(ref AccountID);
        serializer.SerializeValue(ref FamilyName);
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref Title);
        serializer.SerializeValue(ref ZoneID);
        serializer.SerializeValue(ref XLoc);
        serializer.SerializeValue(ref YLoc);
        serializer.SerializeValue(ref ZLoc);
        serializer.SerializeValue(ref Race);
        serializer.SerializeValue(ref Gender);
        serializer.SerializeValue(ref Face);
        serializer.SerializeValue(ref CombatExp);
        serializer.SerializeValue(ref CraftingExp);
        serializer.SerializeValue(ref ArcaneExp);
        serializer.SerializeValue(ref SpiritExp);
        serializer.SerializeValue(ref VeilExp);
        
        // Serialize species stats
        serializer.SerializeValue(ref SpeciesStrength);
        serializer.SerializeValue(ref SpeciesDexterity);
        serializer.SerializeValue(ref SpeciesConstitution);
        serializer.SerializeValue(ref SpeciesIntelligence);
        serializer.SerializeValue(ref SpeciesSpirit);
    }
}

[System.Serializable]
public struct CharacterListResult : INetworkSerializable
{
    public bool Success;
    public string ErrorMessage;
    public CharacterData[] Characters;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Success);
        serializer.SerializeValue(ref ErrorMessage);
        serializer.SerializeValue(ref Characters);
    }
}
