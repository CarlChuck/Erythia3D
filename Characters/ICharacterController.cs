using UnityEngine;

/// <summary>
/// Interface for character controllers (Player Input or AI)
/// Defines the contract for controlling a NetworkedPlayer
/// </summary>
public interface ICharacterController
{
    /// <summary>
    /// Initialize the controller with a reference to the NetworkedPlayer it controls
    /// </summary>
    void Initialize(NetworkedPlayer networkedPlayer);
    
    /// <summary>
    /// Called when this controller becomes active
    /// </summary>
    void OnControllerActivated();
    
    /// <summary>
    /// Called when this controller becomes inactive
    /// </summary>
    void OnControllerDeactivated();
    
    /// <summary>
    /// Get the current movement input (for both player and AI)
    /// </summary>
    Vector2 GetMovementInput();
    
    /// <summary>
    /// Get the current look input (for both player and AI)
    /// </summary>
    Vector2 GetLookInput();
    
    /// <summary>
    /// Check if interaction input is pressed this frame
    /// </summary>
    bool GetInteractionInput();
    
    /// <summary>
    /// Check if jump input is pressed this frame
    /// </summary>
    bool GetJumpInput();
    
    /// <summary>
    /// Handle interaction with target object
    /// </summary>
    void HandleInteraction(GameObject target);
    
    /// <summary>
    /// Get the controller type for identification
    /// </summary>
    ControllerType GetControllerType();
}

/// <summary>
/// Types of character controllers
/// </summary>
public enum ControllerType
{
    None,
    PlayerInput,
    AI
} 