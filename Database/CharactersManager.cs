using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Threading;

public class CharactersManager : MonoBehaviour
{
    private const string CharacterDataTableName = "CharacterData";
    [SerializeField] private SpeciesTemplate raceAelystian;
    [SerializeField] private SpeciesTemplate raceAnurian;
    [SerializeField] private SpeciesTemplate raceGetaii;
    [SerializeField] private SpeciesTemplate raceHivernian;
    [SerializeField] private SpeciesTemplate raceKasmiran;
    [SerializeField] private SpeciesTemplate raceMeliviaen;
    [SerializeField] private SpeciesTemplate raceQadian;
    [SerializeField] private SpeciesTemplate raceTkyan;
    [SerializeField] private SpeciesTemplate raceValahoran;
    private Task tableInitializationTask;
    public bool IsTableInitialized { get; private set; } = false;

    #region Singleton
    public static CharactersManager Instance;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }
    #endregion
    private async void Start() // Changed to async void
    {
        // Start and store the task
        tableInitializationTask = InitializeCharacterDataTableIfNotExistsAsync();
        // Await it here
        await tableInitializationTask;
        IsTableInitialized = true; // Mark as complete
        Debug.Log("CharactersManager table initialization sequence complete.");
    }
    public async Task WaitForInitialization()
    {
        if (tableInitializationTask != null)
        {
            await tableInitializationTask;
        }
    }

    private async Task InitializeCharacterDataTableIfNotExistsAsync()
    {
        Dictionary<string, string> charColumns = GetCharacterTableDefinition();
        try
        {
            Debug.Log($"Table '{CharacterDataTableName}' does not exist. Attempting to create async...");
            bool tableCreated = await DatabaseManager.Instance.CreateTableIfNotExistsAsync(CharacterDataTableName, charColumns);
            if (tableCreated)
            {
                Debug.Log("Character table created successfully async.");
            }
            else
            {
                throw new Exception($"Failed to create critical table: {CharacterDataTableName}");
            }

        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during Character table initialization: {ex.Message}. Character management may fail.");
        }
    }
    private Dictionary<string, string> GetCharacterTableDefinition()
    {
        return new Dictionary<string, string> 
        {
            {"CharID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"AccountID", "INT"},
            {"Familyname", "VARCHAR(255) UNIQUE"},
            {"Name", "VARCHAR(255)"},
            {"Title", "VARCHAR(255)"},
            {"ZoneID", "INT"},
            {"Xloc", "INT"},
            {"Yloc", "INT"},
            {"Zloc", "INT"},
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
    public async Task<bool> CreateNewCharacterAsync(int accountID, string familyName, string characterName, string title = null, int startingArea = 1, int race = 1, int gender = 1, int face = 1)
    {
        int xLoc = 0; // TODO: Get actual starting location based on startingArea
        int yLoc = 0;
        int zLoc = 0;

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            {"AccountID", accountID},
            {"Familyname", familyName},
            {"Name", characterName},
            {"Title", title},
            {"ZoneID", startingArea},
            {"Xloc", xLoc},
            {"Yloc", yLoc},
            {"Zloc", zLoc},
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
            // Use the asynchronous InsertDataAsync method
            bool characterAdded = await DatabaseManager.Instance.InsertDataAsync(CharacterDataTableName, values);

            if (characterAdded)
            {
                Debug.Log($"New character '{characterName}' created successfully for Account ID {accountID}.");
            }
            else
            {
                Debug.LogWarning($"Failed to create new character '{characterName}'. Check DatabaseManager logs or constraints (e.g., duplicate Familyname?).");
            }
            return characterAdded;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception during character creation for '{characterName}': {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }
    public async Task<List<Dictionary<string, object>>> GetCharactersbyAccountIDAsync(int accountId)
    {
        string query = $"SELECT * FROM `{CharacterDataTableName}` WHERE AccountID = @AccountID"; // Use backticks
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@AccountID", accountId } };
        try
        {
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query, parameters);

            // Check for null result which indicates a query execution error
            if (results == null)
            {
                Debug.LogError($"Error retrieving characters for Account ID: {accountId}. Query execution failed.");
                return new List<Dictionary<string, object>>(); // Return empty list on error
            }

            if (results.Count > 0)
            {
                Debug.Log($"Found {results.Count} character(s) for Account ID: {accountId}");
            }
            else
            {
                Debug.Log($"No characters found for Account ID: {accountId}");
            }
            return results; // Return results (could be empty list if none found)
        }
        catch (Exception ex) // Catch unexpected exceptions during processing
        {
            Debug.LogError($"Exception retrieving characters by Account ID {accountId}: {ex.Message}\n{ex.StackTrace}");
            return new List<Dictionary<string, object>>(); // Return empty list on error
        }
    }

    // Update the synchronous wrapper method accordingly
    public async Task<List<Dictionary<string, object>>> GetCharactersbyAccountID(int accountId)
    {
        return await GetCharactersbyAccountIDAsync(accountId);
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
}