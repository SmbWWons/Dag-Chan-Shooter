using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Смерть игрока получает состояние из HealthController; у врагов этот компонент не используется.
[DisallowMultipleComponent]
[RequireComponent(typeof(HealthController),typeof(PlayerMovement))]
public sealed class PlayerDeathController : MonoBehaviour
{
    [SerializeField] private AudioClip deathVoice;
    [SerializeField,Min(.1f)] private float fallDuration=.9f;
    [SerializeField,Min(.05f)] private float eyeHeightOnFloor=.18f;
    [SerializeField,Min(0)] private float fadeDelay=.35f;
    [SerializeField,Min(.1f)] private float fadeDuration=1.35f;
    [SerializeField,Range(0,1)] private float deathVoiceVolume=.9f;
    private static PlayerDeathController instance;
    private static bool restartWithoutMenu;
    private HealthController health;
    private DagChanAudio audioSettings;
    private Image fade;
    private bool dying;
    private AudioSource voice;
    public static bool IsDying=>instance&&instance.dying;
    public float FadeOpacity=>fade?fade.color.a:0;
    public AudioSource DeathVoice=>voice;

    /// <summary>
    /// Перед новым запуском игры очищает статическое состояние смерти и одноразовый пропуск меню.
    /// При обычной перезагрузке сцены этот метод не вызывается, поэтому флаг перезапуска доживает до её Start.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState(){instance=null;restartWithoutMenu=false;}

    /// <summary>
    /// Забирает и сбрасывает одноразовый запрос возрождения без стартового меню.
    /// MonitorPauseController вызывает метод при старте новой сцены после смерти.
    /// </summary>
    public static bool ConsumeRestartWithoutMenu(){bool skip=restartWithoutMenu;restartWithoutMenu=false;return skip;}

    /// <summary>
    /// Получает контроллер здоровья игрока и общую аудиосистему и назначает активный экземпляр смерти.
    /// Возвращает обычную громкость музыки после загрузки нового уровня, не изменяя сохранённые настройки.
    /// </summary>
    private void Awake(){instance=this;health=GetComponent<HealthController>();audioSettings=DagChanAudio.Instance;audioSettings.SetMusicDuck(1);}

    /// <summary>
    /// Подписывает запуск смерти на Died контроллера ХП.
    /// Скрипт не ведёт собственный запас здоровья и не дублирует расчёт урона.
    /// </summary>
    private void OnEnable(){health.Died+=BeginDeath;}

    /// <summary>
    /// Отписывается от события здоровья при выключении компонента.
    /// </summary>
    private void OnDisable(){if(health)health.Died-=BeginDeath;}

    /// <summary>
    /// Один раз запускает последовательность после подтверждённой смерти в HealthController.
    /// Отключает движение и действия оружия, закрывает паузу, освобождает камеру от капсулы и создаёт затемнение.
    /// </summary>
    private void BeginDeath()
    {
        if(dying||!health.IsDead)return;
        dying=true;
        GetComponent<PlayerMovement>().enabled=false;
        var weapons=GetComponent<DualWeaponController>();if(weapons)weapons.enabled=false;
        var ultimate=GetComponent<UltimateAttackController>();if(ultimate)ultimate.enabled=false;
        var parry=GetComponent<WeaponParryAnimation>();if(parry)parry.enabled=false;
        foreach(var weapon in GetComponentsInChildren<WeaponAnimation>(true))weapon.enabled=false;
        var pause=FindFirstObjectByType<MonitorPauseController>();if(pause)pause.enabled=false;
        Time.timeScale=1;AudioListener.pause=false;
        Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
        var body=GetComponent<CharacterController>();if(body)body.enabled=false;
        CreateFade();PlayDeathVoice();StartCoroutine(DeathSequence());
    }

