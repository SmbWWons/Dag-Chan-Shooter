using UnityEngine;

// Input, animation and damage share this single cooldown gate.
public class WeaponAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField, Min(0.01f)] private float shotInterval = 0.3f;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Material laserMaterial;
    [SerializeField] private Camera viewModelCamera;
    [SerializeField] private Material muzzleFlashMaterial;
    [SerializeField, Min(.01f)] private float muzzleFlashDuration = .065f;
    [SerializeField, Min(.1f), Tooltip("Visual muzzle flash size multiplier")] private float muzzleFlashSize = 2f;
    private RevolverMuzzleFlash muzzleFlash;
    [SerializeField, Min(1f)] private float range = 150f;
    [SerializeField] private LayerMask hitMask = ~((1 << 2) | (1 << 5) | (1 << 6) | (1 << 7));
    [SerializeField, Min(0.01f)] private float laserLifetime = 0.05f;
    [Header("Shot audio")]
    [SerializeField] private AudioClip shotSound;
    [SerializeField,Range(0,1)] private float shotVolume=.65f;
    [SerializeField] private Vector2 shotPitchRange=new Vector2(.94f,1.06f);
    private RevolverShotPlaylist shotPlaylist;
    public AudioClip LastShotClip {get;private set;}
    private readonly AudioSource[] shotVoices=new AudioSource[16];
    private int nextVoice;
    public float LastShotPitch {get;private set;}
    public int ShotSoundsPlayed {get;private set;}
    private float nextShotTime;
    private HealthController ownerHealth;
    private static readonly int FireState = Animator.StringToHash("Base Layer.Fire");
    public DualWeaponController Coordinator { get; set; }
    public RevolverMuzzleFlash CurrentMuzzleFlash => muzzleFlash;
    public float ShotInterval => shotInterval;
    public int ShotsFired { get; private set; }

    /// <summary>
    /// Находит общий контроллер, плейлист, Animator, камеру прицеливания и здоровье владельца.
    /// Здоровье используется для запрета выстрелов после смерти и исключения попаданий в себя.
    /// </summary>
    private void Awake()
    {
        Coordinator=GetComponentInParent<DualWeaponController>();
        shotPlaylist=GetComponentInParent<RevolverShotPlaylist>();
        if (!animator) animator = GetComponent<Animator>();
        if (!aimCamera)
        {
            var player = GetComponentInParent<PlayerMovement>();
            aimCamera = player ? player.GetComponent<Camera>() : Camera.main;
        }
        ownerHealth = aimCamera ? aimCamera.GetComponentInParent<HealthController>() : null;
    }
    /// <summary>
    /// При паузе не обрабатывает огонь, а при наличии Coordinator оставляет это общей очереди.
    /// Самостоятельное оружие пробует стрелять при удержании Attack.
    /// </summary>
    private void Update()
    {
        if (MonitorPauseController.InputBlocked) return;
        if(Coordinator) return;
        if(DagChanInput.AttackHeld) TryFire();
    }
    /// <summary>
    /// Передаёт запрос общей очереди двух рук либо выполняет локальную проверку одиночного оружия.
    /// Возвращает результат выбранного способа стрельбы.
    /// </summary>
    public bool TryFire() { return Coordinator ? Coordinator.TryFire() : TryFireLocal(); }
    /// <summary>
    /// Запрещает обычный выстрел во время занятой ульты и локальной задержки.
    /// При выполнении условий вызывает EmitShot с обычной отдачей и звуком.
    /// </summary>
    internal bool TryFireLocal()
    {
        if (Coordinator && Coordinator.Ultimate && Coordinator.Ultimate.IsBusy) return false;
        if (Time.time < nextShotTime) return false;
        return EmitShot(true);
    }
    /// <summary>
    /// Разрешает выстрел только при связи с контроллером и активной фазе ульты.
    /// Вызывает EmitShot без обычной отдачи и звука; частоту парных залпов определяет контроллер ульты.
    /// </summary>
    internal bool TryFireUltimate()
    {
        if (!Coordinator || !Coordinator.Ultimate || Coordinator.Ultimate.Phase != UltimateAttackController.UltimatePhase.Active) return false;
        return EmitShot(false);
    }
    /// <summary>
    /// Проверяет камеру, владельца и ввод, запускает задержку и при необходимости отдачу.
    /// Первый луч даёт медведю попытку уклонения, второй определяет попадание и снимает одно ХП.
    /// Затем создаёт звук обычного выстрела, вспышку и видимую линию от дула; линия не является летящей пулей.
    /// </summary>
    private bool EmitShot(bool animateRecoil)
    {
        if (MonitorPauseController.InputBlocked || !isActiveAndEnabled || !aimCamera || (ownerHealth && ownerHealth.IsDead)) return false;
        nextShotTime = Time.time + shotInterval;
        ShotsFired++;
        if (animateRecoil && animator && animator.HasState(0, FireState)) animator.Play(FireState, 0, 0f);
        var ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        Physics.SyncTransforms();
        if (FindHit(ray, out var initial))
        {
            var bear = initial.collider.GetComponentInParent<GummyBearEnemy>();
            if (bear) bear.TryDodge(ray.direction);
        }
        // Dodge is resolved before this second query, in the same shot/frame.
        Physics.SyncTransforms();
        Vector3 end = ray.GetPoint(range);
        if (FindHit(ray, out var hit))
        {
            end = hit.point;
            var health = hit.collider.GetComponentInParent<HealthController>();
            if (health && health != ownerHealth) health.TakeDamage(1, hit.point, ray.direction);
        }
        if(animateRecoil)PlayShotSound(true);
        ShowMuzzleFlash();
        DrawLaser(muzzle ? muzzle.position : aimCamera.transform.TransformPoint(new Vector3(.25f, -.18f, .7f)), end);
        return true;
    }
    /// <summary>
    /// Проверяет столкновения луча с учётом дальности и слоёв, игнорируя триггеры и владельца.
    /// Выбирает ближайшее допустимое попадание и возвращает false при его отсутствии.
    /// </summary>
    private bool FindHit(Ray ray, out RaycastHit nearest)
    {
        nearest = default;
        float closest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(ray, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            if (ownerHealth && hit.collider.GetComponentInParent<HealthController>() == ownerHealth) continue;
            if (hit.distance >= closest) continue;
            closest = hit.distance;
            nearest = hit;
        }
        return !float.IsPositiveInfinity(closest);
    }
    /// <summary>
    /// Берёт клип из плейлиста или запасной звук и выбирает свободный AudioSource из пула.
    /// Меняет pitch, избегая слишком похожего предыдущего значения, применяет громкость эффектов и запускает звук отдельно от уже звучащих хвостов.
    /// </summary>
    private void PlayShotSound(bool normalShot)
    {
        var clip=shotPlaylist?shotPlaylist.NextClip():shotSound;
        if(!clip)return;
        LastShotClip=clip;
        AudioSource voice=null;
        for(int i=0;i<shotVoices.Length;i++)
        {
            int index=(nextVoice+i)%shotVoices.Length;
            if(shotVoices[index] && shotVoices[index].isPlaying)continue;
            voice=shotVoices[index];
            if(!voice)
            {
                var go=new GameObject("Revolver Shot Voice "+index);go.transform.SetParent(transform,false);
                voice=go.AddComponent<AudioSource>();voice.playOnAwake=false;voice.loop=false;voice.spatialBlend=0;
                voice.dopplerLevel=0;voice.reverbZoneMix=0;voice.priority=64;shotVoices[index]=voice;
            }
            nextVoice=(index+1)%shotVoices.Length;break;
        }
        // Independent voices keep the pitch of already playing tails unchanged during rapid fire.
        if(!voice){voice=shotVoices[nextVoice];nextVoice=(nextVoice+1)%shotVoices.Length;voice.Stop();}
        float low=Mathf.Clamp(Mathf.Min(shotPitchRange.x,shotPitchRange.y),.5f,1.5f);
        float high=Mathf.Clamp(Mathf.Max(shotPitchRange.x,shotPitchRange.y),low,1.5f);
        float pitch=Random.Range(low,high);
        if(high-low>.025f && Mathf.Abs(pitch-LastShotPitch)<.012f)pitch=low+Mathf.Repeat(pitch-low+(high-low)*.5f,high-low);
        LastShotPitch=pitch;
        voice.clip=clip;voice.pitch=pitch;var gain=voice.GetComponent<DagChanEffectVolume>();if(!gain)gain=voice.gameObject.AddComponent<DagChanEffectVolume>();gain.SetBaseVolume(shotVolume*(normalShot?1f:.6f));voice.Play();ShotSoundsPlayed++;
    }
    /// <summary>
    /// Скрывает вспышку и останавливает все созданные звуковые голоса оружия.
    /// Выключенный револьвер не оставляет эти эффекты активными.
    /// </summary>
    private void OnDisable()
    {
        if(muzzleFlash)muzzleFlash.Hide();
        foreach(var voice in shotVoices)if(voice)voice.Stop();
    }
    /// <summary>
    /// Удаляет отдельный объект вспышки при уничтожении оружия.
    /// Он находится вне иерархии модели и требует явной очистки.
    /// </summary>
    private void OnDestroy() { if(muzzleFlash) Destroy(muzzleFlash.gameObject); }
    /// <summary>
    /// При наличии дула и материала один раз создаёт и настраивает вспышку.
    /// Последующие выстрелы используют тот же объект с заданными размером и временем жизни.
    /// </summary>
    private void ShowMuzzleFlash()
    {
        if(!muzzle || !muzzleFlashMaterial) return;
        if(!muzzleFlash)
        {
            var go=new GameObject("Revolver Muzzle Flash");
            muzzleFlash=go.AddComponent<RevolverMuzzleFlash>();
            muzzleFlash.Initialize(muzzleFlashMaterial,muzzle.gameObject.layer,muzzle,viewModelCamera ? viewModelCamera : aimCamera);
        }
        muzzleFlash.Show(muzzleFlashDuration, muzzleFlashSize);
    }
    /// <summary>
    /// Согласует видимое положение дула между камерами и создаёт линию до попадания.
    /// Назначает толщину и материал, добавляет стирание через RevolverLaserAnchor и планирует удаление.
    /// </summary>
    private void DrawLaser(Vector3 start, Vector3 end)
    {
        // The weapon camera has a different FOV. Match the visible muzzle projection.
        if(viewModelCamera && aimCamera && viewModelCamera != aimCamera)
            start=aimCamera.ViewportToWorldPoint(viewModelCamera.WorldToViewportPoint(start));
        var trace = new GameObject("Revolver Laser");
        var line = trace.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = .055f;
        line.endWidth = .025f;
        line.numCapVertices = 3;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = laserMaterial;
        line.startColor = line.endColor = new Color(1f, .16f, .025f, 1f);
        if(muzzle) trace.AddComponent<RevolverLaserAnchor>().Bind(muzzle,aimCamera,viewModelCamera,line,laserLifetime);
        Destroy(trace, laserLifetime);
    }
}












