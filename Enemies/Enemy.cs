using UnityEngine;
using System.Collections.Generic; // Required for Dictionary
using System.Linq; // Required for OrderByDescending

[RequireComponent(typeof(SphereCollider))]
public class Enemy : StatBlock
{
    public enum EnemyState
    {
        Idle,
        Chasing,
        Attacking
    }

    public EnemyState currentState;
    public float attackRange = 2f;
    // private Transform playerTransform; // Replaced with aggroTable and currentTarget
    private Dictionary<Transform, float> aggroTable = new Dictionary<Transform, float>();
    private Transform currentTarget;
    private SphereCollider perceptionCollider;

    void Start()
    {
        currentState = EnemyState.Idle;
        perceptionCollider = GetComponent<SphereCollider>();
        if (perceptionCollider != null)
        {
            perceptionCollider.isTrigger = true;
        }
        else
        {
            Debug.LogError("SphereCollider not found on Enemy. Please add one.", this);
        }
    }

    void Update()
    {
        StateMachine();
        // Periodically update target in case hate values change dynamically elsewhere
        // For now, target update is primarily event-driven (enter/exit/damage)
        // Consider adding a timer if hate decays or passive hate generation is needed.
        // UpdateCurrentTarget(); // Potentially call this less frequently if needed
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (!aggroTable.ContainsKey(other.transform))
            {
                aggroTable[other.transform] = 1f; // Initial base hate
                Debug.Log($"Player {other.name} entered perception range and added to aggro table.");
                UpdateCurrentTarget();
            }
            // If player already in table, could potentially increase their hate slightly for re-entering
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && aggroTable.ContainsKey(other.transform))
        {
            aggroTable.Remove(other.transform);
            Debug.Log($"Player {other.name} exited perception range and removed from aggro table.");
            if (currentTarget == other.transform)
            {
                UpdateCurrentTarget(); // Find a new target if the current one left
            }
        }
    }

    void UpdateCurrentTarget()
    {
        if (aggroTable.Count == 0)
        {
            currentTarget = null;
            if (currentState != EnemyState.Idle) {
                currentState = EnemyState.Idle;
                 Debug.Log("No targets in range, switching to Idle.");
            }
            return;
        }

        // Find the player with the highest hate
        // Using LINQ for simplicity here. For extreme performance, a manual loop might be considered.
        currentTarget = aggroTable.OrderByDescending(pair => pair.Value).FirstOrDefault().Key;
        if (currentTarget != null && (currentState == EnemyState.Idle || currentState == EnemyState.Attacking && Vector3.Distance(transform.position, currentTarget.position) > attackRange) )
        {
            currentState = EnemyState.Chasing; // Switch to chasing if we have a new valid target
            Debug.Log($"New target: {currentTarget.name} with hate {aggroTable[currentTarget]}");
        }
         else if (currentTarget == null && currentState != EnemyState.Idle)
        {
            currentState = EnemyState.Idle;
            Debug.Log("Lost all targets, switching to Idle.");
        }
    }

    // Call this method when a player deals damage or performs an action that should increase hate
    public void IncreaseHate(Transform player, float amount)
    {
        if (aggroTable.ContainsKey(player))
        {
            aggroTable[player] += amount;
            Debug.Log($"Hate for player {player.name} increased to {aggroTable[player]}.");
            UpdateCurrentTarget(); // Re-evaluate target after hate changes
        }
        // Optionally, add player to aggro table if they deal damage from outside perception (e.g. sniper)
        // else if (player.CompareTag("Player")) 
        // {
        //     aggroTable[player] = amount; 
        //     UpdateCurrentTarget();
        // }
    }

    void StateMachine()
    {
        // Target acquisition and loss is handled by OnTrigger events and UpdateCurrentTarget now.
        // The StateMachine primarily reacts to the state of currentTarget.

        if (currentTarget == null)
        {
            if (currentState != EnemyState.Idle)
            {
                currentState = EnemyState.Idle;
                IdleBehavior(); // Ensure idle behavior is called immediately
            }
        }

        float distanceToTarget = 0f;
        if (currentTarget != null)
        {
            distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
        }

        switch (currentState)
        {
            case EnemyState.Idle:
                IdleBehavior();
                // Transition to Chasing is handled by UpdateCurrentTarget when a target is found/changes
                break;
            case EnemyState.Chasing:
                if (currentTarget == null) // Target lost (e.g. despawned, or left trigger and aggro cleared)
                {
                    currentState = EnemyState.Idle;
                    break;
                }
                ChaseBehavior();
                if (distanceToTarget <= attackRange)
                {
                    currentState = EnemyState.Attacking;
                }
                break;
            case EnemyState.Attacking:
                if (currentTarget == null) // Target lost
                {
                    currentState = EnemyState.Idle;
                    break;
                }
                AttackBehavior();
                if (distanceToTarget > attackRange)
                {
                    // If still has a target but out of attack range, go back to chasing
                    currentState = EnemyState.Chasing;
                }
                break;
        }
    }

    void IdleBehavior()
    {
        // Debug.Log("Enemy is Idle");
    }

    void ChaseBehavior()
    {
        if (currentTarget == null) return;
        Debug.Log($"Enemy is Chasing {currentTarget.name}");
        // Example: transform.position = Vector3.MoveTowards(transform.position, currentTarget.position, speed * Time.deltaTime);
    }

    void AttackBehavior()
    {
        if (currentTarget == null) return;
        Debug.Log($"Enemy is Attacking {currentTarget.name}");
    }
}
