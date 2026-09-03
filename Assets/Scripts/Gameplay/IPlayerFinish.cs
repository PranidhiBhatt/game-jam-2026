using UnityEngine;

namespace DangerousArena.Gameplay
{
    /// <summary>
    /// Implemented by finish/goal tiles or portal objects that conclude the level.
    /// When triggered by the player, it notifies GameManager that the finish has been reached.
    /// </summary>
    public interface IPlayerFinish
    {
        /// <summary>
        /// Triggered when the player reaches the finish marker.
        /// </summary>
        /// <param name="player">The GameObject representing the player.</param>
        void TriggerFinish(GameObject player);
    }
}
