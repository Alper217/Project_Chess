using System;
using UnityEngine;

public class MouseInteractionManager : MonoBehaviour
{
    private IHoverable currentHoveredObject;
    [SerializeField] private LayerMask hoverLayer = ~0;

    void Update()
    {
        if (Camera.main == null) return;
        
        // Sadece TAB tuşuna basılı tutulduğunda ışın gönder (Optimizasyon)
        if (Input.GetKey(KeyCode.Tab))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f, hoverLayer);
            if (hits != null && hits.Length > 0)
            {
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                IHoverable hoverable = null;
                foreach (var h in hits)
                {
                    hoverable = h.collider.GetComponentInParent<IHoverable>();
                    if (hoverable != null) break;
                }

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
