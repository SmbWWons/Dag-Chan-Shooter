using UnityEngine;
using UnityEngine.Rendering.Universal;

// The view-model camera follows the player; it must never run its own locomotion.
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class FirstPersonViewRig : MonoBehaviour
{
    private Camera worldCamera;
    private Camera[] views;
    /// <summary>
    /// Вызывает Configure при создании компонента.
    /// Настройка камер выполняется до обычного игрового обновления.
    /// </summary>
    private void Awake(){Configure();}
    /// <summary>
    /// Находит дочерние камеры URP Overlay и отключает лишние движение, CharacterController, AudioListener и тег MainCamera.
    /// Настраивает ближние плоскости отсечения и совмещает камеры оружия с основной камерой локально.
    /// </summary>
    public void Configure()
    {
        worldCamera=GetComponent<Camera>();
        worldCamera.nearClipPlane=Mathf.Min(worldCamera.nearClipPlane,.035f);
        var all=GetComponentsInChildren<Camera>(true);
        var list=new System.Collections.Generic.List<Camera>();
        foreach(var view in all)
        {
            if(view==worldCamera)continue;
            var data=view.GetComponent<UniversalAdditionalCameraData>();
            if(!data || data.renderType!=CameraRenderType.Overlay)continue;
            var movement=view.GetComponent<PlayerMovement>();if(movement)movement.enabled=false;
            var body=view.GetComponent<CharacterController>();if(body)body.enabled=false;
            var listener=view.GetComponent<AudioListener>();if(listener)listener.enabled=false;
            if(view.CompareTag("MainCamera"))view.tag="Untagged";
            view.nearClipPlane=.01f;
            view.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            list.Add(view);
        }
        views=list.ToArray();
    }
    /// <summary>
    /// После обновления движения возвращает дочерние камеры оружия в нулевые локальные позицию и поворот.
    /// Это предотвращает накопление смещения и связанное с ним дрожание рук.
    /// </summary>
    private void LateUpdate()
    {
        // Local alignment is independent of frame rate, animation state, or distance travelled.
        if(views==null)return;
        foreach(var view in views)if(view)view.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
    }
}
