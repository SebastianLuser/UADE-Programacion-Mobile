namespace AI.Interfaces
{
    public interface IAIAnimationController
    {
        void PlayAnimation(string animationName);
        void SetBool(string parameterName, bool value);
        void SetTrigger(string triggerName);
        void SetFloat(string parameterName, float value);
    }
}