using UnityEngine;
using TMPro;

public class CharacterCreator : MonoBehaviour
{
    private int selectedGender = 1;
    private int selectedRace = 1;
    private int selectedFace = 1;
    private string newCharacterName = "";
    private string newFamilyName = "";

    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField familyNameInput;


    [SerializeField] private GameObject aelystianModelM;
    [SerializeField] private GameObject aelystianModelF;
    [SerializeField] private GameObject anurianModelM;
    [SerializeField] private GameObject anurianModelF;
    [SerializeField] private GameObject getaiiModelM;
    [SerializeField] private GameObject getaiiModelF;
    [SerializeField] private GameObject kasmiranModelM;
    [SerializeField] private GameObject kasmiranModelF;
    [SerializeField] private GameObject qadianModelM;
    [SerializeField] private GameObject qadianModelF;
    [SerializeField] private GameObject valahorModelM;
    [SerializeField] private GameObject valahorModelF;
    [SerializeField] private GameObject hivernianModelM;
    [SerializeField] private GameObject hivernianModelS;
    [SerializeField] private GameObject hivernianModelF;
    [SerializeField] private GameObject meliviaenModelM;
    [SerializeField] private GameObject meliviaenModelS;
    [SerializeField] private GameObject meliviaenModelF;
    [SerializeField] private GameObject tkyanModelM;
    [SerializeField] private GameObject tkyanModelS;
    [SerializeField] private GameObject tkyanModelF;

    private void Start()
    {
        DisableAllModels();
    }
    #region Setters
    public void SetGender(int gender)
    {
        selectedGender = gender;
        ShowCharacterGraphic();
    }
    public void SetRace(int race)
    {
        int lastRace = selectedRace;
        selectedRace = race;
        if (race == 4 || race == 6 || race == 8)
        {

        }
        else
        {
            if (lastRace == 4 || lastRace == 6 || lastRace == 8)
            {
                selectedGender = 1;
            }
            else
            {

            }
        }
        ShowCharacterGraphic();
    }
    public void SetFace(int face)
    {
        selectedFace = face;
        ShowCharacterGraphic();
    }
    public void SetName()
    {
        newCharacterName = nameInput.text;
    }
    public void SetFamilyName()
    {
        newFamilyName = familyNameInput.text;
    }
    #endregion

    #region Getters
    public int GetGender()
    {
        return selectedGender;
    }
    public int GetRace()
    {
        return selectedRace;
    }
    public int GetFace()
    {
        return selectedFace;
    }
    public string GetName()
    {
        return newCharacterName;
    }

    public string GetFamilyName()
    {
        return newFamilyName;
    }
    #endregion

    private void ShowCharacterGraphic()
    {

        int currentRace = GetRace();
        int currentGender = GetGender();
        DisableAllModels();
        Debug.Log(currentRace + ", " + currentGender);
        switch (currentRace)
        {
            case 1:
                if (currentGender == 1)
                {
                    aelystianModelM.SetActive(true);
                }
                else
                {
                    aelystianModelF.SetActive(true);
                }
                break;
            case 2:
                if (currentGender == 1)
                {
                    anurianModelM.SetActive(true);
                }
                else
                {
                    anurianModelF.SetActive(true);
                }
                break;
            case 3:
                if (currentGender == 1)
                {
                    getaiiModelM.SetActive(true);
                }
                else
                {
                    getaiiModelF.SetActive(true);
                }
                break;
            case 5:
                if (currentGender == 1)
                {
                    kasmiranModelM.SetActive(true);
                }
                else
                {
                    kasmiranModelF.SetActive(true);
                }
                break;
            case 7:
                if (currentGender == 1)
                {
                    qadianModelM.SetActive(true);
                }
                else
                {
                    qadianModelF.SetActive(true);
                }
                break;
            case 9:
                if (currentGender == 1)
                {
                    valahorModelM.SetActive(true);
                }
                else
                {
                    valahorModelF.SetActive(true);
                }
                break;
            case 4:
                if (currentGender == 1)
                {
                    hivernianModelM.SetActive(true);
                }
                else if (currentGender == 2)
                {
                    hivernianModelF.SetActive(true);
                }
                else
                {
                    hivernianModelS.SetActive(true);
                }
                break;
            case 6:
                if (currentGender == 1)
                {
                    meliviaenModelM.SetActive(true);
                }
                else if (currentGender == 2)
                {
                    meliviaenModelF.SetActive(true);
                }
                else
                {
                    meliviaenModelS.SetActive(true);
                }
                break;
            case 8:
                if (currentGender == 1)
                {
                    tkyanModelM.SetActive(true);
                }
                else if (currentGender == 2)
                {
                    tkyanModelF.SetActive(true);
                }
                else
                {
                    tkyanModelS.SetActive(true);
                }
                break;
            default:
                if (currentGender == 1)
                {
                    aelystianModelM.SetActive(true);
                }
                else
                {
                    aelystianModelF.SetActive(true);
                }
                break;
        }
    }
    private void DisableAllModels()
    {
        aelystianModelM.SetActive(false);
        aelystianModelF.SetActive(false);
        anurianModelM.SetActive(false);
        anurianModelF.SetActive(false);
        getaiiModelM.SetActive(false);
        getaiiModelF.SetActive(false);
        kasmiranModelM.SetActive(false);
        kasmiranModelF.SetActive(false);
        qadianModelM.SetActive(false);
        qadianModelF.SetActive(false);
        valahorModelM.SetActive(false);
        valahorModelF.SetActive(false);
        hivernianModelM.SetActive(false);
        hivernianModelS.SetActive(false);
        hivernianModelF.SetActive(false);
        meliviaenModelM.SetActive(false);
        meliviaenModelS.SetActive(false);
        meliviaenModelF.SetActive(false);
        tkyanModelM.SetActive(false);
        tkyanModelS.SetActive(false);
        tkyanModelF.SetActive(false);
    }
}
/* Gender Numbers:
 * 1 = Male / Mirili
 * 2 = Female / Folli
 * 3 = Synthi


/* Race Numbers:
 * 1 = Aelystian
 * 2 = Anurian
 * 3 = Getaii
 * 4 = Hivernian
 * 5 = Kasmiran
 * 6 = Meliviaen
 * 7 = Qadian
 * 8 = TKyan
 * 9 = Valahoran
 */