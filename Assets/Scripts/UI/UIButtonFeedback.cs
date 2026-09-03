using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Provides smooth game-jam-friendly visual and audio feedback for UI buttons.
/// Handles hover (scale up), press (scale down), and click feedback.
/// Safely plays audio feedback if an AudioSource, AudioClip, or AudioManager exists.
/// </summary>
[DisallowMultipleComponent]
public class UIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Scale Feedback")]
    [Tooltip("Target local scale when the cursor hovers over the button.")]
    [SerializeField] private Vector3 hoverScale = new Vector3(1.06f, 1.06f, 1f);

    [Tooltip("Target local scale when the button is actively pressed down.")]
    [SerializeField] private Vector3 pressedScale = new Vector3(0.94f, 0.94f, 1f);

    [Tooltip("Duration in seconds for the smooth scale transition.")]
    [SerializeField] private float transitionDuration = 0.1f;

    [Tooltip("Whether to animate using unscaled delta time (so feedback works while paused).")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Audio Feedback (Optional)")]
#pragma warning disable 0649
    [Tooltip("Audio clip played when the cursor hovers over the button.")]
    [SerializeField] private AudioClip hoverSound;

    [Tooltip("Audio clip played when the button is clicked.")]
    [SerializeField] private AudioClip clickSound;

    [Tooltip("Audio source used for playback. If unassigned, one will be created or discovered.")]
    [SerializeField] private AudioSource audioSource;
#pragma warning restore 0649

    private Selectable selectable;
    private Vector3 originalScale = Vector3.one;
    private Coroutine scaleCoroutine;
    private bool isHovered = false;
    private bool isPressed = false;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        originalScale = transform.localScale;

        if (audioSource == null && (hoverSound != null || clickSound != null))
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D sound
            }
        }
    }

    private void OnDisable()
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }

        transform.localScale = originalScale;
        isHovered = false;
        isPressed = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable()) return;

        isHovered = true;
        if (!isPressed)
        {
            AnimateScale(hoverScale);
            PlayClip(hoverSound);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (!isPressed)
        {
            AnimateScale(originalScale);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsInteractable()) return;

        isPressed = true;
        AnimateScale(pressedScale);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        if (IsInteractable())
        {
            AnimateScale(isHovered ? hoverScale : originalScale);
        }
        else
        {
            AnimateScale(originalScale);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsInteractable()) return;

        PlayClickFeedback();
    }

    private bool IsInteractable()
    {
        if (selectable == null)
        {
            selectable = GetComponent<Selectable>();
        }

        return selectable == null || selectable.interactable;
    }

    private void AnimateScale(Vector3 targetScale)
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }

        if (gameObject.activeInHierarchy)
        {
            scaleCoroutine = StartCoroutine(ScaleRoutine(targetScale));
        }
        else
        {
            transform.localScale = targetScale;
        }
    }

    private IEnumerator ScaleRoutine(Vector3 target)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            // Smooth ease out
            float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);
            transform.localScale = Vector3.Lerp(start, target, smoothT);
            yield return null;
        }

        transform.localScale = target;
        scaleCoroutine = null;
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null) return;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        }
    }

    private void PlayClickFeedback()
    {
        if (clickSound != null)
        {
            PlayClip(clickSound);
            return;
        }

        // Safe dynamic check for any AudioManager in the project
        TryPlayAudioManagerClick();
    }

    private void TryPlayAudioManagerClick()
    {
        try
        {
            Type audioMgrType = Type.GetType("AudioManager")
                             ?? Type.GetType("DangerousArena.Managers.AudioManager")
                             ?? Type.GetType("DangerousArena.AudioManager");

            if (audioMgrType != null)
            {
                PropertyInfo instanceProp = audioMgrType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                object instance = instanceProp != null ? instanceProp.GetValue(null) : UnityEngine.Object.FindAnyObjectByType(audioMgrType);

                if (instance != null)
                {
                    MethodInfo playMethod = audioMgrType.GetMethod("PlaySFX", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null)
                                         ?? audioMgrType.GetMethod("PlaySound", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null)
                                         ?? audioMgrType.GetMethod("PlayClick", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

                    if (playMethod != null)
                    {
                        if (playMethod.GetParameters().Length == 1)
                        {
                            playMethod.Invoke(instance, new object[] { "ButtonClick" });
                        }
                        else
                        {
                            playMethod.Invoke(instance, null);
                        }
                    }
                }
            }
        }
        catch
        {
            // Silently ignore if no AudioManager exists
        }
    }
}
