using UnityEngine;
using UnityEngine.UI;

public class LauncherUI : MonoBehaviour
{
    [Header("Referencias UI")]
    public Slider angleSlider; // 0-80
    public Slider forceSlider; // 0-1200
    public Button fireButton;

    [Header("Launcher refs")]
    public Transform muzzle; // donde spawn el proyectil
    public GameObject projectilePrefab;

    [Header("Opciones")]
    public bool useAddForce = true; // si false usa velocity

    void Start()
    {
        angleSlider.onValueChanged.AddListener(OnAngleSliderChanged);
        forceSlider.onValueChanged.AddListener(OnForceSliderChanged);
        fireButton.onClick.AddListener(Fire);
    }

    void OnAngleSliderChanged(float v) { }

    void OnForceSliderChanged(float v) { }

    public void Fire()
    {
        float angleDeg = angleSlider.value;
        float force = forceSlider.value;

        Vector3 dir = Quaternion.AngleAxis(angleDeg, muzzle.right) * muzzle.forward;
        Quaternion rot = Quaternion.LookRotation(dir);

        GameObject p = Instantiate(projectilePrefab, muzzle.position, rot);
        Rigidbody rb = p.GetComponent<Rigidbody>();
        if (rb == null) rb = p.AddComponent<Rigidbody>();

        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        float mass = rb.mass;

        Projectile proj = p.GetComponent<Projectile>();
        if (proj != null) proj.Setup(angleDeg, force, mass, useAddForce, dir);

        GameManager.Instance?.OnShotFired(p, angleDeg, force, mass, dir);
    }
}
