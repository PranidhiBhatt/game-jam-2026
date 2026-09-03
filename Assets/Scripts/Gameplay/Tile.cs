using System;
using UnityEngine;

namespace DangerousArena.Gameplay
{
    /// <summary>
    /// Defines the gameplay classification of a tile in the arena.
    /// </summary>
    public enum TileType
    {
        Safe,
        Danger,
        Bonus,
        Finish
    }

    /// <summary>
    /// Represents an individual grid tile in the Dangerous Arena.
    /// Handles visual representation, dynamic type changes, and provides
    /// event hooks for player interaction without hardcoding any game managers.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class Tile : MonoBehaviour
    {
        [Header("Tile Configuration")]
        [Tooltip("The gameplay type of this tile.")]
        [SerializeField] private TileType tileType = TileType.Safe;

        [Tooltip("Whether this tile is currently active in the arena.")]
        [SerializeField] private bool isTileActive = true;

        [Header("Components")]
        [Tooltip("Renderer used to display the tile's visual state. If left empty, it will be found automatically.")]
        [SerializeField] private Renderer tileRenderer;

        [Header("Player Detection")]
        [Tooltip("Tag used to identify player objects entering this tile.")]
        [SerializeField] private string playerTag = "Player";

        [Header("Visual Colors")]
        [Tooltip("Visual color applied when TileType is Safe.")]
        [SerializeField] private Color safeColor = new Color(0.25f, 0.75f, 0.35f, 1f); // Green

        [Tooltip("Visual color applied when TileType is Danger.")]
        [SerializeField] private Color dangerColor = new Color(0.9f, 0.2f, 0.2f, 1f); // Red

        [Tooltip("Visual color applied when TileType is Bonus.")]
        [SerializeField] private Color bonusColor = new Color(1f, 0.82f, 0.1f, 1f); // Gold / Yellow

        [Tooltip("Visual color applied when TileType is Finish.")]
        [SerializeField] private Color finishColor = new Color(0.15f, 0.6f, 1f, 1f); // Blue / Cyan

        [Header("Optional Custom Materials")]
        [Tooltip("Optional custom material for Safe tiles. Overrides color tint if assigned.")]
        [SerializeField] private Material safeMaterial;

        [Tooltip("Optional custom material for Danger tiles. Overrides color tint if assigned.")]
        [SerializeField] private Material dangerMaterial;

        [Tooltip("Optional custom material for Bonus tiles. Overrides color tint if assigned.")]
        [SerializeField] private Material bonusMaterial;

        [Tooltip("Optional custom material for Finish tiles. Overrides color tint if assigned.")]
        [SerializeField] private Material finishMaterial;

        // Shader property IDs for URP and Standard shaders
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock propertyBlock;
        private bool isPlayerOnTile = false;

        #region Public Events & Callbacks

        /// <summary>
        /// Triggered when the player enters or steps onto this tile.
        /// Parameters: (Tile tile, GameObject player)
        /// </summary>
        public event Action<Tile, GameObject> OnPlayerEntered;

        /// <summary>
        /// Triggered when the player leaves or steps off this tile.
        /// Parameters: (Tile tile, GameObject player)
        /// </summary>
        public event Action<Tile, GameObject> OnPlayerExited;

        /// <summary>
        /// Triggered whenever this tile's type is changed dynamically.
        /// Parameters: (Tile tile, TileType newType)
        /// </summary>
        public event Action<Tile, TileType> OnTileTypeChanged;

        /// <summary>
        /// Triggered whenever this tile's active status changes.
        /// Parameters: (Tile tile, bool isActive)
        /// </summary>
        public event Action<Tile, bool> OnTileActiveChanged;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureRendererReference();
            UpdateVisual();
        }

        private void Start()
        {
            // Ensure visuals are updated after all Awake calls have completed
            UpdateVisual();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Reflect inspector adjustments immediately in the Scene view
            if (tileRenderer == null)
            {
                EnsureRendererReference();
            }
            UpdateVisual();
        }
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Gets the current TileType.
        /// </summary>
        /// <returns>The current TileType enum value.</returns>
        public TileType GetTileType()
        {
            return tileType;
        }

        /// <summary>
        /// Read-only property returning the current TileType.
        /// </summary>
        public TileType CurrentType => tileType;

        /// <summary>
        /// Changes the tile's gameplay type dynamically, updates its visuals,
        /// and notifies any subscribed gameplay listeners.
        /// </summary>
        /// <param name="newType">The new TileType to apply.</param>
        public void SetTileType(TileType newType)
        {
            if (tileType == newType) return;

            tileType = newType;
            UpdateVisual();
            OnTileTypeChanged?.Invoke(this, tileType);
        }

