using UnityEngine;

/// <summary>
/// Represents the playable arena container in Dangerous Arena.
/// Provides a clean public API for gameplay systems to access and query
/// arena tiles without requiring knowledge of the internal scene hierarchy.
/// </summary>
[DisallowMultipleComponent]
public class Arena : MonoBehaviour
{
    [Header("Arena Configuration")]
    [Tooltip("Cached collection of tiles in this arena. Populated automatically if unassigned.")]
    [SerializeField] private Tile[] cachedTiles;

    private void Awake()
    {
        EnsureTilesCached();
    }

    /// <summary>
    /// Returns all Tile components belonging to this arena.
    /// Discovers and caches tiles from child objects if not already cached.
    /// </summary>
    /// <returns>Array of all Tile components belonging to this arena.</returns>
    public Tile[] GetTiles()
    {
        if (cachedTiles == null || cachedTiles.Length == 0)
        {
            EnsureTilesCached();
        }

        return cachedTiles;
    }

    /// <summary>
    /// Refreshes the internal cache of tiles by scanning child components.
    /// Useful if tiles are added or removed dynamically.
    /// </summary>
    public void RefreshTiles()
    {
        cachedTiles = GetComponentsInChildren<Tile>(true);
    }

    private void EnsureTilesCached()
    {
        if (cachedTiles == null || cachedTiles.Length == 0)
        {
            cachedTiles = GetComponentsInChildren<Tile>(true);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (cachedTiles == null || cachedTiles.Length == 0)
        {
            cachedTiles = GetComponentsInChildren<Tile>(true);
        }
    }
#endif
}
