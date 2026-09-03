using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the Main Menu in Dangerous Arena.
/// Displays the title 'DANGEROUS ARENA' and provides PLAY and QUIT buttons.
/// Dispatches PLAY requests directly to the existing GameManager start method.
/// Dispatches QUIT requests safely through GameManager or standard application quit.
/// Safely handles situations where GameManager is temporarily unavailable without throwing NullReferenceExceptions.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Panel References")]
    [Tooltip("The root Main Menu panel GameObject to show/hide.")]
    [SerializeField] private GameObject menuPanel;

    [Tooltip("Optional CanvasGroup on the menu panel for alpha/interactivity control.")]
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("UI Text References")]
    [Tooltip("Title text component displaying 'DANGEROUS ARENA'.")]
    [SerializeField] private TMP_Text titleText;

    [Header("UI Buttons")]
    [Tooltip("Button that triggers game start.")]
    [SerializeField] private Button playButton;

    [Tooltip("Button that quits the application.")]
    [SerializeField] private Button quitButton;

    // Cached GameManager methods and target instance
    private object subscribedGMTarget;
    private MethodInfo cachedPlayMethod;
    private MethodInfo cachedQuitMethod;

    #region Unity Lifecycle

    private void Awake()
    {
        EnsureReferences();
        SetupButtonListeners();
    }

    private void OnEnable()
    {
        SubscribeToGameManager();
    }

    private void Start()
    {
        // If GameManager initialized after OnEnable, retry method discovery
        if (cachedPlayMethod == null)
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
    /// Displays the Main Menu panel.
    /// </summary>
    [ContextMenu("Test Show Menu")]
    public void ShowMenu()
    {
        if (menuPanel == null)
        {
            EnsureReferences();
            if (menuPanel == null) return;
        }

        menuPanel.SetActive(true);

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        if (titleText != null)
        {
            titleText.text = "DANGEROUS ARENA";
        }
    }

    /// <summary>
    /// Hides the Main Menu panel.
    /// </summary>
    [ContextMenu("Test Hide Menu")]
    public void HideMenu()
    {
        HideMenuImmediate();
    }

    /// <summary>
    /// Returns true if the Main Menu panel is currently visible.
    /// </summary>
    public bool IsMenuVisible
    {
        get
        {
            if (menuPanel == null) return false;
            if (panelCanvasGroup != null)
            {
                return menuPanel.activeSelf && panelCanvasGroup.alpha > 0f;
            }
            return menuPanel.activeSelf;
        }
    }

    /// <summary>
    /// Explicit runtime binding method if GameManager is spawned or registered dynamically.
    /// </summary>
    /// <param name="gameManagerInstance">The instance of GameManager.</param>
    public void BindGameManager(object gameManagerInstance)
    {
        if (gameManagerInstance != null)
        {
            CacheManagerMethods(gameManagerInstance.GetType(), gameManagerInstance);
        }
    }

    #endregion

    #region Button Click Handlers

    private void SetupButtonListeners()
    {
        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayButtonClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(OnQuitButtonClicked);
        }
    }

    private void RemoveButtonListeners()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveListener(OnPlayButtonClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitButtonClicked);
        }
    }

    /// <summary>
    /// Invoked when PLAY button is clicked.
    /// Forwards execution to the existing GameManager start method.
    /// Does not invent duplicate game-start logic.
    /// </summary>
    private void OnPlayButtonClicked()
    {
        if (cachedPlayMethod != null)
        {
            bool isStatic = cachedPlayMethod.IsStatic;
            object target = isStatic ? null : subscribedGMTarget;
            cachedPlayMethod.Invoke(target, null);
            HideMenu();
            return;
        }

        // Fallback discovery if GameManager method was not cached earlier
        MethodInfo playMethod = ResolveGameManagerMethod(new[] { "StartGame", "PlayGame", "Play", "LoadFirstLevel", "StartFirstLevel", "RestartGame", "StartMatch" }, out object targetInstance);
        if (playMethod != null)
        {
            cachedPlayMethod = playMethod;
            bool isStatic = playMethod.IsStatic;
            object target = isStatic ? null : targetInstance;
            playMethod.Invoke(target, null);
            HideMenu();
        }
        else
        {
            Debug.LogWarning("[MainMenuUI] PLAY button clicked, but no game start method (StartGame/PlayGame/Play/LoadFirstLevel) was found on GameManager.");
        }
    }

    /// <summary>
    /// Invoked when QUIT button is clicked.
    /// Uses GameManager quit method if available, and safely falls back to standard project-safe application quit.
    /// </summary>
    private void OnQuitButtonClicked()
    {
        if (cachedQuitMethod != null)
        {
            bool isStatic = cachedQuitMethod.IsStatic;
            object target = isStatic ? null : subscribedGMTarget;
            cachedQuitMethod.Invoke(target, null);
            return;
        }

        // Fallback discovery if GameManager quit method was not cached earlier
        MethodInfo quitMethod = ResolveGameManagerMethod(new[] { "QuitGame", "Quit", "ExitGame", "Exit" }, out object targetInstance);
        if (quitMethod != null)
        {
            cachedQuitMethod = quitMethod;
            bool isStatic = quitMethod.IsStatic;
            object target = isStatic ? null : targetInstance;
            quitMethod.Invoke(target, null);
            return;
        }

        // Project-safe quit execution
        PerformSafeQuit();
    }

    /// <summary>
    /// Universal, project-safe quit method compatible with both Unity Editor and Standalone builds.
    /// </summary>
    private void PerformSafeQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Safe GameManager Method Resolution

    private void SubscribeToGameManager()
    {
        if (cachedPlayMethod != null) return;

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
                CacheManagerMethods(targetType, instance);
                if (cachedPlayMethod != null)
                {
                    break;
                }
            }
        }
    }

    private void CacheManagerMethods(Type gmType, object gmInstance)
    {
        string[] playCandidates = { "StartGame", "PlayGame", "Play", "LoadFirstLevel", "StartFirstLevel", "RestartGame", "StartMatch" };
        foreach (string name in playCandidates)
        {
            MethodInfo m = gmType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (m != null)
            {
                cachedPlayMethod = m;
                subscribedGMTarget = m.IsStatic ? null : gmInstance;
                break;
            }
        }

        string[] quitCandidates = { "QuitGame", "Quit", "ExitGame", "Exit" };
        foreach (string name in quitCandidates)
        {
            MethodInfo m = gmType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (m != null)
            {
                cachedQuitMethod = m;
                if (subscribedGMTarget == null && !m.IsStatic)
                {
                    subscribedGMTarget = gmInstance;
                }
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
        subscribedGMTarget = null;
        cachedPlayMethod = null;
        cachedQuitMethod = null;
    }

    #endregion

    #region Helpers

    private void HideMenuImmediate()
    {
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }
    }

    private void EnsureReferences()
    {
        if (menuPanel == null)
        {
            Transform panelTrans = transform.Find("MainMenuPanel") ?? transform.Find("MenuPanel") ?? transform.Find("Panel");
            if (panelTrans != null)
            {
                menuPanel = panelTrans.gameObject;
            }
            else
            {
                menuPanel = gameObject;
            }
        }

        if (panelCanvasGroup == null && menuPanel != null)
        {
            panelCanvasGroup = menuPanel.GetComponent<CanvasGroup>();
        }

        if (playButton == null && menuPanel != null)
        {
            Button[] buttons = menuPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                string btnName = btn.gameObject.name.ToLowerInvariant();
                if (btnName.Contains("play") || btnName.Contains("start"))
                {
                    playButton = btn;
                    break;
                }
            }
        }

        if (quitButton == null && menuPanel != null)
        {
            Button[] buttons = menuPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                string btnName = btn.gameObject.name.ToLowerInvariant();
                if (btnName.Contains("quit") || btnName.Contains("exit"))
                {
                    quitButton = btn;
                    break;
                }
            }
        }

        if (titleText == null && menuPanel != null)
        {
            TMP_Text[] texts = menuPanel.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in texts)
            {
                string tName = t.gameObject.name.ToLowerInvariant();
                if (tName.Contains("title") || tName.Contains("name") || t.text.ToLowerInvariant().Contains("dangerous arena"))
                {
                    titleText = t;
                    break;
                }
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
