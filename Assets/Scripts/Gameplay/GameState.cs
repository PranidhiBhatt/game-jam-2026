namespace DangerousArena.Gameplay
{
    /// <summary>
    /// Represents the high-level states of the game session for Dangerous Arena.
    /// Consumed by GameManager and other gameplay systems.
    /// </summary>
    public enum GameState
    {
        WaitingForStart,
        Playing,
        ChangingWorld,
        GameOver,
        Victory,
        LevelComplete
    }
}
