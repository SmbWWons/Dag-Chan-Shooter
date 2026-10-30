using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(GummyBearEnemy))]
public sealed class ArenaBearNavigation : MonoBehaviour
{
    private GummyBearEnemy enemy;
    private NavMeshPath path;
    private float nextPathTime;
    private Vector3 waypoint;
    /// <summary>
    /// Находит GummyBearEnemy, создаёт NavMeshPath и ставит начальную путевую точку в текущую позицию.
    /// Сам компонент не перемещает тело врага.
    /// </summary>
    private void Awake(){enemy=GetComponent<GummyBearEnemy>();path=new NavMeshPath();waypoint=transform.position;}
    /// <summary>
    /// При наличии цели каждые 0,2 секунды строит путь NavMesh и выбирает следующий угол маршрута.
    /// Передаёт направление в SteeringDirection и лучом проверяет HasClearAttackPath; реальные прыжки выполняет GummyBearEnemy.
    /// </summary>
    private void Update()
    {
        if(!enemy.Target)return;
        if(Time.time>=nextPathTime)
        {
            nextPathTime=Time.time+.2f;
            if(NavMesh.SamplePosition(transform.position,out var from,2f,NavMesh.AllAreas) &&
               NavMesh.SamplePosition(enemy.Target.position,out var to,3f,NavMesh.AllAreas) &&
               NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path) && path.corners.Length>1)
            {
                int corner=1;while(corner<path.corners.Length-1 && Vector3.Distance(transform.position,path.corners[corner])<.8f)corner++;
                waypoint=path.corners[corner];
            }
            else waypoint=enemy.Target.position;
        }
        Vector3 direction=waypoint-transform.position;direction.y=0;enemy.SteeringDirection=direction;
        var start=transform.position+Vector3.up;var end=enemy.Target.position;
        enemy.HasClearAttackPath=true;
        foreach(var hit in Physics.RaycastAll(start,(end-start).normalized,Vector3.Distance(start,end),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
        {
            if(hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<PlayerMovement>())continue;
            enemy.HasClearAttackPath=false;break;
        }
    }
}
