using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Services.MicroServices.PoolObjectsService
{
    public class PoolObjectsService : IPoolObjectsService
    {
        private Dictionary<Type, IPoolWrapper> m_poolWrappers;
        
        public void Initialize()
        {
            m_poolWrappers = new Dictionary<Type, IPoolWrapper>();
        }

        public T GetOrCreateObject<T>(T p_prefab, Transform p_content = null) where T : Object
        {
            var l_type = typeof(T);

            if (m_poolWrappers.TryGetValue(l_type, out var l_poolWrapper))
            {
                if (l_poolWrapper is PoolWrapper<T> l_wrapper)
                    return l_wrapper.GetOrCreate();
            }
            
            var l_newPoolWrapper = new PoolWrapper<T>(p_prefab, p_content);
            m_poolWrappers.Add(l_type, l_newPoolWrapper);
            return l_newPoolWrapper.GetOrCreate();
        }

        public void ReturnObject<T>(T p_object) where T : Object
        {
            var l_type = typeof(T);

            if (!m_poolWrappers.TryGetValue(l_type, out var l_poolWrapper))
            {
                Object.Destroy(p_object);
                return;
            }
            
            if (l_poolWrapper is PoolWrapper<T> l_wrapper)
                l_wrapper.ReturnToPool(p_object);
        }
    }
}