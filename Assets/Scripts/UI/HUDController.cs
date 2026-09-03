using System;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;

/// <summary>
/// Controls the in-game HUD display (TIME and LEVEL) and warning effects.
/// Subscribes strictly to TimerManager and GameManager events without polling or counting down via Time.deltaTime.
/// Safely handles situations where managers are temporarily unavailable without throwing NullReferenceExceptions.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("UI Text References")]
    [Tooltip("TextMeshPro component displaying the time value.")]
    [SerializeField] private TMP_Text timeText;

    [Tooltip("TextMeshPro component displaying the level value.")]
    [SerializeField] private TMP_Text levelText;

    [Header("Display Formatting")]
    [Tooltip("Format string for time display.")]
    [SerializeField] private string timeFormat = "TIME: {0}";

    [Tooltip("Format string for level display.")]
    [SerializeField] private string levelFormat = "LEVEL: {0}";

    [Header("Initial Values")]
    [SerializeField] private int initialTime = 17;
    [SerializeField] private int initialLevel = 1;

    [Header("Timer Warning Settings")]
    [Tooltip("Remaining seconds threshold at or below which warning effect activates (default 5).")]
    [SerializeField] private int warningThreshold = 5;

    [Tooltip("Color applied to the time text during warning state (timer <= 5).")]
    [SerializeField] private Color warningTimeColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Tooltip("Default/normal color applied to the time text (timer > 5).")]
    [SerializeField] private Color normalTimeColor = Color.white;

    [Tooltip("Scale punch multiplier applied to time text on each warning tick.")]
    [SerializeField] private float warningPulseScale = 1.2f;

    [Tooltip("Duration in seconds for the pulse to return to normal scale.")]
    [SerializeField] private float pulseDuration = 0.2f;

    // Current displayed values
    private int currentTime;
    private int currentLevel;

    // Warning state tracking
    private bool isWarningActive = false;
    private Coroutine pulseCoroutine;
    private Vector3 originalTimeScale = Vector3.one;

    // Active event subscriptions for clean unsubscription
    private EventInfo subscribedTimerEvent;
    private object subscribedTimerTarget;
    private Delegate subscribedTimerDelegate;

    private EventInfo subscribedGMEvent;
    private object subscribedGMTarget;
    private Delegate subscribedGMDelegate;

    #region Unity Lifecycle

    private void Awake()
    {
        // Auto-discover child text components if not assigned in Inspector
        EnsureTextReferences();

        if (timeText != null)
        {
            normalTimeColor = timeText.color;
            originalTimeScale = timeText.rectTransform.localScale;
        }

        // Initialize HUD display with default required values (TIME: 17, LEVEL: 1)
        UpdateTime(initialTime);
        UpdateLevel(initialLevel);
    }

    private void OnEnable()
    {
        SubscribeToManagers();
    }

    private void Start()
    {
        // If managers initialized after OnEnable (e.g. in Awake/Start of another script), retry subscription
        if (subscribedTimerEvent == null || subscribedGMEvent == null)
        {
            SubscribeToManagers();
        }
    }

    private void OnDisable()
    {
        ResetWarningAppearance();
        UnsubscribeFromManagers();
    }

    private void OnDestroy()
    {
        ResetWarningAppearance();
        UnsubscribeFromManagers();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Updates the displayed TIME text and applies warning appearance when timer reaches 5 or less.
    /// Does NOT create any internal timer or countdown.
    /// </summary>
    /// <param name="timeRemaining">The remaining time value from TimerManager.</param>
    public void UpdateTime(int timeRemaining)
    {
        currentTime = timeRemaining;
        if (timeText != null)
        {
            timeText.text = string.Format(timeFormat, currentTime);
        }

        // Apply warning appearance strictly driven by the received timer value
        UpdateTimerWarning(timeRemaining);
    }

    /// <summary>
    /// Overload for float time remaining (rounds up to nearest integer).
    /// </summary>
    /// <param name="timeRemaining">The remaining time value from TimerManager.</param>
    public void UpdateTime(float timeRemaining)
    {
        UpdateTime(Mathf.CeilToInt(timeRemaining));
    }

    /// <summary>
    /// Updates the displayed LEVEL text.
    /// </summary>
    /// <param name="level">The current level value from GameManager.</param>
    public void UpdateLevel(int level)
    {
        currentLevel = level;
        if (levelText != null)
        {
            levelText.text = string.Format(levelFormat, currentLevel);
        }
    }

    /// <summary>
    /// Returns true if the timer warning is currently active (timeRemaining <= warningThreshold).
    /// </summary>
    public bool IsWarningActive
    {
        get { return isWarningActive; }
    }

    /// <summary>
    /// Explicit runtime binding method if TimerManager is spawned or registered dynamically.
    /// </summary>
    /// <param name="timerManagerInstance">The instance of TimerManager.</param>
    public void BindTimerManager(object timerManagerInstance)
    {
        if (timerManagerInstance != null && subscribedTimerEvent == null)
        {
            TrySubscribeTimerEvents(timerManagerInstance.GetType(), timerManagerInstance);
        }
    }

    /// <summary>
    /// Explicit runtime binding method if GameManager is spawned or registered dynamically.
    /// </summary>
    /// <param name="gameManagerInstance">The instance of GameManager.</param>
    public void BindGameManager(object gameManagerInstance)
    {
        if (gameManagerInstance != null && subscribedGMEvent == null)
        {
            TrySubscribeGameManagerEvents(gameManagerInstance.GetType(), gameManagerInstance);
        }
    }

    #endregion

    #region Timer Warning System

    /// <summary>
    /// Updates visual appearance based on timer value:
    /// timer > 5: normal appearance
    /// timer <= 5: warning appearance (color change and subtle pulse)
    /// </summary>
    /// <param name="timeRemaining">Seconds remaining from TimerManager.</param>
    private void UpdateTimerWarning(int timeRemaining)
    {
        if (timeText == null) return;

        if (timeRemaining <= warningThreshold)
        {
            isWarningActive = true;
            timeText.color = warningTimeColor;

            // Trigger a subtle scale pulse effect on this warning tick
            if (gameObject.activeInHierarchy)
            {
                if (pulseCoroutine != null)
                {
                    StopCoroutine(pulseCoroutine);
                }
                pulseCoroutine = StartCoroutine(PulseRoutine());
            }
        }
        else
        {
            // Reset to normal appearance when above threshold
            if (isWarningActive || timeText.color != normalTimeColor)
            {
                ResetWarningAppearance();
            }
        }
    }

    /// <summary>
    /// Resets the time display to its normal appearance and scale.
    /// </summary>
    private void ResetWarningAppearance()
    {
        isWarningActive = false;
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        if (timeText != null)
        {
            timeText.color = normalTimeColor;
            timeText.rectTransform.localScale = originalTimeScale;
        }
    }

    /// <summary>
    /// Subtle pulse effect that punches the time text scale and smoothly returns to normal.
    /// </summary>
    private IEnumerator PulseRoutine()
    {
        if (timeText == null) yield break;

        RectTransform rt = timeText.rectTransform;
        Vector3 targetScale = originalTimeScale * warningPulseScale;
        rt.localScale = targetScale;

        float elapsed = 0f;
        while (elapsed < pulseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / pulseDuration;
            if (rt != null)
            {
                rt.localScale = Vector3.Lerp(targetScale, originalTimeScale, t);
            }
            yield return null;
        }

        if (rt != null)
        {
            rt.localScale = originalTimeScale;
        }
        pulseCoroutine = null;
    }

    #endregion

    #region Safe Manager Subscriptions

    private void SubscribeToManagers()
    {
        TrySubscribeTimerManager();
        TrySubscribeGameManager();
    }

    private void UnsubscribeFromManagers()
    {
        TryUnsubscribeTimerManager();
        TryUnsubscribeGameManager();
    }

    /// <summary>
    /// Safely locates TimerManager and subscribes to its timer tick event.
    /// Does not throw NullReferenceException if TimerManager is temporarily unavailable.
    /// </summary>
    private void TrySubscribeTimerManager()
    {
        if (subscribedTimerEvent != null) return;

        Type timerType = FindType("TimerManager") 
                      ?? FindType("DangerousArena.Managers.TimerManager")
                      ?? FindType("DangerousArena.TimerManager");

        if (timerType == null)
        {
            // TimerManager is not yet created or loaded in assemblies
            return;
        }

        object timerInstance = ResolveInstance(timerType);
        TrySubscribeTimerEvents(timerType, timerInstance);
    }

    private void TrySubscribeTimerEvents(Type timerType, object timerInstance)
    {
        // Candidate event names in order of project conventions
        string[] candidateEvents = { "OnTimerTick", "OnTimeChanged", "OnTimerUpdated", "OnTick", "OnTimeRemainingChanged" };

        foreach (string eventName in candidateEvents)
        {
            EventInfo ev = timerType.GetEvent(eventName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (ev != null)
            {
                MethodInfo invokeMethod = ev.EventHandlerType.GetMethod("Invoke");
                if (invokeMethod != null)
                {
                    ParameterInfo[] parameters = invokeMethod.GetParameters();
                    MethodInfo targetHandler = null;

                    if (parameters.Length == 1)
                    {
                        if (parameters[0].ParameterType == typeof(int))
                        {
                            targetHandler = GetType().GetMethod("HandleTimerTickInt", BindingFlags.NonPublic | BindingFlags.Instance);
                        }
                        else if (parameters[0].ParameterType == typeof(float))
                        {
                            targetHandler = GetType().GetMethod("HandleTimerTickFloat", BindingFlags.NonPublic | BindingFlags.Instance);
                        }
                    }
                    else if (parameters.Length == 0)
                    {
                        targetHandler = GetType().GetMethod("HandleTimerTickVoid", BindingFlags.NonPublic | BindingFlags.Instance);
                    }

                    if (targetHandler != null)
                    {
                        try
                        {
                            Delegate del = Delegate.CreateDelegate(ev.EventHandlerType, this, targetHandler);
                            bool isStatic = ev.GetAddMethod(true).IsStatic;
                            object target = isStatic ? null : timerInstance;

                            ev.AddEventHandler(target, del);
                            subscribedTimerEvent = ev;
                            subscribedTimerTarget = target;
                            subscribedTimerDelegate = del;
                            break;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[HUDController] Failed to bind event " + eventName + ": " + ex.Message);
                        }
                    }
                }
            }
        }
    }

    private void TryUnsubscribeTimerManager()
    {
        if (subscribedTimerEvent != null && subscribedTimerDelegate != null)
        {
            try
            {
                subscribedTimerEvent.RemoveEventHandler(subscribedTimerTarget, subscribedTimerDelegate);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[HUDController] Error unsubscribing from TimerManager: " + ex.Message);
            }
            finally
            {
                subscribedTimerEvent = null;
                subscribedTimerTarget = null;
                subscribedTimerDelegate = null;
            }
        }
    }

    /// <summary>
    /// Safely locates GameManager and subscribes to its level change event.
    /// Does not throw NullReferenceException if GameManager is temporarily unavailable.
    /// </summary>
    private void TrySubscribeGameManager()
    {
        if (subscribedGMEvent != null) return;

        Type gmType = FindType("GameManager") 
                   ?? FindType("DangerousArena.Managers.GameManager")
                   ?? FindType("DangerousArena.GameManager");

        if (gmType == null)
        {
            // GameManager is not yet created or loaded in assemblies
            return;
        }

        object gmInstance = ResolveInstance(gmType);
        TrySubscribeGameManagerEvents(gmType, gmInstance);
    }

    private void TrySubscribeGameManagerEvents(Type gmType, object gmInstance)
    {
        // Candidate event names in order of project conventions
        string[] candidateEvents = { "OnLevelChanged", "OnLevelLoaded", "OnLevelStart", "OnStageChanged" };

        foreach (string eventName in candidateEvents)
        {
            EventInfo ev = gmType.GetEvent(eventName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (ev != null)
            {
                MethodInfo invokeMethod = ev.EventHandlerType.GetMethod("Invoke");
                if (invokeMethod != null)
                {
                    ParameterInfo[] parameters = invokeMethod.GetParameters();
                    MethodInfo targetHandler = null;

                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(int))
                    {
                        targetHandler = GetType().GetMethod("HandleLevelChangedInt", BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                    else if (parameters.Length == 0)
                    {
                        targetHandler = GetType().GetMethod("HandleLevelChangedVoid", BindingFlags.NonPublic | BindingFlags.Instance);
                    }

                    if (targetHandler != null)
                    {
                        try
                        {
                            Delegate del = Delegate.CreateDelegate(ev.EventHandlerType, this, targetHandler);
                            bool isStatic = ev.GetAddMethod(true).IsStatic;
                            object target = isStatic ? null : gmInstance;

                            ev.AddEventHandler(target, del);
                            subscribedGMEvent = ev;
                            subscribedGMTarget = target;
                            subscribedGMDelegate = del;
                            break;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[HUDController] Failed to bind event " + eventName + ": " + ex.Message);
                        }
                    }
                }
            }
        }
    }

    private void TryUnsubscribeGameManager()
    {
        if (subscribedGMEvent != null && subscribedGMDelegate != null)
        {
            try
            {
                subscribedGMEvent.RemoveEventHandler(subscribedGMTarget, subscribedGMDelegate);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[HUDController] Error unsubscribing from GameManager: " + ex.Message);
            }
            finally
            {
                subscribedGMEvent = null;
                subscribedGMTarget = null;
                subscribedGMDelegate = null;
            }
        }
    }

    #endregion

    #region Event Handlers

    private void HandleTimerTickInt(int timeRemaining)
    {
        UpdateTime(timeRemaining);
    }

    private void HandleTimerTickFloat(float timeRemaining)
    {
        UpdateTime(timeRemaining);
    }

    private void HandleTimerTickVoid()
    {
        if (subscribedTimerTarget != null)
        {
            PropertyInfo prop = subscribedTimerTarget.GetType().GetProperty("TimeRemaining") 
                             ?? subscribedTimerTarget.GetType().GetProperty("CurrentTime");
            if (prop != null)
            {
                object val = prop.GetValue(subscribedTimerTarget, null);
                if (val is int)
                {
                    UpdateTime((int)val);
                }
                else if (val is float)
                {
                    UpdateTime((float)val);
                }
            }
        }
    }

    private void HandleLevelChangedInt(int newLevel)
    {
        UpdateLevel(newLevel);
    }

    private void HandleLevelChangedVoid()
    {
        if (subscribedGMTarget != null)
        {
            PropertyInfo prop = subscribedGMTarget.GetType().GetProperty("CurrentLevel") 
                             ?? subscribedGMTarget.GetType().GetProperty("Level");
            if (prop != null)
            {
                object val = prop.GetValue(subscribedGMTarget, null);
                if (val is int)
                {
                    UpdateLevel((int)val);
                }
            }
        }
    }

    #endregion

    #region Helpers

    private void EnsureTextReferences()
    {
        if (timeText != null && levelText != null) return;

        TMP_Text[] tmpTexts = GetComponentsInChildren<TMP_Text>(true);
        foreach (var textComp in tmpTexts)
        {
            string nameLower = textComp.gameObject.name.ToLowerInvariant();
            if (timeText == null && nameLower.Contains("time"))
            {
                timeText = textComp;
            }
            else if (levelText == null && nameLower.Contains("level"))
            {
                levelText = textComp;
            }
        }
    }

    private static Type FindType(string typeName)
    {
        Type direct = Type.GetType(typeName);
        if (direct != null) return direct;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type found = assemblies[i].GetType(typeName);
            if (found != null) return found;
        }
        return null;
    }

    private static object ResolveInstance(Type targetType)
    {
        // 1. Static property Instance (standard singleton pattern)
        PropertyInfo instanceProp = targetType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        if (instanceProp != null)
        {
            object inst = instanceProp.GetValue(null);
            if (inst != null) return inst;
        }

        // 2. Component instance in scene
        if (typeof(Component).IsAssignableFrom(targetType))
        {
            return UnityEngine.Object.FindAnyObjectByType(targetType);
        }

        return null;
    }

    #endregion
}
