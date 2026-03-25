using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MenuController : MonoBehaviour
{
    private const string BgmVolumeKey = "BGM_VOLUME";
    private const string SfxVolumeKey = "SFX_VOLUME";

    [SerializeField] private string gameSceneName = "Main_Game";
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private AudioSource[] bgmSources;
    [SerializeField] private AudioSource[] sfxSources;

    private StarterAssetsInputs _gameplayInput;

    public static bool IsGamePaused { get; private set; }

    private void Awake()
    {
        if (pausePanel == null)
        {
            pausePanel = GameObject.Find("Pause_Canvas") ?? GameObject.Find("Puase_Canvas");
        }

        if (optionsPanel == null)
        {
            optionsPanel = GameObject.Find("Options_Canvas");
        }
    }

    private void Start()
    {
        _gameplayInput = Object.FindFirstObjectByType<StarterAssetsInputs>();

        float bgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, 1f);
        float sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);

        if (bgmSlider != null)
        {
            bgmSlider.SetValueWithoutNotify(bgmVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(sfxVolume);
        }

        SetBgmVolume(bgmVolume);
        SetSfxVolume(sfxVolume);

        if (HasGameplayInput())
        {
            HideAllMenus();
            ResumeGameplayState();
        }
        else
        {
            ShowCursor();
        }
    }

    private void Update()
    {
        if (!HasGameplayInput())
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
#else
        if (Input.GetKeyDown(KeyCode.Escape))
#endif
        {
            HandleEscape();
        }
    }

    private void OnDestroy()
    {
        if (IsGamePaused)
        {
            Time.timeScale = 1f;
            IsGamePaused = false;
        }
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        IsGamePaused = false;
        SceneManager.LoadScene(gameSceneName);
    }

    public void BackToMainMenu()
    {
        StartGame();
    }

    public void PauseGame()
    {
        if (!HasGameplayInput())
        {
            return;
        }

        IsGamePaused = true;
        Time.timeScale = 0f;
        PauseGameplayState();

        GameObject primaryPausePanel = GetPrimaryPausePanel();
        if (primaryPausePanel != null)
        {
            primaryPausePanel.SetActive(true);
        }

        if (optionsPanel != null && optionsPanel != primaryPausePanel)
        {
            optionsPanel.SetActive(false);
        }
    }

    public void ResumeGame()
    {
        if (!HasGameplayInput())
        {
            return;
        }

        IsGamePaused = false;
        Time.timeScale = 1f;
        HideAllMenus();
        ResumeGameplayState();
    }

    public void ToggleOptions()
    {
        if (optionsPanel == null)
        {
            return;
        }

        if (optionsPanel.activeSelf)
        {
            CloseSettings();
        }
        else
        {
            OpenSettings();
        }
    }

    public void OpenSettings()
    {
        if (optionsPanel == null)
        {
            return;
        }

        optionsPanel.SetActive(true);

        GameObject primaryPausePanel = GetPrimaryPausePanel();
        if (IsGamePaused && primaryPausePanel != null && primaryPausePanel != optionsPanel)
        {
            primaryPausePanel.SetActive(false);
        }

        ShowCursor();
    }

    public void CloseSettings()
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(false);
        }

        GameObject primaryPausePanel = GetPrimaryPausePanel();
        if (IsGamePaused && primaryPausePanel != null && primaryPausePanel != optionsPanel)
        {
            primaryPausePanel.SetActive(true);
        }
    }

    public void ExitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();
        Debug.Log("Exit Game");
    }

    public void SetBgmVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(BgmVolumeKey, volume);
        PlayerPrefs.Save();
        ApplyVolumeToSources(bgmSources, volume);
    }

    public void SetSfxVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SfxVolumeKey, volume);
        PlayerPrefs.Save();
        ApplyVolumeToSources(sfxSources, volume);
    }

    private void HandleEscape()
    {
        if (!IsGamePaused)
        {
            PauseGame();
            return;
        }

        GameObject primaryPausePanel = GetPrimaryPausePanel();
        if (optionsPanel != null && optionsPanel.activeSelf && primaryPausePanel != optionsPanel)
        {
            CloseSettings();
            return;
        }

        ResumeGame();
    }

    private bool HasGameplayInput()
    {
        return _gameplayInput != null;
    }

    private GameObject GetPrimaryPausePanel()
    {
        if (pausePanel != null && pausePanel.transform.childCount > 0)
        {
            return pausePanel;
        }

        if (optionsPanel != null)
        {
            return optionsPanel;
        }

        return pausePanel;
    }

    private void HideAllMenus()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        if (optionsPanel != null)
        {
            optionsPanel.SetActive(false);
        }
    }

    private void PauseGameplayState()
    {
        if (_gameplayInput != null)
        {
            _gameplayInput.SetGameplayInputEnabled(false);
        }
        else
        {
            ShowCursor();
        }
    }

    private void ResumeGameplayState()
    {
        if (_gameplayInput != null)
        {
            _gameplayInput.SetGameplayInputEnabled(true);
        }
        else
        {
            ShowCursor();
        }
    }

    private static void ShowCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void ApplyVolumeToSources(AudioSource[] sources, float volume)
    {
        if (sources == null)
        {
            return;
        }

        foreach (AudioSource source in sources)
        {
            if (source != null)
            {
                source.volume = volume;
            }
        }
    }
}
