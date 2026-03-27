using StarterAssets;
using UnityEngine;

public class HealthPickup : PickupBase
{
    [SerializeField] private int healAmount = 35;

    protected override bool TryApply(FirstPersonController player, out string notification)
    {
        notification = string.Empty;

        int restoredHealth = player.RestoreHealth(healAmount);
        if (restoredHealth <= 0)
        {
            return false;
        }

        notification = $"HP +{restoredHealth}";
        return true;
    }
}
