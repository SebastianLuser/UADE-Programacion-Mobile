using UnityEngine;
using AI.Interfaces;

namespace AI.Controllers
{
    /// <summary>
    /// Animation controller that does nothing - for testing without animations
    /// </summary>
    public class NullAnimationController : IAIAnimationController
    {
        public void PlayAnimation(string animationName)
        {
            // No animation - just log for debugging if needed
            // Debug.Log($"Would play animation: {animationName}");
        }

        public void SetBool(string parameterName, bool value)
        {
            // No animation - just log for debugging if needed
            // Debug.Log($"Would set {parameterName} to {value}");
        }

        public void SetTrigger(string triggerName)
        {
            // No animation - just log for debugging if needed
            // Debug.Log($"Would trigger: {triggerName}");
        }

        public void SetFloat(string parameterName, float value)
        {
            // No animation - just log for debugging if needed
            // Debug.Log($"Would set {parameterName} to {value}");
        }
    }
}