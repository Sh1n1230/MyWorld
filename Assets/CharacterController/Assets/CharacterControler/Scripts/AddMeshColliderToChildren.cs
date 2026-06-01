using UnityEngine;

public class AddMeshColliderToChildren : MonoBehaviour
{

    [ContextMenu("Add Mesh Colliders")]
    void DoStuff() {
        Transform[] children = GetComponentsInChildren<Transform>();
        foreach (Transform child in children) {
            MeshCollider meshCollider = child.gameObject.GetComponent<MeshCollider>();
            if (meshCollider == null) {
                meshCollider = child.gameObject.AddComponent<MeshCollider>();
            }
            meshCollider.convex = false;
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(child);
            #endif
        }

    }

}
