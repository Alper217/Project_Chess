using UnityEngine;
using UnityEngine.UI;

public class PawnHealthController : MonoBehaviour
{
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private GameObject pawn;
    [SerializeField] private int maxHealth;
    private int currentHealth;
    [SerializeField] private Slider slider;
    [SerializeField] private int damage;
    void Start()
    {
        maxHealth = 100;
        currentHealth = maxHealth;
        slider.maxValue = maxHealth;
        slider.value = currentHealth;
    }

    private void OnMouseEnter()
    {
        visualRoot.SetActive(true);
    }

    private void OnMouseExit()
    {
        visualRoot.SetActive(false);
    }
    void Update()
    {
          if (Camera.main != null) 
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit))
        {
            Debug.Log("Fare şu an şuna değiyor: " + hit.transform.name);
        }
    }
        if (Input.GetMouseButtonDown(0))
        {
            currentHealth -= damage;
            slider.value = currentHealth;
        }
        if (currentHealth <= 0)
        {
            Destroy(pawn);
        }
    }
}
