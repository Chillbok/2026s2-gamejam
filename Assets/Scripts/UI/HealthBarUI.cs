using UnityEngine;

[RequireComponent(typeof(ResourceBarUI))]
public sealed class HealthBarUI : MonoBehaviour
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
            SubscribeToHealth();
    }

    private void Start()
    {
        hasStarted = true;
        SubscribeToHealth();
    }

    private void OnDisable()
    {
        if (playerStats != null)
            playerStats.HealthChanged -= HandleHealthChanged;
    }

    private void SubscribeToHealth()
    {
        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStatsController>();

        if (playerStats == null)
        {
            Debug.LogError("HealthBarUI could not find a PlayerStatsController.", this);
            return;
        }

        playerStats.HealthChanged -= HandleHealthChanged;
        playerStats.HealthChanged += HandleHealthChanged;
        HandleHealthChanged(playerStats.CurrentHealth, playerStats.MaxHealth);
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        float progress = maxHealth > 0f ? currentHealth / maxHealth : 0f;
        resourceBar.SetProgress(progress);
    }
}
