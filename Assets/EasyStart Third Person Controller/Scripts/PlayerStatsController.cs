using System;
using UnityEngine;

public class PlayerStatsController : MonoBehaviour, IDamageable
{
    [Serializable]
    public class ResourceSettings
    {
        [SerializeField, Min(0f)] private float maxValue = 100f;
        [SerializeField, Min(0f)] private float startingValue = 100f;

        public float MaxValue => Mathf.Max(0f, maxValue);
        public float StartingValue => Mathf.Clamp(startingValue, 0f, MaxValue);
    }

    [Serializable]
    public class MovementSettings
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 7.5f;
        [SerializeField, Min(0f)] private float crouchSpeed = 2f;
        [SerializeField, Min(0f)] private float turnSpeed = 720f;

        [Header("Jump and Gravity")]
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.5f;
        [SerializeField, Min(0.1f)] private float gravity = 20f;
        [SerializeField, Min(1f)] private float fallGravityMultiplier = 2f;

        [Header("Crouch")]
        [SerializeField, Min(0.5f)] private float crouchHeight = 0.95f;

        public float WalkSpeed => Mathf.Max(0f, walkSpeed);
        public float SprintSpeed => Mathf.Max(0f, sprintSpeed);
        public float CrouchSpeed => Mathf.Max(0f, crouchSpeed);
        public float TurnSpeed => Mathf.Max(0f, turnSpeed);
        public float JumpHeight => Mathf.Max(0.1f, jumpHeight);
        public float Gravity => Mathf.Max(0.1f, gravity);
        public float FallGravityMultiplier => Mathf.Max(1f, fallGravityMultiplier);
        public float CrouchHeight => Mathf.Max(0.5f, crouchHeight);
    }

    [Header("Health")]
    [SerializeField] private ResourceSettings health = new ResourceSettings();

    [Header("Stamina")]
    [SerializeField] private ResourceSettings stamina = new ResourceSettings();
    [SerializeField, Min(0f)] private float staminaDrainPerSecond = 20f;
    [SerializeField, Min(0f)] private float staminaRegenPerSecond = 15f;
    [SerializeField, Min(0f)] private float staminaRegenDelay = 1.25f;

    [Header("Movement Stats")]
    [SerializeField] private MovementSettings movement = new MovementSettings();

    private float staminaTimeSinceLastUse;

    public float MaxHealth => health.MaxValue;
    public float CurrentHealth { get; private set; }
    public float HealthRatio => MaxHealth > 0f ? CurrentHealth / MaxHealth : 0f;
    public bool IsAlive => CurrentHealth > 0f;

    public float MaxStamina => stamina.MaxValue;
    public float CurrentStamina { get; private set; }
    public float StaminaRatio => MaxStamina > 0f ? CurrentStamina / MaxStamina : 0f;
    public float StaminaDrainPerSecond => Mathf.Max(0f, staminaDrainPerSecond);
    public float StaminaRegenPerSecond => Mathf.Max(0f, staminaRegenPerSecond);
    public float StaminaRegenDelay => Mathf.Max(0f, staminaRegenDelay);
    public bool CanSprint => IsAlive && CurrentStamina > 0f;

    public float WalkSpeed => movement.WalkSpeed;
    public float SprintSpeed => movement.SprintSpeed;
    public float CrouchSpeed => movement.CrouchSpeed;
    public float TurnSpeed => movement.TurnSpeed;
    public float JumpHeight => movement.JumpHeight;
    public float Gravity => movement.Gravity;
    public float FallGravityMultiplier => movement.FallGravityMultiplier;
    public float CrouchHeight => movement.CrouchHeight;

    public event Action<float, float> HealthChanged;
    public event Action<float, float> StaminaChanged;
    public event Action Died;

    private void Awake()
    {
        CurrentHealth = health.StartingValue;
        CurrentStamina = stamina.StartingValue;
        staminaTimeSinceLastUse = StaminaRegenDelay;
    }

    private void Update()
    {
        if (CurrentStamina >= MaxStamina || StaminaRegenPerSecond <= 0f)
            return;

        staminaTimeSinceLastUse += Time.deltaTime;
        if (staminaTimeSinceLastUse < StaminaRegenDelay)
            return;

        SetCurrentStamina(CurrentStamina + StaminaRegenPerSecond * Time.deltaTime);
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f)
            return;

        SetCurrentHealth(CurrentHealth - amount);
        if (!IsAlive)
            Died?.Invoke();
    }

    public void Heal(float amount)
    {
        if (!IsAlive || amount <= 0f)
            return;

        SetCurrentHealth(CurrentHealth + amount);
    }

    public bool TryConsumeStamina(float amount)
    {
        if (!CanSprint)
            return false;

        if (amount <= 0f)
            return true;

        staminaTimeSinceLastUse = 0f;
        SetCurrentStamina(CurrentStamina - amount);
        return CanSprint;
    }

    public void RestoreStamina(float amount)
    {
        if (amount <= 0f)
            return;

        SetCurrentStamina(CurrentStamina + amount);
    }

    private void SetCurrentHealth(float value)
    {
        float clampedValue = Mathf.Clamp(value, 0f, MaxHealth);
        if (Mathf.Approximately(CurrentHealth, clampedValue))
            return;

        CurrentHealth = clampedValue;
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    private void SetCurrentStamina(float value)
    {
        float clampedValue = Mathf.Clamp(value, 0f, MaxStamina);
        if (Mathf.Approximately(CurrentStamina, clampedValue))
            return;

        CurrentStamina = clampedValue;
        StaminaChanged?.Invoke(CurrentStamina, MaxStamina);
    }
}
