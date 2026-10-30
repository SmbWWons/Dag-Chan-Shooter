using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class LevelExitController : MonoBehaviour
{
    [SerializeField] private ArenaWaveController[] requiredArenas;
    [SerializeField] private GameObject exitGate;
    public bool Finished {get;private set;}
    /// <summary>
    /// Проверяет, что список обязательных арен задан и все они существуют и завершены. Только при выполнении этих условий разрешает открыть выход.
    /// </summary>
    public bool Unlocked
    {
        get{if(requiredArenas==null)return false;foreach(var arena in requiredArenas)if(!arena || !arena.Completed)return false;return true;}
    }
    /// <summary>
    /// Запоминает обязательные арены и объект ворот выхода.
    /// Свойство Unlocked затем определяет доступность по завершению всех заданных арен.
    /// </summary>
    public void Configure(ArenaWaveController[] arenas,GameObject gate){requiredArenas=arenas;exitGate=gate;}
    /// <summary>
    /// Синхронизирует активность ворот со свойством Unlocked.
    /// Пока обязательные арены не пройдены, проход остаётся перекрытым.
    /// </summary>
    private void Update(){if(exitGate)exitGate.SetActive(!Unlocked);}
    /// <summary>
    /// Проверяет вход игрока и доступность выхода, после чего выставляет Finished.
    /// Загрузка следующего уровня этим методом не выполняется.
    /// </summary>
    private void OnTriggerEnter(Collider other){if(Unlocked && other.GetComponentInParent<PlayerMovement>())Finished=true;}
}
