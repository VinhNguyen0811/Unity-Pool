using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VinhNguyen
{
    public class PoolManager : Singleton<PoolManager>
    {
        private sealed class Pool
        {
            #region Fields
            /// <summary>
            /// Prefab
            /// </summary>
            private Pooling prefab;

            /// <summary>
            /// Danh sách các đối tượng đã được phát hành
            /// </summary>
            private List<Pooling> released;

            /// <summary>
            /// Hàng đợi chứa các đối tượng đã được thu thập
            /// </summary>
            private Queue<Pooling> collected;
            #endregion

            #region Properties
            /// <summary>
            /// Số lượng đối tượng đã được phát hành
            /// </summary>
            public int ReleasedCount
            {
                get { return released.Count; }
            }

            /// <summary>
            /// Số lượng đối tượng đã được thu thập
            /// </summary>
            public int CollectedCount
            {
                get { return collected.Count; }
            }
            #endregion

            #region Constructor

            public Pool(Pooling prefab, int capacity, Transform parent)
            {
                this.prefab = prefab;

                collected = new Queue<Pooling>(capacity);
                released = new List<Pooling>(capacity);

                if (prefab.gameObject.activeInHierarchy)
                {
                    for (int i = 0; i < capacity; i++)
                    {
                        Pooling instance = Instantiate(prefab);
                        instance.transform.SetParent(parent);
                        released.Add(instance);
                    }
                }
                else
                {
                    for (int i = 0; i < capacity; i++)
                    {
                        Pooling instance = Instantiate(prefab);
                        instance.transform.SetParent(parent);
                        collected.Enqueue(instance);
                    }
                }
            }

            #endregion

            #region Methods
            /// <summary>
            /// Lấy một đối tượng trong hồ bơi nếu có, không thì sẽ tạo ra một đối tượng mới
            /// SetActivate(true) tự động được gọi
            /// </summary>
            /// <returns></returns>
            public Pooling Release()
            {
                Pooling instance = null;
                if (collected.Count > 0)
                {
                    instance = collected.Dequeue();
                    if (instance == null)
                    {
                        // Đối tượng đã bị gọi Destroy() không mong muốn, tiếp tục lấy người tiếp theo
                        return Release();
                    }
                }
                else
                {
                    instance = Instantiate(prefab);
                }

                instance.gameObject.SetActive(true);
                released.Add(instance);

                return instance;
            }

            /// <summary>
            /// Đưa đối tượng vào hồ bơi
            /// SetActivate(false) tự động được gọi
            /// </summary>
            /// <returns></returns>
            public void Collect(Pooling target)
            {
                target.gameObject.SetActive(false);

                if (released.Remove(target))
                {
                    collected.Enqueue(target);
                }
            }

            /// <summary>
            /// Phá hủy các đối tượng
            /// </summary>
            /// <param name="isSpawned"> là true sẽ phá hủy các đối tượng đang hoạt động, là false sẽ phá hủy các đối tượng không hoạt động, mặc định false</param>
            public void Destroy(bool isSpawned = false)
            {
                if (isSpawned)
                {
                    for (int i = 0; i < released.Count; i++)
                    {
                        Destroy(released[i].gameObject);
                    }
                }
                else
                {
                    while (collected.Count > 0)
                    {
                        Destroy(collected.Dequeue().gameObject);
                    }
                }
            }

            /// <summary>
            /// Phá hủy tất cả đối tượng kể cả nó có đang hoạt động hay không
            /// </summary>
            public void DestroyAll()
            {
                Destroy(true);
                released = null;

                Destroy(false);
                collected = null;
            }
            #endregion
        }

        [System.NonSerialized]
        private Dictionary<int, Pool> pools = new Dictionary<int, Pool>();

        #region Methods
        /// <summary>
        /// Tạo một hồ bơi của prefab với kích thước ban đầu
        /// SetActivate không tự động được gọi
        /// </summary>
        public void CreatePool(Pooling prefab, int capacity, Transform parent = null)
        {
            int instanceId = prefab.GetInstanceID();

            if (pools.ContainsKey(instanceId))
                return;

            prefab.instanceIdPrefab = instanceId;

            Pool pool = new Pool(prefab, capacity, parent);

            pools.Add(instanceId, pool);
        }

        /// <summary>
        /// Tạo một hồ bơi của prefab với kích thước ban đầu
        /// SetActivate không tự động được gọi
        /// </summary>
        public void CreatePool<T>(T target, int capacity, Transform parent = null) where T : MonoBehaviour
        {
            Pooling pooling = target.GetComponent<Pooling>();

            CreatePool(pooling, capacity, parent);
        }

        /// <summary>
        /// Phá hủy hồ bơi của target
        /// </summary>
        public void DestroyPool(Pooling target)
        {
            Pool pool;
            int instanceId = target.instanceIdPrefab;
            if (pools.TryGetValue(instanceId, out pool))
            {
                pool.DestroyAll();
                pools.Remove(instanceId);
            }
        }

        /// <summary>
        /// Phá hủy hồ bơi của target
        /// </summary>
        public void DestroyPool<T>(T target) where T : MonoBehaviour
        {
            Pooling pooling = target.GetComponent<Pooling>();
            
            DestroyPool(pooling);
        }

        /// <summary>
        /// Lấy một đối tượng dựa trên phiên bản gốc
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        public GameObject Release(GameObject original)
        {
            Pooling pooling = original.GetComponent<Pooling>();

            if (pooling != null)
            {
                Pool pool;
                if (pools.TryGetValue(pooling.instanceIdPrefab, out pool))
                {
                    Pooling instance = pool.Release();
                    return instance.gameObject;
                }
            }

            GameObject ins = Instantiate(original);
            ins.gameObject.SetActive(true);
            return ins;
        }

        /// <summary>
        /// Lấy một đối tượng từ hồ bơi của target
        /// SetActivate(true) tự động được gọi
        /// </summary>
        public T Release<T>(T original) where T : MonoBehaviour
        {
            Pooling pooling = original.GetComponent<Pooling>();
            
            if (pooling != null)
            {
                Pool pool;
                if (pools.TryGetValue(pooling.instanceIdPrefab, out pool))
                {
                    Pooling instance = pool.Release();
                    return instance.GetComponent<T>();
                }
            }
            
            T ins= Instantiate(original);
            ins.gameObject.SetActive(true);
            return ins;
        }

        /// <summary>
        /// Đưa một đối tượng vào hồ bơi target
        /// SetActivate(false) tự động được gọi
        /// </summary>
        public void Collect(GameObject obj)
        {
            Pooling pooling = obj.GetComponent<Pooling>();
            if (pooling != null)
            {
                Pool pool;
                if (pools.TryGetValue(pooling.instanceIdPrefab, out pool))
                {
                    pool.Collect(pooling);
                    return;
                }
            }

            obj.SetActive(false);
        }

        /// <summary>
        /// Đưa một đối tượng vào hồ bơi target
        /// SetActivate(false) tự động được gọi
        /// </summary>
        public void Collect<T>(T obj) where T : MonoBehaviour
        {
            Pooling pooling = obj.GetComponent<Pooling>();

            if (pooling != null)
            {
                Pool pool;
                if (pools.TryGetValue(pooling.instanceIdPrefab, out pool))
                {
                    pool.Collect(pooling);
                    return;
                }
            }

            obj.gameObject.SetActive(false);
        }

        /// <summary>
        /// Đưa một đối tượng vào hồ bơi target
        /// SetActivate(false) tự động được gọi
        /// </summary>
        public void Destroy(GameObject obj)
        {
            Pooling pooling = obj.GetComponent<Pooling>();
            if (pooling != null)
            {
                Pool pool;
                if (pools.TryGetValue(pooling.instanceIdPrefab, out pool))
                {
                    pool.Destroy(pooling);
                    return;
                }
            }

            Object.Destroy(obj);
        }

        /// <summary>
        /// Đưa một đối tượng vào hồ bơi target
        /// SetActivate(false) tự động được gọi
        /// </summary>
        public void Destroy(GameObject obj, float t)
        {
            Pooling pooling = obj.GetComponent<Pooling>();
            if (pooling != null)
            {
                Pool pool;
                if (pools.TryGetValue(pooling.instanceIdPrefab, out pool))
                {
                    StartCoroutine(IEDestroy(pool, pooling, t));
                    return;
                }
            }

            Object.Destroy(obj, t);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pool"></param>
        /// <param name="pooling"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        private IEnumerator IEDestroy(Pool pool, Pooling pooling, float t)
        {
            yield return new WaitForSeconds(t);
            pool.Destroy(pooling);
        }

        /// <summary>
        /// Lấy số lượng đối tượng đang hoạt động
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public int ReleasedCount(Pooling target)
        {
            Pool pool;
            if (pools.TryGetValue(target.instanceIdPrefab, out pool))
            {
                return pool.ReleasedCount;
            }

            return -1;
        }

        /// <summary>
        /// Lấy số lượng đối tượng đang hoạt động
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public int ReleasedCount<T>(T target) where T : MonoBehaviour
        {
            Pooling pooling = target.GetComponent<Pooling>();
            if (pooling == null)
                return -1;

            return ReleasedCount(pooling);
        }

        /// <summary>
        /// Lấy số lượng đối tượng đang không hoạt động
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public int CollectedCount(Pooling target)
        {
            Pool pool;
            if (pools.TryGetValue(target.instanceIdPrefab, out pool))
            {
                return pool.CollectedCount;
            }

            return -1;
        }

        /// <summary>
        /// Lấy số lượng đối tượng đang không hoạt động
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public int CollectedCount<T>(T target) where T : MonoBehaviour
        {
            Pooling pooling = target.GetComponent<Pooling>();
            if (pooling == null)
                return -1;

            return CollectedCount(pooling);
        }

        /// <summary>
        /// Lấy tổng số lượng đối tượng đã được tạo ra
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public int TotalCount(Pooling target)
        {
            Pool pool;
            if (pools.TryGetValue(target.instanceIdPrefab, out pool))
            {
                return pool.ReleasedCount + pool.CollectedCount;
            }

            return -1;
        }

        /// <summary>
        /// Lấy tổng số lượng đối tượng đã được tạo ra
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public int TotalCount<T>(T target) where T : MonoBehaviour
        {
            Pooling pooling = target.GetComponent<Pooling>();
            if (pooling == null)
                return -1;

            return TotalCount(pooling);
        }
        
        #endregion
    }
}