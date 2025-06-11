using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// Handles player input for controlling a NetworkedPlayer
/// Implements ICharacterController interface
/// </summary>
public class PlayerInputController : MonoBehaviour, ICharacterController
{
    #region Input Actions
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction interactAction;
    #endregion
    
    #region Controller State
    private NetworkedPlayer networkedPlayer;
    private bool isActive = false;
    
    [Header("Input Settings")]
    [SerializeField] private float mouseSensitivity = 1f;
    [SerializeField] private bool invertMouseY = false;
    #endregion
    
    #region Input Values
    private Vector2 currentMoveInput;
    private Vector2 currentLookInput;
    private bool jumpPressed;
    private bool interactPressed;
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {
        SetupInputActions();
    }
    
    private void OnEnable()
    {
        EnableInputActions();
    }
    
    private void OnDisable()
    {
        DisableInputActions();
    }
    
    private void Update()
    {
        if (isActive)
        {
            UpdateInputValues();
        }
    }
    #endregion
    
    #region Input Setup
    private void SetupInputActions()
    {
        if (inputActions != null)
        {
            moveAction = inputActions.FindAction("Move");
            lookAction = inputActions.FindAction("Look");
            jumpAction = inputActions.FindAction("Jump");
            interactAction = inputActions.FindAction("Interact");
        }
        else
        {
            Debug.LogWarning("PlayerInputController: Input Actions asset not assigned, using fallback input");
        }
    }
    
    private void EnableInputActions()
    {
        moveAction?.Enable();
        lookAction?.Enable();
        jumpAction?.Enable();
        interactAction?.Enable();
    }
    
    private void DisableInputActions()
    {
        moveAction?.Disable();
        lookAction?.Disable();
        jumpAction?.Disable();
        interactAction?.Disable();
    }
    #endregion
    
    #region Input Reading
    private void UpdateInputValues()
    {
        // Read movement input
        if (moveAction != null)
        {
            currentMoveInput = moveAction.ReadValue<Vector2>();
        }
        else
        {
            // Fallback to legacy input
            currentMoveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        }
        
        // Read look input
        if (lookAction != null)
        {
            Vector2 rawLookInput = lookAction.ReadValue<Vector2>();
            currentLookInput = new Vector2(
                rawLookInput.x * mouseSensitivity * Time.deltaTime,
                (invertMouseY ? -rawLookInput.y : rawLookInput.y) * mouseSensitivity * Time.deltaTime
            );
        }
        else
        {
            // Fallback to legacy input
            currentLookInput = new Vector2(
                Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime,
                (invertMouseY ? -Input.GetAxis("Mouse Y") : Input.GetAxis("Mouse Y")) * mouseSensitivity * Time.deltaTime
            );
        }
        
        // Read action inputs
        jumpPressed = jumpAction?.WasPressedThisFrame() ?? Input.GetKeyDown(KeyCode.Space);
        interactPressed = interactAction?.WasPressedThisFrame() ?? Input.GetMouseButtonDown(0);
    }
    #endregion
    
    #region ICharacterController Implementation
    public void Initialize(NetworkedPlayer networkedPlayer)
    {
        this.networkedPlayer = networkedPlayer;
        Debug.Log($"PlayerInputController: Initialized with NetworkedPlayer {networkedPlayer.name}");
    }
    
    public void OnControllerActivated()
    {
        isActive = true;
        enabled = true;
        
        // Setup camera to follow this networked player
        if (PlayerManager.LocalInstance != null)
        {
            PlayerManager.LocalInstance.SetCameraTarget(networkedPlayer.transform);
        }
        
        Debug.Log("PlayerInputController: Activated");
    }
    
    public void OnControllerDeactivated()
    {
        isActive = false;
        enabled = false;
        
        // Clear input values
        currentMoveInput = Vector2.zero;
        currentLookInput = Vector2.zero;
        jumpPressed = false;
        interactPressed = false;
        
        Debug.Log("PlayerInputController: Deactivated");
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
        bool wasPressed = interactPressed;
        interactPressed = false; // Reset so it only returns true once per press
        return wasPressed;
    }
    
    public bool GetJumpInput()
    {
        bool wasPressed = jumpPressed;
        jumpPressed = false; // Reset so it only returns true once per press
        return wasPressed;
    }
    
    public void HandleInteraction(GameObject target)
    {
        if (networkedPlayer == null) return;
        
        // Check if target has a NetworkObject for server interaction
        NetworkObject targetNetworkObject = target.GetComponent<NetworkObject>();
        if (targetNetworkObject != null)
        {
            networkedPlayer.InteractWithObjectServerRpc(targetNetworkObject.NetworkObjectId);
        }
        else
        {
            Debug.LogWarning($"PlayerInputController: Target {target.name} does not have NetworkObject component");
        }
    }
    
    public ControllerType GetControllerType()
    {
        return ControllerType.PlayerInput;
    }
    #endregion
    
    #region Public Interface
    public bool IsActive()
    {
        return isActive;
    }
    
    public void SetMouseSensitivity(float sensitivity)
    {
        mouseSensitivity = sensitivity;
    }
    
    public void SetInvertMouseY(bool invert)
    {
        invertMouseY = invert;
    }
    #endregion
} 