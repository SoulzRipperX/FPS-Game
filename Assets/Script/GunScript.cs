using System;
using System.Collections;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public interface IDamageable
{
    void TakeDamage(int damage);
}

[RequireComponent(typeof(AudioSource))]
public class GunScript : MonoBehaviour
{
    [Header("Shooting")]
    [SerializeField] private Camera ShootCamera;
    [SerializeField] private Transform MuzzlePoint;
    [SerializeField] private float FireRate = 0.18f;
    [SerializeField] private float Range = 100f;
    [SerializeField] private float ImpactForce = 10f;
    [SerializeField] private int DamagePerShot = 1;
    [SerializeField] private LayerMask HitLayers = ~0;

    [Header("Ammo")]
    [SerializeField] private int MagazineSize = 12;
    [SerializeField] private float ReloadDuration = 1.15f;
    [SerializeField] private bool AutoReload = true;

    [Header("Feedback")]
    [SerializeField] private ParticleSystem MuzzleFlash;
    [SerializeField] private Light MuzzleLight;
    [SerializeField] private AudioClip ShootClip;
    [SerializeField] private TMP_Text AmmoText;

    private AudioSource AudioSource;
    private Transform OwnerRoot;
    private int CurrentAmmo;
    private float NextFireTime;
    private bool IsReloading;

    private void Awake()
    {
        AudioSource = GetComponent<AudioSource>();
        AudioSource.playOnAwake = false;
        AudioSource.spatialBlend = 0f;
        AudioSource.volume = 0.85f;

        OwnerRoot = transform.root;
        CurrentAmmo = Mathf.Max(1, MagazineSize);
    }

    private void Start()
    {
        ResolveReferences();
        ValidateSetup();
        UpdateAmmoLabel();
    }

    private void Update()
    {
        ResolveReferences();

        if (IsReloading)
        {
            return;
        }

        if (WasReloadPressedThisFrame() && CurrentAmmo < MagazineSize)
        {
            StartCoroutine(ReloadRoutine());
            return;
        }

        if (!WasFirePressedThisFrame() || Time.time < NextFireTime)
        {
            return;
        }

        if (CurrentAmmo <= 0)
        {
            if (AutoReload)
            {
                StartCoroutine(ReloadRoutine());
            }

            return;
        }

        Fire();
    }

    public void Configure(Camera shootCamera, Transform ownerRoot)
    {
        if (shootCamera != null)
        {
            ShootCamera = shootCamera;
        }

        if (ownerRoot != null)
        {
            OwnerRoot = ownerRoot;
        }
    }

    private void ResolveReferences()
    {
    }

    private void Fire()
    {
        NextFireTime = Time.time + FireRate;
        CurrentAmmo--;
        UpdateAmmoLabel();

        PlayShootFeedback();

        if (ShootCamera == null)
        {
            return;
        }

        Ray ray = ShootCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, Range, HitLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        for (int index = 0; index < hits.Length; index++)
        {
            RaycastHit hit = hits[index];
            if (OwnerRoot != null && hit.transform.IsChildOf(OwnerRoot))
            {
                continue;
            }

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(DamagePerShot);
            }

            if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
            {
                hit.rigidbody.AddForce(ray.direction * ImpactForce, ForceMode.Impulse);
            }

            break;
        }

        if (CurrentAmmo <= 0 && AutoReload)
        {
            StartCoroutine(ReloadRoutine());
        }
    }

    private void PlayShootFeedback()
    {
        if (MuzzleFlash != null)
        {
            MuzzleFlash.Emit(12);
        }

        if (MuzzleLight != null)
        {
            StartCoroutine(FlashMuzzleLight());
        }

        if (ShootClip != null)
        {
            AudioSource.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
            AudioSource.PlayOneShot(ShootClip);
        }
    }

    private IEnumerator ReloadRoutine()
    {
        if (IsReloading)
        {
            yield break;
        }

        IsReloading = true;
        UpdateAmmoLabel("Reloading...");

        yield return new WaitForSeconds(ReloadDuration);

        CurrentAmmo = MagazineSize;
        IsReloading = false;
        UpdateAmmoLabel();
    }

    private IEnumerator FlashMuzzleLight()
    {
        MuzzleLight.enabled = true;
        yield return new WaitForSeconds(0.04f);
        MuzzleLight.enabled = false;
    }

    private void ValidateSetup()
    {
        if (ShootCamera == null)
        {
            Debug.LogWarning($"{nameof(GunScript)} on {name} has no ShootCamera assigned.");
        }

        if (MuzzlePoint == null)
        {
            Debug.LogWarning($"{nameof(GunScript)} on {name} has no MuzzlePoint assigned.");
        }

        if (AmmoText == null)
        {
            Debug.LogWarning($"{nameof(GunScript)} on {name} has no AmmoText assigned.");
        }

        if (ShootClip == null)
        {
            Debug.LogWarning($"{nameof(GunScript)} on {name} has no ShootClip assigned.");
        }
    }

    private void UpdateAmmoLabel(string customText = null)
    {
        if (AmmoText == null)
        {
            return;
        }

        AmmoText.text = customText ?? $"AMMO {CurrentAmmo} / {MagazineSize}";
    }

    private bool WasFirePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private bool WasReloadPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }
}
