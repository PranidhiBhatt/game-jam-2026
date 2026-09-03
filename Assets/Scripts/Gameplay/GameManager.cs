using System;
using UnityEngine;
using DangerousArena.Player;

namespace DangerousArena.Gameplay
{
    /// <summary>
    /// Central gameplay coordinator for Dangerous Arena.
    /// Manages high-level GameState, level progression, player lifecycle (death/respawn),
    /// and dispatches events for UI (Member 3) and Level Systems (Member 2).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Level Progression Settings")]
        [Tooltip("Starting level index.")]
        [SerializeField] private int startingLevel = 1;

        [Tooltip("Total number of levels before Victory.")]
        [SerializeField] private int maxLevels = 3;

        [Tooltip("If true, GameManager persists across scene loads.")]
        [SerializeField] private bool persistAcrossScenes = false;

        [Tooltip("If true, automatically begins gameplay on Start for immediate playability in test scenes.")]
        [SerializeField] private bool autoStartOnPlay = true;

        [Header("Player Reference (Optional - auto-located if null)")]
        [SerializeField] private PlayerController player;

        // Current Session State
        [SerializeField] private GameState currentState = GameState.WaitingForStart;
        [SerializeField] private int currentLevel = 1;

        // Public Properties
        public GameState CurrentState => currentState;
        public int CurrentLevel => currentLevel;
        public int MaxLevels => maxLevels;
        public bool IsGameplayActive => currentState == GameState.Playing || currentState == GameState.ChangingWorld;

        // Public Events (Decoupled from UI / Level implementations)
        public event Action OnGameStarted;
        public event Action OnGameOver;
        public event Action OnVictory;
        public event Action OnLevelComplete;
        public event Action OnLevelRestarted;
        public event Action<int> OnLevelChanged;
        public event Action<int> OnLevelLoadRequested;
        public event Action<GameState> OnGameStateChanged;
        public event Action<BonusType, int> OnBonusCollected;

        private ILevelLoader customLevelLoader;

        private void Awake()
        {
            // Enforce Singleton
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            currentLevel = startingLevel;
            currentState = GameState.WaitingForStart;
        }

