using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider))]
public class ExitDoor : MonoBehaviour
{
    [SerializeField] private int requiredKeys = 3;
    [SerializeField] private string sceneToLoad = "Main_Menu";
    private FirstPersonController _playerInRange;
    private Collider _interactionTrigger;

    private void Awake()
    {
        _interactionTrigger = GetComponent<Collider>();
        ValidateCollider();
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

        _playerInRange.SetInteractionPrompt(GetPrompt(_playerInRange));

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
        _playerInRange.SetInteractionPrompt(GetPrompt(player));
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

    public string GetPrompt(FirstPersonController player)
    {
        if (player == null)
        {
            return string.Empty;
        }

        if (!player.HasEnoughKeys(requiredKeys))
        {
            return $"Need {requiredKeys} keys to exit ({player.CurrentKeys}/{requiredKeys})";
        }

        return "Press E to exit";
    }

    public void TryUse(FirstPersonController player)
    {
        if (player == null || !player.HasEnoughKeys(requiredKeys))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneToLoad))
        {
            Debug.LogWarning($"{nameof(ExitDoor)} on {name} has no sceneToLoad configured.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneToLoad);
    }

    private void ClearTrackedPlayer()
    {
        if (_playerInRange != null)
        {
            _playerInRange.ClearInteractionPrompt();
            _playerInRange = null;
        }
    }

    private void ValidateCollider()
    {
        if (_interactionTrigger == null)
        {
            Debug.LogWarning($"{nameof(ExitDoor)} on {name} needs a Collider set as Trigger.");
            return;
        }

        if (!_interactionTrigger.isTrigger)
        {
            Debug.LogWarning($"{nameof(ExitDoor)} on {name} requires its Collider to use Is Trigger.");
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

    private static bool WasInteractPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}
