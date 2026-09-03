using UnityEngine;

namespace DangerousArena.Gameplay
{
    /// <summary>
    /// Implemented by hazard tiles, traps, or lethal obstacles in the arena.
    /// When triggered by the player, it reports hazard contact and initiates player death.
    /// </summary>
    public interface IPlayerHazard
    {
        /// <summary>
        /// Triggered when the player comes into contact with this hazard.
        /// </summary>
        /// <param name="player">The GameObject representing the player.</param>
        void TriggerHazard(GameObject player);
    }
}
