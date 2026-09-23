using UnityEngine;

/// <summary>
/// 音频变速播放：让音频在指定时间内（默认1秒）播放完。
/// 原理：pitch = 音频原始时长 / 目标时长，pitch加快播放速度但会改变音调。
/// 挂在带AudioSource的物体上即可，也可通过PlayInSeconds()动态调用。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioSpeedPlay : MonoBehaviour
{
    [Tooltip("目标播放时长（秒），音频会变速到在该时长内播完")]
    public float targetDuration = 1f;
    [Tooltip("Start时自动按变速播放")]
    public bool playOnStart = true;

    private AudioSource _audioSource;
    private float _originalPitch;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _originalPitch = _audioSource.pitch;
    }

    void Start()
    {
        if (playOnStart)
        {
            PlayInSeconds(targetDuration);
        }
    }

    /// <summary>
    /// 变速播放当前clip，使其在seconds秒内播完
    /// </summary>
    public void PlayInSeconds(float seconds)
    {
        if (_audioSource.clip == null)
        {
            Debug.LogWarning("[AudioSpeedPlay] AudioSource没有clip，无法播放");
            return;
        }
        if (seconds <= 0f)
        {
            Debug.LogWarning("[AudioSpeedPlay] 目标时长必须大于0");
            return;
        }

        _audioSource.pitch = _originalPitch * (_audioSource.clip.length / seconds);
        _audioSource.Play();
    }

    /// <summary>
    /// 恢复原始速度播放
    /// </summary>
    public void PlayNormal()
    {
        _audioSource.pitch = _originalPitch;
        _audioSource.Play();
    }
}
