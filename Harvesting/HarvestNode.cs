using UnityEngine;

public class HarvestNode : ResourceNode
{
    //[Header("Harvest Specific Settings")]
    protected override void Awake()
    {
        base.Awake();
        hType = HarvestType.Harvesting;
    }

    public override void OnInteract(PlayerCharacter pCharacter)
    {
        base.OnInteract(pCharacter);
    }
}
