using StarterAssets;
using UnityEngine;

public class KeyPickup : PickupBase
{
    [SerializeField] private int keyAmount = 1;
    [SerializeField] private string keyLabel = "Key";

    protected override bool TryApply(FirstPersonController player, out string notification)
    {
        notification = string.Empty;
        if (!player.TryCollectKey(keyAmount, out int collectedKeys) || collectedKeys <= 0)
        {
            return false;
        }

        notification = $"{keyLabel} {player.CurrentKeys}/{player.TargetKeys}";
        return true;
    }
}
