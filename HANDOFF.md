# Dangerous Arena — Gameplay & Technical Systems Handoff

**Branch:** `member1-technical`  
**Unity Version:** `6000.0.26f1` (Unity 6)  
**Author:** Technical / Gameplay Lead (Member 1)  

---

## 1. System Architecture Overview

The technical architecture is built strictly decoupled using event buses and interface contracts.
- **UI Developer (Member 3):** Consumes purely C# events and public state properties. Zero UI dependencies exist in gameplay code.
- **Level Developer (Member 2):** Implements tiles via clean interfaces (`IPlayerHazard`, `IPlayerBonus`, `IPlayerFinish`) or drop-in components, and hooks into `WorldManager` and `GameManager` for arena shifts and level progression.

---

## 2. Core Systems Reference

### 1. PlayerController (`DangerousArena.Player.PlayerController`)
- **Purpose:** Handles responsive 3D character movement, single jump, camera-relative steering, gravity, and contact detection with tiles.
- **Key Methods:**
  - `Die()`: Halts movement, zeroes velocity, fires `OnPlayerDied`, notifies `GameManager`.
  - `Respawn(Vector3 pos, Quaternion? rot)`: Teleports and revives player.
  - `ResetToInitialSpawn()`: Teleports player back to their initial scene spawn point.
  - `SetMovementEnabled(bool enabled)`: Enables or disables player input and motion.
  - `ResetVelocity()`: Clears momentum and restores grounded pull.
  - `Teleport(Vector3 pos, Quaternion? rot)`: Safely moves player through `CharacterController`.
  - `SetCamera(Camera cam)`: Binds active camera for direction alignment.
- **Key Properties:**
  - `bool IsAlive { get; }`: Returns alive status.
  - `bool IsMovementEnabled { get; }`: Returns whether input is active.
  - `bool IsGrounded { get; }`: Dual-check ground contact status.
  - `Vector3 Velocity { get; }`: Current full velocity.
  - `Vector3 HorizontalVelocity { get; }`: Smoothed horizontal velocity vector.
- **Events:**
  - `OnPlayerDied`: Fired upon lethal hazard contact.
  - `OnJumped`: Fired on jump impulse.
  - `OnLanded`: Fired when landing on ground.
- **Callers:** `HazardTile`, `GameManager` (on death/restart/freeze), Level loader.
- **Subscribers:** Audio (jump/death sounds), VFX (dust/death particles), Animation.

---

### 2. GameState (`DangerousArena.Gameplay.GameState`)
- **Purpose:** Enumerates the complete session state machine:
  - `WaitingForStart`: Scene loaded, waiting for play input.
  - `Playing`: Active gameplay; timers tick, player moves.
  - `ChangingWorld`: Arena mutation in progress; timer pauses.
  - `GameOver`: Player died; input frozen.
  - `LevelComplete`: Current level cleared (ready for next level).
  - `Victory`: Final level (Level 3) cleared; game won.

---

### 3. GameManager (`DangerousArena.Gameplay.GameManager`)
- **Purpose:** Singleton coordinator owning `GameState`, `CurrentLevel`, and win/death rules.
- **Key Methods:**
  - `StartGame()`: Begins gameplay at Level 1 (`Playing`).
  - `RestartLevel()`: Restarts current level, preserving `CurrentLevel`.
  - `LoadNextLevel()`: Increments `CurrentLevel++` or triggers `Victory` on final level.
  - `LoadLevel(int level)`: Jumps to specific level (clamped 1 to `MaxLevels`).
  - `HandlePlayerDeath()`: Transitions to `GameOver` (guarded against duplicate calls).
  - `PlayerReachedFinish()`: Transitions to `LevelComplete` or `Victory`.
  - `HandleBonusCollected(BonusType, int)`: Dispatches bonus collection.
  - `SetGameState(GameState)`: Transitions state safely.
  - `RegisterLevelLoader(ILevelLoader)`: Binds custom scene or procedural loader.
