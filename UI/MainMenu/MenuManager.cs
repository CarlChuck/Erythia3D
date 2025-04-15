using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private GameObject spotLight;
    [SerializeField] private GameObject characterWP;
    [SerializeField] private GameObject mainPane;
    [SerializeField] private GameObject charactersPane;
    [SerializeField] private GameObject settingsPane;
    [SerializeField] private Animator animator;

    [SerializeField] private GameObject playButton;
    [SerializeField] private GameObject createCharButton;
    [SerializeField] private GameObject genderSButton;

    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private CharacterCreator characterCreator;
    [SerializeField] private GameObject menuCamera;
    [SerializeField] private GameObject menuCanvas;

    #region Singleton
    public static MenuManager Instance;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        menuCanvas.SetActive(true);
    }
    #endregion

    void Start()
    {
        OnMainMenu();
        menuCamera.SetActive(true);
        playerManager.PlayerManagerControlSetActive(false);
        StartCoroutine(DelayedCharacterCheck());
    }

    #region Panes
    public void OnCreateButton()
    {
        animator.SetBool("Menu", false);
        animator.SetBool("Characters", true);
        animator.SetBool("Settings", false);
        spotLight.SetActive(true);
    }
    public void OnSettingsButton()
    {
        animator.SetBool("Menu", false);
        animator.SetBool("Characters", false);
        animator.SetBool("Settings", true);
        OnOpenSettings();
    }
    public void OnMainMenu()
    {
        spotLight.SetActive(false);
        animator.SetBool("Menu", true);
        animator.SetBool("Characters", false);
        animator.SetBool("Settings", false);
    }
    public void OnExitButton()
    {
        Application.Quit();
    }
    #endregion

    public void OnCreateCharacter()
    {
        if (characterCreator.GetName() == "")
        {
            Debug.Log("No Name");
            return;
        }
        if (characterCreator.GetRace() == 0)
        {
            Debug.Log("No Race");
            return;
        }
        if (characterCreator.GetGender() == 0)
        {
            Debug.Log("No Gender");
            return;
        }
        if (characterCreator.GetFace() == 0)
        {
            Debug.Log("No Face");
            return;
        }
        playerManager.OnCreateCharacter(characterCreator.GetName(), characterCreator.GetRace(), characterCreator.GetGender(), characterCreator.GetFace());
        OnEnterWorld();
    }
    public void OnEnterWorld()
    {
        playerManager.SetCharactersList();
        menuCamera.SetActive(false);
        playerManager.PlayerManagerControlSetActive(true);
        SceneManager.LoadScene("IthoriaSouth"); //TODO change to the correct scene
    }
    public void OnOpenSettings()
    {
        //TODO open settings
    }
    public void SetFamilyName(string familyName)
    {
        playerManager.OnSetFamilyName(familyName);
    }

    public void OnSelectRace(int raceNumber)
    {
        switch (raceNumber)
        {
            case 1:
                characterCreator.SetRace(1);
                break;
            case 2:
                characterCreator.SetRace(2);
                break;
            case 3:
                characterCreator.SetRace(3);
                break;
            case 4:
                characterCreator.SetRace(4);
                break;
            case 5:
                characterCreator.SetRace(5);
                break;
            case 6:
                characterCreator.SetRace(6);
                break;
            case 7:
                characterCreator.SetRace(7);
                break;
            case 8:
                characterCreator.SetRace(8);
                break;
            case 9:
                characterCreator.SetRace(9);
                break;
            default:
                characterCreator.SetRace(1);
                break;
        }
    }
    public void OnSetGenderS(bool setS)
    {
        if (setS)
        {
            genderSButton.SetActive(true);
        }
        else
        {
            genderSButton.SetActive(false);
        }
    }
    public void OnSetGender(int genderToSet)
    {
        characterCreator.SetGender(genderToSet);
    }

    private IEnumerator DelayedCharacterCheck()
    {
        yield return new WaitForSeconds(0.1f);

        if (!IsCharacterSelected())
        {
            playButton.SetActive(true);
            createCharButton.SetActive(false);
        }
        else
        {
            playButton.SetActive(false);
            createCharButton.SetActive(true);
        }
    }
    private bool IsCharacterSelected()
    {
        if (playerManager.GetSelectedPlayerCharacter() != null)
        {
            Debug.Log("Character Selected");
            return true;
        }
        else
        {
            Debug.Log("No Character Selected");
            return false;
        }
    }
}
