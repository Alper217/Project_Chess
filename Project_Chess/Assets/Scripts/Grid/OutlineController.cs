using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sahne içindeki GameObject'lere runtime'da outline ekleyip kaldırır.
/// 
/// KULLANIM:
///   // Outline ekle (fareyle hover, seçim vs.)
///   OutlineController.Register(gameObject);
///
///   // Outline kaldır
///   OutlineController.Unregister(gameObject);
/// </summary>
public class OutlineController : MonoBehaviour
{
    // Tüm outline alacak Renderer'ların listesi (static — Feature tarafından okunur)
    public static readonly List<MeshRenderer> RegisteredObjects = new List<MeshRenderer>();

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Verilen GameObject'in altındaki tüm MeshRenderer'lara outline ekler.
    /// </summary>
    public static void Register(GameObject go)
    {
        if (go == null) return;
        var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in renderers)
            if (!RegisteredObjects.Contains(r))
                RegisteredObjects.Add(r);
    }

    /// <summary>
    /// Verilen GameObject'in outline'ını kaldırır.
    /// </summary>
    public static void Unregister(GameObject go)
    {
        if (go == null) return;
        var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in renderers)
            RegisteredObjects.Remove(r);
    }

    /// <summary>
    /// Tüm outline'ları temizler.
    /// </summary>
    public static void Clear()
    {
        RegisteredObjects.Clear();
    }

    // ── Singleton Kolaylığı ───────────────────────────────────────────────
    private static OutlineController _instance;
    public static OutlineController Instance => _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnDestroy()
    {
        Clear();
        if (_instance == this) _instance = null;
    }
}
