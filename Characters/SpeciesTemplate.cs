using UnityEngine;

[CreateAssetMenu(fileName = "New Species", menuName = "ERYTHIA/Species")]
public class SpeciesTemplate : ScriptableObject
{
    public string speciesName;
    public int strength;
    public int dexterity;
    public int constitution;
    public int intelligence;
    public int spirit; 

    private void Awake()
    {
        speciesName = name; //Just to sync the names in case accidently reading the wrong string
    }
}
