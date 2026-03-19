using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    private const string BgmVolumeKey = "BGM_VOLUME";
    private const string SfxVolumeKey = "SFX_VOLUME";

    [SerializeField] private string gameSceneName = "Main_Game";
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private AudioSource[] bgmSources;
    [SerializeField] private AudioSource[] sfxSources;

    private void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

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
    }

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void ToggleOptions()
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(!optionsPanel.activeSelf);
        }
    }

    public void ExitGame()
    {
        Application.Quit();
        Debug.Log("Exit Game");
    }

    public void SetBgmVolume(float volume)
    {
        PlayerPrefs.SetFloat(BgmVolumeKey, volume);

        foreach (AudioSource source in bgmSources)
        {
            if (source != null)
            {
                source.volume = volume;
            }
        }
    }

    public void SetSfxVolume(float volume)
    {
        PlayerPrefs.SetFloat(SfxVolumeKey, volume);

        foreach (AudioSource source in sfxSources)
        {
            if (source != null)
            {
                source.volume = volume;
            }
        }
    }
}
