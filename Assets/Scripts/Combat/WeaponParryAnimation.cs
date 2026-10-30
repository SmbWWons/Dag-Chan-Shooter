using UnityEngine;

// Plays the weapon gesture only. No blocking, invulnerability or reflected damage.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class WeaponParryAnimation : MonoBehaviour
{
    [SerializeField] private Animator rightAnimator;
    [SerializeField] private Animator leftAnimator;
    [SerializeField] private DualWeaponController weapons;
    [SerializeField, Min(.01f)] private float duration = .5f;
    private float finishTime;
    private bool playing;
    private static readonly int ParryState = Animator.StringToHash("Base Layer.Parry");
    private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
    public bool IsPlaying => playing && Time.time < finishTime;
    public float Duration => duration;

    /// <summary>
    /// Сохраняет Animator обеих рук, контроллер оружия и длительность жеста.
    /// Инструменты настройки используют это для связи с конкретными моделями.
    /// </summary>
    public void Configure(Animator right, Animator left, DualWeaponController coordinator, float seconds)
    {
        rightAnimator = right;
        leftAnimator = left;
        weapons = coordinator;
        duration = seconds;
    }

    /// <summary>
    /// Пропускает ввод на паузе, завершает флаг истёкшей анимации и реагирует на Parry.
    /// Возможность начала проверяется внутри TryPlay.
    /// </summary>
    private void Update()
    {
        if (MonitorPauseController.InputBlocked) return;
        if (playing && Time.time >= finishTime) playing = false;
        if(DagChanInput.ParryPressed) TryPlay();
    }

    /// <summary>
    /// Проверяет доступность состояний Animator, повторное проигрывание, задержку выстрела и занятость ультой.
    /// Запускает жест обеих рук и его таймер; отражения урона и неуязвимости здесь нет.
    /// </summary>
    public bool TryPlay()
    {
        if (MonitorPauseController.InputBlocked || !isActiveAndEnabled || IsPlaying || !rightAnimator || !leftAnimator ||
            !rightAnimator.HasState(0, ParryState) || !leftAnimator.HasState(0, ParryState)) return false;
        if (weapons && (!weapons.IsReadyToFire || (weapons.Ultimate && weapons.Ultimate.IsBusy))) return false;
        rightAnimator.Play(ParryState, 0, 0f);
        leftAnimator.Play(ParryState, 0, 0f);
        finishTime = Time.time + duration;
        playing = true;
        return true;
    }

    /// <summary>
    /// Сбрасывает незавершённый жест и при наличии Idle возвращает руки в обычное состояние.
    /// Выключение компонента не оставляет флаг проигрывания активным.
    /// </summary>
    private void OnDisable()
    {
        if (!playing) return;
        playing = false;
        if (rightAnimator && rightAnimator.HasState(0, IdleState)) rightAnimator.Play(IdleState, 0, 0f);
        if (leftAnimator && leftAnimator.HasState(0, IdleState)) leftAnimator.Play(IdleState, 0, 0f);
    }
}
