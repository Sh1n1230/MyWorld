using Unity.VisualScripting;
using UnityEngine;

public class PathNode : MonoBehaviour
{
    public PathNode nextNode;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnDrawGizmos() {
        Gizmos.color = Color.yellow;
        
        Gizmos.DrawSphere(transform.position, 0.5f);

        if (nextNode != null) {
            Gizmos.DrawLine(transform.position, nextNode.transform.position);
        }
    }
}
