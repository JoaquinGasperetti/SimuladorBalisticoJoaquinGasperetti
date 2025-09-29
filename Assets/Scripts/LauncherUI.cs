using UnityEngine;
using UnityEngine.UI;

public class LauncherUI : MonoBehaviour
{
    public Slider angleSlider;
    public Slider forceSlider;
    public Button fireButton;
    public Transform muzzle; // el Muzzle o el objeto que querés girar horizontalmente
    public GameObject projectilePrefab;
    public bool useAddForce = true;

    Quaternion muzzleInitialLocalRot;

    void Start()
    {
        if (muzzle != null) muzzleInitialLocalRot = muzzle.localRotation;
        if (angleSlider != null) angleSlider.onValueChanged.AddListener(OnAngleSliderChanged);
        if (forceSlider != null) forceSlider.onValueChanged.AddListener(OnForceSliderChanged);
        if (fireButton != null) fireButton.onClick.AddListener(Fire);
        if (angleSlider != null) OnAngleSliderChanged(angleSlider.value);
    }

    void OnAngleSliderChanged(float v)
    {
        if (muzzle == null) return;
        // Rotación horizontal: yaw alrededor del eje up (Y) en espacio local del launcher
        muzzle.localRotation = muzzleInitialLocalRot * Quaternion.AngleAxis(v, Vector3.up);
        Debug.Log($"[LauncherUI] Yaw -> {v:F1}°");
    }

    void OnForceSliderChanged(float v) { }

    public void Fire()
    {
        float angleDeg = angleSlider != null ? angleSlider.value : 0f;
        float force = forceSlider != null ? forceSlider.value : 0f;

        // Dirección basada en yaw del muzzle (horizontal). Si querés también un pitch, habría que combinar.
        Vector3 dir = muzzle.forward.normalized;
        Quaternion rot = Quaternion.LookRotation(dir);

        GameObject p = Instantiate(projectilePrefab, muzzle.position, rot);
        Rigidbody rb = p.GetComponent<Rigidbody>();
        if (rb == null) rb = p.AddComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        Projectile proj = p.GetComponent<Projectile>();
        float mass = rb.mass;
        if (proj != null) proj.Setup(0f, force, mass, useAddForce, dir); // angle no se usa para física aquí

        GameManager.Instance?.OnShotFired(p, angleDeg, force, mass, dir);
    }
}
