using System.Collections.Generic;
using UnityEngine;

public class HitLocationProfile : MonoBehaviour
{
    [SerializeField] private List<HitLocation> hitLocations;
    private int totalWeight;

    public void Initialise(int baseHealth)
    {
        foreach (HitLocation hitLocation in hitLocations)
        {
            totalWeight += hitLocation.GetLocationWeight();
            hitLocation.SetHitLocationHealth(baseHealth);
        }
    }

    public HitLocation GetRandomHitLocation()
    {
        HitLocation hitLocationToReturn = hitLocations[0];
        int randNumber = Random.Range(0, totalWeight);
        int runningTotal = 0;
        foreach (HitLocation hitLocation in hitLocations)
        {
            runningTotal += hitLocation.GetLocationWeight();
            if (randNumber < runningTotal)
            {
                hitLocationToReturn = hitLocation;
            }
        }
        return hitLocationToReturn;
    }

    public HitLocation GetLocationByType(HitLocationType hitLocType)
    {
        //Makes new list of hitlocations by selected type
        HitLocation hitLocationToReturn = hitLocations[0]; // Just in case something goes wrong, it will give first location
        List<HitLocation> hitLocationsOfType = new List<HitLocation>();
        foreach (HitLocation hitLocation in hitLocations)
        {
            if (hitLocation.GetHitLocationType() == hitLocType)
            {
                hitLocationsOfType.Add(hitLocation);
            }
        }

        //Gets New temp Weight
        int totalWeightOfType = 0;
        foreach (HitLocation hitLocation in hitLocationsOfType)
        {
            totalWeightOfType += hitLocation.GetLocationWeight();
        }

        //Gets random hit location of the type
        hitLocationToReturn = hitLocationsOfType[0]; // Just in case something goes wrong, it will give first location of selected type
        int randNumber = Random.Range(0, totalWeightOfType);
        int runningTotal = 0;
        foreach (HitLocation hitLocation in hitLocationsOfType)
        {
            runningTotal += hitLocation.GetLocationWeight();
            if (randNumber < runningTotal)
            {
                hitLocationToReturn = hitLocation;
            }
        }

        return hitLocationToReturn;
    }
}