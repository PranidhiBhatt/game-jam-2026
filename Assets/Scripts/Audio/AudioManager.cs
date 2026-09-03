using System;
using UnityEngine;

/// <summary>
/// Minimal UI-safe Audio Manager for Dangerous Arena.
/// Manages Background Music and 2D Sound Effects (SFX).
/// Safe for Game Jam use: auto-creates AudioSources if missing and does not throw if audio clips are unassigned.
/// </summary>
[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public enum SoundEffect
    {
        ButtonClick,
        PlayerDeath,
        WorldShift,
        BonusCollected,
        Victory
    }

    [Header("Audio Sources")]
    [Tooltip("AudioSource dedicated to looping music.")]
    [SerializeField] private AudioSource musicSource;

    [Tooltip("AudioSource dedicated to 2D one-shot sound effects.")]
    [SerializeField] private AudioSource sfxSource;

    [Header("Music Tracks")]
#pragma warning disable 0649
    [Tooltip("Main background music track.")]
    [SerializeField] private AudioClip backgroundMusic;

    [Header("Sound Effects (SFX)")]
    [Tooltip("Played on UI button click.")]
    [SerializeField] private AudioClip buttonClickSFX;

    [Tooltip("Played on player death / game over.")]
    [SerializeField] private AudioClip playerDeathSFX;

    [Tooltip("Played on arena world shift.")]
    [SerializeField] private AudioClip worldShiftSFX;

    [Tooltip("Played when a bonus item is collected.")]
    [SerializeField] private AudioClip bonusCollectedSFX;

    [Tooltip("Played on player victory.")]
    [SerializeField] private AudioClip victorySFX;
#pragma warning restore 0649

    [Header("Volume Controls")]
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.75f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    [Header("Settings")]
    [Tooltip("Automatically start background music on Awake if clip is assigned.")]
    [SerializeField] private bool autoPlayMusic = true;

    [Tooltip("Persist this AudioManager across scene loads.")]
    [SerializeField] private bool persistAcrossScenes = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (persistAcrossScenes && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureAudioSources();
        ApplyVolumes();

        if (autoPlayMusic && backgroundMusic != null)
        {
            PlayMusic(backgroundMusic);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #region Music Methods

    /// <summary>
    /// Plays or changes the current background music track.
    /// </summary>
    public void PlayMusic(AudioClip clip = null, bool loop = true)
    {
        EnsureAudioSources();
        if (musicSource == null) return;

        AudioClip track = clip != null ? clip : backgroundMusic;
        if (track == null) return;

        if (musicSource.clip == track && musicSource.isPlaying) return;

        musicSource.clip = track;
        musicSource.loop = loop;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    /// <summary>
    /// Stops the currently playing background music.
    /// </summary>
    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    /// <summary>
    /// Pauses the current background music.
    /// </summary>
    public void PauseMusic()
    {
        if (musicSource != null)
        {
            musicSource.Pause();
        }
    }

    /// <summary>
    /// Resumes the current background music.
    /// </summary>
    public void ResumeMusic()
    {
        if (musicSource != null && !musicSource.isPlaying)
        {
            musicSource.UnPause();
        }
    }

    /// <summary>
    /// Sets music volume (0.0 to 1.0).
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }
    }

    #endregion

    #region SFX Methods

    /// <summary>
    /// Plays a sound effect by enum type.
    /// </summary>
    public void PlaySFX(SoundEffect effect)
    {
        AudioClip clip = GetClip(effect);
        if (clip != null)
        {
            PlayClip(clip);
        }
    }

    /// <summary>
    /// Plays a sound effect by string name (case-insensitive).
    /// </summary>
    public void PlaySFX(string effectName)
    {
        if (string.IsNullOrEmpty(effectName)) return;

        if (Enum.TryParse(effectName, true, out SoundEffect effect))
        {
            PlaySFX(effect);
        }
        else
        {
            Debug.LogWarning($"[AudioManager] Unknown SFX name: '{effectName}'");
        }
    }

    /// <summary>
    /// Plays an arbitrary audio clip through the 2D SFX channel.
    /// </summary>
    public void PlayClip(AudioClip clip)
    {
        if (clip == null) return;

        EnsureAudioSources();
        if (sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, sfxVolume);
        }
    }

    /// <summary>
    /// Sets SFX volume (0.0 to 1.0).
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume;
        }
    }

    public void PlayButtonClick() => PlaySFX(SoundEffect.ButtonClick);
    public void PlayPlayerDeath() => PlaySFX(SoundEffect.PlayerDeath);
    public void PlayWorldShift() => PlaySFX(SoundEffect.WorldShift);
    public void PlayBonusCollected() => PlaySFX(SoundEffect.BonusCollected);
    public void PlayVictory() => PlaySFX(SoundEffect.Victory);

    #endregion

    #region Helpers

    private AudioClip GetClip(SoundEffect effect)
    {
        switch (effect)
        {
            case SoundEffect.ButtonClick: return buttonClickSFX;
            case SoundEffect.PlayerDeath: return playerDeathSFX;
            case SoundEffect.WorldShift: return worldShiftSFX;
            case SoundEffect.BonusCollected: return bonusCollectedSFX;
            case SoundEffect.Victory: return victorySFX;
            default: return null;
        }
    }

    private void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length > 0)
            {
                musicSource = sources[0];
            }
            else
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }

            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f; // 2D
        }

        if (sfxSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length > 1)
            {
                sfxSource = sources[1];
            }
            else
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f; // 2D
        }
    }

    private void ApplyVolumes()
    {
        if (musicSource != null) musicSource.volume = musicVolume;
        if (sfxSource != null) sfxSource.volume = sfxVolume;
    }

    #endregion

    #region Context Menu Testing

    [ContextMenu("Test Play Music")]
    private void TestPlayMusic() => PlayMusic(backgroundMusic);

    [ContextMenu("Test Stop Music")]
    private void TestStopMusic() => StopMusic();

    [ContextMenu("Test ButtonClick SFX")]
    private void TestButtonClick() => PlayButtonClick();

    [ContextMenu("Test PlayerDeath SFX")]
    private void TestPlayerDeath() => PlayPlayerDeath();

    [ContextMenu("Test WorldShift SFX")]
    private void TestWorldShift() => PlayWorldShift();

    [ContextMenu("Test BonusCollected SFX")]
    private void TestBonusCollected() => PlayBonusCollected();

    [ContextMenu("Test Victory SFX")]
    private void TestVictory() => PlayVictory();

    #endregion
}
