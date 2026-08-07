using UnityEngine;

/// <summary>
/// Fake game API for PlaytestRunner integration tests.
/// Simulates a simple game with player, currency, and cargo.
/// </summary>
public class TestPlayableAPI : MonoBehaviour
{
    public float health = 100f;
    public float money = 0f;
    public int cargoCount = 0;
    public bool isMoving = false;
    public bool isAlive = true;
    public string playerName = "TestPlayer";

    public float GetHealth() => health;
    public float GetMoney() => money;
    public int GetCargoCount() => cargoCount;
    public bool GetIsMoving() => isMoving;

    public void AddMoney(float amount) { money += amount; }
    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0) { health = 0; isAlive = false; }
    }
    public void AddCargo(int count) { cargoCount += count; }
    public void ClearCargo() { cargoCount = 0; }
    public void Heal(float amount) { health = Mathf.Min(100, health + amount); }

    public string GetStatus()
    {
        return $"hp={health} money={money} cargo={cargoCount}";
    }
}
