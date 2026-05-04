using System;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
  public static AudioManager instance { get; private set; }
  
  [SerializeField] private AudioSource sfxSource;

  private void Awake()
  {
    if (instance == null) instance = this;
    else Destroy(gameObject);
  }

  public void PlaySfx(AudioClip clip)
  {
    if(clip != null && sfxSource != null) 
    {
      sfxSource.PlayOneShot(clip);
    }
  }
}
