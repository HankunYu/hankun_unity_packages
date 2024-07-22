using UnityEngine;

public class ReverseNormals : MonoBehaviour
{
    private void Start()
    {
        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            Debug.LogError("Mesh filter not found!");
            return;
        }

        var mesh = meshFilter.mesh;
        var normals = mesh.normals;

        // 反转法线方向
        for (var i = 0; i < normals.Length; i++)
        {
            normals[i] = -normals[i];
        }

        mesh.normals = normals;

        // 重新计算顶点顺序以保持正确的渲染
        for (var i = 0; i < mesh.subMeshCount; i++)
        {
            var triangles = mesh.GetTriangles(i);
            for (var j = 0; j < triangles.Length; j += 3)
            {
                (triangles[j], triangles[j + 1]) = (triangles[j + 1], triangles[j]);
            }

            mesh.SetTriangles(triangles, i);
        }
    }
}