using StarterAssets;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public abstract class PickupBase : MonoBehaviour
{
    public bool CanBeCollected => isActiveAndEnabled && gameObject.activeInHierarchy;

    public bool TryCollect(FirstPersonController player)
    {
        if (!CanBeCollected || player == null || !player.IsAlive)
        {
            return false;
        }

        if (!TryApply(player, out string notification))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(notification))
        {
            player.ShowPickupNotification(notification);
        }

        gameObject.SetActive(false);
        return true;
    }

    private void Awake()
    {
        ValidateCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        FirstPersonController player = other.GetComponentInParent<FirstPersonController>();
        if (player == null)
        {
            return;
        }

        TryCollect(player);
    }

    private void ValidateCollider()
    {
        Collider pickupCollider = GetComponent<Collider>();
        if (pickupCollider == null)
        {
            Debug.LogWarning($"{nameof(PickupBase)} on {name} needs a Collider set as Trigger.");
            return;
        }

        if (!pickupCollider.isTrigger)
        {
            Debug.LogWarning($"{nameof(PickupBase)} on {name} requires its Collider to use Is Trigger.");
        }
    }

    protected abstract bool TryApply(FirstPersonController player, out string notification);
}
