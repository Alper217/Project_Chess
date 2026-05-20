using System;
using UnityEngine;
using UnityEngine.UI;

public class VolumeSettingsController : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Localization Settings")]
    [SerializeField] private TMPro.TMP_Dropdown languageDropdown;

    private void Start()
    {
        // Başlangıçta panel kapalı olsun
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (AudioManager.instance != null)
        {
            // Hafızadaki değerleri Slider'lara aktar
            if (musicSlider != null)
            {
                musicSlider.value = AudioManager.instance.GetMusicVolume();
                musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.value = AudioManager.instance.GetSFXVolume();
                sfxSlider.onValueChanged.AddListener(OnSFXSliderChanged);
            }
        }

        // Dil Seçiciyi Kur
        InitializeLanguageSelection();
    }

    private void InitializeLanguageSelection()
    {
        if (languageDropdown != null)
        {
            languageDropdown.ClearOptions();
            
            var options = new System.Collections.Generic.List<string> { "Türkçe", "English", "Deutsch", "Français", "Português", "Español" };
            languageDropdown.AddOptions(options);

            // Mevcut dili eşleştir
            languageDropdown.SetValueWithoutNotify((int)AlperKocasalih.Chess.Grid.LocalizationManager.CurrentLanguage);
            languageDropdown.onValueChanged.AddListener(OnLanguageDropdownChanged);
        }
    }

    private void UpdateDropdownSelection()
    {
        if (languageDropdown != null)
        {
            languageDropdown.SetValueWithoutNotify((int)AlperKocasalih.Chess.Grid.LocalizationManager.CurrentLanguage);
        }
    }

    /// <summary>
    /// Dropdown dil değişimi tetiklendiğinde çalışır.
    /// </summary>
    private void OnLanguageDropdownChanged(int index)
    {
        AlperKocasalih.Chess.Grid.LocalizationManager.CurrentLanguage = (AlperKocasalih.Chess.Grid.Language)index;
    }

    /// <summary>
    /// Doğrudan Türkçe butonuna tıklanıldığında çağrılabilir.
    /// </summary>
    public void SetLanguageTR()
    {
        AlperKocasalih.Chess.Grid.LocalizationManager.CurrentLanguage = AlperKocasalih.Chess.Grid.Language.TR;
        UpdateDropdownSelection();
    }

    /// <summary>
    /// Doğrudan İngilizce butonuna tıklanıldığında çağrılabilir.
    /// </summary>
    public void SetLanguageEN()
    {
        AlperKocasalih.Chess.Grid.LocalizationManager.CurrentLanguage = AlperKocasalih.Chess.Grid.Language.EN;
        UpdateDropdownSelection();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel != null)
            {
                // Mevcut durumun tam tersini yapar (Açıksa kapatır, kapalıysa açar)
                bool isActive = settingsPanel.activeSelf;
                settingsPanel.SetActive(!isActive);
            }
        }
    }

    private void OnMusicSliderChanged(float value)
    {
        if (AudioManager.instance != null)
            AudioManager.instance.SetMusicVolume(value);
    }

    private void OnSFXSliderChanged(float value)
    {
        if (AudioManager.instance != null)
            AudioManager.instance.SetSFXVolume(value);
    }
}
