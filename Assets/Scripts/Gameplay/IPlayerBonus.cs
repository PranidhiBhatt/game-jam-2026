using System;
using UnityEngine;
using DangerousArena.Player;

namespace DangerousArena.Gameplay
{
    /// <summary>
    /// Identifies the category of bonus for future expansion.
    /// Supports Speed Boost, Temporary Protection, Extra Time, and Score.
    /// </summary>
    public enum BonusType
    {
        Generic,
        SpeedBoost,
        TemporaryProtection,
        ExtraTime,
        Score
    }

    /// <summary>
    /// Lightweight payload carrying contextual data when a bonus is collected.
    /// Communicates which player collected it, what type it is, and optional value/duration.
    /// </summary>
    [Serializable]
    public struct BonusData
    {
        public BonusType Type;
        public float Value;
        public float Duration;
        public PlayerController Player;

        public BonusData(BonusType type, float value = 0f, float duration = 0f, PlayerController player = null)
        {
            Type = type;
            Value = value;
            Duration = duration;
            Player = player;
        }
    }

    /// <summary>
    /// Decoupled event bus for bonus interactions.
    /// Allows the Level developer to fire bonus events and systems (UI, Audio, Abilities)
    /// to listen without modifying GameManager or PlayerController.
    /// </summary>
    public static class BonusEvents
    {
        public static event Action<BonusData> OnBonusTriggered;

        public static void TriggerBonus(BonusData data)
        {
            OnBonusTriggered?.Invoke(data);
        }
    }

    /// <summary>
    /// Implemented by bonus tiles or collectible pickups in the arena.
    /// Enables the Level developer to implement custom bonus tiles without tight coupling.
    /// </summary>
    public interface IPlayerBonus
    {
        /// <summary>
        /// Collects the bonus for the specified player.
        /// </summary>
        /// <param name="player">The player controller that collected this bonus.</param>
        void CollectBonus(PlayerController player);
    }
}
