using UnityEngine;
[DisallowMultipleComponent]
public class DualWeaponController : MonoBehaviour
{
    [SerializeField] private WeaponAnimation rightWeapon;
    [SerializeField] private WeaponAnimation leftWeapon;
    [SerializeField, Min(.01f)] private float shotInterval=.53f;
    private float nextShotTime;
    private bool nextIsLeft;
    private WeaponParryAnimation parry;
    private UltimateAttackController ultimate;
    public UltimateAttackController Ultimate => ultimate;
    public bool IsReadyToFire => Time.time >= nextShotTime;
    public bool NextIsLeft => nextIsLeft;
    public WeaponAnimation RightWeapon => rightWeapon;
    public WeaponAnimation LeftWeapon => leftWeapon;
    /// <summary>
    /// Связывает револьверы с общим контроллером и находит парирование и ульту.
    /// Их состояния затем запрещают несовместимые действия.
    /// </summary>
    private void Awake() { BindWeapons(); parry=GetComponent<WeaponParryAnimation>(); ultimate=GetComponent<UltimateAttackController>(); }
    /// <summary>
    /// Назначает обе руки и общую задержку, ограниченную положительным значением.
    /// Обновляет обратные ссылки оружия через BindWeapons.
    /// </summary>
    public void Configure(WeaponAnimation right, WeaponAnimation left, float interval)
    {
        rightWeapon=right;leftWeapon=left;shotInterval=Mathf.Max(.01f,interval);BindWeapons();
    }
    /// <summary>
    /// Записывает себя в Coordinator каждого существующего револьвера.
    /// Поэтому ввод обрабатывается общей очередью, а не независимо двумя руками.
    /// </summary>
    private void BindWeapons()
    {
        if(rightWeapon) rightWeapon.Coordinator=this;
        if(leftWeapon) leftWeapon.Coordinator=this;
    }
    /// <summary>
    /// При разрешённом вводе каждый кадр удержания Attack вызывает TryFire.
    /// Фактическую частоту стрельбы ограничивает таймер внутри TryFire.
    /// </summary>
    private void Update()
    {
        if (MonitorPauseController.InputBlocked) return;
        if(DagChanInput.AttackHeld) TryFire();
    }
    /// <summary>
    /// Проверяет паузу, задержку, парирование и ульту, затем выбирает следующую руку.
    /// Только успешный выстрел запускает общий таймер и переключает очередь; возвращаемое значение сообщает результат попытки.
    /// </summary>
    public bool TryFire()
    {
        if(MonitorPauseController.InputBlocked || !isActiveAndEnabled || Time.time<nextShotTime || (parry && parry.IsPlaying) || (ultimate && ultimate.IsBusy)) return false;
        var weapon=nextIsLeft ? leftWeapon : rightWeapon;
        if(!weapon || !weapon.isActiveAndEnabled || !weapon.TryFireLocal()) return false;
        nextShotTime=Time.time+shotInterval;
        nextIsLeft=!nextIsLeft;
        return true;
    }
}

