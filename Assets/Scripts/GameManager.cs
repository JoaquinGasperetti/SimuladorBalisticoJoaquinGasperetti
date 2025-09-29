using UnityEngine;
using System.Text;
using System.Collections.Generic;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Reporte UI")]
    public GameObject panelReport;
    public Text txtScore;
    public Text txtReportDetails;

    [Header("Target tracking")]
    public Transform targetsRoot;

    // datos del intento actual
    GameObject currentProjectile;
    bool shotFired = false;
    float shotAngle, shotForce, shotMass;
    Vector3 shotDir;
    float shotLaunchTime;

    // resultado del primer impacto
    bool impactRecorded = false;
    Vector3 impactPoint;
    Vector3 impactRelativeVelocity;
    float impactImpulse;
    float timeOfFlight;

    // pieces
    List<TargetPiece> allPieces = new List<TargetPiece>();
    HashSet<TargetPiece> fallenPieces = new HashSet<TargetPiece>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        panelReport.SetActive(false);
        // encontrar todas las TargetPiece en la escena bajo targetsRoot
        if (targetsRoot != null)
        {
            allPieces.Clear();
            foreach (var t in targetsRoot.GetComponentsInChildren<TargetPiece>())
                allPieces.Add(t);
        }
    }

    public void OnShotFired(GameObject projectile, float angle, float force, float mass, Vector3 dir)
    {
        shotFired = true;
        currentProjectile = projectile;
        shotAngle = angle;
        shotForce = force;
        shotMass = mass;
        shotDir = dir;
        shotLaunchTime = Time.time;
        impactRecorded = false;
        fallenPieces.Clear();

        // refrescar lista de piezas
        if (targetsRoot != null)
        {
            allPieces.Clear();
            foreach (var t in targetsRoot.GetComponentsInChildren<TargetPiece>())
                allPieces.Add(t);
        }
    }

    public void OnProjectileImpact(GameObject projectile, GameObject hitObject, Vector3 point, Vector3 relVel, float impulseMag, float timeOfFlightReceived)
    {
        if (!shotFired || impactRecorded) return;

        impactRecorded = true;
        impactPoint = point;
        impactRelativeVelocity = relVel;
        impactImpulse = impulseMag;
        timeOfFlight = timeOfFlightReceived;

        // a partir de acá, espera N segundos para evaluar derribadas y luego mostrar reporte
        StartCoroutine(WaitAndEvaluateAndShowReport(2.0f)); // espera 2s para que caigan más piezas
    }

    public void OnPieceFallen(TargetPiece piece, string reason)
    {
        if (!fallenPieces.Contains(piece))
        {
            fallenPieces.Add(piece);
            Debug.Log($"Piece fallen: {piece.name} reason: {reason}");
        }
    }

    System.Collections.IEnumerator WaitAndEvaluateAndShowReport(float wait)
    {
        yield return new WaitForSeconds(wait);

        // contar piezas derribadas
        int fallenCount = 0;
        foreach (var p in allPieces)
            if (p.IsFallen()) fallenCount++;

        // score (ejemplo simple):
        float score = CalculateScore(fallenCount, impactImpulse, impactRelativeVelocity.magnitude);

        // armar reporte de texto
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Piezas derribadas: {fallenCount} / {allPieces.Count}");
        sb.AppendLine($"Tiempo de vuelo: {timeOfFlight:F2} s");
        sb.AppendLine($"Punto de impacto: {impactPoint.ToString("F3")}");
        sb.AppendLine($"Velocidad relativa en impacto: {impactRelativeVelocity.magnitude:F2} m/s");
        sb.AppendLine($"Impulso de colisión: {impactImpulse:F2} N·s");
        sb.AppendLine($"Score calculado: {score:F0}");

        // mostrar UI
        panelReport.SetActive(true);
        txtScore.text = Mathf.RoundToInt(score).ToString();
        txtReportDetails.text = sb.ToString();

        // reset estado básico
        shotFired = false;
        impactRecorded = false;
    }

    float CalculateScore(int fallenCount, float impulse, float relVel)
    {
        // Ejemplo: ponderacion sencilla
        float score = fallenCount * 100f;
        score += Mathf.Clamp(impulse, 0f, 100f) * 2f;
        score += Mathf.Clamp(relVel, 0f, 50f) * 5f;
        return score;
    }

    // Podés añadir métodos para exportar los datos a CSV o guardarlos en archivo local si necesitas persistencia.
}
