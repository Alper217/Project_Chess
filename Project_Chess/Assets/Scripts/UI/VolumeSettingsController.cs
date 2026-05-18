using System;
using UnityEngine;
using UnityEngine.UI;

public class VolumeSettingsController : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private void Start()
    {
        // Başlangıçta panel kapalı olsun
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (AudioManager.instance == null) return;

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
