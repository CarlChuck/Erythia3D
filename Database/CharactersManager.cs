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
            LogInfo("CharactersManager Initialization Complete.");
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
        LogInfo("Checking and initializing character data table async...");
        bool tableOK = await EnsureTableExistsAsync(CharacterDataTableName, GetCharacterTableDefinition());

        if (!tableOK)
        {
            throw new Exception("Failed to initialize character database table async.");
        }
        LogInfo("Character data table checked/initialized async.");
    }
    #endregion

    public async Task<bool> CreateNewCharacterAsync(int accountID, string familyName, string characterName, string title = null, int startingArea = 1, int race = 1, int gender = 1, int face = 1)
    {
        int xLoc = 0; // TODO: Get actual starting location based on startingArea
        int yLoc = 0;
        int zLoc = 0;

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

            if (characterAdded)
            {
                LogInfo($"New character '{characterName}' created successfully for Account ID {accountID}.");
            }
            else
            {
                LogWarning($"Failed to create new character '{characterName}'. Check DatabaseManager logs or constraints (e.g., duplicate Familyname?).");
            }
            return characterAdded;
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
            if (success)
            {
                LogInfo($"Character data updated successfully for CharID: {characterID}");
            }
            else
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

            if (results.Count > 0)
            {
                LogInfo($"Found {results.Count} character(s) for Account ID: {accountId}");
            }
            else
            {
                LogInfo($"No characters found for Account ID: {accountId}");
            }
            return results;
        }
        catch (Exception ex)
        {
            LogError($"Exception retrieving characters by Account ID {accountId}", ex);
            return new List<Dictionary<string, object>>();
        }
    }
    private Dictionary<string, string> GetCharacterTableDefinition()
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
