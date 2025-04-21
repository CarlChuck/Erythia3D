using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ResourceNode : Interactable
{
    [Header("Resource Node Settings")]
    [SerializeField] protected string nodeName = "Resource";
    [SerializeField] protected int maxHealth = 100;
    [SerializeField] protected int currentHealth;
    [SerializeField] protected float respawnTime = 300f; // Time in seconds
    [SerializeField] protected int nodeResistence = 0; // Resistance to damage
    [SerializeField] protected HarvestType hType;

    [Header("Effects")]
    [SerializeField] protected GameObject hitEffect;
    [SerializeField] protected GameObject destroyEffect;
    [SerializeField] protected GameObject respawnEffect;

    [Header("Components")]
    [SerializeField] protected Collider nodeCollider; //for detecting interaction
    [SerializeField] protected GameObject resourceModel; //Set in inspector

    protected Coroutine respawnCoroutine;

    [SerializeField] protected ResourceItem resourceItemPrefab;
    [SerializeField] protected Resource resource;

    [SerializeField] protected ResourceType resourceType; // Type that this node can drop - Set in Inspector
    [SerializeField] protected ResourceSubType resourceSubType; // Type that this node can drop - Set by ResourceNodeManager

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
    }
    public override void OnInteract(PlayerCharacter pCharacter)
    {
        ToolItem equippedTool = null;
        if (pCharacter != null)
        {
            if (hType == HarvestType.Mining)
            {
                equippedTool = pCharacter.GetEquipmentProfile().GetItemInSlot(ItemType.MiningTool) as ToolItem;
                pCharacter.OnMiningHit();
            }
            else if (hType == HarvestType.Woodcutting)
            {
                equippedTool = pCharacter.GetEquipmentProfile().GetItemInSlot(ItemType.WoodTool) as ToolItem;
                pCharacter.OnWoodCuttingHit();
            }
            else if (hType == HarvestType.Harvesting)
            {
                equippedTool = pCharacter.GetEquipmentProfile().GetItemInSlot(ItemType.HarvestingTool) as ToolItem;
                pCharacter.OnHarvestHit();
            }
            int damagetoSend = equippedTool != null ? equippedTool.GetDamage() : 2; 
            damagetoSend = Mathf.Max(0, damagetoSend - nodeResistence); // Ensure damage is not negative            
            TakeDamage(damagetoSend);
            
            // Only generate resources if we're actually doing damage
            if (damagetoSend > 0)
            {
                ResourceItem resourceItem = GenerateResource(damagetoSend);
                pCharacter.OnPickupResourceItem(resourceItem);
            }
        }
    }

    public virtual void TakeDamage(int damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);

        if (hitEffect != null)
        {
            Instantiate(hitEffect, transform.position, Quaternion.identity);
        }

        // If health reached zero, disable the node
        if (currentHealth <= 0)
        {
            if (destroyEffect != null)
            {
                Instantiate(destroyEffect, transform.position, Quaternion.identity);
            }

            // Disable node
            DisableNode();

            // Start respawn timer
            respawnCoroutine = StartCoroutine(RespawnAfterDelay());
        }
    }

    protected virtual ResourceItem GenerateResource(int amount)
    {
        // Get resource drops
        ResourceItem resourceItem = Instantiate(resourceItemPrefab, transform.position, Quaternion.identity);
        // Ensure at least 1 resource is generated, even with 0 damage
        int quantity = Mathf.Max(1, amount);
        resourceItem.Initialize(resource, quantity);
        return resourceItem;
    }

    protected virtual void DisableNode()
    {
        if (resourceModel != null)
        {
            resourceModel.SetActive(false);
        }
        if (nodeCollider != null)
        {
            nodeCollider.enabled = false; // Disable the collider
        }
    }

    protected virtual void EnableNode()
    {
        if (resourceModel != null) 
        {
            resourceModel.SetActive(true);
        }
        if (nodeCollider != null)
        {
            nodeCollider.enabled = true; // Enable the collider
        }
        currentHealth = maxHealth; 
    }

    protected virtual IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnTime);
        OnRespawn();
        respawnCoroutine = null;
    }

    protected virtual void OnRespawn()
    {
        EnableNode();
        if (respawnEffect != null)
        {
            Instantiate(respawnEffect, transform.position, Quaternion.identity);
        }
    }

    public virtual void ForceRespawn()
    {
        if (respawnCoroutine != null)
        {
            StopCoroutine(respawnCoroutine);
            respawnCoroutine = null;
        }
        OnRespawn();
    }

    public float GetHealthPercentage()
    {
        return (float)currentHealth / maxHealth;
    }

    public void SetResource(Resource res) 
    {
        resource = res;
    }
    public void SetResourceSubType(ResourceSubType sType)
    {
        resourceSubType = sType;
    }

    public Resource GetResource()
    {
        return resource;
    }
    public ResourceType GetResourceType()
    {
        return resourceType;
    }
    public ResourceSubType GetResourceSubType()
    {
        return resourceSubType;
    }
    public void SetResourceItemPrefab(ResourceItem resourceItem)
    {
        resourceItemPrefab = resourceItem;
    }
}
public enum HarvestType
{
    Mining,
    Woodcutting,
    Harvesting
}
