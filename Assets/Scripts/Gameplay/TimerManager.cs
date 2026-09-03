using System;
using UnityEngine;

namespace DangerousArena.Gameplay
{
    /// <summary>
    /// Manages the periodic countdown timer (~20s) that triggers arena and world shifts.
    /// Operates strictly while GameState is Playing, pausing during ChangingWorld, GameOver, and Victory.
    /// </summary>
    public class TimerManager : MonoBehaviour
    {
        [Header("Timer Settings")]
        [Tooltip("Default duration between world changes in seconds (approximately 20 seconds).")]
        [SerializeField] private float defaultInterval = 20.0f;

        [Tooltip("If true, automatically synchronizes timer lifecycle with GameManager state events.")]
        [SerializeField] private bool autoSyncWithGameManager = true;

        // Internal State
        private float currentInterval;
        private float remainingTime;
        private bool isRunning;

        // Public Properties
        public float RemainingTime => remainingTime;
        public float TotalInterval => currentInterval;
        public float NormalizedProgress => currentInterval > 0f ? Mathf.Clamp01(remainingTime / currentInterval) : 0f;
        public bool IsRunning => isRunning;

        // Public Events (Decoupled from UI)
        public event Action<float> OnTimerUpdated;
        public event Action OnTimerFinished;

        private void Awake()
        {
            currentInterval = defaultInterval;
            remainingTime = defaultInterval;
            isRunning = false;
        }

        private void OnEnable()
        {
            if (autoSyncWithGameManager && GameManager.Instance != null)
            {
                SubscribeToGameManager();
            }
        }

        private void Start()
        {
            if (autoSyncWithGameManager && GameManager.Instance != null)
            {
                SubscribeToGameManager();

                // If game is already in Playing state when TimerManager initializes
                if (GameManager.Instance.CurrentState == GameState.Playing)
                {
                    StartTimer();
                }
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromGameManager();
        }

        private void Update()
        {
            // Early return to prevent unnecessary computation when inactive
            if (!isRunning)
            {
                return;
            }

            // Safety guard: stop if game left Playing state
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                isRunning = false;
                return;
            }

            remainingTime -= Time.deltaTime;
            OnTimerUpdated?.Invoke(remainingTime);

            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                isRunning = false;
                OnTimerUpdated?.Invoke(0f);
                OnTimerFinished?.Invoke();
            }
        }

        // --- PUBLIC CONTROL APIS ---

        /// <summary>
        /// Starts or restarts the countdown timer with an optional custom duration.
        /// </summary>
        public void StartTimer(float? customDuration = null)
        {
            currentInterval = customDuration.HasValue ? Mathf.Max(0.1f, customDuration.Value) : defaultInterval;
            remainingTime = currentInterval;
            isRunning = true;
            OnTimerUpdated?.Invoke(remainingTime);
        }

        /// <summary>
        /// Resumes the countdown from where it was paused without resetting.
        /// </summary>
        public void ResumeTimer()
        {
            if (remainingTime > 0f)
            {
                isRunning = true;
            }
        }

        /// <summary>
        /// Pauses or stops the countdown.
        /// </summary>
        public void StopTimer()
        {
            isRunning = false;
        }

        /// <summary>
        /// Resets the remaining time back to the interval.
        /// </summary>
        public void ResetTimer(float? newInterval = null)
        {
            if (newInterval.HasValue)
            {
                currentInterval = Mathf.Max(0.1f, newInterval.Value);
            }
            remainingTime = currentInterval;
            OnTimerUpdated?.Invoke(remainingTime);
        }

        /// <summary>
        /// Sets a new default interval for future cycles.
        /// </summary>
        public void SetInterval(float newInterval)
        {
            defaultInterval = Mathf.Max(0.1f, newInterval);
            currentInterval = defaultInterval;
        }

        // --- GAME MANAGER EVENT SYNCHRONIZATION ---

        private void SubscribeToGameManager()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;

            GameManager.Instance.OnLevelChanged -= HandleLevelChanged;
            GameManager.Instance.OnLevelChanged += HandleLevelChanged;

            GameManager.Instance.OnLevelRestarted -= HandleLevelRestarted;
            GameManager.Instance.OnLevelRestarted += HandleLevelRestarted;
        }

        private void UnsubscribeFromGameManager()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnLevelChanged -= HandleLevelChanged;
            GameManager.Instance.OnLevelRestarted -= HandleLevelRestarted;
        }

        private void HandleGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Playing:
                    if (remainingTime <= 0f)
                    {
                        StartTimer();
                    }
                    else
                    {
                        ResumeTimer();
                    }
                    break;

                case GameState.WaitingForStart:
                case GameState.GameOver:
                case GameState.Victory:
                case GameState.ChangingWorld:
                case GameState.LevelComplete:
                    StopTimer();
                    break;
            }
        }

        private void HandleLevelChanged(int newLevel)
        {
            ResetTimer();
        }

        private void HandleLevelRestarted()
        {
            StartTimer();
        }
    }
}