    /// <summary>
    /// Ищет поверхность под игроком лучом вниз, игнорируя триггеры и все объекты со здоровьем.
    /// Добавляет небольшой отступ над полом, чтобы камера не оказалась под поверхностью; без попадания использует высоту стоящего игрока.
    /// </summary>
    private Vector3 FindFloorPosition()
    {
        Vector3 position=transform.position;float nearest=float.PositiveInfinity;Vector3 result=position-Vector3.up*1.42f;
        foreach(var hit in Physics.RaycastAll(position+Vector3.up*.05f,Vector3.down,20,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
        {
            if(hit.collider.GetComponentInParent<HealthController>()||hit.distance>=nearest)continue;
            nearest=hit.distance;result=new Vector3(position.x,Mathf.Min(position.y,hit.point.y+eyeHeightOnFloor),position.z);
        }
        return result;
    }

    /// <summary>
    /// Создаёт чёрное изображение поверх всего интерфейса с начальной прозрачностью ноль.
    /// Канвас живёт вместе с игроком и автоматически удаляется при перезапуске сцены.
    /// </summary>
    private void CreateFade()
    {
        var go=new GameObject("Player Death Fade",typeof(RectTransform),typeof(Canvas));go.layer=5;go.transform.SetParent(transform,false);
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32767;
        var panel=new GameObject("Blackout",typeof(RectTransform),typeof(Image));panel.layer=5;panel.transform.SetParent(go.transform,false);
        var rect=panel.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        fade=panel.GetComponent<Image>();fade.color=new Color(0,0,0,0);fade.raycastTarget=false;
    }

    /// <summary>
    /// Один раз проигрывает назначенную реплику смерти двумерным источником с неизменённым pitch.
    /// DagChanEffectVolume применяет к ней пользовательскую громкость эффектов; источник не зависит от паузы слушателя.
    /// </summary>
    private void PlayDeathVoice()
    {
        if(!deathVoice)return;
        var go=new GameObject("Player Death Voice");go.transform.SetParent(transform,false);voice=go.AddComponent<AudioSource>();
        voice.playOnAwake=false;voice.spatialBlend=0;voice.pitch=1;voice.ignoreListenerPause=true;voice.priority=24;voice.clip=deathVoice;
        go.AddComponent<DagChanEffectVolume>().SetBaseVolume(deathVoiceVolume);voice.Play();
    }

    /// <summary>
    /// Опускает камеру к найденному полу с ускорением и постепенно затемняет экран, приглушая музыку.
    /// Использует независящее от паузы время и ждёт окончания реплики, чтобы не оборвать её перезагрузкой.
    /// После полного затемнения заново загружает текущую сцену с полным здоровьем и без стартового меню.
    /// </summary>
    private IEnumerator DeathSequence()
    {
        Vector3 from=transform.position,to=FindFloorPosition();float elapsed=0;
        float duration=Mathf.Max(fallDuration,fadeDelay+fadeDuration,deathVoice?deathVoice.length+.35f:0);
        while(elapsed<duration)
        {
            elapsed+=Time.unscaledDeltaTime;float fall=Mathf.Clamp01(elapsed/fallDuration);transform.position=Vector3.Lerp(from,to,fall*fall);
            float opacity=Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-fadeDelay)/fadeDuration));fade.color=new Color(0,0,0,opacity);audioSettings.SetMusicDuck(1-opacity);
            yield return null;
        }
        fade.color=Color.black;restartWithoutMenu=true;Time.timeScale=1;AudioListener.pause=false;
        var scene=gameObject.scene;yield return SceneManager.LoadSceneAsync(scene.path,LoadSceneMode.Single);
    }

    /// <summary>
    /// При удалении игрока очищает ссылку на текущий экземпляр и возвращает музыку из приглушения.
    /// Одноразовый запрос пропуска меню сохраняется до старта нового уровня.
    /// </summary>
    private void OnDestroy(){if(instance==this)instance=null;if(audioSettings)audioSettings.SetMusicDuck(1);}
}
