using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthUIManager : MonoBehaviour
{
    public static HealthUIManager Instance;

    public GameObject healthCanvas; 
    public Slider healthSlider;
    [Tooltip("Kameradan bakıldığında can barlığının ne kadar yukarıda duracağını ayarlar.")]
    public float heightOffsetMultiplier = 2f; 
    public TextMeshProUGUI healthText; // Optional: Assign in inspector, or auto-found in Awake
    private Transform currentTarget;

    private void Awake() 
    {
        Instance = this;
        if (healthCanvas != null)
        {
            healthCanvas.SetActive(false); // Başlangıçta gizli başlasın
            if (healthText == null)
            {
                healthText = healthCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }
    }

    public void ShowHealthBar(Transform target, int current, int max)
    {
        currentTarget = target;
        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value = current;
        }
        
        if (healthText != null)
        {
            healthText.text = $"{current} / {max}";
        }
        if (healthCanvas != null)
        {
            healthCanvas.SetActive(true); 
            UpdateHealthCanvasPosition(); // Açılır açılmaz doğru yere geçsin
        }
    }

    public void HideHealthBar()
    {
        currentTarget = null;
        if (healthCanvas != null)
            healthCanvas.SetActive(false);
    }

    private void LateUpdate()
    {
        UpdateHealthCanvasPosition();
    }

    private void UpdateHealthCanvasPosition()
    {
        // Eğer takip edilen piyon (hedef) yok edildiyse UI'ı hemen gizle
        if (currentTarget == null)
        {
            if (healthCanvas != null && healthCanvas.activeSelf)
            {
                HideHealthBar();
            }
            return;
        }

        // Piyonun kafasını takip etme ve kameraya dönük kalma işlemi
        if (healthCanvas != null && healthCanvas.activeSelf)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // Can barını piyonun konumunda, ancak "kameranın yukarısı" (Camera.up) yönünde öteleyerek konumlandırıyoruz.
                // Bu sayede ekranın neresinde olursa olsun bar her zaman piyonun tam tepesinde görünür.
                healthCanvas.transform.position = currentTarget.position + (mainCam.transform.up * heightOffsetMultiplier);
                healthCanvas.transform.rotation = mainCam.transform.rotation;
            }
            else 
            {
                // Kamera yoksa eski sistem düz çalışsın fallback olarak
                healthCanvas.transform.position = currentTarget.position + new Vector3(0, heightOffsetMultiplier, 0);
            }
        }
    }
}
