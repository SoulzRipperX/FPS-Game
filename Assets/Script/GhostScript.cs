using System.Collections;
using UnityEngine;

namespace Sample
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CharacterController))]
    public class GhostScript : MonoBehaviour, IDamageable
    {
        private static readonly int IdleState = Animator.StringToHash("Base Layer.idle");
        private static readonly int MoveState = Animator.StringToHash("Base Layer.move");
        private static readonly int SurprisedState = Animator.StringToHash("Base Layer.surprised");
        private static readonly int AttackState = Animator.StringToHash("Base Layer.attack_shift");
        private static readonly int DissolveState = Animator.StringToHash("Base Layer.dissolve");

        [Header("References")]
        [SerializeField] private Transform PlayerTarget;
        [SerializeField] private string PlayerTag = "Player";
        [SerializeField] private SkinnedMeshRenderer[] MeshR;

        [Header("Movement")]
        [SerializeField] private float Speed = 4f;
        [SerializeField] private float WanderRadius = 8f;
        [SerializeField] private float WanderPauseMin = 0.75f;
        [SerializeField] private float WanderPauseMax = 2f;
        [SerializeField] private float RotationSpeed = 8f;
        [SerializeField] private float Gravity = -20f;

        [Header("Combat")]
        [SerializeField] private float DetectRange = 12f;
        [SerializeField] private float AttackRange = 1.75f;
        [SerializeField] private Transform AttackPoint;
        [SerializeField] private float AttackForwardOffset = 0.15f;
        [SerializeField] private float AttackHeightOffset = 0.45f;
        [SerializeField] private float AttackHitRadius = 0.35f;
        [SerializeField] private float AttackCooldown = 1.2f;
        [SerializeField] private float AttackWindup = 0.35f;
        [SerializeField] private float HitStunDuration = 0.35f;
        [SerializeField] private int AttackDamage = 15;
        [SerializeField] private int MaxHP = 3;
        [SerializeField] private float RespawnDelay = 3f;

        private Animator Anim;
        private CharacterController Ctrl;
        private Vector3 SpawnPosition;
        private Quaternion SpawnRotation;
        private Vector3 CurrentDestination;
        private float VerticalVelocity;
        private float NextWanderDecisionTime;
        private float AttackCooldownTimer;
        private float HitStunTimer;
        private float RespawnTimer;
        private float Dissolve_value = 1f;
        private int HP;
        private bool DissolveFlg;
        private bool HasDestination;
        private bool AttackNeedsReset;
        private Coroutine AttackRoutineHandle;
        private readonly Collider[] AttackHitBuffer = new Collider[8];

        private void Awake()
        {
            Anim = GetComponent<Animator>();
            Ctrl = GetComponent<CharacterController>();
            SpawnPosition = transform.position;
            SpawnRotation = transform.rotation;
            HP = MaxHP;

            if (MeshR == null || MeshR.Length == 0)
            {
                MeshR = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }
        }

        private void Start()
        {
            ResolvePlayerTarget();
            ResetDissolve();
            CrossFadeState(IdleState);
        }

        private void Update()
        {
            ResolvePlayerTarget();

            if (AttackCooldownTimer > 0f)
            {
                AttackCooldownTimer -= Time.deltaTime;
            }

            if (HitStunTimer > 0f)
            {
                HitStunTimer -= Time.deltaTime;
            }

            if (AttackNeedsReset && !IsPlayerInsideAttackZone())
            {
                AttackNeedsReset = false;
            }

            if (DissolveFlg)
            {
                UpdateDissolveAndRespawn();
                return;
            }

            Vector3 horizontalVelocity = Vector3.zero;

            if (AttackRoutineHandle != null)
            {
                FaceTarget(PlayerTarget != null ? PlayerTarget.position : transform.position + transform.forward);
            }
            else if (HitStunTimer <= 0f)
            {
                float playerDistance = GetHorizontalDistanceToPlayer();
                if (PlayerTarget != null && playerDistance <= DetectRange)
                {
                    FaceTarget(PlayerTarget.position);

                    if (IsPlayerInsideAttackZone())
                    {
                        TryAttackPlayer();
                    }
                    else
                    {
                        horizontalVelocity = GetMoveVelocity(PlayerTarget.position, Speed);
                    }
                }
                else
                {
                    horizontalVelocity = GetWanderVelocity();
                }
            }

            ApplyMovement(horizontalVelocity);
            UpdateAnimation(horizontalVelocity);
        }

        public void TakeDamage(int damage)
        {
            if (damage <= 0 || HP <= 0)
            {
                return;
            }

            HP = Mathf.Max(0, HP - damage);

            if (HP <= 0)
            {
                BeginDeath();
                return;
            }

            if (AttackRoutineHandle != null)
            {
                StopCoroutine(AttackRoutineHandle);
                AttackRoutineHandle = null;
            }

            HitStunTimer = HitStunDuration;
            CrossFadeState(SurprisedState);
        }

        private void ResolvePlayerTarget()
        {
            if (PlayerTarget != null)
            {
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag(PlayerTag);
            if (player != null)
            {
                PlayerTarget = player.transform;
            }
        }

        private float GetHorizontalDistanceToPlayer()
        {
            if (PlayerTarget == null)
            {
                return float.MaxValue;
            }

            Vector3 toPlayer = PlayerTarget.position - transform.position;
            toPlayer.y = 0f;
            return toPlayer.magnitude;
        }

        private Vector3 GetMoveVelocity(Vector3 targetPosition, float moveSpeed)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.01f)
            {
                return Vector3.zero;
            }

            FaceTarget(targetPosition);
            return direction.normalized * moveSpeed;
        }

        private Vector3 GetWanderVelocity()
        {
            if (!HasDestination || Time.time >= NextWanderDecisionTime)
            {
                ChooseNewDestination();
                return Vector3.zero;
            }

            Vector3 toDestination = CurrentDestination - transform.position;
            toDestination.y = 0f;

            if (toDestination.sqrMagnitude <= 0.4f)
            {
                HasDestination = false;
                NextWanderDecisionTime = Time.time + Random.Range(WanderPauseMin, WanderPauseMax);
                return Vector3.zero;
            }

            return GetMoveVelocity(CurrentDestination, Speed * 0.55f);
        }

        private void ChooseNewDestination()
        {
            Vector2 randomCircle = Random.insideUnitCircle * WanderRadius;
            CurrentDestination = SpawnPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);
            CurrentDestination.y = transform.position.y;
            HasDestination = true;
            NextWanderDecisionTime = Time.time + Random.Range(2f, 4f);
        }

        private void TryAttackPlayer()
        {
            if (AttackRoutineHandle != null || AttackCooldownTimer > 0f || PlayerTarget == null || AttackNeedsReset)
            {
                return;
            }

            AttackRoutineHandle = StartCoroutine(AttackPlayerRoutine());
        }

        private IEnumerator AttackPlayerRoutine()
        {
            AttackCooldownTimer = AttackCooldown;
            CrossFadeState(AttackState);

            yield return new WaitForSeconds(AttackWindup);

            if (!DissolveFlg && TryGetPlayerDamageableInAttackZone(out IDamageable damageable))
            {
                damageable.TakeDamage(AttackDamage);
                AttackNeedsReset = true;
            }

            AttackRoutineHandle = null;
        }

        private bool IsPlayerInsideAttackZone()
        {
            return TryGetPlayerDamageableInAttackZone(out _);
        }

        private bool TryGetPlayerDamageableInAttackZone(out IDamageable damageable)
        {
            damageable = null;
            if (PlayerTarget == null)
            {
                return false;
            }

            Vector3 attackOrigin = GetAttackOrigin();
            int hitCount = Physics.OverlapSphereNonAlloc(
                attackOrigin,
                AttackHitRadius,
                AttackHitBuffer,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

            Transform playerRoot = PlayerTarget.root;
            for (int index = 0; index < hitCount; index++)
            {
                Collider hitCollider = AttackHitBuffer[index];
                if (hitCollider == null)
                {
                    continue;
                }

                Transform hitRoot = hitCollider.transform.root;
                if (hitRoot != playerRoot)
                {
                    continue;
                }

                damageable = hitCollider.GetComponentInParent<IDamageable>();
                if (damageable == null)
                {
                    damageable = PlayerTarget.GetComponentInParent<IDamageable>();
                }

                return damageable != null;
            }

            return false;
        }

        private Vector3 GetAttackOrigin()
        {
            if (AttackPoint != null)
            {
                return AttackPoint.position;
            }

            float horizontalScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            float verticalScale = Mathf.Abs(transform.lossyScale.y);
            float bodyFrontOffset = Ctrl != null ? Ctrl.radius * horizontalScale : 0.35f * horizontalScale;
            float bodyHeight = Ctrl != null ? Ctrl.height * verticalScale : 1f * verticalScale;
            float attackHeight = Mathf.Clamp(bodyHeight * 0.5f, 0.35f, 1.4f) + AttackHeightOffset;

            return transform.position
                + Vector3.up * attackHeight
                + transform.forward * (bodyFrontOffset + AttackForwardOffset);
        }

        private void BeginDeath()
        {
            if (AttackRoutineHandle != null)
            {
                StopCoroutine(AttackRoutineHandle);
                AttackRoutineHandle = null;
            }

            DissolveFlg = true;
            RespawnTimer = RespawnDelay;
            HitStunTimer = 0f;
            HasDestination = false;
            AttackNeedsReset = false;
            CrossFadeState(DissolveState);
        }

        private void UpdateDissolveAndRespawn()
        {
            RespawnTimer -= Time.deltaTime;
            Dissolve_value = Mathf.Max(0f, Dissolve_value - Time.deltaTime);
            ApplyDissolveValue();

            if (Dissolve_value <= 0f && Ctrl.enabled)
            {
                Ctrl.enabled = false;
            }

            if (RespawnTimer <= 0f)
            {
                Respawn();
            }
        }

        private void Respawn()
        {
            HP = MaxHP;
            AttackCooldownTimer = 0f;
            HitStunTimer = 0f;
            DissolveFlg = false;
            HasDestination = false;
            AttackNeedsReset = false;

            ResetDissolve();

            Ctrl.enabled = false;
            transform.position = SpawnPosition;
            transform.rotation = SpawnRotation;
            Ctrl.enabled = true;

            VerticalVelocity = 0f;
            CrossFadeState(IdleState);
        }

        private void ApplyMovement(Vector3 horizontalVelocity)
        {
            if (!Ctrl.enabled)
            {
                return;
            }

            if (CheckGrounded() && VerticalVelocity < 0f)
            {
                VerticalVelocity = -2f;
            }

            VerticalVelocity += Gravity * Time.deltaTime;

            Vector3 motion = horizontalVelocity;
            motion.y = VerticalVelocity;
            Ctrl.Move(motion * Time.deltaTime);
        }

        private bool CheckGrounded()
        {
            if (Ctrl.isGrounded)
            {
                return true;
            }

            Vector3 origin = transform.position + Vector3.up * 0.15f;
            return Physics.Raycast(origin, Vector3.down, 0.3f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        }

        private void FaceTarget(Vector3 worldPosition)
        {
            Vector3 lookDirection = worldPosition - transform.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, RotationSpeed * Time.deltaTime);
        }

        private void UpdateAnimation(Vector3 horizontalVelocity)
        {
            if (DissolveFlg || AttackRoutineHandle != null || HitStunTimer > 0f)
            {
                return;
            }

            if (horizontalVelocity.sqrMagnitude > 0.05f)
            {
                CrossFadeState(MoveState);
            }
            else
            {
                CrossFadeState(IdleState);
            }
        }

        private void CrossFadeState(int stateHash)
        {
            AnimatorStateInfo currentState = Anim.GetCurrentAnimatorStateInfo(0);
            if (currentState.fullPathHash == stateHash)
            {
                return;
            }

            Anim.CrossFade(stateHash, 0.1f, 0, 0f);
        }

        private void ResetDissolve()
        {
            Dissolve_value = 1f;
            ApplyDissolveValue();
        }

        private void ApplyDissolveValue()
        {
            if (MeshR == null)
            {
                return;
            }

            for (int index = 0; index < MeshR.Length; index++)
            {
                if (MeshR[index] == null)
                {
                    continue;
                }

                Material material = MeshR[index].material;
                if (material.HasProperty("_Dissolve"))
                {
                    material.SetFloat("_Dissolve", Dissolve_value);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.25f);
            Gizmos.DrawSphere(transform.position, DetectRange);

            Gizmos.color = new Color(1f, 0.8f, 0.15f, 0.2f);
            Gizmos.DrawSphere(transform.position, AttackRange);

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
            Gizmos.DrawSphere(GetAttackOrigin(), AttackHitRadius);

            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.2f);
            Gizmos.DrawSphere(Application.isPlaying ? SpawnPosition : transform.position, WanderRadius);
        }
    }
}
