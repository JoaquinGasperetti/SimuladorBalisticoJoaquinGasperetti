using UnityEngine;
using UnityEngine.UI;
using System;

public class LauncherUI : MonoBehaviour
{
    [Header("Referencias UI")]
    public Slider angleSlider; // 0-80
    public InputField angleInput;
    public Slider forceSlider; // 0-1200
    public InputField forceInput;
    public Dropdown massDropdown;
    public Button fireButton;

    [Header("Launcher refs")]
    public Transform muzzle; // donde spawn el proyectil
    public GameObject projectilePrefab;

    [Header("Opciones")]
    public bool useAddForce = true; // si false usa velocity

    void Start()
    {
        // llenar dropdown con opciones
        massDropdown.ClearOptions();
        massDropdown.AddOptions(new System.Collections.Generic.List<string> { "0.1", "0.5", "1", "2", "5" });

        angleSlider.onValueChanged.AddListener(OnAngleSliderChanged);
        forceSlider.onValueChanged.AddListener(OnForceSliderChanged);
        angleInput.onEndEdit.AddListener(OnAngleInputEdited);
        forceInput.onEndEdit.AddListener(OnForceInputEdited);
        fireButton.onClick.AddListener(Fire);

        // inicializar valores
        OnAngleSliderChanged(angleSlider.value);
        OnForceSliderChanged(forceSlider.value);
    }

    void OnAngleSliderChanged(float v) => angleInput.text = v.ToString("F1");
    void OnForceSliderChanged(float v) => forceInput.text = v.ToString("F0");
    void OnAngleInputEdited(string s)
    {
        if (float.TryParse(s, out float v)) angleSlider.value = Mathf.Clamp(v, angleSlider.minValue, angleSlider.maxValue);
        else angleInput.text = angleSlider.value.ToString("F1");
    }
    void OnForceInputEdited(string s)
    {
        if (float.TryParse(s, out float v)) forceSlider.value = Mathf.Clamp(v, forceSlider.minValue, forceSlider.maxValue);
        else forceInput.text = forceSlider.value.ToString("F0");
    }

    public void Fire()
    {
        float angleDeg = angleSlider.value;
        float force = forceSlider.value;
        float mass = float.Parse(massDropdown.options[massDropdown.value].text);

        // instanciar
        GameObject p = Instantiate(projectilePrefab, muzzle.position, Quaternion.identity);
        Rigidbody rb = p.GetComponent<Rigidbody>();
        if (rb == null) rb = p.AddComponent<Rigidbody>();

        rb.mass = mass;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // calcular dirección según ángulo local del muzzle en y (suponemos eje forward = adelante)
        Vector3 dir = Quaternion.Euler(0, angleDeg, 0) * muzzle.forward;
        Projectile proj = p.GetComponent<Projectile>();
        if (proj != null) proj.Setup(angleDeg, force, mass, useAddForce, dir);

        // notificar al GameManager
        GameManager.Instance?.OnShotFired(p, angleDeg, force, mass, dir);
    }
}
