using UnityEngine;

public class AutoDestroy : MonoBehaviour
{
    [Header("自动销毁设置")]
    [Tooltip("为0时自动根据 ParticleSystem 或 AudioSource 计算销毁时间")]
    public float destroyDelay = 0f;

    void Start()
    {
        float delay = destroyDelay;

        // 如果未手动设置延迟，自动根据组件计算
        if (delay <= 0f)
        {
            ParticleSystem particle = GetComponent<ParticleSystem>();
            AudioSource audioSource = GetComponent<AudioSource>();

            if (particle != null)
            {
                // 粒子系统持续时间 + 粒子最大生命周期
                delay = particle.main.duration + particle.main.startLifetime.constantMax;
            }
            else if (audioSource != null && audioSource.clip != null)
            {
                delay = audioSource.clip.length;
            }
            else
            {
                // 默认1秒后销毁
                delay = 1f;
            }
        }

        Destroy(gameObject, delay);
    }
}
