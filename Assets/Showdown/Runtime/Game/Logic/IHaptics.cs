#nullable enable
namespace Showdown.Game
{
    /// <summary>The vibration motor. Never throws: a phone without one is simply silent to the touch.</summary>
    public interface IHaptics
    {
        void Pulse(HapticKind kind);
    }

    public sealed class NullHaptics : IHaptics
    {
        public static readonly NullHaptics Instance = new NullHaptics();

        public void Pulse(HapticKind kind)
        {
        }
    }

    /// <summary>Feedback that does nothing — for tests, and as the fallback when the audio layer cannot start.</summary>
    public sealed class NullFeedback : IFeedback
    {
        public static readonly NullFeedback Instance = new NullFeedback();

        public void Play(Cue cue)
        {
        }
    }
}
