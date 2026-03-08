using UnityEngine;

public class MouseInteractionManager : MonoBehaviour
{
    private IHoverable currentHoveredObject;

    void Update()
    {
        if (Camera.main == null) return;
        
        // Sadece TAB tuşuna basılı tutulduğunda ışın gönder (Optimizasyon)
        if (Input.GetKey(KeyCode.Tab))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                IHoverable hoverable = hit.collider.GetComponent<IHoverable>();
                
                if (hoverable != currentHoveredObject)
                {
                    if (currentHoveredObject != null) currentHoveredObject.OnHoverExit();
                    currentHoveredObject = hoverable;
                    if (currentHoveredObject != null) currentHoveredObject.OnHoverEnter();
                }
            }
            else if (currentHoveredObject != null)
            {
                currentHoveredObject.OnHoverExit();
                currentHoveredObject = null;
            }
        }
        else 
        {
            // TAB tuşu bırakıldığında (veya basılı değilse) eğer ekranda açık kalmış bir UI varsa kapat.
            if (currentHoveredObject != null)
            {
                currentHoveredObject.OnHoverExit();
                currentHoveredObject = null;
            }
        }
    }
}
