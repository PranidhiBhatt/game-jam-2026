namespace DangerousArena.Gameplay
{
    /// <summary>
    /// Optional interface that a Level or Scene manager can implement
    /// to handle scene loading, arena generation, or layout swapping per level.
    /// </summary>
    public interface ILevelLoader
    {
        /// <summary>
        /// Instructs the loader to transition to or construct the specified level.
        /// </summary>
        /// <param name="levelIndex">1-based level index (e.g. 1, 2, 3).</param>
        void LoadLevel(int levelIndex);
    }
}