- **Key Properties:**
  - `Instance { get; }`: Singleton access.
  - `CurrentState { get; }`: Active `GameState`.
  - `CurrentLevel { get; }`: Active level index (starts at 1).
  - `MaxLevels { get; }`: Total levels (default 3).
  - `IsGameplayActive { get; }`: Returns `true` if `Playing` or `ChangingWorld`.
- **Events:**
  - `OnGameStarted`: Session began.
  - `OnGameOver`: Player died.
  - `OnLevelComplete`: Intermediate level cleared.
  - `OnVictory`: Game completed.
  - `OnLevelRestarted`: Level retry triggered.
  - `OnLevelChanged(int level)`: Level index changed.
  - `OnLevelLoadRequested(int level)`: Requests actual scene/arena load.
  - `OnGameStateChanged(GameState state)`: State changed.
  - `OnBonusCollected(BonusType type, int value)`: Bonus collected.
- **Callers:** UI buttons, `FinishTile`, `HazardTile`, `WorldManager`.
- **Subscribers:** UI screens, Audio managers, Level loaders, Analytics.

---

### 4. TimerManager (`DangerousArena.Gameplay.TimerManager`)
- **Purpose:** Drives the periodic ~20-second countdown for arena world shifts.
- **Key Methods:**
  - `StartTimer(float? customDuration)`: Starts/restarts countdown.
  - `ResumeTimer()`: Resumes ticking.
  - `StopTimer()`: Pauses countdown.
  - `ResetTimer(float? interval)`: Resets remaining seconds.
- **Key Properties:**
  - `RemainingTime { get; }`: Seconds left in cycle.
  - `TotalInterval { get; }`: Full cycle duration (default 20.0s).
  - `NormalizedProgress { get; }`: `1.0` to `0.0` (ideal for fill bars).
  - `IsRunning { get; }`: Running status.
- **Events:**
  - `OnTimerUpdated(float remainingTime)`: Dispatched each frame while ticking.
  - `OnTimerFinished`: Dispatched when timer reaches 0.0s.
- **Callers:** `GameManager` (auto-sync), `WorldManager`.
- **Subscribers:** `WorldManager` (triggers shifts), UI (timer text/bar), Audio (ticking SFX).

---

### 5. WorldManager (`DangerousArena.Gameplay.WorldManager`)
- **Purpose:** Coordinates the world mutation sequence when the timer expires.
- **Key Methods:**
  - `RequestWorldChange()`: Validates state and initiates world change.
  - `BeginWorldChange()`: Sets `GameState.ChangingWorld` and fires `OnWorldChangeStarted`.
  - `CompleteWorldChange()`: Increments `ShiftCount`, fires `OnWorldChanged`, restores `Playing`, restarts timer.
- **Key Properties:**
  - `Instance { get; }`: Singleton access.
  - `ShiftCount { get; }`: Total shifts completed.
  - `IsChangingWorld { get; }`: Active mutation flag.
  - `TransitionDuration { get; }`: Transition delay in seconds (default 1.5s).
- **Events:**
  - `OnWorldChangeStarted`: Fired when shift sequence begins.
  - `OnWorldChanged`: Fired when new layout is active and safe.
- **Callers:** `TimerManager` (on timer expired), Debug triggers, Level developer.
- **Subscribers:** Level Designer (arena shifting logic), UI, Camera shake, VFX/SFX.

---

### 6. Tile Interaction Interfaces (`DangerousArena.Gameplay`)
- **`IPlayerHazard`:** `void TriggerHazard(GameObject player);`
- **`IPlayerBonus`:** `void CollectBonus(PlayerController player);`
- **`IPlayerFinish`:** `void TriggerFinish(GameObject player);`
- **Drop-in Components:** `HazardTile`, `FinishTile`, `BonusTile` in `TileTriggers.cs`.

---

### 7. Player Death System
- Death is guarded against duplicate execution (`isAlive` check and `IsGameplayActive` check).
- Player input and velocity are stopped immediately.
- The GameObject is **not** destroyed, allowing instant replay without garbage collection spikes.

