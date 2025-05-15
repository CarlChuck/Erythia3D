using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(OutlineToggle))] // Ensure OutlineToggle is present
public abstract class ResourceNode : Interactable
{
    [Header("Resource Node Settings")]
    [SerializeField] protected string nodeName = "Resource";
    [SerializeField] protected int maxHealth = 100;
    [SerializeField] protected int currentHealth;
    [SerializeField] protected float respawnTime = 300f; // Time in seconds
    [SerializeField] protected int nodeResistence = 0; // Resistance to damage
    [SerializeField] protected HarvestType hType;
    [SerializeField] private float interactionRange = 5f; // Max distance for outline highlight

    [Header("Effects")]
    [SerializeField] protected GameObject hitEffect;
    [SerializeField] protected GameObject destroyEffect;
    [SerializeField] protected GameObject respawnEffect;

    [Header("Components")]
    [SerializeField] protected Collider nodeCollider; //for detecting interaction
    [SerializeField] protected GameObject resourceModel; //Set in inspector
    private OutlineToggle outlineToggle; // Reference to the outline controller

    protected Coroutine respawnCoroutine;

    [SerializeField] protected ResourceItem resourceItemPrefab;
    [SerializeField] protected Resource resource;

    [SerializeField] protected ResourceType resourceType; // Type that this node can drop - Set in Inspector
    [SerializeField] protected ResourceSubType resourceSubType; // Type that this node can drop - Set by ResourceNodeManager

    private bool isMouseOver = false;
    private PlayerCharacter playerCharacter; // Reference to the player

    protected virtual void Awake()
    {
        currentHealth = maxHealth;

        outlineToggle = GetComponent<OutlineToggle>();
        if (outlineToggle == null)
        {
            Debug.LogError($"ResourceNode '{name}' is missing the required OutlineToggle component.", this);
        }
    }

    protected virtual void Start()
    {
        playerCharacter = FindFirstObjectByType<PlayerCharacter>();
        if (playerCharacter == null)
        {
            Debug.LogWarning("ResourceNode could not find PlayerCharacter in the scene.", this);
        }

        // Ensure outline is initially off when the game starts
        if (outlineToggle != null)
        {
            outlineToggle.SetOutlineActive(false);
        }
    }

    // Called when the mouse pointer enters the Collider
    protected virtual void OnMouseEnter()
    {
        isMouseOver = true; // Flag that the mouse is over the node
        // Outline logic moved to Update
    }

    // Called when the mouse pointer exits the Collider
    protected virtual void OnMouseExit()
    {
        isMouseOver = false; // Flag that the mouse left
        if (outlineToggle != null)
        {
            outlineToggle.SetOutlineActive(false); // Always turn off outline on exit
        }
    }

    protected virtual void Update()
    {
        // Handle outline visibility based on distance and mouse hover
        if (isMouseOver && playerCharacter != null && outlineToggle != null)
        {
            float distance = Vector3.Distance(playerCharacter.transform.position, transform.position);
            bool shouldOutlineBeActive = (distance <= interactionRange && currentHealth > 0);
            outlineToggle.SetOutlineActive(shouldOutlineBeActive);
        }
        else if (!isMouseOver && outlineToggle != null)
        {
             // Ensure outline is off if mouse is not over
            outlineToggle.SetOutlineActive(false);
        }
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
        resourceItem.SetResourceItem(resource, quantity);
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
        // Ensure outline is turned off when node is disabled
        if (outlineToggle != null)
        {
            outlineToggle.SetOutlineActive(false);
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
        // No need to turn outline on here, OnMouseEnter will handle it
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
