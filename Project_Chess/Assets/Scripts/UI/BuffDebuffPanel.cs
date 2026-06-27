using System;
using UnityEngine;

public class BuffDebuffPanel : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject exitPanel;

    private void Start()
    {
        panel.SetActive(false);
    }

    public void OpenPanel()
    {
        bool isActive = panel.activeSelf;
        panel.SetActive(!isActive);
    }

    public void ReturnToMainMenu()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
    public void OpenExitPanel()
    {
        exitPanel.SetActive(true);
    }

    public void CloseExitPanel()
    {
        exitPanel.SetActive(false);
    }
}
