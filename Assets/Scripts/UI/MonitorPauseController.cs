using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class MonitorPauseController : MonoBehaviour
{
    [SerializeField] private Texture2D menuArtwork;
    [SerializeField] private Material compositeMaterial;
    [SerializeField,Min(.1f)] private float transitionDuration=.7f;
    [SerializeField] private bool startInMenu=true;
    private MonitorMenuSettings settings;
    private bool openingAtStartup;
    private static MonitorPauseController instance;
    private bool blocked,wantsPause,capturing;
    private float progress,previousTimeScale;
    private bool previousAudioPause,previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private int ignoreInputUntilFrame=-1;
    private Canvas overlay;
    private RawImage image;
    private Material material;
    private Texture2D frozenFrame;
    public static bool InputBlocked=>PlayerDeathController.IsDying || (instance && (instance.blocked || Time.frameCount<=instance.ignoreInputUntilFrame));
    public static bool OverlayVisible=>instance && instance.overlay && instance.overlay.enabled;
    public bool IsPaused=>blocked;
    public bool IsTransitioning=>capturing || (blocked && (wantsPause?progress<1:progress>0));
    public float Progress=>progress;
    public Texture2D FrozenFrame=>frozenFrame;
    /// <summary>
    /// Перед новым запуском очищает статическую ссылку на контроллер.
    /// Новый Play Mode не использует объект из предыдущего сеанса.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState(){instance=null;}
    /// <summary>
    /// Назначает единственный экземпляр паузы и отключает лишний.
    /// Находит или добавляет MonitorMenuSettings для построения интерактивных элементов.
    /// </summary>
    private void Awake()
    {
        if(instance && instance!=this){enabled=false;return;}
        instance=this;
        settings=GetComponent<MonitorMenuSettings>();if(!settings)settings=gameObject.AddComponent<MonitorMenuSettings>();
    }
    /// <summary>
    /// При включённом startInMenu открывает мониторы через TogglePause.
    /// Задаёт состояние захваченного курсора, которое должно восстановиться после первого возвращения в игру.
    /// При перезапуске после смерти одноразовый флаг пропускает стартовое меню и сразу возвращает игрока на уровень.
    /// </summary>
    private void Start()
    {
        bool respawn=PlayerDeathController.ConsumeRestartWithoutMenu();
        if(startInMenu && !respawn){openingAtStartup=true;TogglePause();previousCursorLock=CursorLockMode.Locked;previousCursorVisible=false;}
    }
    /// <summary>
    /// Меняет желаемое состояние открытого меню на возвращение в игру.
    /// Сам переход и восстановление выполняются позднее через Update.
    /// </summary>
    public void Resume(){if(blocked)wantsPause=false;}
    /// <summary>
    /// Обрабатывает действие Pause и после захвата кадра меняет progress по независящему от паузы времени.
    /// Передаёт сглаженное значение материалу и вызывает Restore при окончании возвращения в игру.
    /// </summary>
    private void Update()
    {
        if(DagChanInput.PausePressed)TogglePause();
        if(!blocked || capturing)return;
        progress=Mathf.MoveTowards(progress,wantsPause?1:0,Time.unscaledDeltaTime/transitionDuration);
        float eased=progress*progress*(3-2*progress);
        if(material)material.SetFloat("_Progress",eased);
        if(!wantsPause && progress<=0)Restore();
    }
    /// <summary>
    /// Во время смерти не открывает меню; при уже открытом меню меняет направление перехода.
    /// При новом открытии проверяет ресурсы, запоминает время, звук и курсор, блокирует игровой ввод, ставит игру и слушатель на паузу и запускает Capture.
    /// </summary>
    public void TogglePause()
    {
        if(PlayerDeathController.IsDying)return;
        if(!isActiveAndEnabled || !menuArtwork || !compositeMaterial)return;
        if(blocked){wantsPause=!wantsPause;return;}
        previousTimeScale=Time.timeScale;previousAudioPause=AudioListener.pause;
        previousCursorLock=Cursor.lockState;previousCursorVisible=Cursor.visible;
        blocked=wantsPause=capturing=true;progress=openingAtStartup?1:0;
        Time.timeScale=0;AudioListener.pause=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        StartCoroutine(Capture());
    }
    /// <summary>
    /// Корутина ждёт конца кадра, делает снимок игры и при первом открытии создаёт канвас, рабочий материал и настройки.
    /// Передаёт снимок и картинку мониторов материалу с учётом цветового пространства.
    /// При неудачном захвате восстанавливает игру, иначе показывает меню и освобождает курсор.
    /// </summary>
    private IEnumerator Capture()
    {
        // Capture after all game cameras and HUD have rendered, before showing the desk.
        yield return new WaitForEndOfFrame();
        if(!blocked)yield break;
        frozenFrame=ScreenCapture.CaptureScreenshotAsTexture();
        if(!frozenFrame){Restore();yield break;}
        frozenFrame.wrapMode=TextureWrapMode.Clamp;frozenFrame.filterMode=FilterMode.Bilinear;
        if(!overlay)
        {
            var go=new GameObject("Monitor Pause Overlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            go.transform.SetParent(transform,false);overlay=go.GetComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.sortingOrder=32760;
            var ui=new GameObject("Monitor Composite",typeof(RectTransform),typeof(RawImage));ui.transform.SetParent(go.transform,false);
            image=ui.GetComponent<RawImage>();image.raycastTarget=false;
            var rect=image.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            material=new Material(compositeMaterial);image.material=material;
            if(settings)settings.Build(overlay);
        }
        material.SetFloat("_DecodeCapture",QualitySettings.activeColorSpace==ColorSpace.Linear && !UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(frozenFrame.graphicsFormat)?1:0);
        image.texture=frozenFrame;material.SetTexture("_MenuTex",menuArtwork);material.SetFloat("_Progress",progress*progress*(3-2*progress));overlay.enabled=true;capturing=false;openingAtStartup=false;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    /// <summary>
    /// Скрывает меню и возвращает сохранённые время, звук и курсор.
    /// Сбрасывает флаги, ненадолго задерживает ввод после закрытия и освобождает текстуру замершего кадра.
    /// </summary>
    private void Restore()
    {
        if(!blocked)return;
        if(overlay)overlay.enabled=false;
        Time.timeScale=previousTimeScale;AudioListener.pause=previousAudioPause;
        Cursor.lockState=previousCursorLock;Cursor.visible=previousCursorVisible;
        ignoreInputUntilFrame=Time.frameCount+1;blocked=wantsPause=capturing=false;progress=0;
        if(image)image.texture=null;
        if(frozenFrame)Destroy(frozenFrame);frozenFrame=null;
    }
    /// <summary>
    /// Останавливает корутины и восстанавливает игру при выключении компонента.
    /// Это не оставляет время и звук в состоянии открытого меню.
    /// </summary>
    private void OnDisable(){StopAllCoroutines();Restore();}
    /// <summary>
    /// Очищает ссылку на удаляемый общий контроллер и уничтожает его рабочий материал.
    /// Исходный материал-ресурс проекта не удаляется.
    /// </summary>
    private void OnDestroy(){if(instance==this)instance=null;if(material)Destroy(material);}
}
