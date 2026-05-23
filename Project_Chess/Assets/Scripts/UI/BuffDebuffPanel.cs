using System;
using UnityEngine;

public class BuffDebuffPanel : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    private void Start()
    {
        panel.SetActive(false);
    }
    
    public void OpenPanel()
    {
        bool isActive = panel.activeSelf;
        panel.SetActive(!isActive);
    }
    
}
