using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MiningNode : ResourceNode
{
    //[Header("Mining Specific Settings")]


    protected override void Awake()
    {
        base.Awake();
        hType = HarvestType.Mining;
    }

    public override void OnInteract(PlayerCharacter pCharacter)
    {
        base.OnInteract(pCharacter);
    }

    protected override void OnRespawn()
    {

    }
}
