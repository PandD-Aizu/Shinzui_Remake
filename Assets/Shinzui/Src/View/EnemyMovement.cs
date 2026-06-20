using UnityEngine;
using UnityEngine.AI;

public class EnemyMovement : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform target;
    
    void OnTriggerStay(Collider other)
    {
        if (other.name == "Player")
        {
            agent.SetDestination(target.position);
        }
    }
}
