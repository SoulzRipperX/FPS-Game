using StarterAssets;
using UnityEngine;

public class AmmoPickup : PickupBase
{
    [SerializeField] private int ammoAmount = 12;

    protected override bool TryApply(FirstPersonController player, out string notification)
    {
        notification = string.Empty;
        if (!player.TryCollectAmmo(ammoAmount, out int collectedAmmo))
        {
            return false;
        }

        notification = $"Ammo +{collectedAmmo}";
        return true;
    }
}