        private void Start()
        {
            LocatePlayerIfMissing();

            if (autoStartOnPlay && currentState == GameState.WaitingForStart)
            {
                StartGame();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Starts a fresh game session starting at level 1.
        /// </summary>
        public void StartGame()
        {
            currentLevel = startingLevel;
            SetState(GameState.Playing);

            LocatePlayerIfMissing();
            if (player != null)
            {
                player.ResetToInitialSpawn();
            }

            OnGameStarted?.Invoke();
            OnLevelLoadRequested?.Invoke(currentLevel);
            customLevelLoader?.LoadLevel(currentLevel);
            OnLevelChanged?.Invoke(currentLevel);
        }

        /// <summary>
        /// Restarts the current active level after death or manual restart.
        /// Restores player alive state, resets timer, and sets GameState to Playing.
        /// Preserves CurrentLevel unchanged.
        /// </summary>
        public void RestartLevel()
        {
            SetState(GameState.Playing);

            LocatePlayerIfMissing();
            if (player != null)
            {
                player.ResetToInitialSpawn();
            }

            OnLevelRestarted?.Invoke();
            OnLevelLoadRequested?.Invoke(currentLevel);
            customLevelLoader?.LoadLevel(currentLevel);
            OnLevelChanged?.Invoke(currentLevel);
        }

        /// <summary>
        /// Advances strictly to the next sequential level (1 -> 2 -> 3), or triggers Victory if max levels reached.
        /// Dispatches OnLevelLoadRequested and OnLevelChanged for level loading systems.
        /// </summary>
        public void LoadNextLevel()
        {
            if (currentLevel < maxLevels)
            {
                currentLevel++;
                SetState(GameState.Playing);

                LocatePlayerIfMissing();
                if (player != null)
                {
                    player.ResetToInitialSpawn();
                }

                OnLevelLoadRequested?.Invoke(currentLevel);
                customLevelLoader?.LoadLevel(currentLevel);
                OnLevelChanged?.Invoke(currentLevel);
            }
            else
            {
                SetState(GameState.Victory);

                LocatePlayerIfMissing();
                if (player != null)
                {
                    player.SetMovementEnabled(false);
                }

                OnVictory?.Invoke();
            }
        }

        /// <summary>
        /// Loads a specific level index within valid range (1 to maxLevels).
        /// Reusable by Level selection UI or developer debugging.
        /// </summary>
        public void LoadLevel(int targetLevel)
        {
            if (targetLevel < 1 || targetLevel > maxLevels)
            {
                Debug.LogWarning($"[GameManager] Cannot load level {targetLevel}. Must be between 1 and {maxLevels}.");
                return;
            }

            currentLevel = targetLevel;
            SetState(GameState.Playing);

            LocatePlayerIfMissing();
            if (player != null)
            {
                player.ResetToInitialSpawn();
            }

            OnLevelLoadRequested?.Invoke(currentLevel);
            customLevelLoader?.LoadLevel(currentLevel);
            OnLevelChanged?.Invoke(currentLevel);
        }

        /// <summary>
        /// Registers an optional external Level/Scene loader implementing ILevelLoader.
        /// </summary>
        public void RegisterLevelLoader(ILevelLoader loader)
        {
            customLevelLoader = loader;
        }

        /// <summary>
        /// Unregisters the current external Level/Scene loader.
        /// </summary>
        public void UnregisterLevelLoader(ILevelLoader loader)
        {
            if (customLevelLoader == loader)
            {
                customLevelLoader = null;
            }
        }

        /// <summary>
        /// Handles player death (hazard contact, falling into void, etc.).
        /// Ignores duplicate calls or calls when gameplay is not active.
        /// </summary>
        public void HandlePlayerDeath()
        {
            // Ignore duplicate death calls or calls outside active gameplay
            if (!IsGameplayActive)
            {
                return;
            }

            SetState(GameState.GameOver);

            LocatePlayerIfMissing();
            if (player != null)
            {
                player.SetMovementEnabled(false);
            }

            OnGameOver?.Invoke();
        }

        /// <summary>
        /// Called when the player steps on the finish/goal tile.
        /// Determines whether to complete the level or trigger overall game victory.
        /// </summary>
        public void PlayerReachedFinish()
        {
            // Ignore if gameplay is not active (e.g. already dead or already finished)
            if (!IsGameplayActive)
            {
                return;
            }

            LocatePlayerIfMissing();
            if (player != null)
            {
                player.SetMovementEnabled(false);
            }

            if (currentLevel >= maxLevels)
            {
                SetState(GameState.Victory);
                OnVictory?.Invoke();
            }
            else
            {
                SetState(GameState.LevelComplete);
                OnLevelComplete?.Invoke();
            }
        }

        /// <summary>
        /// Direct hook to trigger level completion.
        /// </summary>
        public void CompleteLevel()
        {
            PlayerReachedFinish();
        }

        /// <summary>
        /// Handles bonus item collection. Notifies external subscribers (UI, Audio, Player).
        /// </summary>
        public void HandleBonusCollected(BonusType bonusType, int value = 0)
        {
            if (!IsGameplayActive)
            {
                return;
            }

            OnBonusCollected?.Invoke(bonusType, value);
        }

        /// <summary>
        /// Explicit state setter for companion systems (e.g., TimerManager transitioning to ChangingWorld).
        /// </summary>
        public void SetGameState(GameState newState)
        {
            SetState(newState);
        }

        /// <summary>
        /// Dynamically assigns the active player reference at runtime.
        /// </summary>
        public void RegisterPlayer(PlayerController playerController)
        {
            player = playerController;
        }

        private void SetState(GameState newState)
        {
            if (currentState == newState)
            {
                return;
            }

            currentState = newState;
            OnGameStateChanged?.Invoke(currentState);
        }

        private void LocatePlayerIfMissing()
        {
            if (player == null)
            {
#if UNITY_6000_0_OR_NEWER
                player = FindFirstObjectByType<PlayerController>();
#else
                player = FindObjectOfType<PlayerController>();
#endif
            }
        }
    }
}
