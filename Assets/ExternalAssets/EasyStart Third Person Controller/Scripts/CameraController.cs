
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Camera movement script for third person games.
/// This Script should not be applied to the camera! It is attached to an empty object and inside
/// it (as a child object) should be your game's MainCamera.
/// </summary>
public class CameraController : MonoBehaviour
{

    [Tooltip("Enable to move the camera by holding the right mouse button. Does not work with joysticks.")]
    public bool clickToMoveCamera = false;
    [Tooltip("Mouse look sensitivity in degrees per pixel.")]
    [Min(0f)] public float sensitivity = 0.12f;

    [Tooltip("Camera Y rotation limits. The X axis is the maximum it can go up and the Y axis is the maximum it can go down.")]
    public Vector2 cameraLimit = new Vector2(-45, 40);

    private float mouseX;
    private float mouseY;
    private float offsetDistanceY;
    private Transform player;
    private InputAction lookAction;

    private void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null)
        {
            Debug.LogError("CameraController could not find an object tagged Player.", this);
            enabled = false;
            return;
        }

        player = playerObject.transform;
        PlayerInput playerInput = playerObject.GetComponent<PlayerInput>();
        if (playerInput == null || playerInput.actions == null)
        {
            Debug.LogError("The Player needs a PlayerInput component with InputSystem_Actions assigned.", this);
            enabled = false;
            return;
        }

        lookAction = playerInput.actions.FindAction("Player/Look", true);
        offsetDistanceY = transform.position.y;

        if (!clickToMoveCamera)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LateUpdate()
    {
        transform.position = player.position + new Vector3(0, offsetDistanceY, 0);

        if (clickToMoveCamera && Mouse.current != null && !Mouse.current.rightButton.isPressed)
            return;

        Vector2 look = lookAction.ReadValue<Vector2>();
        mouseX += look.x * sensitivity;
        mouseY += look.y * sensitivity;
        mouseY = Mathf.Clamp(mouseY, cameraLimit.x, cameraLimit.y);
        transform.rotation = Quaternion.Euler(-mouseY, mouseX, 0);
    }
}
