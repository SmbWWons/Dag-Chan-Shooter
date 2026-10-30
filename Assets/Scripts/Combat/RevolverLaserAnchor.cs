using UnityEngine;

// The visible tail catches up to the impact point, erasing the beam from the barrel outwards.
public class RevolverLaserAnchor : MonoBehaviour
{
    private Transform muzzle;
    private Camera worldCamera;
    private Camera weaponCamera;
    private LineRenderer line;
    private float started;
    private float duration;
    private Vector3 impact;
    /// <summary>
    /// Запоминает дуло, камеры, линию, точку попадания и время её жизни.
    /// Сразу обновляет начало линии, чтобы она появилась у видимого дула.
    /// </summary>
    public void Bind(Transform origin, Camera world, Camera view, LineRenderer renderer, float lifetime)
    {
        muzzle=origin;worldCamera=world;weaponCamera=view;line=renderer;
        impact=line.GetPosition(1);started=Time.time;duration=Mathf.Max(.01f,lifetime);
        LateUpdate();
    }
    /// <summary>
    /// Согласует проекции дула между камерами оружия и мира.
    /// По прошедшему игровому времени сдвигает начало линии к попаданию, стирая луч от дула; урон здесь не вычисляется.
    /// </summary>
    private void LateUpdate()
    {
        if(!muzzle || !line) return;
        var position=muzzle.position;
        if(worldCamera && weaponCamera && worldCamera!=weaponCamera)
            position=worldCamera.ViewportToWorldPoint(weaponCamera.WorldToViewportPoint(position));
        float progress=Mathf.Clamp01((Time.time-started)/duration);
        line.SetPosition(0,Vector3.Lerp(position,impact,progress));
    }
}
