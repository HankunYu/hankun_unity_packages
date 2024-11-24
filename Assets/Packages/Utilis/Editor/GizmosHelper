using UnityEngine;

public static class GizmoHelper
{
    public static void DrawCircle(Vector3 centre, Vector3 normal, float radius, int divisions = 20)
    {
        Vector3 startingDirection = normal.OrthogonalVector();
        startingDirection = startingDirection.normalized * radius;

        Vector3 offset = startingDirection;
        Vector3 from = centre + offset;

        for (int i = 0; i < divisions; i++)
        {
            float angle = 360f * i / divisions;
            offset = Quaternion.AngleAxis(angle, normal) * startingDirection;
            Vector3 to = centre + offset;
            Gizmos.DrawLine(from, to);
            from = to;
        }
    }
    
    public static void DrawArrow(Vector3 direction)
    {
        Gizmos.DrawLine(Vector3.zero, direction);

        float arrowLinesAngle = 25f;
        float arrowLinesLength = 0.4f;
        Vector3 orthogonal = direction.OrthogonalVector();
        Vector3 arrowLine1 = Quaternion.AngleAxis(arrowLinesAngle, orthogonal) * direction;
        Vector3 arrowLine2 = Quaternion.AngleAxis(-arrowLinesAngle, orthogonal) * direction;

        Gizmos.DrawLine(direction, direction - arrowLine1 * arrowLinesLength);
        Gizmos.DrawLine(direction, direction - arrowLine2 * arrowLinesLength);
    }
    
    public static bool TryDrawGizmoForCollider(Collider collider)
    {
        Gizmos.matrix = collider.transform.localToWorldMatrix;
        switch (collider)
        {
            case BoxCollider boxCollider:
                Gizmos.DrawCube(boxCollider.center, boxCollider.size);
                return true;
            case SphereCollider sphereCollider:
                Gizmos.DrawSphere(sphereCollider.center, sphereCollider.radius);
                return true;
            case MeshCollider meshCollider:
                Gizmos.DrawMesh(meshCollider.sharedMesh);
                return true;
        }

        // terrain, wheel, capsule colliders not supported by this function
        return false;
    }
}