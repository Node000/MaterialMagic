using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public enum GameSfxId
{
    None = 0,
    CardPlay = 1,
    CardReturnToHand = 2,
    CardRefresh = 3,
    ButtonClick = 4,
    EnemyAttack = 5,
    NegativeStatusApply = 6,
    PositiveStatusApply = 7,
    PlayerCastSwing = 8
}

[Serializable]
public class GameSfxClipEntry
{
    public GameSfxId id;
    public AudioClip clip;

    public GameSfxClipEntry(GameSfxId id)
    {
        this.id = id;
    }
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip startMenuMusic;
    [SerializeField] private AudioClip gameplayMusic;
    [SerializeField] private AudioClip battleMusic;
    [SerializeField] private GameSfxClipEntry[] gameSfxClips =
    {
        new GameSfxClipEntry(GameSfxId.CardPlay),
        new GameSfxClipEntry(GameSfxId.CardReturnToHand),
        new GameSfxClipEntry(GameSfxId.CardRefresh),
        new GameSfxClipEntry(GameSfxId.ButtonClick),
        new GameSfxClipEntry(GameSfxId.EnemyAttack),
        new GameSfxClipEntry(GameSfxId.NegativeStatusApply),
        new GameSfxClipEntry(GameSfxId.PositiveStatusApply),
        new GameSfxClipEntry(GameSfxId.PlayerCastSwing)
    };
    [SerializeField] private float defaultMusicVolume = 0.8f;
    [SerializeField] private float defaultSfxVolume = 0.8f;

    public float MusicVolume { get; private set; }
    public float SfxVolume { get; private set; }
    public AudioSource MusicSource => musicSource;

    private readonly List<AudioSource> pitchedSfxSources = new List<AudioSource>();

    private const string MusicVolumeKey = "MusicVolume";
    private const string SfxVolumeKey = "SfxVolume";
    private const string MusicMixerParameter = "MusicVolume";
    private const string SfxMixerParameter = "SfxVolume";
    private const string StartSceneName = "StartScene";
    private const float MinimumSfxPitch = 0.1f;
    private const float MaximumSfxPitch = 3f;
    private const int MaxPitchedSfxSources = 4;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureSfxClipEntries();
        EnsureAudioSources();
        LoadVolumes();
        PlaySceneMusic(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }

    private void OnValidate()
    {
        EnsureSfxClipEntries();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        ApplyMixerVolume(MusicMixerParameter, MusicVolume);
        if (musicSource != null)
            musicSource.volume = MusicVolume;
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        ApplyMixerVolume(SfxMixerParameter, SfxVolume);
        if (sfxSource != null)
            sfxSource.volume = SfxVolume;
        for (int i = 0; i < pitchedSfxSources.Count; i++)
        {
            if (pitchedSfxSources[i] != null)
                pitchedSfxSources[i].volume = SfxVolume;
        }
    }

    public void PlayStartSceneMusic()
    {
        PlayMusic(startMenuMusic);
    }

    public void PlayGameplayMusic()
    {
        PlayMusic(gameplayMusic);
    }

    public void PlayBattleMusic()
    {
        PlayMusic(battleMusic);
    }

    public void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip);
    }

    public void PlaySfx(AudioClip clip, float pitch)
    {
        if (clip == null)
            return;

        AudioSource source = GetPitchedSfxSource();
        if (source == null)
            return;

        source.clip = clip;
        source.pitch = Mathf.Clamp(pitch, MinimumSfxPitch, MaximumSfxPitch);
        source.volume = SfxVolume;
        source.Play();
    }

    public void PlaySfx(GameSfxId id)
    {
        PlaySfx(GetSfxClip(id));
    }

    public void PlaySfx(GameSfxId id, float pitch)
    {
        PlaySfx(GetSfxClip(id), pitch);
    }

    public AudioClip GetSfxClip(GameSfxId id)
    {
        if (id == GameSfxId.None || gameSfxClips == null)
            return null;

        for (int i = 0; i < gameSfxClips.Length; i++)
        {
            GameSfxClipEntry entry = gameSfxClips[i];
            if (entry != null && entry.id == id)
                return entry.clip;
        }

        return null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlaySceneMusic(scene.name);
    }

    private void PlaySceneMusic(string sceneName)
    {
        if (sceneName == StartSceneName)
            PlayStartSceneMusic();
        else
            PlayGameplayMusic();
    }

    private void PlayMusic(AudioClip clip)
    {
        if (musicSource == null)
            return;

        if (clip == null)
        {
            musicSource.Stop();
            musicSource.clip = null;
            return;
        }

        if (musicSource.clip == clip)
        {
            if (!musicSource.isPlaying)
                musicSource.Play();
            return;
        }

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = MusicVolume;
        musicSource.Play();
    }

    private void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        ConfigureSfxSource(sfxSource);
    }

    private AudioSource GetPitchedSfxSource()
    {
        EnsureAudioSources();

        for (int i = 0; i < pitchedSfxSources.Count; i++)
        {
            AudioSource source = pitchedSfxSources[i];
            if (source != null && !source.isPlaying)
                return source;
        }

        if (pitchedSfxSources.Count < MaxPitchedSfxSources)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            ConfigureSfxSource(source);
            pitchedSfxSources.Add(source);
            return source;
        }

        return pitchedSfxSources.Count > 0 ? pitchedSfxSources[0] : null;
    }

    private void ConfigureSfxSource(AudioSource source)
    {
        if (source == null)
            return;

        source.loop = false;
        source.playOnAwake = false;
        source.volume = SfxVolume;
        source.pitch = 1f;
        if (sfxSource != null && source != sfxSource)
        {
            source.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
            source.spatialBlend = sfxSource.spatialBlend;
        }
    }

    private void LoadVolumes()
    {
        SetMusicVolume(PlayerPrefs.GetFloat(MusicVolumeKey, defaultMusicVolume));
        SetSfxVolume(PlayerPrefs.GetFloat(SfxVolumeKey, defaultSfxVolume));
        PlayerPrefs.Save();
    }

    private void EnsureSfxClipEntries()
    {
        EnsureSfxClipEntry(GameSfxId.CardPlay);
        EnsureSfxClipEntry(GameSfxId.CardReturnToHand);
        EnsureSfxClipEntry(GameSfxId.CardRefresh);
        EnsureSfxClipEntry(GameSfxId.ButtonClick);
        EnsureSfxClipEntry(GameSfxId.EnemyAttack);
        EnsureSfxClipEntry(GameSfxId.NegativeStatusApply);
        EnsureSfxClipEntry(GameSfxId.PositiveStatusApply);
        EnsureSfxClipEntry(GameSfxId.PlayerCastSwing);
    }

    private void EnsureSfxClipEntry(GameSfxId id)
    {
        if (HasSfxClipEntry(id))
            return;

        int length = gameSfxClips != null ? gameSfxClips.Length : 0;
        Array.Resize(ref gameSfxClips, length + 1);
        gameSfxClips[length] = new GameSfxClipEntry(id);
    }

    private bool HasSfxClipEntry(GameSfxId id)
    {
        if (gameSfxClips == null)
            return false;

        for (int i = 0; i < gameSfxClips.Length; i++)
        {
            GameSfxClipEntry entry = gameSfxClips[i];
            if (entry != null && entry.id == id)
                return true;
        }

        return false;
    }

    private void ApplyMixerVolume(string parameterName, float value)
    {
        if (audioMixer == null)
            return;

        float decibels = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
        audioMixer.SetFloat(parameterName, decibels);
    }
}
