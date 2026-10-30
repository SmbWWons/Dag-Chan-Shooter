using System;
using UnityEngine;

// One point of authority for health. One unit is one cell on the existing HUD.
[DisallowMultipleComponent]
public class HealthController : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 15;
    [SerializeField] private bool disappearOnDeath;
    public enum UltimateBarStatus { Charging, Ready, Active }
    public float UltimateCharge { get; private set; } = 1f;
    public UltimateBarStatus UltimateStatus { get; private set; } = UltimateBarStatus.Ready;
    public event Action<float, UltimateBarStatus> UltimateChanged;
    /// <summary>
    /// Ограничивает заряд ульты диапазоном от 0 до 1.
    /// Только при изменении заряда или статуса сохраняет данные и уведомляет интерфейс событием UltimateChanged.
    /// </summary>
    public void SetUltimateStatus(float charge, UltimateBarStatus status)
    {
        charge = Mathf.Clamp01(charge);
        if (Mathf.Approximately(UltimateCharge, charge) && UltimateStatus == status) return;
        UltimateCharge = charge; UltimateStatus = status;
        UltimateChanged?.Invoke(charge, status);
    }
    private int currentHealth;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0;
    public event Action<int, int> Changed;
    public event Action Died;
    public event Action<Vector3, Vector3> Damaged;

    /// <summary>
    /// Заполняет текущее здоровье из maxHealth, не допуская значения меньше единицы.
    /// Выполняется отдельно у игрока и каждого созданного врага.
    /// </summary>
    private void Awake() { currentHealth = Mathf.Max(1, maxHealth); }
    /// <summary>
    /// Упрощённая перегрузка для урона без точного попадания.
    /// Передаёт количество, позицию объекта и нулевое направление полной версии TakeDamage и возвращает её результат.
    /// </summary>
    public bool TakeDamage(int amount = 1) => TakeDamage(amount, transform.position, Vector3.zero);
    /// <summary>
    /// Отклоняет неположительный урон и удары по мёртвому объекту, затем уменьшает ХП до минимума ноль.
    /// Сначала вызывает Changed и Damaged, затем при смерти Died и при disappearOnDeath скрывает и удаляет объект.
    /// Такой порядок даёт эффекту кусочков получить данные до удаления врага.
    /// </summary>
    public bool TakeDamage(int amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (amount <= 0 || IsDead) return false;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        Changed?.Invoke(currentHealth, maxHealth);
        Damaged?.Invoke(hitPoint, hitDirection);
        if (IsDead)
        {
            Died?.Invoke();
            if (disappearOnDeath)
            {
                // Hide and remove colliders immediately; Destroy completes at end of frame.
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }
        return true;
    }
    /// <summary>
    /// Восстанавливает положительное количество ХП живому объекту, ограничивая результат максимумом.
    /// Вызывает Changed; воскресить умершего этим методом нельзя.
    /// </summary>
    public void Heal(int amount = 1)
    {
        if (amount <= 0 || IsDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        Changed?.Invoke(currentHealth, maxHealth);
    }
    /// <summary>
    /// При редактировании Inspector исправляет maxHealth, если значение меньше единицы.
    /// Текущее игровое здоровье этот метод не пересчитывает.
    /// </summary>
    private void OnValidate() { maxHealth = Mathf.Max(1, maxHealth); }
}
