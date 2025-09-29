using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TargetPiece : MonoBehaviour
{
    Vector3 initialPosition;
    Quaternion initialRotation;
    public float fallDistanceThreshold = 0.3f; // cu�nto se tiene que mover para considerarla derribada
    public float fallAngleThreshold = 30f; // en grados

    bool fallen = false;

    void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    void Update()
    {
        if (fallen) return;

        // desplazamiento global
        float dist = Vector3.Distance(transform.position, initialPosition);
        if (dist > fallDistanceThreshold)
        {
            MarkFallen("desplazamiento");
            return;
        }

        // rotaci�n excesiva
        float angle = Quaternion.Angle(initialRotation, transform.rotation);
        if (angle > fallAngleThreshold)
        {
            MarkFallen("rotacion");
            return;
        }
    }

    void MarkFallen(string reason)
    {
        fallen = true;
        GameManager.Instance?.OnPieceFallen(this, reason);
    }

    // si la pieza ten�a un joint y se rompe, Unity llama a OnJointBreak en el objeto que ten�a el joint
    void OnJointBreak(float breakForce)
    {
        if (!fallen)
        {
            MarkFallen("joint_roto");
        }
    }

    public bool IsFallen() => fallen;
}
