using System;

public enum UltimateBarStatus { Charging, Ready, Active }
/// <summary>Данные полоски ульты. Таймеры вычисляет контроллер, а отображение наблюдает за Changed.</summary>
public sealed class UltimateModel
{
    public float Charge { get; private set; } = 1f;
    public UltimateBarStatus Status { get; private set; } = UltimateBarStatus.Ready;
    public event Action<float, UltimateBarStatus> Changed;
    /// <summary>Ограничивает заряд и уведомляет подписчиков только при изменении данных.</summary>
    public void SetStatus(float charge, UltimateBarStatus status)
    {
        charge = float.IsNaN(charge) ? 0f : Math.Max(0f, Math.Min(1f, charge));
        if (Math.Abs(Charge - charge) < .000001f && Status == status) return;
        Charge = charge; Status = status; Changed?.Invoke(charge, status);
    }
}
