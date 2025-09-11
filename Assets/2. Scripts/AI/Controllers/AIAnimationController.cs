using UnityEngine;
using AI.Interfaces;

namespace AI.Controllers
{
    public class AIAnimationController : IAIAnimationController
    {
        private readonly Animator animator;

        public AIAnimationController(Animator animator)
        {
            this.animator = animator;
        }

        public void PlayAnimation(string animationName)
        {
            if (animator != null)
                animator.Play(animationName);
        }

        public void SetBool(string parameterName, bool value)
        {
            if (animator != null)
                animator.SetBool(parameterName, value);
        }

        public void SetTrigger(string triggerName)
        {
            if (animator != null)
                animator.SetTrigger(triggerName);
        }

        public void SetFloat(string parameterName, float value)
        {
            if (animator != null)
                animator.SetFloat(parameterName, value);
        }
    }
}