        /// <summary>
        /// Returns whether the tile is currently marked active.
        /// </summary>
        public bool IsTileActive => isTileActive;

        /// <summary>
        /// Enables or disables the tile, toggling its GameObject state
        /// and notifying subscribed gameplay listeners.
        /// </summary>
        /// <param name="active">True to activate the tile, false to deactivate it.</param>
        public void SetTileActive(bool active)
        {
            isTileActive = active;
            gameObject.SetActive(active);
            OnTileActiveChanged?.Invoke(this, active);
        }

        /// <summary>
        /// Updates the tile's visual appearance based on its current TileType.
        /// Uses MaterialPropertyBlock and material color assignments to avoid leaking
        /// materials in the Unity Editor or at runtime.
        /// </summary>
        public void UpdateVisual()
        {
            if (tileRenderer == null)
            {
                EnsureRendererReference();
                if (tileRenderer == null) return;
            }

            // 1. Apply optional custom material if assigned for this type
            Material customMaterial = GetMaterialForType(tileType);
            if (customMaterial != null)
            {
                tileRenderer.sharedMaterial = customMaterial;
            }

            // 2. Apply color tint for the current tile type
            Color targetColor = GetColorForType(tileType);

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            tileRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, targetColor);
            propertyBlock.SetColor(ColorId, targetColor);
            tileRenderer.SetPropertyBlock(propertyBlock);

            // In Play mode, also apply directly to instance material to guarantee compatibility
            // with all shaders (URP Lit, Simple Lit, Standard, Unlit)
            if (Application.isPlaying && customMaterial == null && tileRenderer != null)
            {
                if (tileRenderer.material.HasProperty(BaseColorId))
                {
                    tileRenderer.material.SetColor(BaseColorId, targetColor);
                }
                else if (tileRenderer.material.HasProperty(ColorId))
                {
                    tileRenderer.material.SetColor(ColorId, targetColor);
                }
            }
        }

        #endregion

        #region Collision & Trigger Detection

        private void OnTriggerEnter(Collider other)
        {
            HandlePlayerEnter(other.gameObject);
        }

        private void OnTriggerExit(Collider other)
        {
            HandlePlayerExit(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandlePlayerEnter(collision.gameObject);
        }

        private void OnCollisionExit(Collision collision)
        {
            HandlePlayerExit(collision.gameObject);
        }

        private void HandlePlayerEnter(GameObject obj)
        {
            if (!isTileActive) return;
            if (isPlayerOnTile) return;

            if (IsPlayer(obj))
            {
                isPlayerOnTile = true;
                OnPlayerEntered?.Invoke(this, obj);
            }
        }

        private void HandlePlayerExit(GameObject obj)
        {
            if (!isPlayerOnTile) return;

            if (IsPlayer(obj))
            {
                isPlayerOnTile = false;
                OnPlayerExited?.Invoke(this, obj);
            }
        }

        private bool IsPlayer(GameObject obj)
        {
            if (string.IsNullOrEmpty(playerTag))
            {
                return true;
            }
            return obj.CompareTag(playerTag);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Safely finds and caches the Renderer component if not already assigned.
        /// </summary>
        private void EnsureRendererReference()
        {
            if (tileRenderer == null)
            {
                tileRenderer = GetComponent<Renderer>();
                if (tileRenderer == null)
                {
                    tileRenderer = GetComponentInChildren<Renderer>();
                }
            }
        }

        /// <summary>
        /// Gets the configured color for a given TileType.
        /// </summary>
        private Color GetColorForType(TileType type)
        {
            switch (type)
            {
                case TileType.Safe:
                    return safeColor;
                case TileType.Danger:
                    return dangerColor;
                case TileType.Bonus:
                    return bonusColor;
                case TileType.Finish:
                    return finishColor;
                default:
                    return safeColor;
            }
        }

        /// <summary>
        /// Gets the optional custom material for a given TileType.
        /// </summary>
        private Material GetMaterialForType(TileType type)
        {
            switch (type)
            {
                case TileType.Safe:
                    return safeMaterial;
                case TileType.Danger:
                    return dangerMaterial;
                case TileType.Bonus:
                    return bonusMaterial;
                case TileType.Finish:
                    return finishMaterial;
                default:
                    return null;
            }
        }

        #endregion
    }
}
