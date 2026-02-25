using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 音频管理器 - 负责背景音乐和音效的播放
/// </summary>
public class AudioManager : MonoBehaviour
{
    private AudioSource _bgmSource;
    private List<AudioSource> _sfxSources = new List<AudioSource>();
    
    private float _bgmVolume = 0.5f;
    private float _sfxVolume = 0.5f;

    public void Initialize()
    {
        // 创建BGM音频源
        GameObject bgmGo = new GameObject("BGMSource");
        bgmGo.transform.SetParent(transform);
        _bgmSource = bgmGo.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.volume = _bgmVolume;

        Debug.Log("[AudioManager] Initialized");
    }

    /// <summary>
    /// 播放背景音乐
    /// </summary>
    public void PlayBGM(string bgmName)
    {
        AudioClip clip = GameManager.Instance.ResourceManager.LoadAudio(bgmName);
        if (clip == null)
        {
            Debug.LogError($"[AudioManager] BGM not found: {bgmName}");
            return;
        }

        _bgmSource.clip = clip;
        _bgmSource.Play();
        Debug.Log($"[AudioManager] Playing BGM: {bgmName}");
    }

    /// <summary>
    /// 停止背景音乐
    /// </summary>
    public void StopBGM()
    {
        _bgmSource.Stop();
    }

    /// <summary>
    /// 播放音效
    /// </summary>
    public void PlaySFX(string sfxName)
    {
        AudioClip clip = GameManager.Instance.ResourceManager.LoadAudio(sfxName);
        if (clip == null)
        {
            Debug.LogError($"[AudioManager] SFX not found: {sfxName}");
            return;
        }

        AudioSource sfxSource = GetAvailableSFXSource();
        sfxSource.PlayOneShot(clip, _sfxVolume);
        Debug.Log($"[AudioManager] Playing SFX: {sfxName}");
    }

    /// <summary>
    /// 获取可用的音效源
    /// </summary>
    private AudioSource GetAvailableSFXSource()
    {
        foreach (AudioSource source in _sfxSources)
        {
            if (!source.isPlaying)
                return source;
        }

        // 如果没有可用的，创建一个新的
        GameObject sfxGo = new GameObject($"SFXSource_{_sfxSources.Count}");
        sfxGo.transform.SetParent(transform);
        AudioSource newSource = sfxGo.AddComponent<AudioSource>();
        newSource.volume = _sfxVolume;
        _sfxSources.Add(newSource);
        
        return newSource;
    }

    /// <summary>
    /// 设置背景音乐音量
    /// </summary>
    public void SetBGMVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);
        _bgmSource.volume = _bgmVolume;
    }

    /// <summary>
    /// 设置音效音量
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        foreach (AudioSource source in _sfxSources)
        {
            source.volume = _sfxVolume;
        }
    }
}