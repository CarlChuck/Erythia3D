using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;

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
    private void Start()
    {
        InitializeCharacterDataTableIfNotExists();
    }

    private void InitializeCharacterDataTableIfNotExists()
    {
        Dictionary<string, string> charColumns = new Dictionary<string, string>
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

        if (!DatabaseManager.Instance.TableExists(CharacterDataTableName))
        {
            bool tableCreated = DatabaseManager.Instance.CreateTableIfNotExists(CharacterDataTableName, charColumns);
            if (tableCreated)
            {
                Debug.Log("Character table created successfully.");
            }
            else
            {
                Debug.LogWarning("Failed to create Characters table.");
            }
        }
    }

    public bool CreateNewCharacter(int accountID, string familyName, string characterName, string title = null, int startingArea = 1, int race = 1, int gender = 1, int face = 1)
    {
        int xLoc = 0; 
        int yLoc = 0; 
        int zLoc = 0;
        //Get Starting Location by Zone


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

        bool characterAdded = DatabaseManager.Instance.InsertData(CharacterDataTableName, values);
        if (characterAdded)
        {
            Debug.Log($"New character created with Username: {characterName}");
        }
        else
        {
            Debug.LogWarning("Failed to create new character.");
        }
        return characterAdded;
    }
    private async Task<List<Dictionary<string, object>>> GetCharactersbyAccountIDAsync(int accountId)
    {
        string query = "SELECT * FROM CharacterData WHERE AccountID = @AccountID";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@AccountID", accountId } };
        try
        {
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query, parameters);

            if (results.Count > 0)
            {
                Debug.Log($"Found {results.Count} character(s) with Account ID: {accountId}");
                return results;
            }
            else
            {
                Debug.LogWarning($"No characters found with Account ID: {accountId}");
                return new List<Dictionary<string, object>>();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error retrieving characters by Account ID: {ex.Message}");
            return new List<Dictionary<string, object>>();
        }
    }

    // Update the synchronous wrapper method accordingly
    public List<Dictionary<string, object>> GetCharactersbyAccountID(int accountId)
    {
        return Task.Run(() => GetCharactersbyAccountIDAsync(accountId)).GetAwaiter().GetResult();
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
