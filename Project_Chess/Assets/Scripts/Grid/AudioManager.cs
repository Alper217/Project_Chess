using System;
using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    [Header("Global SFX")]
    [SerializeField] private AudioClip buttonClickSound;

    private const string MUSIC_KEY = "MusicVolume";
    private const string SFX_KEY = "SFXVolume";

    private Coroutine musicCoroutine;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadVolumeSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlaySfx(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    public void PlayButtonClickSound()
    {
        if (buttonClickSound != null)
        {
            PlaySfx(buttonClickSound);
        }
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null) return;
        
        if (musicSource.clip == clip && musicSource.isPlaying && musicCoroutine == null) return;

        if (musicCoroutine != null)
        {
            StopCoroutine(musicCoroutine);
            musicCoroutine = null;
        }

        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void PlayMusicWithIntro(AudioClip introClip, AudioClip loopClip)
    {
        if (musicSource == null) return;

        if (musicCoroutine != null)
        {
            StopCoroutine(musicCoroutine);
        }

        musicCoroutine = StartCoroutine(MusicSequenceCoroutine(introClip, loopClip));
    }

    private IEnumerator MusicSequenceCoroutine(AudioClip introClip, AudioClip loopClip)
    {
        if (introClip != null)
        {
            musicSource.clip = introClip;
            musicSource.loop = false;
            musicSource.Play();

            yield return new WaitForSeconds(introClip.length);
        }

        if (loopClip != null)
        {
            musicSource.clip = loopClip;
            musicSource.loop = true;
            musicSource.Play();
        }
        
        musicCoroutine = null;
    }

    public void SetMusicVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        if (musicSource != null) musicSource.volume = volume;
        PlayerPrefs.SetFloat(MUSIC_KEY, volume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        if (sfxSource != null) sfxSource.volume = volume;
        PlayerPrefs.SetFloat(SFX_KEY, volume);
        PlayerPrefs.Save();
    }

    public float GetMusicVolume() => PlayerPrefs.GetFloat(MUSIC_KEY, 0.5f);
    public float GetSFXVolume() => PlayerPrefs.GetFloat(SFX_KEY, 0.5f);

    private void LoadVolumeSettings()
    {
        if (musicSource != null) musicSource.volume = GetMusicVolume();
        if (sfxSource != null) sfxSource.volume = GetSFXVolume();
    }
}
