using System;
using System.Collections;
using UnityEngine;

namespace DangerousArena.Gameplay
{
    /// <summary>
    /// Central coordinator for arena and world transformations.
    /// Manages the world-change lifecycle (request, begin, complete) and coordinates
    /// with GameManager and TimerManager without hardcoding any tile or level implementations.
    /// </summary>
    public class WorldManager : MonoBehaviour
    {
        public static WorldManager Instance { get; private set; }

        [Header("World Change Settings")]
        [Tooltip("If true, automatically calls CompleteWorldChange() after transitionDuration.")]
        [SerializeField] private bool autoCompleteTransition = true;

        [Tooltip("Duration in seconds for the world transition before auto-completing (if enabled).")]
        [SerializeField] private float transitionDuration = 1.5f;

        [Header("Optional References (Auto-located if unassigned)")]
        [SerializeField] private TimerManager timerManager;

        // Internal State
        private int shiftCount = 0;
        private bool isChangingWorld = false;
        private Coroutine transitionCoroutine;

        // Public Properties
        public int ShiftCount => shiftCount;
        public bool IsChangingWorld => isChangingWorld;
        public float TransitionDuration => transitionDuration;

        // Public Events (Decoupled from UI and Level designs)
        public event Action OnWorldChangeStarted;
        public event Action OnWorldChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            if (timerManager == null)
            {
#if UNITY_6000_0_OR_NEWER
                timerManager = FindFirstObjectByType<TimerManager>();
#else
                timerManager = FindObjectOfType<TimerManager>();
#endif
            }

            if (timerManager != null)
            {
                timerManager.OnTimerFinished -= HandleTimerFinished;
                timerManager.OnTimerFinished += HandleTimerFinished;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelRestarted -= HandleLevelRestarted;
                GameManager.Instance.OnLevelRestarted += HandleLevelRestarted;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (timerManager != null)
            {
                timerManager.OnTimerFinished -= HandleTimerFinished;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelRestarted -= HandleLevelRestarted;
            }
        }

        private void HandleLevelRestarted()
        {
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }
            isChangingWorld = false;
        }

        // --- PUBLIC CONTROL APIS ---

        /// <summary>
        /// Requests a world change. Validates active gameplay and initiates the transition.
        /// Can be called by TimerManager, debug triggers, or external game systems.
        /// </summary>
        public void RequestWorldChange()
        {
            // Ignore if already changing or if gameplay is not active (e.g. GameOver, Victory, WaitingForStart)
            if (isChangingWorld)
            {
                return;
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayActive)
            {
                return;
            }

            BeginWorldChange();
        }

        /// <summary>
        /// Begins the world change process: transitions GameState to ChangingWorld,
        /// notifies listeners via OnWorldChangeStarted, and starts transition timing.
        /// </summary>
        public void BeginWorldChange()
        {
            if (isChangingWorld)
            {
                return;
            }

            // Never initiate world change if gameplay is not active (GameOver, Victory, WaitingForStart)
            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayActive)
            {
                return;
            }

            isChangingWorld = true;

            // Transition GameManager to ChangingWorld state
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.ChangingWorld);
            }

            // Notify subscribers (Level Designer's arena systems, VFX, Audio, UI)
            OnWorldChangeStarted?.Invoke();

            if (autoCompleteTransition)
            {
                if (transitionCoroutine != null)
                {
                    StopCoroutine(transitionCoroutine);
                }
                transitionCoroutine = StartCoroutine(AutoTransitionRoutine());
            }
        }

        /// <summary>
        /// Completes the world change process: increments shift count, notifies subscribers
        /// via OnWorldChanged, restores GameState to Playing, and restarts the countdown timer.
        /// Can be called manually by Level systems when their custom animations finish.
        /// </summary>
        public void CompleteWorldChange()
        {
            if (!isChangingWorld)
            {
                return;
            }

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }

            shiftCount++;
            isChangingWorld = false;

            // Notify subscribers that the arena transformation is finished
            OnWorldChanged?.Invoke();

            // Return to Playing state ONLY if the game is still in ChangingWorld
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.ChangingWorld)
            {
                GameManager.Instance.SetGameState(GameState.Playing);
            }

            // Restart timer for the next cycle ONLY if gameplay is actively Playing
            if (timerManager != null && (GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing))
            {
                timerManager.StartTimer();
            }
        }

        /// <summary>
        /// Dynamically binds a TimerManager reference at runtime.
        /// </summary>
        public void RegisterTimerManager(TimerManager newTimerManager)
        {
            if (timerManager != null)
            {
                timerManager.OnTimerFinished -= HandleTimerFinished;
            }

            timerManager = newTimerManager;

            if (timerManager != null)
            {
                timerManager.OnTimerFinished += HandleTimerFinished;
            }
        }

        // --- INTERNAL EVENT HANDLERS ---

        private void HandleTimerFinished()
        {
            RequestWorldChange();
        }

        private IEnumerator AutoTransitionRoutine()
        {
            yield return new WaitForSeconds(transitionDuration);
            transitionCoroutine = null;
            CompleteWorldChange();
        }
    }
}
