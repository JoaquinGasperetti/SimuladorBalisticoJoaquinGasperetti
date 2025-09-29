using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    Rigidbody rb;
    bool launched = false;
    float launchTime;
    Vector3 launchDir;
    float launchForce;
    float launchMass;
    bool useAddForce;

    public void Setup(float angleDeg, float force, float mass, bool useAddForce, Vector3 dir)
    {
        rb = GetComponent<Rigidbody>();
        this.launchDir = dir.normalized;
        this.launchForce = force;
        this.launchMass = mass;
        this.useAddForce = useAddForce;

        // lanzar en el siguiente frame para asegurar transform OK
        StartCoroutine(DoLaunchNextFrame());
    }

    IEnumerator DoLaunchNextFrame()
    {
        yield return null;
        rb = rb ?? GetComponent<Rigidbody>();
        if (useAddForce)
        {
            rb.AddForce(launchDir * launchForce, ForceMode.Impulse);
        }
        else
        {
            // usar fuerza convertida a velocidad (simple approx: v = impulse/mass => force here is treated as impulse)
            rb.linearVelocity = launchDir * (launchForce / Mathf.Max(0.0001f, rb.mass));
        }
        launchTime = Time.time;
        launched = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!launched) return;

        // notificar al GameManager con los datos del primer impacto
        Vector3 impactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
        Vector3 relativeVelocity = collision.relativeVelocity; // velocidad relativa
        float timeOfFlight = Time.time - launchTime;
        float impulseMag = collision.impulse.magnitude; // magnitud del impulso total aplicado
        GameManager.Instance?.OnProjectileImpact(this.gameObject, collision.gameObject, impactPoint, relativeVelocity, impulseMag, timeOfFlight);

        // opcional: destruir proyectil tras impacto
        Destroy(this.gameObject, 2f);
    }
}
