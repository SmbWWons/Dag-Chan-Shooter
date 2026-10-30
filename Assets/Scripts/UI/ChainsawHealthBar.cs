using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ChainsawHealthBar : MonoBehaviour
{
    [SerializeField] private HealthController health;
    public int MaxCells => maxCells;
    [Header("UI")]
    [SerializeField] private Image greenFill;
    [SerializeField] private Image ultimateFill;
    [SerializeField] private Color ultimateChargingColor = new Color(1f, .05f, .09f, 1f);
    [SerializeField] private Color ultimateReadyColor = new Color(0f, 1f, .35f, 1f);
    public Image UltimateFill => ultimateFill;
    [SerializeField] private Image portraitImage;

    [Header("Портреты (от здорового к избитому)")]
    [SerializeField] private Sprite[] portraits;

    [Header("Помехи при смене портрета")]
    [SerializeField] private Sprite[] noiseFrames;
    [SerializeField] private float noiseDuration = 0.5f;

    [Header("Настройки")]
    [SerializeField] private int maxCells = 15;


    private int currentCells;
    private int currentPortraitIndex = -1;
    private Coroutine noiseRoutine;

    /// <summary>
    /// Если здоровье не назначено, ищет игрока и его HealthController.
    /// Подписывает интерфейс через BindHealth либо показывает полный запас ячеек при отсутствии контроллера.
    /// </summary>
    private void Start()
    {
        if (!health) { var player = FindFirstObjectByType<PlayerMovement>(); if (player) health = player.GetComponent<HealthController>(); }
        if (health) BindHealth(health); else SetCells(maxCells, true);
    }

    /// <summary>
    /// Отписывается от прежнего здоровья и подключается к Changed и UltimateChanged нового.
    /// Сразу синхронизирует максимум ХП, ячейки, портрет и полоску ульты, не ожидая следующего изменения.
    /// </summary>
    public void BindHealth(HealthController value)
    {
        if (health) { health.Changed -= OnHealthChanged; health.UltimateChanged -= OnUltimateChanged; }
        health = value;
        if (!health) return;
        maxCells = health.MaxHealth;
        health.Changed += OnHealthChanged;
        health.UltimateChanged += OnUltimateChanged;
        OnUltimateChanged(health.UltimateCharge, health.UltimateStatus);
        SetCells(health.CurrentHealth, true);
    }
    /// <summary>
    /// При повторном включении подключает уже назначенный HealthController.
    /// BindHealth предварительно снимает подписки, поэтому обработчики не накапливаются.
    /// </summary>
    private void OnEnable() { if (health) BindHealth(health); }
    /// <summary>
    /// Отписывает канвас от событий здоровья и ульты.
    /// Выключенный интерфейс больше не получает их обновления.
    /// </summary>
    private void OnDisable() { if (health) { health.Changed -= OnHealthChanged; health.UltimateChanged -= OnUltimateChanged; } }
    /// <summary>
    /// Выставляет заполнение полоски по переданному заряду.
    /// Зелёный цвет применяется только при Ready, остальные состояния используют цвет зарядки.
    /// </summary>
    private void OnUltimateChanged(float charge, HealthController.UltimateBarStatus status)
    {
        if (!ultimateFill) return;
        ultimateFill.fillAmount = charge;
        ultimateFill.color = status == HealthController.UltimateBarStatus.Ready ? ultimateReadyColor : ultimateChargingColor;
    }
    /// <summary>
    /// Обновляет максимальное число ячеек и передаёт текущее здоровье в SetCells.
    /// Урон этот обработчик самостоятельно не вычисляет.
    /// </summary>
    private void OnHealthChanged(int current, int maximum)
    {
        maxCells = maximum;
        SetCells(current);
    }
    /// <summary>
    /// При разрешённом игровом вводе реагирует на отладочные действия Damage и Heal.
    /// Изменение здоровья передаётся соответствующим методам и не выполняется на паузе.
    /// </summary>
    private void Update()
    {
        if(MonitorPauseController.InputBlocked)return;
        if(DagChanInput.DamagePressed)TakeDamage();
        if(DagChanInput.HealPressed)Heal();
    }

    /// <summary>
    /// Ограничивает число ячеек и вычисляет долю заполнения от максимального здоровья.
    /// Обновляет зелёную заливку и портрет с учётом запроса мгновенного отображения.
    /// </summary>
    public void SetCells(int cells, bool instant = false)
    {
        currentCells = Mathf.Clamp(cells, 0, maxCells);
        float fraction = (float)currentCells / maxCells;

        if (greenFill) greenFill.fillAmount = fraction;
        UpdatePortrait(fraction, instant);
    }

    /// <summary>
    /// Выбирает портрет по доле потерянных ХП и не переключает уже показанный вариант.
    /// Останавливает прежние помехи, затем меняет спрайт сразу либо запускает PlayNoise.
    /// </summary>
    private void UpdatePortrait(float fraction, bool instant)
    {
        if (portraitImage == null || portraits == null || portraits.Length == 0) return;

        // fraction = 1 -> первый спрайт, fraction = 0 -> последний
        int index = Mathf.FloorToInt((1f - fraction) * portraits.Length);
        index = Mathf.Clamp(index, 0, portraits.Length - 1);

        // Портрет не поменялся: помехи не нужны
        if (index == currentPortraitIndex) return;
        currentPortraitIndex = index;

        if (noiseRoutine != null)
        {
            StopCoroutine(noiseRoutine);
            noiseRoutine = null;
        }

        bool noNoise = noiseFrames == null || noiseFrames.Length == 0 || noiseDuration <= 0f;

        if (instant || noNoise || !isActiveAndEnabled)
        {
            portraitImage.sprite = portraits[index];
        }
        else
        {
            noiseRoutine = StartCoroutine(PlayNoise(portraits[index]));
        }
    }

    /// <summary>
    /// Показывает последовательность кадров помех в течение noiseDuration и затем целевой портрет.
    /// Использует независящее от паузы время, поэтому переход может завершиться при остановленной игре.
    /// </summary>
    private IEnumerator PlayNoise(Sprite target)
    {
        float timer = 0f;

        while (timer < noiseDuration)
        {
            int frame = Mathf.FloorToInt(timer / noiseDuration * noiseFrames.Length);
            frame = Mathf.Clamp(frame, 0, noiseFrames.Length - 1);

            portraitImage.sprite = noiseFrames[frame];

            timer += Time.unscaledDeltaTime; // работает и на паузе (timeScale = 0)
            yield return null;
        }

        portraitImage.sprite = target;
        noiseRoutine = null;
    }

    // Можно вызывать из других скриптов или из UI-кнопок
    /// <summary>
    /// Передаёт потерю ячеек HealthController, если он назначен.
    /// В запасном режиме без контроллера изменяет только локальное отображение через SetCells.
    /// </summary>
    public void TakeDamage(int cellsLost = 1)
    {
        if (health) health.TakeDamage(cellsLost); else SetCells(currentCells - cellsLost);
    }

    /// <summary>
    /// Передаёт восстановление ячеек HealthController.
    /// Без контроллера обновляет только локальное число отображаемых ячеек.
    /// </summary>
    public void Heal(int cellsGained = 1)
    {
        if (health) health.Heal(cellsGained); else SetCells(currentCells + cellsGained);
    }
}
