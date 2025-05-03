using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputs : MonoBehaviour
{
	[Header("Character Input Values")]
	public Vector2 move;
	public Vector2 look;
	public bool jump;
	public bool sprint;

	[Header("Movement Settings")]
	public bool analogMovement;

	[Header("Mouse Cursor Settings")]
	public bool cursorLocked = true;
	public bool cursorInputForLook = true;

	private bool cursorWasLockedBeforeHold = false; // Track state for hold action
	private Vector2 savedCursorPosition; // To restore cursor position

	public void OnMove(InputValue value)
	{
		MoveInput(value.Get<Vector2>());
	}

	public void OnLook(InputValue value)
	{
		if(cursorInputForLook)
		{
			LookInput(value.Get<Vector2>());
		}
	}

	public void OnJump(InputValue value)
	{
		JumpInput(value.isPressed);
	}

	public void OnSprint(InputValue value)
	{
		SprintInput(value.isPressed);
	}

	public void OnCharacterWindow(InputValue value)
    {
        if (value.isPressed)
        {
			// Open character window
			PlayerManager.Instance.GetUIManager().ToggleCharacterWindow();
        }
    }
	public void OnInteract(InputValue value)
	{
        if (value.isPressed)
        {
            var playerCharacter = PlayerManager.Instance?.GetSelectedPlayerCharacter();
            if (playerCharacter != null)
            {
                playerCharacter.InteractWithTarget();
            }
        }
    }
	public void OnCursorToggle(InputValue value)
	{
		if (value.isPressed)
		{
            bool currentlyLocked = Cursor.lockState == CursorLockMode.Locked;
            SetCursorState(!currentlyLocked); // Use the helper function
		}
	}
	public void OnCursorHold(InputValue value)
	{
		bool buttonIsPressed = value.isPressed;

        if (buttonIsPressed)
        {
            cursorWasLockedBeforeHold = (Cursor.lockState == CursorLockMode.Locked);

            if (!cursorWasLockedBeforeHold)
            {
                 // Save position BEFORE locking/hiding
                 savedCursorPosition = Mouse.current.position.ReadValue();
                 SetCursorState(true); // Lock the cursor
            }
        }
        else // Button released
        {
            // If the cursor was NOT locked before we started holding, unlock it now
            if (!cursorWasLockedBeforeHold)
            {
                 SetCursorState(false);
                 Mouse.current.WarpCursorPosition(savedCursorPosition);
                 LookInput(Vector2.zero);
            }
        }
	}

    public void MoveInput(Vector2 newMoveDirection)
	{
		move = newMoveDirection;
	} 

	public void LookInput(Vector2 newLookDirection)
	{
		look = newLookDirection;
	}

	public void JumpInput(bool newJumpState)
	{
		jump = newJumpState;
	}

	public void SprintInput(bool newSprintState)
	{
		sprint = newSprintState;
	}
		
	private void OnApplicationFocus(bool hasFocus)
	{
        // Apply the last explicitly set cursor state on focus change
		SetCursorState(cursorLocked);
	}

	private void SetCursorState(bool newState)
	{
		cursorLocked = newState;
        CursorLockMode targetMode = newState ? CursorLockMode.Locked : CursorLockMode.None;
		Cursor.lockState = targetMode;
        Cursor.visible = !newState;
        cursorInputForLook = newState;
    }	
}