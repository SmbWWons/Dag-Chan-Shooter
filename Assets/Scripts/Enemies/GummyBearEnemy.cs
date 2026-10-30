using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GummyBearTint), typeof(GummyBearHitEffects))]
[RequireComponent(typeof(Animator), typeof(CharacterController), typeof(HealthController))]
public class GummyBearEnemy : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField, Range(0f, 1f)] private float dodgeChance = .2f;
    [SerializeField, Min(.1f)] private float dodgeDistance = 1.7f;
    [SerializeField, Min(.1f)] private float attackRange = 4.6f;
    [SerializeField, Min(.1f)] private float damageRange = 1.65f;
    [SerializeField, Min(.1f)] private float stopDistance = 1.05f;
    [SerializeField, Min(.1f)] private float attackCooldown = 1.5f;
    [SerializeField] private float gravity = -22f;
    [SerializeField, Min(.1f)] private float hopAnimationSpeed = 1.2f;
    [SerializeField, Min(.1f)] private float hopDistanceMultiplier = 1.875f;
    private Animator animator;
    private CharacterController body;
    private HealthController health;
    private HealthController targetHealth;
    private float fallSpeed;
    private float nextAttackTime;
    private float dodgeUntil;
    private float nextTargetSearch;
    private bool attackRequested;
    private bool attackStarted;
    private bool attackHit;
    private bool airborne;
    public Transform Target => target;
    public Vector3 SteeringDirection {get;set;}
    public bool HasClearAttackPath {get;set;} = true;
    public int DodgesPerformed { get; private set; }
    public int SuccessfulAttacks { get; private set; }
    private static readonly int Moving = Animator.StringToHash("Moving");
    private static readonly int Attack = Animator.StringToHash("Attack");

    /// <summary>
    /// Получает Animator, CharacterController и здоровье и обеспечивает компоненты цвета и желейных эффектов.
    /// Включает Root Motion с заданной скоростью и обновляет ссылку на здоровье цели.
    /// </summary>
    private void Awake()
    {
        if(!GetComponent<GummyBearTint>()) gameObject.AddComponent<GummyBearTint>();
        if(!GetComponent<GummyBearHitEffects>()) gameObject.AddComponent<GummyBearHitEffects>();
        animator = GetComponent<Animator>();
        body = GetComponent<CharacterController>();
        health = GetComponent<HealthController>();
        animator.applyRootMotion = true; animator.speed = hopAnimationSpeed;
        ResolveTargetHealth();
    }
    /// <summary>
    /// Назначает цель преследования и сразу находит её здоровье.
    /// Арена вызывает этот метод после создания медведя.
    /// </summary>
    public void SetTarget(Transform value) { target = value; ResolveTargetHealth(); }
    /// <summary>
    /// Ищет HealthController у цели или её родителей.
    /// Если цели нет, очищает ссылку на здоровье прежнего объекта.
    /// </summary>
    private void ResolveTargetHealth() { targetHealth = target ? target.GetComponentInParent<HealthController>() : null; }
    /// <summary>
    /// Если цель не назначена, не чаще раза в полсекунды ищет PlayerMovement.
    /// При его отсутствии использует главную камеру как запасной вариант и вызывает SetTarget.
    /// </summary>
    private void FindPlayer()
    {
        if (target || Time.time < nextTargetSearch) return;
        nextTargetSearch = Time.time + .5f;
        var player = FindFirstObjectByType<PlayerMovement>();
        if (player) SetTarget(player.transform);
        else if (Camera.main) SetTarget(Camera.main.transform);
    }
    /// <summary>
    /// Проверяет цель и здоровье, читает Animator и один раз наносит урон в нужной фазе атаки.
    /// На земле разворачивает врага по навигации, учитывает уклонение и проверяет дальность, задержку и свободный путь для атаки.
    /// Параметр Moving запускает обычные прыжки преследования.
    /// </summary>
    private void Update()
    {
        FindPlayer();
        if (!target || health.IsDead || (targetHealth && targetHealth.IsDead))
        {
            animator.SetBool(Moving, false);
            return;
        }
        var state = animator.GetCurrentAnimatorStateInfo(0);
        bool attacking = state.IsName("Attack_Leap");
        if (attacking)
        {
            attackStarted = true;
            if (!attackHit && state.normalizedTime >= .56f)
            {
                attackHit = true;
                nextAttackTime = Time.time + attackCooldown;
                TryDealAttackDamage();
            }
        }
        else if (attackStarted)
        {
            attackRequested = attackStarted = false;
        }
        var toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        Vector3 facing = SteeringDirection.sqrMagnitude > .001f ? SteeringDirection : toTarget;
        if (!airborne && facing.sqrMagnitude > .001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), 720f * Time.deltaTime);
        if (Time.time < dodgeUntil) { animator.SetBool(Moving, false); return; }
        bool ready = !animator.IsInTransition(0) && !airborne &&
            (state.IsName("Idle") || (state.IsName("Move_Hop") && state.normalizedTime % 1f >= .90f));
        if (!attackRequested && ready && HasClearAttackPath && distance <= attackRange * hopDistanceMultiplier && Time.time >= nextAttackTime)
        {
            attackRequested = true;
            attackHit = attackStarted = false;
            animator.SetTrigger(Attack);
        }
        animator.SetBool(Moving, !attackRequested && distance > stopDistance);
    }
    /// <summary>
    /// Берёт движение из анимации и увеличивает горизонтальную дальность прыжка.
    /// Ограничивает приближение к игроку, добавляет гравитацию вне воздушной фазы и передаёт движение CharacterController для обработки столкновений.
    /// </summary>
    private void OnAnimatorMove()
    {
        if (!body || !body.enabled || health.IsDead) return;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        float phase = state.normalizedTime % 1f;
        bool jumping = state.IsName("Move_Hop") || state.IsName("Attack_Leap");
        airborne = jumping && phase > (state.IsName("Attack_Leap") ? .18f : .16f) && phase < .57f;
        var motion = animator.deltaPosition; motion.x *= hopDistanceMultiplier; motion.z *= hopDistanceMultiplier;
        if (!target || (targetHealth && targetHealth.IsDead) || Time.time < dodgeUntil) motion.x = motion.z = 0f;
        if (target)
        {
            var gap = target.position - transform.position; gap.y = 0;
            var horizontal = new Vector3(motion.x, 0, motion.z);
            horizontal = Vector3.ClampMagnitude(horizontal, Mathf.Max(0, gap.magnitude - stopDistance));
            motion.x = horizontal.x; motion.z = horizontal.z;
        }
        if (airborne) fallSpeed = 0f;
        else
        {
            fallSpeed = body.isGrounded ? -2f : fallSpeed + gravity * Time.deltaTime;
            motion.y += fallSpeed * Time.deltaTime;
        }
        var flags = body.Move(motion);
        if ((flags & CollisionFlags.Below) != 0) fallSpeed = -2f;
    }
    /// <summary>
    /// Проверяет живую цель, горизонтальное и вертикальное расстояния.
    /// Луч запрещает удар через препятствие; при успехе цель теряет одно ХП и растёт счётчик успешных атак.
    /// </summary>
    private void TryDealAttackDamage()
    {
        if (!targetHealth || targetHealth.IsDead) return;
        Vector3 offset = target.position - transform.position;
        float vertical = Mathf.Abs(offset.y); offset.y = 0;
        if (offset.magnitude > damageRange || vertical > 2.6f) return;
        // Walls block melee too; the player's collider is allowed as the first hit.
        var destination = target.GetComponent<CharacterController>();
        var end = destination ? destination.bounds.center : target.position;
        var start = transform.position + Vector3.up;
        foreach(var hit in Physics.RaycastAll(start, (end-start).normalized, Vector3.Distance(start,end), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<HealthController>() == targetHealth) continue;
            return;
        }
        if (targetHealth.TakeDamage(1)) SuccessfulAttacks++;
    }
    /// <summary>
    /// По заданному шансу пробует сместить медведя поперёк выстрела, при препятствии проверяя другую сторону.
    /// Успешный сдвиг обновляет физику перед повторным hitscan, сбрасывает атаку и ненадолго задерживает продолжение движения и удара.
    /// </summary>
    public bool TryDodge(Vector3 shotDirection)
    {
        if (!isActiveAndEnabled || health.IsDead || Time.time < dodgeUntil || Random.value >= dodgeChance) return false;
        shotDirection.y = 0;
        if (shotDirection.sqrMagnitude < .001f) return false;
        Vector3 side = Vector3.Cross(Vector3.up, shotDirection.normalized);
        if (Random.value < .5f) side = -side;
        Vector3 start = transform.position;
        body.Move(side * dodgeDistance);
        Vector3 displacement = transform.position - start; displacement.y = 0;
        if (displacement.magnitude < .9f)
        {
            body.Move(-displacement);
            start = transform.position;
            body.Move(-side * dodgeDistance);
            displacement = transform.position - start; displacement.y = 0;
        }
        if (displacement.magnitude < .9f) return false;
        // Refresh the controller shape before the same-frame hitscan queries it again.
        body.enabled = false; body.enabled = true; Physics.SyncTransforms();
        DodgesPerformed++;
        dodgeUntil = Time.time + .22f;
        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + .4f);
        attackRequested = attackStarted = attackHit = false;
        animator.ResetTrigger(Attack);
        animator.Play("Move_Hop", 0, 0f);
        return true;
    }
}





