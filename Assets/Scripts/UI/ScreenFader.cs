using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides simple screen fade transitions (Fade In / Fade Out) using a CanvasGroup.
/// Ideal for level transitions, deaths, or scene starts.
/// </summary>
[DisallowMultipleComponent]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Header("Fade Components")]
    [Tooltip("The CanvasGroup controlling opacity of the fade overlay.")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Tooltip("The full-screen image providing the fade color (default black).")]
    [SerializeField] private Image fadeImage;

    [Header("Settings")]
    [Tooltip("If true, the screen automatically fades in (from black to clear) on scene start.")]
    [SerializeField] private bool fadeInOnStart = true;

    [Tooltip("Default duration in seconds for fade transitions.")]
    [SerializeField] private float defaultFadeDuration = 0.75f;

    [Tooltip("Color of the fade overlay.")]
    [SerializeField] private Color fadeColor = Color.black;

    private Coroutine currentFadeCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        EnsureReferences();

        if (fadeInOnStart && fadeCanvasGroup != null)
        {
            // Start fully opaque before fading in
            fadeCanvasGroup.alpha = 1f;
            fadeCanvasGroup.blocksRaycasts = true;
        }
    }

    private void Start()
    {
        if (fadeInOnStart)
        {
            FadeIn(defaultFadeDuration);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #region Public API

    /// <summary>
    /// Fades the screen from current opacity to completely transparent (0 alpha).
    /// </summary>
    [ContextMenu("Test Fade In")]
    public void FadeIn(float duration = -1f, Action onComplete = null)
    {
        float dur = duration > 0f ? duration : defaultFadeDuration;
        StartFade(0f, dur, onComplete);
    }

    /// <summary>
    /// Fades the screen from current opacity to fully opaque (1 alpha).
    /// </summary>
    [ContextMenu("Test Fade Out")]
    public void FadeOut(float duration = -1f, Action onComplete = null)
    {
        float dur = duration > 0f ? duration : defaultFadeDuration;
        StartFade(1f, dur, onComplete);
    }

    /// <summary>
    /// Smoothly transitions screen opacity to target alpha value.
    /// </summary>
    public void StartFade(float targetAlpha, float duration, Action onComplete = null)
    {
        EnsureReferences();
        if (fadeCanvasGroup == null) return;

        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }

        currentFadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, duration, onComplete));
    }

    #endregion

    private IEnumerator FadeRoutine(float targetAlpha, float duration, Action onComplete)
    {
        if (fadeCanvasGroup == null) yield break;

        // Block raycasts while opaque or during fade
        fadeCanvasGroup.blocksRaycasts = targetAlpha > 0.05f;

        float startAlpha = fadeCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Smooth sine curve
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
        fadeCanvasGroup.blocksRaycasts = targetAlpha > 0.05f;
        currentFadeCoroutine = null;

        onComplete?.Invoke();
    }

    private void EnsureReferences()
    {
        if (fadeCanvasGroup == null)
        {
            fadeCanvasGroup = GetComponent<CanvasGroup>();
            if (fadeCanvasGroup == null)
            {
                fadeCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (fadeImage == null)
        {
            fadeImage = GetComponent<Image>();
            if (fadeImage == null)
            {
                fadeImage = GetComponentInChildren<Image>(true);
            }

            if (fadeImage != null)
            {
                fadeImage.color = fadeColor;
            }
        }
    }
}
