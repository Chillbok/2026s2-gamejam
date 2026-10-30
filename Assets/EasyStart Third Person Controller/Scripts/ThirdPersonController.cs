
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement")]
    [Min(0f)] public float walkSpeed = 4.5f;
    [Min(0f)] public float sprintSpeed = 7.5f;
    [Min(0f)] public float crouchSpeed = 2f;
    [Min(0f)] public float turnSpeed = 720f;

    [Header("Jump and Gravity")]
    [Tooltip("Target jump height in world units.")]
    [Min(0.1f)] public float jumpHeight = 1.5f;
    [Tooltip("Base downward gravity strength. Higher values make the jump arc faster.")]
    [Min(0.1f)] public float gravity = 20f;
    [Tooltip("Gravity multiplier used only while descending. Increase this for a faster fall.")]
    [Min(1f)] public float fallGravityMultiplier = 2f;

    [Header("Crouch")]
    [Min(0.5f)] public float crouchHeight = 0.95f;

    [Header("Interaction")]
    public UnityEvent onInteract = new UnityEvent();

    private CharacterController characterController;
    private Animator animator;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction jumpAction;
    private InputAction crouchAction;
    private InputAction interactAction;

    private float standingHeight;
    private Vector3 standingCenter;
    private float verticalVelocity;
    private bool isCrouching;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        playerInput = GetComponent<PlayerInput>();
        standingHeight = characterController.height;
        standingCenter = characterController.center;
    }

    private void Start()
    {
        if (playerInput.actions == null)
        {
            Debug.LogError("PlayerInput needs the InputSystem_Actions asset assigned.", this);
            enabled = false;
            return;
        }

        InputActionMap playerMap = playerInput.actions.FindActionMap("Player", true);
        moveAction = playerMap.FindAction("Move", true);
        sprintAction = playerMap.FindAction("Sprint", true);
        jumpAction = playerMap.FindAction("Jump", true);
        crouchAction = playerMap.FindAction("Crouch", true);
        interactAction = playerMap.FindAction("Interact", true);
        interactAction.performed += HandleInteract;
    }

    private void OnDestroy()
    {
        if (interactAction != null)
            interactAction.performed -= HandleInteract;
    }

    private void Update()
    {
        if (crouchAction.WasPressedThisFrame())
            SetCrouching(!isCrouching);

        bool grounded = characterController.isGrounded;
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (jumpAction.WasPressedThisFrame() && grounded)
        {
            if (!isCrouching || SetCrouching(false))
                verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
        }

        Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        Vector3 moveDirection = GetCameraRelativeDirection(input);
        bool isMoving = moveDirection.sqrMagnitude > 0.001f;
        bool isSprinting = !isCrouching && isMoving && sprintAction.IsPressed();
        float speed = isCrouching ? crouchSpeed : isSprinting ? sprintSpeed : walkSpeed;

        if (isMoving)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime);
        }

        float gravityMultiplier = verticalVelocity < 0f ? fallGravityMultiplier : 1f;
        verticalVelocity -= gravity * gravityMultiplier * Time.deltaTime;
        Vector3 velocity = moveDirection * speed + Vector3.up * verticalVelocity;
        CollisionFlags collisions = characterController.Move(velocity * Time.deltaTime);
        if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;

        if (animator != null)
        {
            animator.SetBool("crouch", isCrouching);
            animator.SetBool("run", isMoving);
            animator.SetBool("sprint", isSprinting);
            animator.SetBool("air", !characterController.isGrounded);
        }
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        Transform cameraTransform = Camera.main != null ? Camera.main.transform : transform;
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 direction = forward * input.y + right * input.x;
        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
    }

    private void HandleInteract(InputAction.CallbackContext context)
    {
        Debug.Log("Interaction key pressed (E).", this);
        onInteract?.Invoke();
    }

    private bool SetCrouching(bool crouch)
    {
        if (crouch == isCrouching)
            return true;

        if (crouch)
        {
            float newHeight = Mathf.Clamp(crouchHeight, characterController.radius * 2f, standingHeight);
            characterController.height = newHeight;
            characterController.center = standingCenter + Vector3.down * ((standingHeight - newHeight) * 0.5f);
            isCrouching = true;
            return true;
        }

        if (!HasRoomToStand())
            return false;

        characterController.height = standingHeight;
        characterController.center = standingCenter;
        isCrouching = false;
        return true;
    }

    private bool HasRoomToStand()
    {
        float radius = Mathf.Max(0.01f, characterController.radius - characterController.skinWidth);
        float capsuleSegment = Mathf.Max(0f, standingHeight * 0.5f - radius);
        Vector3 center = transform.TransformPoint(standingCenter);
        Vector3 bottom = center - Vector3.up * capsuleSegment;
        Vector3 top = center + Vector3.up * capsuleSegment;

        Collider[] overlaps = Physics.OverlapCapsule(
            bottom,
            top,
            radius,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        foreach (Collider overlap in overlaps)
        {
            if (overlap == characterController || overlap.transform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }
}
