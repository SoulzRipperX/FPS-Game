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
    private static Texture2D CrosshairTexture;
    private const float AudioLoadTimeout = 1.0f;

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
    [SerializeField] private int StartingReserveAmmo = 24;
    [SerializeField] private int MaxReserveAmmo = 60;
    [SerializeField] private float ReloadDuration = 1.15f;
    [SerializeField] private bool AutoReload = true;

    [Header("Feedback")]
    [SerializeField] private ParticleSystem MuzzleFlash;
    [SerializeField] private Light MuzzleLight;
    [SerializeField] private AudioClip ShootClip;
    [SerializeField] private AudioClip ReloadClip;
    [SerializeField] private TMP_Text AmmoText;

    [Header("Crosshair")]
    [SerializeField] private bool ShowCrosshair = true;
    [SerializeField] private float CrosshairSize = 8f;
    [SerializeField] private Color CrosshairColor = new Color(1f, 0.15f, 0.15f, 0.95f);

    private AudioSource AudioSource;
    private Transform OwnerRoot;
    private int CurrentMagazineAmmo;
    private int CurrentReserveAmmo;
    private float NextFireTime;
    private bool IsReloading;
    private Coroutine PendingShootSoundRoutine;
    private Coroutine PendingReloadSoundRoutine;

    private void Awake()
    {
        EnsureCrosshairTexture();

        AudioSource = GetComponent<AudioSource>();
        AudioSource.playOnAwake = false;
        AudioSource.spatialBlend = 0f;
        AudioSource.volume = 0.85f;

        OwnerRoot = transform.root;
        MagazineSize = Mathf.Max(1, MagazineSize);
        MaxReserveAmmo = Mathf.Max(0, MaxReserveAmmo);
        CurrentMagazineAmmo = MagazineSize;
        CurrentReserveAmmo = Mathf.Clamp(StartingReserveAmmo, 0, MaxReserveAmmo);
    }

    private void Start()
    {
        ResolveReferences();
        PrepareAudioClip(ShootClip);
        PrepareAudioClip(ReloadClip);
        ValidateSetup();
        UpdateAmmoLabel();
    }

    private void Update()
    {
        if (MenuController.IsGamePaused)
        {
            return;
        }

        ResolveReferences();

        if (IsReloading)
        {
            return;
        }

        if (WasReloadPressedThisFrame() && CurrentMagazineAmmo < MagazineSize && CurrentReserveAmmo > 0)
        {
            StartCoroutine(ReloadRoutine());
            return;
        }

        if (!WasFirePressedThisFrame() || Time.time < NextFireTime)
        {
            return;
        }

        if (CurrentMagazineAmmo <= 0)
        {
            if (AutoReload && CurrentReserveAmmo > 0)
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

    private void OnGUI()
    {
        if (!ShowCrosshair || Event.current.type != EventType.Repaint)
        {
            return;
        }

        if (MenuController.IsGamePaused || IsReloading || ShootCamera == null || !ShootCamera.isActiveAndEnabled)
        {
            return;
        }

        float size = Mathf.Max(2f, CrosshairSize);
        float halfSize = size * 0.5f;
        Rect rect = new Rect(
            (Screen.width * 0.5f) - halfSize,
            (Screen.height * 0.5f) - halfSize,
            size,
            size);

        Color previousColor = GUI.color;
        GUI.color = CrosshairColor;
        GUI.DrawTexture(rect, CrosshairTexture);
        GUI.color = previousColor;
    }

    private void ResolveReferences()
    {
    }

    private void Fire()
    {
        NextFireTime = Time.time + FireRate;
        CurrentMagazineAmmo--;
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

        if (CurrentMagazineAmmo <= 0 && AutoReload && CurrentReserveAmmo > 0)
        {
            StartCoroutine(ReloadRoutine());
        }
    }

    public int AddAmmo(int amount)
    {
        if (amount <= 0 || CurrentReserveAmmo >= MaxReserveAmmo)
        {
            return 0;
        }

        int previousReserveAmmo = CurrentReserveAmmo;
        CurrentReserveAmmo = Mathf.Clamp(CurrentReserveAmmo + amount, 0, MaxReserveAmmo);
        UpdateAmmoLabel();
        return CurrentReserveAmmo - previousReserveAmmo;
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
            PlayClip(ShootClip, UnityEngine.Random.Range(0.97f, 1.03f), ref PendingShootSoundRoutine);
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
        PlayReloadFeedback();

        yield return new WaitForSeconds(ReloadDuration);

        int ammoNeeded = Mathf.Max(0, MagazineSize - CurrentMagazineAmmo);
        int ammoToLoad = Mathf.Min(ammoNeeded, CurrentReserveAmmo);
        CurrentMagazineAmmo += ammoToLoad;
        CurrentReserveAmmo -= ammoToLoad;
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

        if (ReloadClip == null)
        {
            Debug.LogWarning($"{nameof(GunScript)} on {name} has no ReloadClip assigned.");
        }
    }

    private void UpdateAmmoLabel(string customText = null)
    {
        if (AmmoText == null)
        {
            return;
        }

        AmmoText.text = customText ?? $"AMMO {CurrentMagazineAmmo} | {CurrentReserveAmmo}";
    }

    private void PlayReloadFeedback()
    {
        if (ReloadClip == null)
        {
            return;
        }

        PlayClip(ReloadClip, 1f, ref PendingReloadSoundRoutine);
    }

    private void PrepareAudioClip(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        if (clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }
    }

    private void PlayClip(AudioClip clip, float pitch, ref Coroutine pendingRoutine)
    {
        if (clip == null)
        {
            return;
        }

        PrepareAudioClip(clip);

        if (clip.loadState == AudioDataLoadState.Loaded)
        {
            AudioSource.pitch = pitch;
            AudioSource.PlayOneShot(clip);
            return;
        }

        if (pendingRoutine != null)
        {
            StopCoroutine(pendingRoutine);
        }

        pendingRoutine = StartCoroutine(PlayClipWhenLoaded(clip, pitch));
    }

    private IEnumerator PlayClipWhenLoaded(AudioClip clip, float pitch)
    {
        float remainingTime = AudioLoadTimeout;

        while (clip != null && clip.loadState == AudioDataLoadState.Loading && remainingTime > 0f)
        {
            remainingTime -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (clip != null && clip.loadState == AudioDataLoadState.Loaded)
        {
            AudioSource.pitch = pitch;
            AudioSource.PlayOneShot(clip);
        }
    }

    private bool WasFirePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private static void EnsureCrosshairTexture()
    {
        if (CrosshairTexture != null)
        {
            return;
        }

        CrosshairTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        CrosshairTexture.SetPixel(0, 0, Color.white);
        CrosshairTexture.Apply();
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
