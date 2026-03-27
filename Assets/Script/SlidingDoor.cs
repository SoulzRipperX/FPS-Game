using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class SlidingDoor : MonoBehaviour
{
    private enum SlideAxis
    {
        X,
        Z
    }

    [Header("References")]
    [SerializeField] private Transform doorPanel;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip moveSound;

    [Header("Movement")]
    [SerializeField] private SlideAxis slideAxis = SlideAxis.X;
    [SerializeField] private float slideDistance = 2f;
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private bool useLocalSpace = true;
    [SerializeField] private bool startsOpen;
    [SerializeField] private bool autoCloseAfterOpen = true;
    [SerializeField] private float openHoldDuration = 2f;

    [Header("Prompt")]
    [SerializeField] private string openPrompt = "Press E to open door";
    [SerializeField] private string closePrompt = "Press E to close door";
    [SerializeField] private string movingPrompt = "Door is moving";
    [SerializeField] private string openedPrompt = "Door is open";

    private const float SnapDistance = 0.001f;

    private FirstPersonController _playerInRange;
    private Collider _interactionTrigger;
    private Vector3 _closedPosition;
    private Vector3 _openPosition;
    private bool _isOpen;
    private bool _isMoving;
    private float _openTimer;

    private void Reset()
    {
        doorPanel = transform;
        audioSource = GetComponent<AudioSource>();
    }

    private void Awake()
    {
        if (doorPanel == null)
        {
            doorPanel = transform;
        }

        _interactionTrigger = GetComponent<Collider>();

        _closedPosition = GetCurrentDoorPosition();
        _openPosition = _closedPosition + GetSlideOffset();
        _isOpen = startsOpen;

        if (_isOpen)
        {
            SetDoorPosition(_openPosition);

            if (autoCloseAfterOpen)
            {
                _openTimer = Mathf.Max(0f, openHoldDuration);
            }
        }

        ValidateSetup();
    }

    private void Update()
    {
        UpdateDoorMovement();
        UpdateOpenTimer();

        if (_playerInRange == null)
        {
            return;
        }

        if (!_playerInRange.IsAlive)
        {
            ClearTrackedPlayer();
            return;
        }

        _playerInRange.SetInteractionPrompt(GetPrompt());

        if (_isMoving || MenuController.IsGamePaused)
        {
            return;
        }

        if (WasInteractPressedThisFrame())
        {
            ToggleDoor();
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

    private string GetPrompt()
    {
        if (_isMoving)
        {
            return movingPrompt;
        }

        if (_isOpen)
        {
            return autoCloseAfterOpen ? openedPrompt : closePrompt;
        }

        return openPrompt;
    }

    private void ToggleDoor()
    {
        _isOpen = !_isOpen;
        _isMoving = true;
        _openTimer = 0f;
        PlayMoveSound();

        if (_playerInRange != null)
        {
            _playerInRange.SetInteractionPrompt(GetPrompt());
        }
    }

    private void UpdateDoorMovement()
    {
        if (doorPanel == null)
        {
            return;
        }

        Vector3 targetPosition = _isOpen ? _openPosition : _closedPosition;
        Vector3 currentPosition = GetCurrentDoorPosition();

        if ((currentPosition - targetPosition).sqrMagnitude <= SnapDistance * SnapDistance)
        {
            if (_isMoving)
            {
                SetDoorPosition(targetPosition);
                _isMoving = false;

                if (_isOpen && autoCloseAfterOpen)
                {
                    _openTimer = Mathf.Max(0f, openHoldDuration);
                }

                if (_playerInRange != null)
                {
                    _playerInRange.SetInteractionPrompt(GetPrompt());
                }
            }

            return;
        }

        _isMoving = true;
        Vector3 nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, Mathf.Max(0.01f, moveSpeed) * Time.deltaTime);
        SetDoorPosition(nextPosition);
    }

    private void UpdateOpenTimer()
    {
        if (!autoCloseAfterOpen || !_isOpen || _isMoving || MenuController.IsGamePaused)
        {
            return;
        }

        if (_openTimer > 0f)
        {
            _openTimer -= Time.deltaTime;
        }

        if (_openTimer <= 0f)
        {
            ToggleDoor();
        }
    }

    private Vector3 GetSlideOffset()
    {
        Vector3 axisVector = slideAxis == SlideAxis.X ? Vector3.right : Vector3.forward;
        return axisVector * slideDistance;
    }

    private Vector3 GetCurrentDoorPosition()
    {
        return useLocalSpace ? doorPanel.localPosition : doorPanel.position;
    }

    private void SetDoorPosition(Vector3 position)
    {
        if (useLocalSpace)
        {
            doorPanel.localPosition = position;
            return;
        }

        doorPanel.position = position;
    }

    private void ClearTrackedPlayer()
    {
        if (_playerInRange != null)
        {
            _playerInRange.ClearInteractionPrompt();
            _playerInRange = null;
        }
    }

    private void PlayMoveSound()
    {
        if (audioSource == null || moveSound == null)
        {
            return;
        }

        audioSource.PlayOneShot(moveSound);
    }

    private void ValidateSetup()
    {
        if (doorPanel == null)
        {
            Debug.LogWarning($"{nameof(SlidingDoor)} on {name} needs a Door Panel assigned.");
        }

        if (_interactionTrigger == null)
        {
            Debug.LogWarning($"{nameof(SlidingDoor)} on {name} needs a trigger Collider for interaction.");
            return;
        }

        if (!_interactionTrigger.isTrigger)
        {
            Debug.LogWarning($"{nameof(SlidingDoor)} on {name} requires the interaction trigger to use Is Trigger.");
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
