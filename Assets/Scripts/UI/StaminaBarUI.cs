using UnityEngine;

[RequireComponent(typeof(ResourceBarUI))]
public sealed class StaminaBarUI : MonoBehaviour
{
    [SerializeField] private PlayerStatsController playerStats;

    private ResourceBarUI resourceBar;
    private bool hasStarted;

    private void Awake()
    {
        resourceBar = GetComponent<ResourceBarUI>();
    }

    private void OnEnable()
    {
        if (hasStarted)
            SubscribeToStamina();
    }

    private void Start()
    {
        hasStarted = true;
        SubscribeToStamina();
    }

    private void OnDisable()
    {
        if (playerStats != null)
            playerStats.StaminaChanged -= HandleStaminaChanged;
    }

    private void SubscribeToStamina()
    {
        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStatsController>();

        if (playerStats == null)
        {
            Debug.LogError("StaminaBarUI could not find a PlayerStatsController.", this);
            return;
        }

        playerStats.StaminaChanged -= HandleStaminaChanged;
        playerStats.StaminaChanged += HandleStaminaChanged;
        HandleStaminaChanged(playerStats.CurrentStamina, playerStats.MaxStamina);
    }

    private void HandleStaminaChanged(float currentStamina, float maxStamina)
    {
        float progress = maxStamina > 0f ? currentStamina / maxStamina : 0f;
        resourceBar.SetProgress(progress);
    }
}
