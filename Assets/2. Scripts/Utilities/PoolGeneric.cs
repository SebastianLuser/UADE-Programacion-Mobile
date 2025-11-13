using System.Collections.Generic;
using UnityEngine;

namespace DevelopmentUtilities
{
    public class PoolGeneric<T> where T : Object
    {
        private readonly T m_prefab;
        private readonly Transform m_parent;
        private readonly Queue<T> m_availables = new();

        public PoolGeneric(T p_prefab, Transform p_transformParent = null)
        {
            m_prefab = p_prefab;
            m_parent = p_transformParent;
        }

        public T GetorCreate()
        {
            if (m_availables.Count > 0)
            {
                var l_obj = m_availables.Dequeue();
                while (l_obj == null &&  m_availables.Count > 0)
                {
                    l_obj = m_availables.Dequeue();
                }
                if (l_obj == null)
                    l_obj = InstantiateAtDefaultPose();

                ResetTransform(l_obj);
                return l_obj;
            }

            var l_newObj = InstantiateAtDefaultPose();
            ResetTransform(l_newObj);
            return l_newObj;
        }

        public void ReturnToPool(T p_poolEntry)
        {
            m_availables.Enqueue(p_poolEntry);
        }

        public void ClearData()
        {
            m_availables.Clear();
        }

        private static void ResetTransform(T p_object)
        {
            if (p_object is Component l_component)
            {
                l_component.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
            else if (p_object is GameObject l_gameObject)
            {
                l_gameObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
        }

        private T InstantiateAtDefaultPose()
        {
            if (m_parent == null)
                return Object.Instantiate(m_prefab, Vector3.zero, Quaternion.identity);

            return Object.Instantiate(m_prefab, Vector3.zero, Quaternion.identity, m_parent);
        }
    }
}
