using System;
using UnityEngine;
using DangerousArena.Player;

namespace DangerousArena.Gameplay
{
    /// <summary>
    /// Drop-in hazard component for any dangerous tile, trap, or red platform.
    /// Implements IPlayerHazard to trigger player death on contact.
    /// </summary>
    [DisallowMultipleComponent]
    public class HazardTile : MonoBehaviour, IPlayerHazard
    {
        public event Action<GameObject> OnHazardTriggered;

        public void TriggerHazard(GameObject player)
        {
            OnHazardTriggered?.Invoke(player);

            if (player != null && player.TryGetComponent<PlayerController>(out var controller))
            {
                controller.Die();
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.HandlePlayerDeath();
            }
        }
    }

    /// <summary>
    /// Drop-in finish component for goal tiles, portals, or extraction zones.
    /// Implements IPlayerFinish to conclude the level or trigger victory.
    /// </summary>
    [DisallowMultipleComponent]
    public class FinishTile : MonoBehaviour, IPlayerFinish
    {
        public event Action<GameObject> OnFinishReached;

        public void TriggerFinish(GameObject player)
        {
            OnFinishReached?.Invoke(player);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayerReachedFinish();
            }
        }
    }

    /// <summary>
    /// Drop-in bonus component for yellow collectible tiles or floating gems.
    /// Implements IPlayerBonus to communicate player, bonus type, value, and duration.
    /// </summary>
    [DisallowMultipleComponent]
    public class BonusTile : MonoBehaviour, IPlayerBonus
    {
        [Header("Bonus Configuration")]
        [SerializeField] private BonusType bonusType = BonusType.Score;
        [SerializeField] private float bonusValue = 100f;
        [SerializeField] private float bonusDuration = 0f;
        [SerializeField] private bool deactivateOnCollect = true;

        public BonusType BonusType => bonusType;
        public float BonusValue => bonusValue;
        public float BonusDuration => bonusDuration;

        public event Action<BonusData> OnBonusCollected;

        public void CollectBonus(PlayerController player)
        {
            BonusData data = new BonusData(bonusType, bonusValue, bonusDuration, player);
            OnBonusCollected?.Invoke(data);
            BonusEvents.TriggerBonus(data);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.HandleBonusCollected(bonusType, (int)bonusValue);
            }

            if (deactivateOnCollect)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
