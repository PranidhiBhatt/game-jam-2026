using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents the playable arena container in Dangerous Arena.
/// Provides a clean public API for gameplay systems (such as WorldManager)
/// to retrieve and query arena tiles without requiring knowledge of the internal scene hierarchy.
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
    /// Returns all Tile components belonging to this arena as a read-only list.
    /// Discovers and caches tiles from child objects if not already cached.
    /// </summary>
    /// <returns>Read-only list of all Tile components belonging to this arena.</returns>
    public IReadOnlyList<Tile> GetTiles()
    {
        if (cachedTiles == null || cachedTiles.Length == 0)
        {
            EnsureTilesCached();
        }

        return cachedTiles;
    }

    /// <summary>
    /// Returns all Tile components belonging to this arena as a direct array.
    /// </summary>
    /// <returns>Array of all Tile components belonging to this arena.</returns>
    public Tile[] GetTilesArray()
    {
        if (cachedTiles == null || cachedTiles.Length == 0)
        {
            EnsureTilesCached();
        }

        return cachedTiles;
    }

    /// <summary>
    /// Gets the total number of tiles belonging to this arena.
    /// </summary>
    public int TileCount => cachedTiles != null ? cachedTiles.Length : 0;

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
