using System;

/// <summary>Модель здоровья: хранит ХП и правила их изменения, не зависит от Unity или интерфейса.</summary>
public sealed class HealthModel
{
    public int MaxHealth { get; }
    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;
    public event Action<int, int> Changed;
    public event Action Died;
    /// <summary>Создаёт полный запас здоровья с положительным максимумом.</summary>
    public HealthModel(int maximum) { MaxHealth = Math.Max(1, maximum); CurrentHealth = MaxHealth; }
    /// <summary>Применяет положительный урон живому объекту, сообщает об изменении и однократно о смерти.</summary>
    public bool TakeDamage(int amount)
    {
        if (amount <= 0 || IsDead) return false;
        CurrentHealth = Math.Max(0, CurrentHealth - amount);
        Changed?.Invoke(CurrentHealth, MaxHealth);
        if (IsDead) Died?.Invoke();
        return true;
    }
    /// <summary>Лечит живого персонажа до максимума; умершего не воскрешает.</summary>
    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead) return;
        CurrentHealth = (int)Math.Min(MaxHealth, (long)CurrentHealth + amount);
        Changed?.Invoke(CurrentHealth, MaxHealth);
    }
}
