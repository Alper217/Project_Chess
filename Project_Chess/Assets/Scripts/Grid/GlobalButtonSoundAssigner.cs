using UnityEngine;
using UnityEngine.UI;

public class GlobalButtonSoundAssigner : MonoBehaviour
{
    private void Start()
    {
        AssignSounds();
    }

    public void AssignSounds()
    {
        Button[] allButtons = Resources.FindObjectsOfTypeAll<Button>();
        
        foreach (Button btn in allButtons)
        {
            // Kartları hariç tut (İsimde 'Card' geçiyorsa veya belirli bir tag varsa)
            if (btn.gameObject.name.Contains("Card") || btn.gameObject.CompareTag("Card"))
            {
                continue;
            }

            // Eğer zaten eklenmemişse ekle
            if (btn.gameObject.GetComponent<UIButtonSound>() == null)
            {
                btn.gameObject.AddComponent<UIButtonSound>();
            }
        }
    }
}
