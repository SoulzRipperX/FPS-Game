using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class LiftTeleporter : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] private Transform destinationPoint;
    [SerializeField] private bool faceDestinationForward = true;

    [Header("Prompt")]
    [SerializeField] private string interactPrompt = "Press E to use lift";
    [SerializeField] private string missingDestinationPrompt = "Lift destination is not configured";
    [SerializeField] private string arrivalNotification = string.Empty;

    private FirstPersonController _playerInRange;
    private Collider _interactionTrigger;

    private void Awake()
    {
        _interactionTrigger = GetComponent<Collider>();
        ValidateSetup();
    }

    private void Update()
    {
        if (_playerInRange == null)
        {
            return;
        }

        if (!_playerInRange.IsAlive)
        {
            ClearTrackedPlayer();
            return;
        }

        if (!IsTrackedPlayerStillInsideTrigger())
        {
            ClearTrackedPlayer();
            return;
        }

        _playerInRange.SetInteractionPrompt(GetPrompt());

        if (MenuController.IsGamePaused)
        {
            return;
        }

        if (WasInteractPressedThisFrame())
        {
            TryUse(_playerInRange);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        FirstPersonController player = other.GetComponentInParent<FirstPersonController>();
        if (player == null)
        {
            return;
        }

        _playerInRange = player;
        _playerInRange.SetInteractionPrompt(GetPrompt());
    }

    private void OnTriggerExit(Collider other)
    {
        FirstPersonController player = other.GetComponentInParent<FirstPersonController>();
        if (player == null || player != _playerInRange)
        {
            return;
        }

        ClearTrackedPlayer();
    }

    private void OnDisable()
    {
        ClearTrackedPlayer();
    }

    public void TryUse(FirstPersonController player)
    {
        if (player == null)
        {
            return;
        }

        if (destinationPoint == null)
        {
            player.ShowPickupNotification(missingDestinationPrompt);
            return;
        }

        ClearTrackedPlayer();
        player.TeleportTo(destinationPoint.position, destinationPoint.eulerAngles.y, faceDestinationForward);

        if (!string.IsNullOrWhiteSpace(arrivalNotification))
        {
            player.ShowPickupNotification(arrivalNotification);
        }
    }

    private string GetPrompt()
    {
        return destinationPoint == null ? missingDestinationPrompt : interactPrompt;
    }

    private void ClearTrackedPlayer()
    {
        if (_playerInRange != null)
        {
            _playerInRange.ClearInteractionPrompt();
            _playerInRange = null;
        }
    }

    private bool IsTrackedPlayerStillInsideTrigger()
    {
        if (_playerInRange == null || _interactionTrigger == null)
        {
            return false;
        }

        CharacterController playerCollider = _playerInRange.GetComponent<CharacterController>();
        if (playerCollider == null)
        {
            return _interactionTrigger.bounds.Contains(_playerInRange.transform.position);
        }

        return _interactionTrigger.bounds.Intersects(playerCollider.bounds);
    }

    private void ValidateSetup()
    {
        if (_interactionTrigger == null)
        {
            Debug.LogWarning($"{nameof(LiftTeleporter)} on {name} needs a Collider set as Trigger.");
            return;
        }

        if (!_interactionTrigger.isTrigger)
        {
            Debug.LogWarning($"{nameof(LiftTeleporter)} on {name} requires its Collider to use Is Trigger.");
        }

        if (destinationPoint == null)
        {
            Debug.LogWarning($"{nameof(LiftTeleporter)} on {name} has no destinationPoint assigned.");
        }
    }

    private static bool WasInteractPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}
