using UnityEngine;

public class CharacterModelManager : MonoBehaviour
{
    [SerializeField] private GameObject aelystiaFModel;
    [SerializeField] private GameObject aelystiaMModel;
    [SerializeField] private GameObject anurianFModel;
    [SerializeField] private GameObject anurianMModel;
    [SerializeField] private GameObject getaiiFModel;
    [SerializeField] private GameObject getaiiMModel;
    [SerializeField] private GameObject kasmiranFModel;
    [SerializeField] private GameObject kasmiranMModel;
    [SerializeField] private GameObject qadianFModel;
    [SerializeField] private GameObject qadianMModel;
    [SerializeField] private GameObject valahoranFModel;
    [SerializeField] private GameObject valahoranMModel;
    [SerializeField] private GameObject hivernianFModel;
    [SerializeField] private GameObject hivernianSModel;
    [SerializeField] private GameObject hivernianMModel;
    [SerializeField] private GameObject meliviaenFModel;
    [SerializeField] private GameObject meliviaenSModel;
    [SerializeField] private GameObject meliviaenMModel;
    [SerializeField] private GameObject tkyanFModel;
    [SerializeField] private GameObject tkyanSModel;
    [SerializeField] private GameObject tkyanMModel;

    public GameObject GetCharacterModel(int race, int gender)
    {
        GameObject characterModel = null;
        switch (race)
        {
            case 2:
                if (gender == 1)
                {
                    characterModel = anurianMModel;
                }
                else if (gender == 2)
                {
                    characterModel = anurianFModel;
                }
                break;
            case 3:
                if (gender == 1)
                {
                    characterModel = getaiiMModel;
                }
                else if (gender == 2)
                {
                    characterModel = getaiiFModel;
                }
                break;
            case 4:
                if (gender == 1)
                {
                    characterModel = hivernianMModel;
                }
                else if (gender == 2)
                {
                    characterModel = hivernianFModel;
                }
                else if (gender == 3)
                {
                    characterModel = hivernianSModel;
                }
                break;
            case 5:
                if (gender == 1)
                {
                    characterModel = kasmiranMModel;
                }
                else if (gender == 2)
                {
                    characterModel = kasmiranFModel;
                }
                break;
            case 6:
                if (gender == 1)
                {
                    characterModel = meliviaenMModel;
                }
                else if (gender == 2)
                {
                    characterModel = meliviaenFModel;
                }
                else if (gender == 3)
                {
                    characterModel = meliviaenSModel;
                }
                break;
            case 7:
                if (gender == 1)
                {
                    characterModel = qadianMModel;
                }
                else if (gender == 2)
                {
                    characterModel = qadianFModel;
                }
                break;
            case 8:
                if (gender == 1)
                {
                    characterModel = tkyanMModel;
                }
                else if (gender == 2)
                {
                    characterModel = tkyanFModel;
                }
                else if (gender == 3)
                {
                    characterModel = tkyanSModel;
                }
                break;
            case 9:
                if (gender == 1)
                {
                    characterModel = valahoranMModel;
                }
                else if (gender == 2)
                {
                    characterModel = valahoranFModel;
                }
                break;
            default:
                if (gender == 1)
                {
                    characterModel = aelystiaMModel;
                }
                else if (gender == 2)
                {
                    characterModel = aelystiaFModel;
                }
                break;
        }
        return characterModel;
    }
}
