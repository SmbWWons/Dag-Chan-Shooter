using System;
using UnityEngine;

/// <summary>Принимает команды урона и лечения, изменяет модель и выполняет действия Unity при смерти.</summary>
[DisallowMultipleComponent]
public class HealthController : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 15;
    [SerializeField] private bool disappearOnDeath;
    private HealthModel model;
    private bool applyingDamage;
    private Vector3 hitPoint, hitDirection;
    public HealthModel Model { get { EnsureModel(); return model; } }
    public int MaxHealth => Model.MaxHealth;
    public int CurrentHealth => Model.CurrentHealth;
    public bool IsDead => Model.IsDead;
    public event Action<int, int> Changed;
    public event Action Died;
    public event Action<Vector3, Vector3> Damaged;

    /// <summary>Создаёт модель до первого игрового действия; повторный вызов не сбрасывает ХП.</summary>
    private void Awake() { EnsureModel(); }
    /// <summary>Связывает события чистой модели с эффектами и существующими игровыми подписчиками.</summary>
    private void EnsureModel()
    {
        if (model != null) return;
        model = new HealthModel(maxHealth);
        model.Changed += OnModelChanged;
        model.Died += OnModelDied;
    }
    /// <summary>Передаёт новые ХП и данные попадания до события смерти, чтобы успели появиться желейные кусочки.</summary>
    private void OnModelChanged(int current, int maximum)
    {
        Changed?.Invoke(current, maximum);
        if (applyingDamage) Damaged?.Invoke(hitPoint, hitDirection);
    }
    /// <summary>Оповещает игровые системы и при необходимости удаляет погибшего врага.</summary>
    private void OnModelDied()
    {
        Died?.Invoke();
        if (disappearOnDeath) { gameObject.SetActive(false); Destroy(gameObject); }
    }
    /// <summary>Применяет урон без точной точки попадания.</summary>
    public bool TakeDamage(int amount = 1) => TakeDamage(amount, transform.position, Vector3.zero);
    /// <summary>Запоминает координаты попадания для эффектов и передаёт изменение ХП модели.</summary>
    public bool TakeDamage(int amount, Vector3 point, Vector3 direction)
    {
        hitPoint = point; hitDirection = direction; applyingDamage = true;
        try { return Model.TakeDamage(amount); }
        finally { applyingDamage = false; }
    }
    /// <summary>Передаёт модели команду лечения.</summary>
    public void Heal(int amount = 1) { Model.Heal(amount); }
    /// <summary>Обрабатывает отладочные действия только для игрока и только при разрешённом вводе.</summary>
    private void Update()
    {
        if (!GetComponent<PlayerMovement>() || MonitorPauseController.InputBlocked) return;
        if (DagChanInput.DamagePressed) TakeDamage();
        if (DagChanInput.HealPressed) Heal();
    }
    /// <summary>Ограничивает настройку максимума в Inspector.</summary>
    private void OnValidate() { maxHealth = Mathf.Max(1, maxHealth); }
    /// <summary>Снимает внутренние подписки при удалении компонента.</summary>
    private void OnDestroy()
    {
        if (model == null) return;
        model.Changed -= OnModelChanged; model.Died -= OnModelDied;
    }
}
