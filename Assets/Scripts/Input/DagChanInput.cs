using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

// One runtime copy of the editable Input Actions asset is shared by gameplay and UI.
[DefaultExecutionOrder(-20000)]
public sealed class DagChanInput : MonoBehaviour
{
    private static DagChanInput instance;
    private InputActionAsset actions;
    private InputAction move,look,jump,attack,dash,slide,parry,ultimate,pause,damage,heal;
    public static InputActionAsset Actions=>Instance.actions;
    /// <summary>
    /// Возвращает единый объект ввода, создавая его при первом обращении. Компонент в Awake сам загружает и включает рабочую копию действий.
    /// </summary>
    private static DagChanInput Instance
    {
        get
        {
            if(!instance)
            {
                var go=new GameObject("DagChan Input Actions");DontDestroyOnLoad(go);go.AddComponent<DagChanInput>();
            }
            return instance;
        }
    }
    /// <summary>
    /// Обнуляет статическую ссылку перед новым запуском игры.
    /// Это поддерживает Play Mode без перезагрузки домена и не оставляет ссылку на старый объект.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset(){instance=null;}
    /// <summary>
    /// До загрузки сцены обращается к Instance и создаёт общий объект ввода.
    /// Игровые компоненты смогут читать действия уже при своей инициализации.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize(){_ = Instance;}
    /// <summary>
    /// Удаляет лишний экземпляр и создаёт рабочую копию DagChanControls из Resources.
    /// Находит действия Player и Debug и включает набор; отсутствие ресурса вызывает явную ошибку.
    /// </summary>
    private void Awake()
    {
        if(instance && instance!=this){Destroy(gameObject);return;}
        instance=this;
        var source=Resources.Load<InputActionAsset>("DagChanControls");
        if(!source)throw new System.InvalidOperationException("DagChanControls.inputactions is missing from Resources");
        actions=Instantiate(source);actions.name="DagChanControls (Runtime)";
        move=actions.FindAction("Player/Move",true);look=actions.FindAction("Player/Look",true);
        jump=actions.FindAction("Player/Jump",true);attack=actions.FindAction("Player/Attack",true);
        dash=actions.FindAction("Player/Dash",true);slide=actions.FindAction("Player/Slide",true);
        parry=actions.FindAction("Player/Parry",true);ultimate=actions.FindAction("Player/Ultimate",true);
        pause=actions.FindAction("Player/Pause",true);damage=actions.FindAction("Debug/Damage",true);heal=actions.FindAction("Debug/Heal",true);
        actions.Enable();
    }
    public static Vector2 Move=>Instance.move.ReadValue<Vector2>();
    /// <summary>
    /// Читает обзор из Input Actions. Для мыши возвращает перемещение указателя, для геймпада умножает отклонение стика на скорость и время кадра.
    /// </summary>
    public static Vector2 LookDelta
    {
        get
        {
            var action=Instance.look;var delta=action.ReadValue<Vector2>();
            // Mouse supplies pixels per frame; a stick supplies a rate.
            return action.activeControl!=null && action.activeControl.device is Gamepad?delta*600f*Time.unscaledDeltaTime:delta;
        }
    }
    public static bool JumpPressed=>Instance.jump.WasPressedThisFrame();
    public static bool AttackHeld=>Instance.attack.IsPressed();
    public static bool DashPressed=>Instance.dash.WasPressedThisFrame();
    public static bool SlideHeld=>Instance.slide.IsPressed();
    public static bool ParryPressed=>Instance.parry.WasPressedThisFrame();
    public static bool UltimatePressed=>Instance.ultimate.WasPressedThisFrame();
    public static bool PausePressed=>Instance.pause.WasPressedThisFrame();
    public static bool DamagePressed=>Instance.damage.WasPressedThisFrame();
    public static bool HealPressed=>Instance.heal.WasPressedThisFrame();
    /// <summary>
    /// Назначает модулю интерфейса рабочую копию Input Actions и ссылки на действия UI.
    /// Подключает указатель, навигацию, нажатия, прокрутку и отслеживаемые устройства к тому же источнику ввода, что использует игра.
    /// </summary>
    public static void ConfigureUI(InputSystemUIInputModule module)
    {
        var asset=Actions;module.actionsAsset=asset;
        module.point=InputActionReference.Create(asset.FindAction("UI/Point",true));
        module.move=InputActionReference.Create(asset.FindAction("UI/Navigate",true));
        module.submit=InputActionReference.Create(asset.FindAction("UI/Submit",true));
        module.cancel=InputActionReference.Create(asset.FindAction("UI/Cancel",true));
        module.leftClick=InputActionReference.Create(asset.FindAction("UI/Click",true));
        module.rightClick=InputActionReference.Create(asset.FindAction("UI/RightClick",true));
        module.middleClick=InputActionReference.Create(asset.FindAction("UI/MiddleClick",true));
        module.scrollWheel=InputActionReference.Create(asset.FindAction("UI/ScrollWheel",true));
        module.trackedDevicePosition=InputActionReference.Create(asset.FindAction("UI/TrackedDevicePosition",true));
        module.trackedDeviceOrientation=InputActionReference.Create(asset.FindAction("UI/TrackedDeviceOrientation",true));
    }
    /// <summary>
    /// Отключает и удаляет рабочую копию действий при удалении текущего общего экземпляра.
    /// Затем очищает статическую ссылку на контроллер.
    /// </summary>
    private void OnDestroy()
    {
        if(instance!=this)return;
        if(actions){actions.Disable();Destroy(actions);}instance=null;
    }
}