---

### 8. Finish & Win Detection
- Goal contact invokes `GameManager.Instance.PlayerReachedFinish()`.
- If `CurrentLevel < MaxLevels`: transitions to `LevelComplete`.
- If `CurrentLevel >= MaxLevels`: transitions to `Victory`.
- Movement is frozen; hazard triggers are locked out.

---

### 9. Bonus System (`BonusType`, `BonusData`, `BonusEvents`)
- Carries player context, bonus type, value, and duration without hardcoding bonus types in `PlayerController`.
- Dispatches via `BonusEvents.OnBonusTriggered(BonusData)` and `GameManager.OnBonusCollected`.

---

### 10. Level Progression System
- `CurrentLevel` begins at 1 and advances sequentially: 1 $\rightarrow$ 2 $\rightarrow$ 3.
- `LoadNextLevel()` advances levels and triggers `OnLevelLoadRequested`.
- `RestartLevel()` preserves `CurrentLevel` intact.
- Scene loading is decoupled via `ILevelLoader` and `OnLevelLoadRequested`.

---

## 3. Level Developer Integration Guide (Member 2)

### A. How to Create a Dangerous Tile (Red)
**Option 1: Add Drop-In Component**
Attach [`HazardTile`](file:///C:/Users/prani/Desktop/GameJam2026/Assets/Scripts/Gameplay/TileTriggers.cs) to any red platform with a Collider.

**Option 2: Implement Interface in Custom Tile Script**
```csharp
using UnityEngine;
using DangerousArena.Gameplay;
using DangerousArena.Player;

public class RedTile : MonoBehaviour, IPlayerHazard
{
    public void TriggerHazard(GameObject player)
    {
        // Kills player and triggers GameOver flow
        player.GetComponent<PlayerController>()?.Die();
    }
}
```

---

### B. How to Create a Bonus Tile (Yellow)
**Option 1: Add Drop-In Component**
Attach [`BonusTile`](file:///C:/Users/prani/Desktop/GameJam2026/Assets/Scripts/Gameplay/TileTriggers.cs). Configure `bonusType` (`Score`, `SpeedBoost`, `TemporaryProtection`, `ExtraTime`), `bonusValue`, and `bonusDuration`.

**Option 2: Implement Interface in Custom Tile Script**
```csharp
using UnityEngine;
using DangerousArena.Gameplay;
using DangerousArena.Player;

public class YellowTile : MonoBehaviour, IPlayerBonus
{
    [SerializeField] private BonusType type = BonusType.Score;
    [SerializeField] private float value = 100f;

    public void CollectBonus(PlayerController player)
    {
        // 1. Dispatch payload to game events
        BonusEvents.TriggerBonus(new BonusData(type, value, duration: 0f, player));

        // 2. Hide / consume pickup
        gameObject.SetActive(false);
    }
}
```

---

### C. How to Create a Finish Tile (Goal)
**Option 1: Add Drop-In Component**
Attach [`FinishTile`](file:///C:/Users/prani/Desktop/GameJam2026/Assets/Scripts/Gameplay/TileTriggers.cs) to the goal tile with a Collider.

**Option 2: Implement Interface in Custom Tile Script**
```csharp
using UnityEngine;
using DangerousArena.Gameplay;

public class GoalTile : MonoBehaviour, IPlayerFinish
{
    public void TriggerFinish(GameObject player)
    {
        // Automatically triggers LevelComplete or Victory depending on CurrentLevel
        GameManager.Instance.PlayerReachedFinish();
    }
}
```

---

### D. How to Respond to a World Change (Arena Shifts)
Subscribe to `WorldManager.Instance.OnWorldChangeStarted` to mutate safe/danger tiles:
```csharp
using UnityEngine;
using DangerousArena.Gameplay;

public class ArenaGrid : MonoBehaviour
{
    private void OnEnable()
    {
        if (WorldManager.Instance != null)
        {
            WorldManager.Instance.OnWorldChangeStarted += HandleWorldShift;
        }
    }

    private void OnDisable()
    {
        if (WorldManager.Instance != null)
        {
            WorldManager.Instance.OnWorldChangeStarted -= HandleWorldShift;
        }
    }

    private void HandleWorldShift()
    {
        // 1. Play telegraph warning / shake
        // 2. Mutate safe tiles into danger tiles
        // 3. Drop disappearing platforms
    }
}
```

---

### E. How to Tell WorldManager That Custom Shifting Finished
If your arena uses custom animations and you want full control over transition timing:
1. In the Inspector on `WorldManager`, uncheck `autoCompleteTransition`.
2. When your tile animations finish, call:
```csharp
WorldManager.Instance.CompleteWorldChange();
```

---

## 4. UI Developer Integration Guide (Member 3)

### A. Displaying Remaining Time & Progress
```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DangerousArena.Gameplay;

public class HUDTimer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Image timerFillBar;
    [SerializeField] private TimerManager timerManager;

    private void OnEnable()
    {
        if (timerManager != null)
        {
            timerManager.OnTimerUpdated += UpdateTimerDisplay;
        }
    }

    private void OnDisable()
    {
        if (timerManager != null)
        {
            timerManager.OnTimerUpdated -= UpdateTimerDisplay;
        }
    }

    private void UpdateTimerDisplay(float remainingSeconds)
    {
        if (timerText != null)
            timerText.text = $"{remainingSeconds:0.0}s";

        if (timerFillBar != null)
            timerFillBar.fillAmount = timerManager.NormalizedProgress;
    }
}
```

---

### B. Reacting to Game Over, Victory, and Level Complete
```csharp
using UnityEngine;
using DangerousArena.Gameplay;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject levelCompletePanel;

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += ShowGameOver;
            GameManager.Instance.OnVictory += ShowVictory;
            GameManager.Instance.OnLevelComplete += ShowLevelComplete;
            GameManager.Instance.OnLevelRestarted += HideAllPanels;
            GameManager.Instance.OnGameStarted += HideAllPanels;
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= ShowGameOver;
            GameManager.Instance.OnVictory -= ShowVictory;
            GameManager.Instance.OnLevelComplete -= ShowLevelComplete;
            GameManager.Instance.OnLevelRestarted -= HideAllPanels;
            GameManager.Instance.OnGameStarted -= HideAllPanels;
        }
    }

    private void ShowGameOver() => gameOverPanel.SetActive(true);
    private void ShowVictory() => victoryPanel.SetActive(true);
    private void ShowLevelComplete() => levelCompletePanel.SetActive(true);
    private void HideAllPanels()
    {
        gameOverPanel.SetActive(false);
        victoryPanel.SetActive(false);
        levelCompletePanel.SetActive(false);
    }
}
```

---

### C. Reacting to Level Changes & GameState Changes
```csharp
private void OnEnable()
{
    GameManager.Instance.OnLevelChanged += HandleLevelChanged;
    GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
}

private void HandleLevelChanged(int newLevel)
{
    levelText.text = $"LEVEL {newLevel}";
}

private void HandleGameStateChanged(GameState state)
{
    switch (state)
    {
        case GameState.ChangingWorld:
            warningBanner.SetActive(true); // "ARENA SHIFTING!"
            break;
        case GameState.Playing:
            warningBanner.SetActive(false);
            break;
    }
}
```

---

### D. Connecting UI Buttons (Start, Retry, Next Level)
Hook these directly to button `OnClick()` in Inspector or script:
```csharp
// Play / Start Button
public void OnClickStart()
{
    GameManager.Instance.StartGame();
}

// Retry / Restart Button
public void OnClickRestart()
{
    GameManager.Instance.RestartLevel();
}

// Next Level Button
public void OnClickNextLevel()
{
    GameManager.Instance.LoadNextLevel();
}
```
