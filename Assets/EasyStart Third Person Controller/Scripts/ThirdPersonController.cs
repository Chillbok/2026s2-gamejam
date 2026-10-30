
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerStatsController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Grounding")]
    [Tooltip("How far below the Character Controller's foot to check for ground.")]
    [Min(0f)] public float groundProbeDistance = 0.15f;
    [Tooltip("How long to remain ungrounded before starting the falling animation.")]
    [Min(0f)] public float fallAnimationDelay = 0.1f;

    [Header("Interaction")]
    public UnityEvent onInteract = new UnityEvent();

    private CharacterController characterController;
    private Animator animator;
    private PlayerInput playerInput;
    private PlayerStatsController playerStats;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction jumpAction;
    private InputAction crouchAction;
    private InputAction interactAction;

    private float standingHeight;
    private Vector3 standingCenter;
    private float verticalVelocity;
    private float ungroundedTime;
    private bool isCrouching;
    private bool wasAirborne;
    private readonly Collider[] groundCheckHits = new Collider[8];

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        playerInput = GetComponent<PlayerInput>();
        playerStats = GetComponent<PlayerStatsController>();
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

        bool grounded = IsGrounded();
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (jumpAction.WasPressedThisFrame() && grounded)
        {
            if (!isCrouching || SetCrouching(false))
                verticalVelocity = Mathf.Sqrt(2f * playerStats.Gravity * playerStats.JumpHeight);
        }

        Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        Vector3 moveDirection = GetCameraRelativeDirection(input);
        bool isMoving = moveDirection.sqrMagnitude > 0.001f;
        bool wantsToSprint = !isCrouching && isMoving && sprintAction.IsPressed();
        bool isSprinting = wantsToSprint && playerStats.CanSprint;
        if (isSprinting)
        {
            isSprinting = playerStats.TryConsumeStamina(
                playerStats.StaminaDrainPerSecond * Time.deltaTime);
        }

        float speed = isCrouching
            ? playerStats.CrouchSpeed
            : isSprinting ? playerStats.SprintSpeed : playerStats.WalkSpeed;

        if (isMoving)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                playerStats.TurnSpeed * Time.deltaTime);
        }

        float gravityMultiplier = verticalVelocity < 0f ? playerStats.FallGravityMultiplier : 1f;
        verticalVelocity -= playerStats.Gravity * gravityMultiplier * Time.deltaTime;
        Vector3 velocity = moveDirection * speed + Vector3.up * verticalVelocity;
        CollisionFlags collisions = characterController.Move(velocity * Time.deltaTime);
        if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;

        bool groundedAfterMove = IsGrounded();
        if (verticalVelocity > 0f || groundedAfterMove)
            ungroundedTime = 0f;
        else
            ungroundedTime += Time.deltaTime;

        bool isAirborne = verticalVelocity > 0f || (!groundedAfterMove && ungroundedTime >= fallAnimationDelay);
        
        // 플레이어가 땅에 있는지 확인하기 위한 주석임. 필요 없으면 지워도 됨.
        /*
        if (isAirborne && !wasAirborne)
            Debug.Log("Player left the ground.", this);
        */

        wasAirborne = isAirborne;

        if (animator != null)
        {
            animator.SetBool("crouch", isCrouching);
            animator.SetBool("run", isMoving);
            animator.SetBool("sprint", isSprinting);
            animator.SetBool("air", isAirborne);
        }
    }

    private bool IsGrounded()
    {
        if (characterController.isGrounded)
            return true;

        Vector3 worldCenter = transform.TransformPoint(characterController.center);
        float bottomSphereOffset = characterController.height * 0.5f - characterController.radius;
        Vector3 footSphereCenter = worldCenter - Vector3.up * bottomSphereOffset;
        Vector3 probeCenter = footSphereCenter - Vector3.up * groundProbeDistance;
        float probeRadius = Mathf.Max(0.05f, characterController.radius * 0.8f);

        int hitCount = Physics.OverlapSphereNonAlloc(
            probeCenter,
            probeRadius,
            groundCheckHits,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = groundCheckHits[i];
            if (hit == null || hit == characterController || hit.transform.IsChildOf(transform))
                continue;

            return true;
        }

        return false;
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
        // 플레이어가 상호작용 키를 눌렀는지 확인하기 위한 로그 코드. 작동 확인했으면 지워도 됨.
        // Debug.Log("Interaction key pressed (E).", this);
        onInteract?.Invoke();
    }

    private bool SetCrouching(bool crouch)
    {
        if (crouch == isCrouching)
            return true;

        if (crouch)
        {
            float newHeight = Mathf.Clamp(
                playerStats.CrouchHeight,
                characterController.radius * 2f,
                standingHeight);
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
