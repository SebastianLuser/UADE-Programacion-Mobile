using Services.MicroServices.EventsServices;

namespace Services.MicroServices.EventsServices.CustomEvents
{
    public readonly struct GameResultEvent : ICustomEventData
    {
        public bool IsVictory { get; }
        public int Score { get; }

        public GameResultEvent(bool isVictory, int score)
        {
            IsVictory = isVictory;
            Score = score;
        }
    }
}
