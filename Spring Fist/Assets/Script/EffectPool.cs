using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用特效对象池：按预制体分组复用实例，播放完毕自动回收，避免反复 Instantiate/Destroy。
/// 注意：池化的特效预制体上不能挂 AutoDestroy / AutoDestroyPS 等自我销毁脚本。
/// </summary>
public class EffectPool : MonoBehaviour
{
    private static EffectPool _instance;

    /// <summary>
    /// 单例，首次访问时自动创建，无需在场景中手动放置
    /// </summary>
    public static EffectPool Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new GameObject("EffectPool").AddComponent<EffectPool>();
            }
            return _instance;
        }
    }

    // 每个预制体对应的空闲实例队列
    private readonly Dictionary<GameObject, Queue<GameObject>> _freeObjects = new Dictionary<GameObject, Queue<GameObject>>();

    /// <summary>
    /// 从池中取出特效放到指定位置并从头播放，播放完毕自动回收
    /// </summary>
    public GameObject Play(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        GameObject obj = Get(prefab);
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);

        // 清空残留粒子并从头播放所有子粒子系统
        ParticleSystem[] systems = obj.GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in systems)
        {
            ps.Clear();
            ps.Play();
        }

        StartCoroutine(ReleaseAfterFinish(obj, prefab, GetEffectDuration(systems)));
        return obj;
    }

    /// <summary>
    /// 取空闲实例，没有才 Instantiate 新实例
    /// </summary>
    private GameObject Get(GameObject prefab)
    {
        Queue<GameObject> queue;
        if (_freeObjects.TryGetValue(prefab, out queue) && queue.Count > 0)
        {
            return queue.Dequeue();
        }

        GameObject obj = Instantiate(prefab, transform);
        obj.name = prefab.name; // 去掉(Clone)后缀，方便在Hierarchy中辨认
        obj.SetActive(false);
        return obj;
    }

    /// <summary>
    /// 计算特效总时长：所有粒子系统中 duration + 最大生命周期 的最大值
    /// </summary>
    private float GetEffectDuration(ParticleSystem[] systems)
    {
        float duration = 1f; // 没有粒子系统时默认1秒后回收
        foreach (ParticleSystem ps in systems)
        {
            float t = ps.main.duration + ps.main.startLifetime.constantMax;
            if (t > duration) duration = t;
        }
        return duration;
    }

    /// <summary>
    /// 等待特效播放完毕后回收入池（隐藏等待下次复用，不销毁）
    /// </summary>
    private IEnumerator ReleaseAfterFinish(GameObject obj, GameObject prefab, float delay)
    {
        yield return new WaitForSeconds(delay);

        obj.SetActive(false);
        obj.transform.SetParent(transform, false);

        Queue<GameObject> queue;
        if (!_freeObjects.TryGetValue(prefab, out queue))
        {
            queue = new Queue<GameObject>();
            _freeObjects[prefab] = queue;
        }
        queue.Enqueue(obj);
    }
}
