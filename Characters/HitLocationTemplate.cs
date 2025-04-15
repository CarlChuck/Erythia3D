using UnityEngine;

[CreateAssetMenu(fileName = "New Location", menuName = "ERYTHIA/HitLocation")]
public class HitLocationTemplate : ScriptableObject
{
    public string locationName;
    public int locationWeight; //Chance to hit
    public int locationHP; //hit point percentage of full
    public HitLocationType hitLocationType;

    private void Awake()
    {
        locationName = name; //Just to sync the names in case accidently reading the wrong string
    }
}
