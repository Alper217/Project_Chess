using UnityEngine;

public class SceneMusicManager : MonoBehaviour
{
    [SerializeField] private AudioClip introClip;
    [SerializeField] private AudioClip loopClip;
    [SerializeField] private bool playOnStart = true;

    private void Start()
    {
        if (playOnStart)
        {
            PlayGameMusic();
        }
    }

    public void PlayGameMusic()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlayMusicWithIntro(introClip, loopClip);
        }
    }
}
