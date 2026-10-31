using Onyx.Game.DevTools;

namespace Onyx.Game.UI
{
    /// <summary>Ob Spielsteuerung (Laufen, Interagieren) gerade gesperrt ist, weil ein Bildschirm offen ist.</summary>
    public static class GameplayInput
    {
        public static bool Blocked => DevMode.BlocksGameplayInput || StarMapScreen.IsOpen || SkillTreeScreen.IsOpen;
    }
}
