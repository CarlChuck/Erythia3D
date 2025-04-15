using System.Collections;
using UnityEngine;

public class HitLocation : MonoBehaviour
{
    [SerializeField] private HitLocationTemplate locationTemplate;
    private string locationName;
    private int baseHitPoints;
    private float currentHitPoints;
    private int resistThrust;
    private int resistCrush;
    private int resistSlash;
    private int resistHeat;
    private int resistCold;
    private int resistShock;


    public void SetHitLocationHealth(int baseHealth)
    {
        baseHitPoints = baseHealth * locationTemplate.locationHP;
        currentHitPoints = baseHitPoints;
        locationName = locationTemplate.name;
        name = locationName;
    }
    public float GetHitLocationHealth() 
    {
        return currentHitPoints;
    }
    public void TakeDamage(float damage)
    {
        int lowerThreshold = -baseHitPoints;
        currentHitPoints -= damage;
        if (currentHitPoints <lowerThreshold)
        {
            currentHitPoints = lowerThreshold;
        }
    }
    public void HealLocation(float healing)
    {
        currentHitPoints += healing;
        if (currentHitPoints > baseHitPoints)
        {
            currentHitPoints = baseHitPoints;
        }
    }
    public bool GetPenalty()
    {
        if (currentHitPoints <= 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
    public int GetLocationWeight()
    {
        return locationTemplate.locationWeight;
    }
    public int GetResistByName(ResistName rName)
    {
        switch (rName)
        {
            case ResistName.Thrust:
                return resistThrust;
            case ResistName.Crush:
                return resistCrush;
            case ResistName.Slash:
                return resistSlash;
            case ResistName.Heat:
                return resistHeat;
            case ResistName.Cold:
                return resistCold;
            case ResistName.Shock:
                return resistShock;
            default:
                return resistSlash;
        }
    }
    public HitLocationType GetHitLocationType()
    {
        return locationTemplate.hitLocationType;
    }
}

public enum ResistName
{
    Thrust, Crush, Slash, Heat, Cold, Shock
}
public enum HitLocationType 
{
    Body, Arm, Leg
}