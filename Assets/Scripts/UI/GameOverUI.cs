using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the Game Over panel in Dangerous Arena.
/// Displays GAME OVER text and provides RETRY and MAIN MENU buttons.
/// Subscribes strictly to GameManager game-over events without death-polling.
/// Connects RETRY and MAIN MENU button clicks directly to existing GameManager methods.
/// Safely handles situations where GameManager is temporarily unavailable without throwing NullReferenceExceptions.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("Panel References")]
    [Tooltip("The root Game Over panel GameObject to show/hide.")]
    [SerializeField] private GameObject gameOverPanel;

    [Tooltip("Optional CanvasGroup on the Game Over panel for alpha/interactivity control.")]
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("UI Text References")]
    [Tooltip("Header text component displaying 'GAME OVER'.")]
    [SerializeField] private TMP_Text gameOverTitleText;

    [Header("UI Buttons")]
    [Tooltip("Button that triggers game/level retry.")]
    [SerializeField] private Button retryButton;

    [Tooltip("Button that triggers navigation back to the main menu.")]
    [SerializeField] private Button mainMenuButton;

    [Header("Visual Feedback Settings")]
    [Tooltip("Duration in seconds for the Game Over panel to smoothly fade in.")]
    [SerializeField] private float fadeInDuration = 0.35f;

    [Tooltip("Initial scale punch applied to the title text when Game Over appears.")]
    [SerializeField] private float titlePunchScale = 1.15f;

    // Visual feedback tracking
    private Coroutine appearanceCoroutine;
    private Vector3 originalTitleScale = Vector3.one;

    // Active GameManager event subscriptions for clean unsubscription
    private EventInfo subscribedGameOverEvent;
    private object subscribedGMTarget;
    private Delegate subscribedGMDelegate;

    // Cached GameManager methods
    private MethodInfo cachedRetryMethod;
    private MethodInfo cachedMainMenuMethod;

    #region Unity Lifecycle

    private void Awake()
    {
        EnsureReferences();
        SetupButtonListeners();

        // Panel must initially be hidden
        HideGameOverImmediate();
    }

    private void OnEnable()
    {
        SubscribeToGameManager();
    }

    private void Start()
    {
        // If GameManager initialized after OnEnable, retry discovery
        if (subscribedGameOverEvent == null)
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
    /// Displays the Game Over panel.
    /// Driven strictly by GameManager game-over events.
    /// </summary>
    [ContextMenu("Test Show Game Over")]
    public void ShowGameOver()
    {
        if (gameOverPanel == null)
        {
            EnsureReferences();
            if (gameOverPanel == null) return;
        }

        gameOverPanel.SetActive(true);

        if (gameOverTitleText != null)
        {
            gameOverTitleText.text = "GAME OVER";
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPlayerDeath();
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
        Vector3 punchScale = originalTitleScale * titlePunchScale;

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

            if (gameOverTitleText != null)
            {
                gameOverTitleText.rectTransform.localScale = Vector3.Lerp(punchScale, originalTitleScale, smoothT);
            }

            yield return null;
        }

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        if (gameOverTitleText != null)
        {
            gameOverTitleText.rectTransform.localScale = originalTitleScale;
        }

        appearanceCoroutine = null;
    }

    /// <summary>
    /// Hides the Game Over panel.
    /// </summary>
    [ContextMenu("Test Hide Game Over")]
    public void HideGameOver()
    {
        HideGameOverImmediate();
    }

    /// <summary>
    /// Returns true if the Game Over panel is currently visible.
    /// </summary>
    public bool IsGameOverVisible
    {
        get
        {
            if (gameOverPanel == null) return false;
            if (panelCanvasGroup != null)
            {
                return gameOverPanel.activeSelf && panelCanvasGroup.alpha > 0f;
            }
            return gameOverPanel.activeSelf;
        }
    }

    /// <summary>
    /// Explicit runtime binding method if GameManager is spawned or registered dynamically.
    /// </summary>
    /// <param name="gameManagerInstance">The instance of GameManager.</param>
    public void BindGameManager(object gameManagerInstance)
    {
        if (gameManagerInstance != null && subscribedGameOverEvent == null)
        {
            TrySubscribeGameManagerEvents(gameManagerInstance.GetType(), gameManagerInstance);
        }
    }

    #endregion

    #region Button Click Handlers

    private void SetupButtonListeners()
    {
        if (retryButton != null)
        {
            retryButton.onClick.AddListener(OnRetryButtonClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(OnMainMenuButtonClicked);
        }
    }

    private void RemoveButtonListeners()
    {
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(OnRetryButtonClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(OnMainMenuButtonClicked);
        }
    }

    /// <summary>
    /// Invoked when RETRY button is clicked.
    /// Forwards execution to the existing GameManager restart/retry method.
    /// Does not invent duplicate restart logic.
    /// </summary>
    private void OnRetryButtonClicked()
    {
        if (cachedRetryMethod != null)
        {
            bool isStatic = cachedRetryMethod.IsStatic;
            object target = isStatic ? null : subscribedGMTarget;
            cachedRetryMethod.Invoke(target, null);
            return;
        }

        // Fallback discovery if GameManager method was not cached earlier
        MethodInfo retryMethod = ResolveGameManagerMethod(new[] { "RestartGame", "RestartLevel", "Retry", "Restart", "ReloadCurrentScene" }, out object targetInstance);
        if (retryMethod != null)
        {
            cachedRetryMethod = retryMethod;
            bool isStatic = retryMethod.IsStatic;
            object target = isStatic ? null : targetInstance;
            retryMethod.Invoke(target, null);
        }
        else
        {
            Debug.LogWarning("[GameOverUI] RETRY button clicked, but no restart method (RestartGame/RestartLevel/Retry) was found on GameManager.");
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
            Debug.LogWarning("[GameOverUI] MAIN MENU button clicked, but no return method (LoadMainMenu/ReturnToMainMenu/GoToMainMenu) was found on GameManager.");
        }
    }

    #endregion

    #region Safe GameManager Subscriptions & Method Resolution

    private void SubscribeToGameManager()
    {
        if (subscribedGameOverEvent != null) return;

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
        // Cache RETRY and MAIN MENU methods from this type
        CacheManagerMethods(gmType, gmInstance);

        // Candidate event names for game over in order of project conventions
        string[] candidateEvents = {
            "OnGameOver",
            "OnGameEnded",
            "OnPlayerDied",
            "OnDefeat",
            "OnPlayerDeath"
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
                        targetHandler = GetType().GetMethod("HandleGameOverVoid", BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                    else if (parameters.Length == 1)
                    {
                        if (parameters[0].ParameterType == typeof(int))
                            targetHandler = GetType().GetMethod("HandleGameOverInt", BindingFlags.NonPublic | BindingFlags.Instance);
                        else if (parameters[0].ParameterType == typeof(float))
                            targetHandler = GetType().GetMethod("HandleGameOverFloat", BindingFlags.NonPublic | BindingFlags.Instance);
                        else if (parameters[0].ParameterType == typeof(string))
                            targetHandler = GetType().GetMethod("HandleGameOverString", BindingFlags.NonPublic | BindingFlags.Instance);
                        else if (parameters[0].ParameterType == typeof(bool))
                            targetHandler = GetType().GetMethod("HandleGameOverBool", BindingFlags.NonPublic | BindingFlags.Instance);
                    }

                    if (targetHandler != null)
                    {
                        try
                        {
                            Delegate del = Delegate.CreateDelegate(ev.EventHandlerType, this, targetHandler);
                            bool isStatic = ev.GetAddMethod(true).IsStatic;
                            object target = isStatic ? null : gmInstance;

                            ev.AddEventHandler(target, del);
                            subscribedGameOverEvent = ev;
                            subscribedGMTarget = target;
                            subscribedGMDelegate = del;
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[GameOverUI] Failed to bind event " + eventName + ": " + ex.Message);
                        }
                    }
                }
            }
        }

        return false;
    }

    private void CacheManagerMethods(Type gmType, object gmInstance)
    {
        string[] retryCandidates = { "RestartGame", "RestartLevel", "Retry", "Restart", "ReloadCurrentScene" };
        foreach (string name in retryCandidates)
        {
            MethodInfo m = gmType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (m != null)
            {
                cachedRetryMethod = m;
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
        if (subscribedGameOverEvent != null && subscribedGMDelegate != null)
        {
            try
            {
                subscribedGameOverEvent.RemoveEventHandler(subscribedGMTarget, subscribedGMDelegate);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameOverUI] Error unsubscribing from GameManager: " + ex.Message);
            }
            finally
            {
                subscribedGameOverEvent = null;
                subscribedGMTarget = null;
                subscribedGMDelegate = null;
                cachedRetryMethod = null;
                cachedMainMenuMethod = null;
            }
        }
    }

    #endregion

    #region Event Handlers

    private void HandleGameOverVoid()
    {
        ShowGameOver();
    }

    private void HandleGameOverInt(int _)
    {
        ShowGameOver();
    }

    private void HandleGameOverFloat(float _)
    {
        ShowGameOver();
    }

    private void HandleGameOverString(string _)
    {
        ShowGameOver();
    }

    private void HandleGameOverBool(bool _)
    {
        ShowGameOver();
    }

    #endregion

    #region Helpers

    private void HideGameOverImmediate()
    {
        if (appearanceCoroutine != null)
        {
            StopCoroutine(appearanceCoroutine);
            appearanceCoroutine = null;
        }

        if (gameOverTitleText != null)
        {
            gameOverTitleText.rectTransform.localScale = originalTitleScale;
        }

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    private void EnsureReferences()
    {
        if (gameOverPanel == null)
        {
            Transform panelTrans = transform.Find("GameOverPanel") ?? transform.Find("Panel");
            if (panelTrans != null)
            {
                gameOverPanel = panelTrans.gameObject;
            }
            else
            {
                gameOverPanel = gameObject;
            }
        }

        if (panelCanvasGroup == null && gameOverPanel != null)
        {
            panelCanvasGroup = gameOverPanel.GetComponent<CanvasGroup>();
        }

        if (retryButton == null && gameOverPanel != null)
        {
            Button[] buttons = gameOverPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                string btnName = btn.gameObject.name.ToLowerInvariant();
                if (btnName.Contains("retry") || btnName.Contains("restart"))
                {
                    retryButton = btn;
                    break;
                }
            }
        }

        if (mainMenuButton == null && gameOverPanel != null)
        {
            Button[] buttons = gameOverPanel.GetComponentsInChildren<Button>(true);
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

        if (gameOverTitleText == null && gameOverPanel != null)
        {
            TMP_Text[] texts = gameOverPanel.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in texts)
            {
                string tName = t.gameObject.name.ToLowerInvariant();
                if (tName.Contains("title") || tName.Contains("gameover") || t.text.ToLowerInvariant().Contains("game over"))
                {
                    gameOverTitleText = t;
                    break;
                }
            }
        }

        if (gameOverTitleText != null)
        {
            originalTitleScale = gameOverTitleText.rectTransform.localScale;
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
