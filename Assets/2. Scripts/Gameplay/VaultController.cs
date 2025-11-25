using System.Collections;
using Services;
using Services.MicroServices.EventsServices;
using StaticClass;
using UnityEngine;

namespace _2._Scripts.Gameplay
{
    public class VaultController : MonoBehaviour
    {
        [SerializeField] private Transform vaultDoorTransform;
        [SerializeField] private float timeToForceOpen = 2f;
        [SerializeField] private AnimationCurve  openAnimationCurve;
        
        private Coroutine m_animationCoroutine;
        private float m_forceOpenTimer;
        private bool m_isDoorOpen;
        
        private static IEventService EventService => ServiceLocator.Get<IEventService>();

        private void OpenDoor()
        {
            if (m_animationCoroutine != null) 
                return;
            
            EventService.DispatchEvent(EventsDefinition.OPEN_VAULT);
            m_isDoorOpen = true;
            m_animationCoroutine = StartCoroutine(OpenDoorCoroutine());
        }

        private IEnumerator OpenDoorCoroutine()
        {
            var l_timer = 0f;
            var l_maxTime = openAnimationCurve.keys[openAnimationCurve.length - 1].time;

            while (l_timer < l_maxTime) 
            {
                vaultDoorTransform.Rotate(0f, openAnimationCurve.Evaluate(l_timer), 0f);
                l_timer += Time.deltaTime;
                yield return null;
            }
            EventService.DispatchEvent(EventsDefinition.END_OPEN_VAULT);
            m_animationCoroutine = null;
        }

        private void OnTriggerEnter(Collider p_other)
        {
            if (p_other.CompareTag("Player"))
                EventService.DispatchEvent(EventsDefinition.START_OPEN_VAULT);
        }

        private void OnTriggerStay(Collider p_other)
        {
            if (m_isDoorOpen)
                return;

            if (!p_other.CompareTag("Player")) 
                return;
            
            m_forceOpenTimer += Time.deltaTime;
            if (m_forceOpenTimer >= timeToForceOpen)
            {
                OpenDoor();
            }
        }

        private void OnTriggerExit(Collider p_other)
        {
            if (m_isDoorOpen)
                return;

            if (!p_other.CompareTag("Player")) 
                return;
            
            m_forceOpenTimer = 0f;
            EventService.DispatchEvent(EventsDefinition.END_OPEN_VAULT);
        }
    }
}