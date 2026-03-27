using System.Collections;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class FirstPersonController : MonoBehaviour, IDamageable
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 4.0f;
        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 6.0f;
        [Tooltip("Rotation speed of the character")]
        public float RotationSpeed = 1.0f;
        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;
        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.1f;
        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;
        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;
        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.5f;
        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;
        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 90.0f;
        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -90.0f;

        [Header("Combat")]
        [Tooltip("Maximum health for the player")]
        public int MaxHealth = 100;
        [Tooltip("Damage immunity window after taking a hit")]
        public float DamageCooldown = 0.35f;
        [Tooltip("Delay before respawning when health reaches zero")]
        public float RespawnDelay = 1.0f;
        [Tooltip("Optional manual respawn point")]
        public Transform RespawnPoint;
        [Tooltip("Assign the weapon object that has GunScript")]
        public Transform EquippedWeapon;
        [Tooltip("Assign the TMP text that shows HP")]
        public TMP_Text HealthText;

        [Header("Inventory")]
        [Tooltip("How many keys the player needs to finish the level")]
        public int KeyGoal = 3;

        [Header("HUD")]
        [Tooltip("Assign the TMP text that shows collected keys")]
        public TMP_Text KeyText;
        [Tooltip("Assign the TMP text used for prompts and pickup messages")]
        public TMP_Text InteractionText;
        [Tooltip("How long pickup notifications stay on screen")]
        public float NotificationDuration = 1.75f;

        private float _cinemachineTargetPitch;
        private float _speed;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private const float _terminalVelocity = 53.0f;

        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;
        private float _damageCooldownDelta;
        private float _notificationTimer;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GunScript _equippedGun;
        private int _currentHealth;
        private int _currentKeys;
        private bool _isRespawning;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private string _notificationMessage = string.Empty;
        private string _interactionPromptMessage = string.Empty;

        private const float _threshold = 0.01f;

        public bool IsAlive => !_isRespawning && _currentHealth > 0;
        public int CurrentKeys => _currentKeys;
        public int TargetKeys => Mathf.Max(1, KeyGoal);

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        private void Start()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#else
            Debug.LogError("Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
            _currentHealth = MaxHealth;
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;

            ValidateSetup();
            CacheWeaponReference();
            UpdateHealthUI();
            UpdateKeyUI();
            UpdateInteractionUI();
        }

        private void Update()
        {
            if (_damageCooldownDelta > 0.0f)
            {
                _damageCooldownDelta -= Time.deltaTime;
            }

            if (_notificationTimer > 0f)
            {
                _notificationTimer -= Time.deltaTime;
                if (_notificationTimer <= 0f)
                {
                    _notificationMessage = string.Empty;
                }
            }

            if (_isRespawning)
            {
                _interactionPromptMessage = string.Empty;
                UpdateInteractionUI();
                return;
            }

            if (MenuController.IsGamePaused)
            {
                return;
            }

            if (_equippedGun == null)
            {
                CacheWeaponReference();
            }

            JumpAndGravity();
            GroundedCheck();
            Move();
        }

        private void LateUpdate()
        {
            if (_isRespawning || MenuController.IsGamePaused)
            {
                return;
            }

            CameraRotation();
        }

        public void TakeDamage(int damage)
        {
            if (damage <= 0 || !IsAlive || _damageCooldownDelta > 0.0f)
            {
                return;
            }

            _currentHealth = Mathf.Max(0, _currentHealth - damage);
            _damageCooldownDelta = DamageCooldown;
            UpdateHealthUI();

            if (_currentHealth <= 0)
            {
                StartCoroutine(RespawnRoutine());
            }
        }

        public int RestoreHealth(int amount)
        {
            if (amount <= 0 || !IsAlive || _currentHealth >= MaxHealth)
            {
                return 0;
            }

            int previousHealth = _currentHealth;
            _currentHealth = Mathf.Clamp(_currentHealth + amount, 0, MaxHealth);
            UpdateHealthUI();
            return _currentHealth - previousHealth;
        }

        public bool TryCollectAmmo(int amount, out int collectedAmmo)
        {
            collectedAmmo = 0;
            if (amount <= 0)
            {
                return false;
            }

            if (_equippedGun == null)
            {
                CacheWeaponReference();
            }

            if (_equippedGun == null)
            {
                return false;
            }

            collectedAmmo = _equippedGun.AddAmmo(amount);
            return collectedAmmo > 0;
        }

        public bool TryCollectKey(int amount, out int collectedKeys)
        {
            collectedKeys = 0;
            if (amount <= 0 || _currentKeys >= TargetKeys)
            {
                return false;
            }

            int previousKeys = _currentKeys;
            _currentKeys = Mathf.Clamp(_currentKeys + amount, 0, TargetKeys);
            collectedKeys = _currentKeys - previousKeys;

            if (collectedKeys <= 0)
            {
                return false;
            }

            UpdateKeyUI();
            return true;
        }

        public bool HasEnoughKeys(int requiredKeys)
        {
            return _currentKeys >= Mathf.Max(0, requiredKeys);
        }

        public void ShowPickupNotification(string notification)
        {
            if (string.IsNullOrWhiteSpace(notification))
            {
                return;
            }

            _notificationMessage = notification;
            _notificationTimer = Mathf.Max(0.1f, NotificationDuration);
            UpdateInteractionUI();
        }

        public void SetInteractionPrompt(string prompt)
        {
            _interactionPromptMessage = string.IsNullOrWhiteSpace(prompt) ? string.Empty : prompt;
            UpdateInteractionUI();
        }

        public void ClearInteractionPrompt()
        {
            if (string.IsNullOrEmpty(_interactionPromptMessage))
            {
                return;
            }

            _interactionPromptMessage = string.Empty;
            UpdateInteractionUI();
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
        }

        private void CameraRotation()
        {
            if (_input.look.sqrMagnitude >= _threshold)
            {
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
                _rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

                _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

                CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);
                transform.Rotate(Vector3.up * _rotationVelocity);
            }
        }

        private void Move()
        {
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

            if (_input.move == Vector2.zero)
            {
                targetSpeed = 0.0f;
            }

            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;
            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            if (_input.move != Vector2.zero)
            {
                inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
            }

            _controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                }

                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }

                _input.jump = false;
            }

            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            Gizmos.color = Grounded ? transparentGreen : transparentRed;
            Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
        }

        private void ValidateSetup()
        {
            if (HealthText == null)
            {
                Debug.LogWarning($"{nameof(FirstPersonController)} on {name} has no HealthText assigned.");
            }

            if (EquippedWeapon == null)
            {
                Debug.LogWarning($"{nameof(FirstPersonController)} on {name} has no EquippedWeapon assigned.");
            }

            if (KeyText == null)
            {
                Debug.LogWarning($"{nameof(FirstPersonController)} on {name} has no KeyText assigned.");
            }

            if (InteractionText == null)
            {
                Debug.LogWarning($"{nameof(FirstPersonController)} on {name} has no InteractionText assigned.");
            }
        }

        private void UpdateHealthUI()
        {
            if (HealthText == null)
            {
                return;
            }

            HealthText.text = $"HP {_currentHealth} / {MaxHealth}";
        }

        private void UpdateKeyUI()
        {
            if (KeyText == null)
            {
                return;
            }

            KeyText.gameObject.SetActive(true);
            KeyText.text = $"KEYS {_currentKeys} / {TargetKeys}";
        }

        private void UpdateInteractionUI()
        {
            if (InteractionText == null)
            {
                return;
            }

            string message = !string.IsNullOrEmpty(_interactionPromptMessage) ? _interactionPromptMessage : _notificationMessage;
            bool hasMessage = !string.IsNullOrEmpty(message);

            InteractionText.gameObject.SetActive(hasMessage);
            if (hasMessage)
            {
                InteractionText.text = message;
            }
        }

        private IEnumerator RespawnRoutine()
        {
            _isRespawning = true;
            _input.move = Vector2.zero;
            _input.jump = false;
            _verticalVelocity = 0.0f;
            _interactionPromptMessage = string.Empty;
            UpdateInteractionUI();

            yield return new WaitForSeconds(RespawnDelay);

            Vector3 targetPosition = RespawnPoint != null ? RespawnPoint.position : _spawnPosition;
            Quaternion targetRotation = RespawnPoint != null ? RespawnPoint.rotation : _spawnRotation;

            _controller.enabled = false;
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            _controller.enabled = true;

            _currentHealth = MaxHealth;
            _damageCooldownDelta = DamageCooldown;
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
            _isRespawning = false;
            UpdateHealthUI();
        }

        private void CacheWeaponReference()
        {
            if (EquippedWeapon == null)
            {
                return;
            }

            _equippedGun = EquippedWeapon.GetComponent<GunScript>();
            if (_equippedGun == null)
            {
                Debug.LogWarning($"{nameof(FirstPersonController)} on {name} needs a {nameof(GunScript)} on the assigned EquippedWeapon.");
                return;
            }

            _equippedGun.Configure(null, transform);
        }

        private bool WasInteractPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }
    }
}
