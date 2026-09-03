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

    [Header("World Shift Warning Settings")]
    [Tooltip("TextMeshPro component displaying the 'WORLD SHIFT!' notification.")]
    [SerializeField] private TMP_Text worldShiftText;

    [Tooltip("Optional CanvasGroup containing the World Shift display (used for fading).")]
    [SerializeField] private CanvasGroup worldShiftCanvasGroup;

    [Tooltip("World shift display message.")]
    [SerializeField] private string worldShiftMessage = "WORLD SHIFT!";

    [Tooltip("Duration in seconds the World Shift notification stays fully visible.")]
    [SerializeField] private float worldShiftDisplayDuration = 1.5f;

    [Tooltip("Duration in seconds for the World Shift notification to fade out.")]
    [SerializeField] private float worldShiftFadeDuration = 0.5f;

    // Current displayed values
    private int currentTime;
    private int currentLevel;

    // Warning state tracking
    private bool isWarningActive = false;
    private Coroutine pulseCoroutine;
    private Vector3 originalTimeScale = Vector3.one;

    // World shift state tracking
    private Coroutine worldShiftCoroutine;
    private Vector3 originalWorldShiftScale = Vector3.one;

    // Active event subscriptions for clean unsubscription
    private EventInfo subscribedTimerEvent;
    private object subscribedTimerTarget;
    private Delegate subscribedTimerDelegate;

    private EventInfo subscribedGMEvent;
    private object subscribedGMTarget;
    private Delegate subscribedGMDelegate;

    private EventInfo subscribedWorldEvent;
    private object subscribedWorldTarget;
    private Delegate subscribedWorldDelegate;

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

        // Ensure World Shift notification is initially hidden
        HideWorldShiftImmediate();
    }

    private void OnEnable()
    {
        SubscribeToManagers();
    }

    private void Start()
    {
        // If managers initialized after OnEnable (e.g. in Awake/Start of another script), retry subscription
        if (subscribedTimerEvent == null || subscribedGMEvent == null || subscribedWorldEvent == null)
        {
            SubscribeToManagers();
        }
    }

    private void OnDisable()
    {
        ResetWarningAppearance();
        ResetWorldShiftAppearance();
        UnsubscribeFromManagers();
    }

    private void OnDestroy()
    {
        ResetWarningAppearance();
        ResetWorldShiftAppearance();
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

    /// <summary>
    /// Displays the "WORLD SHIFT!" notification banner.
    /// Appears immediately when the world-change event occurs, stays visible briefly,
    /// fades out automatically, and becomes hidden.
    /// Strictly event-driven (no 20-second timer, no Update() polling).
    /// </summary>
    [ContextMenu("Test World Shift")]
    public void TriggerWorldShift()
    {
        if (worldShiftText == null && worldShiftCanvasGroup == null)
        {
            EnsureTextReferences();
            if (worldShiftText == null && worldShiftCanvasGroup == null) return;
        }

        if (worldShiftCoroutine != null)
        {
            StopCoroutine(worldShiftCoroutine);
        }

        if (gameObject.activeInHierarchy)
        {
            worldShiftCoroutine = StartCoroutine(WorldShiftRoutine());
        }
    }

    /// <summary>
    /// Explicit runtime binding method if WorldManager is spawned or registered dynamically.
    /// </summary>
    /// <param name="worldManagerInstance">The instance of WorldManager.</param>
    public void BindWorldManager(object worldManagerInstance)
    {
        if (worldManagerInstance != null && subscribedWorldEvent == null)
        {
            TrySubscribeWorldEvents(worldManagerInstance.GetType(), worldManagerInstance);
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

    #region World Shift System

    /// <summary>
    /// Coroutine controlling the presentation of the World Shift warning:
    /// 1. Immediately shows the banner at full opacity.
    /// 2. Holds visibility for worldShiftDisplayDuration.
    /// 3. Smoothly fades opacity to 0 over worldShiftFadeDuration.
    /// 4. Hides the GameObject.
    /// </summary>
    private IEnumerator WorldShiftRoutine()
    {
        if (worldShiftText != null)
        {
            worldShiftText.text = worldShiftMessage;
            worldShiftText.gameObject.SetActive(true);
            worldShiftText.rectTransform.localScale = originalWorldShiftScale * 1.3f;
        }

        if (worldShiftCanvasGroup != null)
        {
            worldShiftCanvasGroup.gameObject.SetActive(true);
            worldShiftCanvasGroup.alpha = 1f;
        }
        else if (worldShiftText != null)
        {
            Color c = worldShiftText.color;
            c.a = 1f;
            worldShiftText.color = c;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWorldShift();
        }

        // Punch scale down from 1.3 to 1.0 smoothly for impact
        float punchElapsed = 0f;
        float punchDuration = 0.25f;
        Vector3 peakScale = originalWorldShiftScale * 1.3f;

        while (punchElapsed < punchDuration)
        {
            punchElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(punchElapsed / punchDuration);
            float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);
            if (worldShiftText != null)
            {
                worldShiftText.rectTransform.localScale = Vector3.Lerp(peakScale, originalWorldShiftScale, smoothT);
            }
            yield return null;
        }

        if (worldShiftText != null)
        {
            worldShiftText.rectTransform.localScale = originalWorldShiftScale;
        }

        // Stay visible briefly
        float remainingDisplay = Mathf.Max(0f, worldShiftDisplayDuration - punchDuration);
        yield return new WaitForSecondsRealtime(remainingDisplay);

        // Fade out smoothly
        float elapsed = 0f;
        while (elapsed < worldShiftFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsed / worldShiftFadeDuration));

            if (worldShiftCanvasGroup != null)
            {
                worldShiftCanvasGroup.alpha = alpha;
            }
            else if (worldShiftText != null)
            {
                Color c = worldShiftText.color;
                c.a = alpha;
                worldShiftText.color = c;
            }

            yield return null;
        }

        ResetWorldShiftAppearance();
    }

    private void ResetWorldShiftAppearance()
    {
        if (worldShiftCoroutine != null)
        {
            StopCoroutine(worldShiftCoroutine);
            worldShiftCoroutine = null;
        }

        HideWorldShiftImmediate();
    }

    private void HideWorldShiftImmediate()
    {
        if (worldShiftText != null)
        {
            worldShiftText.rectTransform.localScale = originalWorldShiftScale;
            worldShiftText.gameObject.SetActive(false);
        }

        if (worldShiftCanvasGroup != null)
        {
            worldShiftCanvasGroup.alpha = 0f;
            worldShiftCanvasGroup.gameObject.SetActive(false);
        }
    }

    #endregion

    #region Safe Manager Subscriptions

    private void SubscribeToManagers()
    {
        TrySubscribeTimerManager();
        TrySubscribeGameManager();
        TrySubscribeWorldManager();
    }

    private void UnsubscribeFromManagers()
    {
        TryUnsubscribeTimerManager();
        TryUnsubscribeGameManager();
        TryUnsubscribeWorldManager();
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

    /// <summary>
    /// Safely locates WorldManager or GameManager and subscribes to world shift / world change events.
    /// Does not throw NullReferenceException if managers are temporarily unavailable.
    /// </summary>
    private void TrySubscribeWorldManager()
    {
        if (subscribedWorldEvent != null) return;

        Type[] candidateTypes = {
            FindType("WorldManager"),
            FindType("DangerousArena.Managers.WorldManager"),
            FindType("DangerousArena.WorldManager"),
            FindType("ArenaManager"),
            FindType("DangerousArena.Managers.ArenaManager"),
            FindType("DangerousArena.ArenaManager"),
            FindType("LevelManager"),
            FindType("DangerousArena.Managers.LevelManager"),
            FindType("DangerousArena.LevelManager"),
            FindType("GameManager"),
            FindType("DangerousArena.Managers.GameManager"),
            FindType("DangerousArena.GameManager")
        };

        foreach (Type targetType in candidateTypes)
        {
            if (targetType != null)
            {
                object instance = ResolveInstance(targetType);
                if (TrySubscribeWorldEvents(targetType, instance))
                {
                    break;
                }
            }
        }
    }

    private bool TrySubscribeWorldEvents(Type targetType, object targetInstance)
    {
        // Candidate event names for world change / world shift in order of project conventions
        string[] candidateEvents = {
            "OnWorldShift",
            "OnWorldShifted",
            "OnWorldChanged",
            "OnWorldChange",
            "OnArenaShift",
            "OnArenaShifted",
            "OnArenaChanged",
            "OnShift",
            "OnStageShift"
        };

        foreach (string eventName in candidateEvents)
        {
            EventInfo ev = targetType.GetEvent(eventName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (ev != null)
            {
                MethodInfo invokeMethod = ev.EventHandlerType.GetMethod("Invoke");
                if (invokeMethod != null)
                {
                    ParameterInfo[] parameters = invokeMethod.GetParameters();
                    MethodInfo targetHandler = null;

                    if (parameters.Length == 0)
                    {
                        targetHandler = GetType().GetMethod("HandleWorldShiftVoid", BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                    else if (parameters.Length == 1)
                    {
                        if (parameters[0].ParameterType == typeof(int))
                        {
                            targetHandler = GetType().GetMethod("HandleWorldShiftInt", BindingFlags.NonPublic | BindingFlags.Instance);
                        }
                        else if (parameters[0].ParameterType == typeof(float))
                        {
                            targetHandler = GetType().GetMethod("HandleWorldShiftFloat", BindingFlags.NonPublic | BindingFlags.Instance);
                        }
                        else if (parameters[0].ParameterType == typeof(string))
                        {
                            targetHandler = GetType().GetMethod("HandleWorldShiftString", BindingFlags.NonPublic | BindingFlags.Instance);
                        }
                        else if (parameters[0].ParameterType == typeof(bool))
                        {
                            targetHandler = GetType().GetMethod("HandleWorldShiftBool", BindingFlags.NonPublic | BindingFlags.Instance);
                        }
                    }

                    if (targetHandler != null)
                    {
                        try
                        {
                            Delegate del = Delegate.CreateDelegate(ev.EventHandlerType, this, targetHandler);
                            bool isStatic = ev.GetAddMethod(true).IsStatic;
                            object target = isStatic ? null : targetInstance;

                            ev.AddEventHandler(target, del);
                            subscribedWorldEvent = ev;
                            subscribedWorldTarget = target;
                            subscribedWorldDelegate = del;
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[HUDController] Failed to bind world event " + eventName + ": " + ex.Message);
                        }
                    }
                }
            }
        }

        return false;
    }

    private void TryUnsubscribeWorldManager()
    {
        if (subscribedWorldEvent != null && subscribedWorldDelegate != null)
        {
            try
            {
                subscribedWorldEvent.RemoveEventHandler(subscribedWorldTarget, subscribedWorldDelegate);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[HUDController] Error unsubscribing from WorldManager: " + ex.Message);
            }
            finally
            {
                subscribedWorldEvent = null;
                subscribedWorldTarget = null;
                subscribedWorldDelegate = null;
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

    private void HandleWorldShiftVoid()
    {
        TriggerWorldShift();
    }

    private void HandleWorldShiftInt(int _)
    {
        TriggerWorldShift();
    }

    private void HandleWorldShiftFloat(float _)
    {
        TriggerWorldShift();
    }

    private void HandleWorldShiftString(string _)
    {
        TriggerWorldShift();
    }

    private void HandleWorldShiftBool(bool _)
    {
        TriggerWorldShift();
    }

    #endregion

    #region Helpers

    private void EnsureTextReferences()
    {
        if (timeText != null && levelText != null && worldShiftText != null) return;

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
            else if (worldShiftText == null && (nameLower.Contains("shift") || nameLower.Contains("world")))
            {
                worldShiftText = textComp;
            }
        }

        if (worldShiftCanvasGroup == null && worldShiftText != null)
        {
            worldShiftCanvasGroup = worldShiftText.GetComponent<CanvasGroup>();
        }

        if (worldShiftText != null)
        {
            originalWorldShiftScale = worldShiftText.rectTransform.localScale;
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
