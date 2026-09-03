using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the Victory panel in Dangerous Arena.
/// Displays 'YOU WIN!' and provides NEXT LEVEL and MAIN MENU buttons.
/// Subscribes strictly to GameManager victory events.
/// Connects NEXT LEVEL and MAIN MENU button clicks directly to existing GameManager methods.
/// Safely handles situations where GameManager is temporarily unavailable without throwing NullReferenceExceptions.
/// </summary>
public class VictoryUI : MonoBehaviour
{
    [Header("Panel References")]
    [Tooltip("The root Victory panel GameObject to show/hide.")]
    [SerializeField] private GameObject victoryPanel;

    [Tooltip("Optional CanvasGroup on the Victory panel for alpha/interactivity control.")]
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("UI Text References")]
    [Tooltip("Header text component displaying 'YOU WIN!'.")]
    [SerializeField] private TMP_Text victoryTitleText;

    [Header("UI Buttons")]
    [Tooltip("Button that triggers progression to the next level.")]
    [SerializeField] private Button nextLevelButton;

    [Tooltip("Button that triggers navigation back to the main menu.")]
    [SerializeField] private Button mainMenuButton;

    [Header("Visual Feedback Settings")]
    [Tooltip("Duration in seconds for the Victory panel to smoothly fade in.")]
    [SerializeField] private float fadeInDuration = 0.4f;

    [Tooltip("Initial bounce scale multiplier applied to the title text when Victory appears.")]
    [SerializeField] private float titleBounceScale = 1.25f;

    // Visual feedback tracking
    private Coroutine appearanceCoroutine;
    private Vector3 originalTitleScale = Vector3.one;

    // Active GameManager event subscriptions for clean unsubscription
    private EventInfo subscribedVictoryEvent;
    private object subscribedGMTarget;
    private Delegate subscribedGMDelegate;

    // Cached GameManager methods
    private MethodInfo cachedNextLevelMethod;
    private MethodInfo cachedMainMenuMethod;

    #region Unity Lifecycle

    private void Awake()
    {
        EnsureReferences();
        SetupButtonListeners();

        // Panel must initially be hidden
        HideVictoryImmediate();
    }

    private void OnEnable()
    {
        SubscribeToGameManager();
    }

    private void Start()
    {
        // If GameManager initialized after OnEnable, retry discovery
        if (subscribedVictoryEvent == null)
        {
            SubscribeToGameManager();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromGameManager();
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
        UnsubscribeFromGameManager();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Displays the Victory panel.
    /// Driven strictly by GameManager victory events.
    /// </summary>
    [ContextMenu("Test Show Victory")]
    public void ShowVictory()
    {
        if (victoryPanel == null)
        {
            EnsureReferences();
            if (victoryPanel == null) return;
        }

        victoryPanel.SetActive(true);

        if (victoryTitleText != null)
        {
            victoryTitleText.text = "YOU WIN!";
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayVictory();
        }

        if (appearanceCoroutine != null)
        {
            StopCoroutine(appearanceCoroutine);
        }

        if (gameObject.activeInHierarchy)
        {
            appearanceCoroutine = StartCoroutine(AppearanceRoutine());
        }
        else
        {
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 1f;
                panelCanvasGroup.interactable = true;
                panelCanvasGroup.blocksRaycasts = true;
            }
        }
    }

    private System.Collections.IEnumerator AppearanceRoutine()
    {
        float elapsed = 0f;
        Vector3 startScale = originalTitleScale * 0.7f;
        Vector3 peakScale = originalTitleScale * titleBounceScale;

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = true;
        }

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeInDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = smoothT;
            }

            if (victoryTitleText != null)
            {
                // Zoom from 0.7 to 1.25 in first half, settle to 1.0 in second half
                if (t < 0.6f)
                {
                    float phase1 = t / 0.6f;
                    victoryTitleText.rectTransform.localScale = Vector3.Lerp(startScale, peakScale, Mathf.Sin(phase1 * Mathf.PI * 0.5f));
                }
                else
                {
                    float phase2 = (t - 0.6f) / 0.4f;
                    victoryTitleText.rectTransform.localScale = Vector3.Lerp(peakScale, originalTitleScale, Mathf.Sin(phase2 * Mathf.PI * 0.5f));
                }
            }

            yield return null;
        }

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        if (victoryTitleText != null)
        {
            victoryTitleText.rectTransform.localScale = originalTitleScale;
        }

        appearanceCoroutine = null;
    }

    /// <summary>
    /// Hides the Victory panel.
    /// </summary>
    [ContextMenu("Test Hide Victory")]
    public void HideVictory()
    {
        HideVictoryImmediate();
    }

    /// <summary>
    /// Returns true if the Victory panel is currently visible.
    /// </summary>
    public bool IsVictoryVisible
    {
        get
        {
            if (victoryPanel == null) return false;
            if (panelCanvasGroup != null)
            {
                return victoryPanel.activeSelf && panelCanvasGroup.alpha > 0f;
            }
            return victoryPanel.activeSelf;
        }
    }

    /// <summary>
    /// Explicit runtime binding method if GameManager is spawned or registered dynamically.
    /// </summary>
    /// <param name="gameManagerInstance">The instance of GameManager.</param>
    public void BindGameManager(object gameManagerInstance)
    {
        if (gameManagerInstance != null && subscribedVictoryEvent == null)
        {
            TrySubscribeGameManagerEvents(gameManagerInstance.GetType(), gameManagerInstance);
        }
    }

    #endregion

    #region Button Click Handlers

    private void SetupButtonListeners()
    {
        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.AddListener(OnNextLevelButtonClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(OnMainMenuButtonClicked);
        }
    }

    private void RemoveButtonListeners()
    {
        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.RemoveListener(OnNextLevelButtonClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(OnMainMenuButtonClicked);
        }
    }

    /// <summary>
    /// Invoked when NEXT LEVEL button is clicked.
    /// Forwards execution to the existing GameManager next level progression method.
    /// Does not invent duplicate scene progression logic.
    /// </summary>
    private void OnNextLevelButtonClicked()
    {
        if (cachedNextLevelMethod != null)
        {
            bool isStatic = cachedNextLevelMethod.IsStatic;
            object target = isStatic ? null : subscribedGMTarget;
            cachedNextLevelMethod.Invoke(target, null);
            return;
        }

        // Fallback discovery if GameManager method was not cached earlier
        MethodInfo nextMethod = ResolveGameManagerMethod(new[] { "NextLevel", "LoadNextLevel", "ProgressToNextLevel", "GoToNextLevel", "AdvanceLevel" }, out object targetInstance);
        if (nextMethod != null)
        {
            cachedNextLevelMethod = nextMethod;
            bool isStatic = nextMethod.IsStatic;
            object target = isStatic ? null : targetInstance;
            nextMethod.Invoke(target, null);
        }
        else
        {
            Debug.LogWarning("[VictoryUI] NEXT LEVEL button clicked, but no level progression method (NextLevel/LoadNextLevel/ProgressToNextLevel) was found on GameManager.");
        }
    }

    /// <summary>
    /// Invoked when MAIN MENU button is clicked.
    /// Forwards execution to the existing GameManager main menu method.
    /// Does not invent duplicate scene navigation logic.
    /// </summary>
    private void OnMainMenuButtonClicked()
    {
        if (cachedMainMenuMethod != null)
        {
            bool isStatic = cachedMainMenuMethod.IsStatic;
            object target = isStatic ? null : subscribedGMTarget;
            cachedMainMenuMethod.Invoke(target, null);
            return;
        }

        // Fallback discovery if GameManager method was not cached earlier
        MethodInfo menuMethod = ResolveGameManagerMethod(new[] { "LoadMainMenu", "ReturnToMainMenu", "GoToMainMenu", "QuitToMainMenu", "MainMenu" }, out object targetInstance);
        if (menuMethod != null)
        {
            cachedMainMenuMethod = menuMethod;
            bool isStatic = menuMethod.IsStatic;
            object target = isStatic ? null : targetInstance;
            menuMethod.Invoke(target, null);
        }
        else
        {
            Debug.LogWarning("[VictoryUI] MAIN MENU button clicked, but no return method (LoadMainMenu/ReturnToMainMenu/GoToMainMenu) was found on GameManager.");
        }
    }

    #endregion

    #region Safe GameManager Subscriptions & Method Resolution

    private void SubscribeToGameManager()
    {
        if (subscribedVictoryEvent != null) return;

        Type[] candidateTypes = {
            FindType("GameManager"),
            FindType("DangerousArena.Managers.GameManager"),
            FindType("DangerousArena.GameManager")
        };

        foreach (Type targetType in candidateTypes)
        {
            if (targetType != null)
            {
                object instance = ResolveInstance(targetType);
                if (TrySubscribeGameManagerEvents(targetType, instance))
                {
                    break;
                }
            }
        }
    }

    private bool TrySubscribeGameManagerEvents(Type gmType, object gmInstance)
    {
        // Cache NEXT LEVEL and MAIN MENU methods from this type
        CacheManagerMethods(gmType, gmInstance);

        // Candidate event names for victory in order of project conventions
        string[] candidateEvents = {
            "OnVictory",
            "OnGameWon",
            "OnLevelCompleted",
            "OnLevelComplete",
            "OnStageCleared",
            "OnWin"
        };

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

                    if (parameters.Length == 0)
                    {
                        targetHandler = GetType().GetMethod("HandleVictoryVoid", BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                    else if (parameters.Length == 1)
                    {
                        if (parameters[0].ParameterType == typeof(int))
                            targetHandler = GetType().GetMethod("HandleVictoryInt", BindingFlags.NonPublic | BindingFlags.Instance);
                        else if (parameters[0].ParameterType == typeof(float))
                            targetHandler = GetType().GetMethod("HandleVictoryFloat", BindingFlags.NonPublic | BindingFlags.Instance);
                        else if (parameters[0].ParameterType == typeof(string))
                            targetHandler = GetType().GetMethod("HandleVictoryString", BindingFlags.NonPublic | BindingFlags.Instance);
                        else if (parameters[0].ParameterType == typeof(bool))
                            targetHandler = GetType().GetMethod("HandleVictoryBool", BindingFlags.NonPublic | BindingFlags.Instance);
                    }

                    if (targetHandler != null)
                    {
                        try
                        {
                            Delegate del = Delegate.CreateDelegate(ev.EventHandlerType, this, targetHandler);
                            bool isStatic = ev.GetAddMethod(true).IsStatic;
                            object target = isStatic ? null : gmInstance;

                            ev.AddEventHandler(target, del);
                            subscribedVictoryEvent = ev;
                            subscribedGMTarget = target;
                            subscribedGMDelegate = del;
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[VictoryUI] Failed to bind event " + eventName + ": " + ex.Message);
                        }
                    }
                }
            }
        }

        return false;
    }

    private void CacheManagerMethods(Type gmType, object gmInstance)
    {
        string[] nextCandidates = { "NextLevel", "LoadNextLevel", "ProgressToNextLevel", "GoToNextLevel", "AdvanceLevel" };
        foreach (string name in nextCandidates)
        {
            MethodInfo m = gmType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (m != null)
            {
                cachedNextLevelMethod = m;
                subscribedGMTarget = m.IsStatic ? null : gmInstance;
                break;
            }
        }

        string[] menuCandidates = { "LoadMainMenu", "ReturnToMainMenu", "GoToMainMenu", "QuitToMainMenu", "MainMenu" };
        foreach (string name in menuCandidates)
        {
            MethodInfo m = gmType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (m != null)
            {
                cachedMainMenuMethod = m;
                subscribedGMTarget = m.IsStatic ? null : gmInstance;
                break;
            }
        }
    }

    private MethodInfo ResolveGameManagerMethod(string[] methodNames, out object targetInstance)
    {
        targetInstance = null;
        Type[] candidateTypes = {
            FindType("GameManager"),
            FindType("DangerousArena.Managers.GameManager"),
            FindType("DangerousArena.GameManager")
        };

        foreach (Type t in candidateTypes)
        {
            if (t != null)
            {
                object inst = ResolveInstance(t);
                foreach (string name in methodNames)
                {
                    MethodInfo m = t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, Type.EmptyTypes, null);
                    if (m != null)
                    {
                        targetInstance = m.IsStatic ? null : inst;
                        return m;
                    }
                }
            }
        }

        return null;
    }

    private void UnsubscribeFromGameManager()
    {
        if (subscribedVictoryEvent != null && subscribedGMDelegate != null)
        {
            try
            {
                subscribedVictoryEvent.RemoveEventHandler(subscribedGMTarget, subscribedGMDelegate);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[VictoryUI] Error unsubscribing from GameManager: " + ex.Message);
            }
            finally
            {
                subscribedVictoryEvent = null;
                subscribedGMTarget = null;
                subscribedGMDelegate = null;
                cachedNextLevelMethod = null;
                cachedMainMenuMethod = null;
            }
        }
    }

    #endregion

    #region Event Handlers

    private void HandleVictoryVoid()
    {
        ShowVictory();
    }

    private void HandleVictoryInt(int _)
    {
        ShowVictory();
    }

    private void HandleVictoryFloat(float _)
    {
        ShowVictory();
    }

    private void HandleVictoryString(string _)
    {
        ShowVictory();
    }

    private void HandleVictoryBool(bool _)
    {
        ShowVictory();
    }

    #endregion

    #region Helpers

    private void HideVictoryImmediate()
    {
        if (appearanceCoroutine != null)
        {
            StopCoroutine(appearanceCoroutine);
            appearanceCoroutine = null;
        }

        if (victoryTitleText != null)
        {
            victoryTitleText.rectTransform.localScale = originalTitleScale;
        }

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
    }

    private void EnsureReferences()
    {
        if (victoryPanel == null)
        {
            Transform panelTrans = transform.Find("VictoryPanel") ?? transform.Find("WinPanel") ?? transform.Find("Panel");
            if (panelTrans != null)
            {
                victoryPanel = panelTrans.gameObject;
            }
            else
            {
                victoryPanel = gameObject;
            }
        }

        if (panelCanvasGroup == null && victoryPanel != null)
        {
            panelCanvasGroup = victoryPanel.GetComponent<CanvasGroup>();
        }

        if (nextLevelButton == null && victoryPanel != null)
        {
            Button[] buttons = victoryPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                string btnName = btn.gameObject.name.ToLowerInvariant();
                if (btnName.Contains("next") || btnName.Contains("level") || btnName.Contains("continue") || btnName.Contains("advance"))
                {
                    nextLevelButton = btn;
                    break;
                }
            }
        }

        if (mainMenuButton == null && victoryPanel != null)
        {
            Button[] buttons = victoryPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                string btnName = btn.gameObject.name.ToLowerInvariant();
                if (btnName.Contains("menu") || btnName.Contains("main"))
                {
                    mainMenuButton = btn;
                    break;
                }
            }
        }

        if (victoryTitleText == null && victoryPanel != null)
        {
            TMP_Text[] texts = victoryPanel.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in texts)
            {
                string tName = t.gameObject.name.ToLowerInvariant();
                if (tName.Contains("title") || tName.Contains("victory") || tName.Contains("win") || t.text.ToLowerInvariant().Contains("you win"))
                {
                    victoryTitleText = t;
                    break;
                }
            }
        }

        if (victoryTitleText != null)
        {
            originalTitleScale = victoryTitleText.rectTransform.localScale;
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
        PropertyInfo instanceProp = targetType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        if (instanceProp != null)
        {
            object inst = instanceProp.GetValue(null);
            if (inst != null) return inst;
        }

        if (typeof(Component).IsAssignableFrom(targetType))
        {
            return UnityEngine.Object.FindAnyObjectByType(targetType);
        }

        return null;
    }

    #endregion
}
