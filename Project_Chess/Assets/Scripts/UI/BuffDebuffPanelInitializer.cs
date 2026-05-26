using UnityEngine;
using TMPro;
using System.Text;
using AlperKocasalih.Chess.Grid;

public class BuffDebuffPanelInitializer : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Tüm buff/debuff nesnelerini barındıran Scroll View Content nesnesi (eğer boş bırakılırsa bu nesnenin kendisi seçilir)")]
    [SerializeField] private Transform contentContainer;

    [Header("Color Coding Settings")]
    [Tooltip("Buff isimlerini yeşil, Debuff isimlerini kırmızı olarak renklendirmeyi açar/kapatır.")]
    [SerializeField] private bool useColorCoding = true;

    [Tooltip("Buff başlıkları için kullanılacak renk (Örn: Canlı Premium Yeşil)")]
    [SerializeField] private Color buffColor = new Color(0.27f, 0.83f, 0.44f); // Sleek Premium Green

    [Tooltip("Debuff başlıkları için kullanılacak renk (Örn: Canlı Premium Kırmızı)")]
    [SerializeField] private Color debuffColor = new Color(0.92f, 0.26f, 0.35f); // Sleek Premium Red

    private void Awake()
    {
        if (contentContainer == null)
        {
            contentContainer = transform;
        }

        InitializeLocalizationKeys();
    }

    /// <summary>
    /// Content altındaki tüm buff/debuff elemanlarını gezip Text bileşenlerine LocalizedText ekler ve eşleştirir.
    /// </summary>
    public void InitializeLocalizationKeys()
    {
        if (contentContainer == null) return;

        // Content altındaki her bir çocuk nesneyi (B_Balyoz, B_Yanki, D_Korkutuk vb.) dolaş
        foreach (Transform child in contentContainer)
        {
            if (child == null) continue;

            string objectName = child.name;
            
            // Türkçe karakterleri temizle ve güvenli bir JSON anahtar kökü üret
            string baseKey = CleanTurkishCharacters(objectName);

            // Çocuk nesne içindeki tüm TextMeshProUGUI bileşenlerini bul
            TextMeshProUGUI[] texts = child.GetComponentsInChildren<TextMeshProUGUI>(true);

            if (texts == null || texts.Length == 0) continue;

            // 1. Metin (Genelde Adı): Name key'i ata ve gerekirse renklendir
            if (texts.Length > 0 && texts[0] != null)
            {
                SetupLocalizedText(texts[0], $"{baseKey}_Name");

                // Renklendirme açıksa ve nesne B_ veya D_ ile başlıyorsa başlığı renklendir
                if (useColorCoding)
                {
                    if (objectName.StartsWith("B_"))
                    {
                        texts[0].color = buffColor;
                    }
                    else if (objectName.StartsWith("D_"))
                    {
                        texts[0].color = debuffColor;
                    }
                }
            }

            // 2. Metin (Genelde Açıklaması): Desc key'i ata
            if (texts.Length > 1 && texts[1] != null)
            {
                SetupLocalizedText(texts[1], $"{baseKey}_Desc");
            }
        }
    }

    /// <summary>
    /// Belirtilen metin bileşenine LocalizedText ekler ve dil anahtarını tanımlar.
    /// </summary>
    private void SetupLocalizedText(TextMeshProUGUI tmp, string key)
    {
        LocalizedText localizedComp = tmp.GetComponent<LocalizedText>();
        if (localizedComp == null)
        {
            localizedComp = tmp.gameObject.AddComponent<LocalizedText>();
        }

        // Anahtarı dinamik olarak ata ve metni hemen güncelle
        localizedComp.SetKey(key);
    }

    /// <summary>
    /// GameObject isimlerindeki Türkçe karakterleri İngilizce eşdeğerleriyle değiştirerek güvenli dil anahtarları üretir.
    /// </summary>
    private string CleanTurkishCharacters(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        StringBuilder sb = new StringBuilder(input);
        
        // Karakter dönüşüm tablosu
        sb.Replace("ı", "i");
        sb.Replace("İ", "I");
        sb.Replace("ğ", "g");
        sb.Replace("Ğ", "G");
        sb.Replace("ü", "u");
        sb.Replace("Ü", "U");
        sb.Replace("ş", "s");
        sb.Replace("Ş", "S");
        sb.Replace("ö", "o");
        sb.Replace("Ö", "O");
        sb.Replace("ç", "c");
        sb.Replace("Ç", "C");
        sb.Replace("â", "a");
        sb.Replace("Â", "A");

        // Boşlukları kaldır
        sb.Replace(" ", "");

        return sb.ToString();
    }
}
