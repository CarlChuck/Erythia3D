using UnityEngine;

/// <summary>
/// Handles AI control for a NetworkedPlayer when not under player control
/// Implements ICharacterController interface
/// </summary>
public class AIController : MonoBehaviour, ICharacterController
{
    #region AI Settings
    [Header("AI Behavior")]
    [SerializeField] private float wanderRadius = 10f;
    [SerializeField] private float wanderTimer = 5f;
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private float rotationSpeed = 2f;
    
    [Header("AI State")]
    [SerializeField] private AIState currentState = AIState.Idle;
    #endregion
    
    #region Controller State
    private NetworkedPlayer networkedPlayer;
    private bool isActive = false;
    
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float wanderTimerCurrent;
    
    private Vector2 currentMoveInput;
    private Vector2 currentLookInput;
    #endregion
    
    #region Unity Lifecycle
    private void Start()
    {
        startPosition = transform.position;
        targetPosition = startPosition;
        wanderTimerCurrent = wanderTimer;
    }
    
    private void Update()
    {
        if (isActive && networkedPlayer != null)
        {
            UpdateAI();
        }
    }
    #endregion
    
    #region AI Logic
    private void UpdateAI()
    {
        switch (currentState)
        {
            case AIState.Idle:
                HandleIdleState();
                break;
            case AIState.Wandering:
                HandleWanderingState();
                break;
            case AIState.Returning:
                HandleReturningState();
                break;
        }
        
        UpdateMovementInput();
    }
    
    private void HandleIdleState()
    {
        wanderTimerCurrent -= Time.deltaTime;
        
        if (wanderTimerCurrent <= 0f)
        {
            // Choose a random wander target
            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
            randomDirection.y = 0; // Keep on ground
            targetPosition = startPosition + randomDirection;
            
            currentState = AIState.Wandering;
            wanderTimerCurrent = wanderTimer;
        }
        
        currentMoveInput = Vector2.zero;
    }
    
    private void HandleWanderingState()
    {
        Vector3 currentPosition = networkedPlayer.GetPosition();
        Vector3 directionToTarget = (targetPosition - currentPosition).normalized;
        
        // Check if we've reached the target
        if (Vector3.Distance(currentPosition, targetPosition) < 1f)
        {
            currentState = AIState.Idle;
            currentMoveInput = Vector2.zero;
            return;
        }
        
        // Check if we're too far from start position
        if (Vector3.Distance(currentPosition, startPosition) > wanderRadius * 1.5f)
        {
            targetPosition = startPosition;
            currentState = AIState.Returning;
            return;
        }
        
        // Set movement input towards target
        currentMoveInput = new Vector2(directionToTarget.x, directionToTarget.z);
        currentLookInput = currentMoveInput * rotationSpeed;
    }
    
    private void HandleReturningState()
    {
        Vector3 currentPosition = networkedPlayer.GetPosition();
        Vector3 directionToStart = (startPosition - currentPosition).normalized;
        
        // Check if we've returned to start
        if (Vector3.Distance(currentPosition, startPosition) < 2f)
        {
            currentState = AIState.Idle;
            currentMoveInput = Vector2.zero;
            return;
        }
        
        // Set movement input towards start position
        currentMoveInput = new Vector2(directionToStart.x, directionToStart.z);
        currentLookInput = currentMoveInput * rotationSpeed;
    }
    
    private void UpdateMovementInput()
    {
        // Normalize and scale movement input
        if (currentMoveInput.magnitude > 1f)
        {
            currentMoveInput = currentMoveInput.normalized;
        }
        
        currentMoveInput *= moveSpeed;
    }
    #endregion
    
    #region ICharacterController Implementation
    public void Initialize(NetworkedPlayer networkedPlayer)
    {
        this.networkedPlayer = networkedPlayer;
        startPosition = networkedPlayer.GetPosition();
        targetPosition = startPosition;
        
        Debug.Log($"AIController: Initialized with NetworkedPlayer {networkedPlayer.name}");
    }
    
    public void OnControllerActivated()
    {
        isActive = true;
        enabled = true;
        
        // Reset AI state when activated
        currentState = AIState.Idle;
        wanderTimerCurrent = wanderTimer;
        
        Debug.Log("AIController: Activated");
    }
    
    public void OnControllerDeactivated()
    {
        isActive = false;
        enabled = false;
        
        // Clear input values
        currentMoveInput = Vector2.zero;
        currentLookInput = Vector2.zero;
        
        Debug.Log("AIController: Deactivated");
    }
    
    public Vector2 GetMovementInput()
    {
        return currentMoveInput;
    }
    
    public Vector2 GetLookInput()
    {
        return currentLookInput;
    }
    
    public bool GetInteractionInput()
    {
        // AI doesn't interact by default
        return false;
    }
    
    public bool GetJumpInput()
    {
        // AI doesn't jump by default
        return false;
    }
    
    public void HandleInteraction(GameObject target)
    {
        // AI doesn't handle interactions by default
        Debug.Log($"AIController: Interaction with {target.name} (AI doesn't handle interactions)");
    }
    
    public ControllerType GetControllerType()
    {
        return ControllerType.AI;
    }
    #endregion
    
    #region Public Interface
    public bool IsActive()
    {
        return isActive;
    }
    
    public AIState GetCurrentState()
    {
        return currentState;
    }
    
    public void SetWanderRadius(float radius)
    {
        wanderRadius = radius;
    }
    
    public void SetWanderTimer(float timer)
    {
        wanderTimer = timer;
    }
    
    public void SetMoveSpeed(float speed)
    {
        moveSpeed = speed;
    }
    
    /// <summary>
    /// Force the AI to return to its start position
    /// </summary>
    public void ReturnToStart()
    {
        targetPosition = startPosition;
        currentState = AIState.Returning;
    }
    
    /// <summary>
    /// Set a new home position for the AI
    /// </summary>
    public void SetHomePosition(Vector3 newHome)
    {
        startPosition = newHome;
        if (currentState == AIState.Idle)
        {
            targetPosition = startPosition;
        }
    }
    #endregion
}

/// <summary>
/// AI behavior states
/// </summary>
public enum AIState
{
    Idle,       // Standing still, waiting
    Wandering,  // Moving to a random nearby location
    Returning   // Returning to start position
} 