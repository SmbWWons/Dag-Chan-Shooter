using UnityEngine;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(DualWeaponController))]
public sealed class UltimateAttackController : MonoBehaviour
{
    public enum UltimatePhase { Ready, Connecting, Active, Disconnecting, Cooldown }
    [SerializeField] private Animator rightAnimator;
    [SerializeField] private Animator leftAnimator;
    [SerializeField, Min(.01f)] private float connectDuration = 1.35f;
    [SerializeField, Min(.01f)] private float disconnectDuration = .5f;
    [SerializeField, Min(.01f)] private float activeDuration = 10f;
    [SerializeField, Min(.01f)] private float cooldown = 30f;
    [SerializeField, Min(.01f)] private float burstInterval = .05f;
    [Header("Activation voice")]
    [SerializeField] private AudioClip activationVoice;
    [SerializeField,Range(0,1)] private float activationVolume=.85f;
    private AudioSource activationSource;
    public int ActivationSoundsPlayed {get;private set;}
    private DualWeaponController weapons;
    private WeaponParryAnimation parry;
    private HealthController health;
    private float phaseEndsAt, nextBurstAt, readyAt;
    private static readonly int Connect = Animator.StringToHash("Base Layer.UltJoin");
    private static readonly int Hold = Animator.StringToHash("Base Layer.UltHold");
    private static readonly int Disconnect = Animator.StringToHash("Base Layer.UltSplit");
    private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
    public UltimatePhase Phase { get; private set; }
    public bool IsBusy => Phase == UltimatePhase.Connecting || Phase == UltimatePhase.Active || Phase == UltimatePhase.Disconnecting;
    public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);
    public float ActiveDuration => activeDuration;
    public float Cooldown => cooldown;
    public float BurstInterval => burstInterval;
    public int BurstsFired { get; private set; }

    public UltimateModel Model { get; } = new UltimateModel();

    public float ActiveRemaining => Phase == UltimatePhase.Active ? Mathf.Max(0f, phaseEndsAt - Time.time) : 0f;
    /// <summary>
    /// После игровой логики передаёт актуальное состояние полоски в отдельную UltimateModel.
    /// Оттуда обновление получает интерфейс.
    /// </summary>
    private void LateUpdate() { PublishBarStatus(); }
    /// <summary>
    /// Вычисляет заряд от 0 до 1 и статус по фазе ульты и таймерам.
    /// При готовности и соединении заряд полный, в активной фазе убывает, при разъединении пустой, на перезарядке растёт.
    /// </summary>
    private void PublishBarStatus()
    {
        float charge = Phase == UltimatePhase.Active ? ActiveRemaining / activeDuration :
            Phase == UltimatePhase.Disconnecting ? 0f :
            Phase == UltimatePhase.Cooldown ? 1f - CooldownRemaining / cooldown : 1f;
        var status = Phase == UltimatePhase.Ready ? UltimateBarStatus.Ready :
            IsBusy ? UltimateBarStatus.Active : UltimateBarStatus.Charging;
        Model.SetStatus(charge, status);
    }

    /// <summary>
    /// Находит общий контроллер оружия, парирование и здоровье на том же объекте.
    /// Эти ссылки используются в проверках активации и смерти игрока.
    /// </summary>
    private void Awake()
    {
        weapons = GetComponent<DualWeaponController>();
        parry = GetComponent<WeaponParryAnimation>();
        health = GetComponent<HealthController>();
    }

    /// <summary>
    /// Назначает Animator обеих рук и время соединения и разъединения.
    /// Длительность активной фазы и перезарядки этим методом не меняется.
    /// </summary>
    public void Configure(Animator right, Animator left, float joinSeconds, float splitSeconds)
    {
        rightAnimator = right;
        leftAnimator = left;
        connectDuration = joinSeconds;
        disconnectDuration = splitSeconds;
    }

    /// <summary>
    /// Учитывает паузу и смерть, читает действия активации и удержания огня.
    /// По таймерам переключает соединение, активную фазу, разъединение и перезарядку; залпы разрешены только в активной фазе.
    /// </summary>
    private void Update()
    {
        if (MonitorPauseController.InputBlocked) return;
        if (health && health.IsDead) { if (IsBusy) Cancel(); return; }
        bool activate=DagChanInput.UltimatePressed, held=DagChanInput.AttackHeld;
        if (activate) TryActivate();
        if (Phase == UltimatePhase.Connecting && Time.time >= phaseEndsAt)
        {
            Phase = UltimatePhase.Active;
            phaseEndsAt = Time.time + activeDuration;
            nextBurstAt = Time.time;
            Play(Hold);
        }
        if (Phase == UltimatePhase.Active)
        {
            if (Time.time >= phaseEndsAt)
            {
                Phase = UltimatePhase.Disconnecting;
                phaseEndsAt = Time.time + disconnectDuration;
                Play(Disconnect);
            }
            else if (held) TryFireBurst();
            else nextBurstAt = Time.time;
        }
        if (Phase == UltimatePhase.Disconnecting && Time.time >= phaseEndsAt)
        {
            Play(Idle);
            Phase = UltimatePhase.Cooldown;
            readyAt = Time.time + cooldown;
        }
        if (Phase == UltimatePhase.Cooldown && Time.time >= readyAt) Phase = UltimatePhase.Ready;
    }

    /// <summary>
    /// Проверяет готовность способности, оружия и здоровья, а также наличие нужных состояний Animator.
    /// Запускает соединение и реплику с громкостью эффектов, обновляет полоску и возвращает true только при успехе.
    /// </summary>
    public bool TryActivate()
    {
        if (MonitorPauseController.InputBlocked) return false;
        if (!isActiveAndEnabled || IsBusy || Time.time < readyAt || (health && health.IsDead) ||
            !weapons || !weapons.IsReadyToFire || (parry && parry.IsPlaying) || !rightAnimator || !leftAnimator) return false;
        foreach (int state in new[] { Connect, Hold, Disconnect })
            if (!rightAnimator.HasState(0, state) || !leftAnimator.HasState(0, state)) return false;
        Phase = UltimatePhase.Connecting;
        phaseEndsAt = Time.time + connectDuration;
        Play(Connect);
        if(activationVoice)
        {
            if(!activationSource)
            {
                var go=new GameObject("Ultimate Activation Voice");go.transform.SetParent(transform,false);
                activationSource=go.AddComponent<AudioSource>();activationSource.playOnAwake=false;activationSource.spatialBlend=0;
                activationSource.dopplerLevel=0;activationSource.reverbZoneMix=0;activationSource.priority=48;
            }
            activationSource.clip=activationVoice;activationSource.pitch=1;var gain=activationSource.GetComponent<DagChanEffectVolume>();if(!gain)gain=activationSource.gameObject.AddComponent<DagChanEffectVolume>();gain.SetBaseVolume(activationVolume);
            activationSource.Play();ActivationSoundsPlayed++;
        }
        PublishBarStatus();
        return true;
    }

    // The shared burst gate emits exactly one hitscan from each muzzle.
    /// <summary>
    /// После очередной задержки вызывает по одному выстрелу обеих рук в активной фазе.
    /// Назначает следующий момент залпа и увеличивает счётчик только при успешном выстреле обоих револьверов.
    /// </summary>
    public bool TryFireBurst()
    {
        if (!isActiveAndEnabled || Phase != UltimatePhase.Active || Time.time >= phaseEndsAt || Time.time < nextBurstAt ||
            (health && health.IsDead) || !weapons.RightWeapon || !weapons.LeftWeapon ||
            !weapons.RightWeapon.isActiveAndEnabled || !weapons.LeftWeapon.isActiveAndEnabled) return false;
        nextBurstAt = Time.time + burstInterval;
        bool right = weapons.RightWeapon.TryFireUltimate();
        bool left = weapons.LeftWeapon.TryFireUltimate();
        if (right && left) BurstsFired++;
        return right && left;
    }

    /// <summary>
    /// Запускает заданное состояние с начала на обоих существующих Animator.
    /// Так обе руки переходят к одной фазе анимации одновременно.
    /// </summary>
    private void Play(int state)
    {
        if (rightAnimator) rightAnimator.Play(state, 0, 0f);
        if (leftAnimator) leftAnimator.Play(state, 0, 0f);
    }
    /// <summary>
    /// Останавливает реплику, возвращает оружие в Idle и назначает полную перезарядку от текущего времени.
    /// Сразу обновляет состояние полоски.
    /// </summary>
    private void Cancel()
    {
        if(activationSource)activationSource.Stop();
        Play(Idle);
        Phase = UltimatePhase.Cooldown;
        readyAt = Time.time + cooldown;
        PublishBarStatus();
    }
    /// <summary>
    /// При выключении отменяет занятую ульту и останавливает её реплику.
    /// Это не оставляет активную способность или звук после отключения объекта.
    /// </summary>
    private void OnDisable() { if (IsBusy) Cancel(); if(activationSource)activationSource.Stop(); }
}
