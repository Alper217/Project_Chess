using UnityEngine;

public class SceneMusicManager : MonoBehaviour
{
    [Header("Menu/Setup Music (Loops in Menu/Lobby)")]
    [SerializeField] private AudioClip setupClip;

    [Header("Gameplay Music (Intro + Loop in GameScene)")]
    [SerializeField] private AudioClip actionIntroClip;
    [SerializeField] private AudioClip actionLoopClip;

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Fallback for when playing directly from Editor
        OnSceneLoaded(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (AudioManager.instance == null) return;

        if (scene.name == "GameScene")
        {
            Debug.Log($"[SceneMusicManager] GameScene loaded. Playing gameplay intro and loop.");
            AudioManager.instance.PlayMusicWithIntro(actionIntroClip, actionLoopClip);
        }
        else
        {
            Debug.Log($"[SceneMusicManager] Menu/Lobby scene loaded ({scene.name}). Playing menu music.");
            if (setupClip != null)
            {
                AudioManager.instance.PlayMusic(setupClip, true);
            }
        }
    }
}
