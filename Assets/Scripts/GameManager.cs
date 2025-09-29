using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameManager: controla el ciclo del tiro, recopila datos del impacto,
/// calcula score y muestra el panel de reporte. También envía un reporte
/// simple a Firebase usando FirebaseService (Proyecto26 RestClient).
/// 
/// Comentarios en español rioplatense, tono formal.
/// </summary>
public class GameManager : MonoBehaviour
{
    // --------------------
    // Singleton
    // --------------------
    public static GameManager Instance { get; private set; }

    // --------------------
    // Inspector - referencias de escena
    // --------------------
    [Header("Referencias de escena")]
    [Tooltip("Raíz que contiene todas las TargetPiece como hijos.")]
    public Transform targetsRoot;

    [Tooltip("Panel que muestra el reporte final (score + detalles).")]
    public GameObject panelReport;

    [Tooltip("Texto para mostrar el score numérico.")]
    public Text txtScore;

    [Tooltip("Texto para mostrar detalles del reporte (lista/estadísticas).")]
    public Text txtReportDetails;

    [Header("Gameplay")]
    [Tooltip("Tiempo en segundos que se espera después del impacto antes de evaluar.")]
    public float waitBeforeEvaluate = 1.5f;

    // --------------------
    // Firebase (configurar en inspector)
    // --------------------
    [Header("Firebase")]
    [Tooltip("URL de Realtime Database. Ej: https://mi-proyecto-default-rtdb.firebaseio.com/")]
    public string firebaseDatabaseUrl;

    [Tooltip("Nombre del jugador (opcional). Se usa en el reporte enviado a Firebase).")]
    public string playerName = "Player";

    // --------------------
    // Estado interno
    // --------------------
    private List<TargetPiece> allPieces = new List<TargetPiece>();

    // Variables para medir el disparo
    private bool shotFired = false;
    private bool impactRecorded = false;
    private float timeOfFlight = 0f;
    private Vector3 impactPoint = Vector3.zero;
    private Vector3 impactRelativeVelocity = Vector3.zero;
    private float impactImpulse = 0f;

    // Control de timeout por si nunca impacta
    private float maxFlightTime = 10f;

    private Coroutine evaluateCoroutine = null;

    // --------------------
    // Unity callbacks
    // --------------------
    private void Awake()
    {
        // Singleton pattern simple
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Inicialización de lista de piezas a partir de targetsRoot
        if (targetsRoot != null)
        {
            allPieces.Clear();
            foreach (var t in targetsRoot.GetComponentsInChildren<TargetPiece>())
                allPieces.Add(t);
        }

        // Poner panel de reporte oculto al inicio
        if (panelReport != null)
            panelReport.SetActive(false);

        // Asignar URL al servicio de Firebase si se provee
        if (!string.IsNullOrEmpty(firebaseDatabaseUrl))
            FirebaseService.DatabaseUrl = firebaseDatabaseUrl;
        else
            Debug.LogWarning("[GameManager] firebaseDatabaseUrl no está seteada en el inspector.");
    }

    private void Update()
    {
        // Si hay un disparo en curso y todavía no hubo impacto, medimos el tiempo de vuelo
        if (shotFired && !impactRecorded)
        {
            timeOfFlight += Time.deltaTime;
            // Si supera maxFlightTime, forzamos evaluación para evitar quedarse colgado
            if (timeOfFlight >= maxFlightTime)
            {
                Debug.LogWarning("[GameManager] Tiempo máximo de vuelo alcanzado, evaluando de todos modos.");
                OnEvaluateAfterImpactTimeout();
            }
        }
    }

    // --------------------
    // API pública - llamada por LauncherUI / Projectile
    // --------------------

    /// <summary>
    /// Llamar cuando se dispare el proyectil (desde LauncherUI o script equivalente).
    /// Resetea el estado y comienza a contar tiempo de vuelo.
    /// </summary>
    public void OnShotFired()
    {
        shotFired = true;
        impactRecorded = false;
        timeOfFlight = 0f;
        impactPoint = Vector3.zero;
        impactRelativeVelocity = Vector3.zero;
        impactImpulse = 0f;

        // ocultar panel de reporte si había quedado abierto
        if (panelReport != null)
            panelReport.SetActive(false);

        // cancelar coroutine anterior si queda alguna
        if (evaluateCoroutine != null)
        {
            StopCoroutine(evaluateCoroutine);
            evaluateCoroutine = null;
        }
    }

    /// <summary>
    /// Sobrecarga usada por LauncherUI que provee información del proyectil.
    /// </summary>
    public void OnShotFired(GameObject projectile, float angleDeg, float force, float mass, Vector3 dir)
    {
        // Por compatibilidad con llamadas externas, delegamos al OnShotFired básico
        OnShotFired();
        Debug.Log($"[GameManager] Shot fired. projectile={projectile?.name}, angle={angleDeg}, force={force}, mass={mass}");
    }

    /// <summary>
    /// Llamar desde Projectile.cs al detectar una colisión válida.
    /// Se recibe punto de impacto, impulso estimado y velocidad relativa.
    /// </summary>
    /// <param name="point">punto del mundo del impacto</param>
    /// <param name="impulse">valor escalar representando el impulso (puede venir de colisión)</param>
    /// <param name="relativeVelocity">velocidad relativa en el momento del impacto</param>
    public void OnProjectileImpact(Vector3 point, float impulse, Vector3 relativeVelocity)
    {
        if (!shotFired)
        {
            Debug.LogWarning("[GameManager] OnProjectileImpact llamado pero no se detectó shotFired.");
            // igual podemos aceptar el impacto si viene desde plugin externo
        }

        // Registrar datos
        impactRecorded = true;
        impactPoint = point;
        impactImpulse = impulse;
        impactRelativeVelocity = relativeVelocity;

        // arrancar coroutine para esperar un pequeño delay y evaluar estado de piezas
        if (evaluateCoroutine != null)
            StopCoroutine(evaluateCoroutine);

        evaluateCoroutine = StartCoroutine(WaitAndEvaluateAndShowReport(waitBeforeEvaluate));
    }

    /// <summary>
    /// Sobrecarga que coincide con la llamada desde Projectile (pasa más datos).
    /// </summary>
    public void OnProjectileImpact(GameObject projectile, GameObject hitObject, Vector3 point, Vector3 relativeVelocity, float impulseMag, float timeOfFlight)
    {
        // Registrar tiempo de vuelo y delegar a la implementación existente
        this.timeOfFlight = timeOfFlight;
        OnProjectileImpact(point, impulseMag, relativeVelocity);
        Debug.Log($"[GameManager] Projectile impact recorded. projectile={projectile?.name}, hit={hitObject?.name}, impulse={impulseMag:F2}, tof={timeOfFlight:F2}");
    }

    /// <summary>
    /// Llamada desde TargetPiece cuando una pieza se detecta como derribada.
    /// </summary>
    public void OnPieceFallen(TargetPiece piece, string reason)
    {
        Debug.Log($"[GameManager] Piece fallen: {piece?.name}, reason={reason}");
        // Por ahora no hacemos más, pero podés agregar tracking o feedback aquí.
    }

    // --------------------
    // Evaluación y reporte
    // --------------------

    /// <summary>
    /// Timeout alternativo si no hubo impacto pero se superó el tiempo máximo de vuelo.
    /// Esto fuerza evaluación igual (impactPoint quedará en Vector3.zero).
    /// </summary>
    private void OnEvaluateAfterImpactTimeout()
    {
        if (evaluateCoroutine != null)
            StopCoroutine(evaluateCoroutine);

        evaluateCoroutine = StartCoroutine(WaitAndEvaluateAndShowReport(0f));
    }

    /// <summary>
    /// Coroutine que espera (para que las piezas terminen de caer), calcula score,
    /// muestra el panel con información y envía un reporte a Firebase.
    /// </summary>
    private IEnumerator WaitAndEvaluateAndShowReport(float wait)
    {
        // Esperar el tiempo para dejar que la física "termine"
        yield return new WaitForSeconds(wait);

        // Contar piezas derribadas
        int fallenCount = 0;
        foreach (var p in allPieces)
        {
            if (p == null) continue;
            if (p.IsFallen()) fallenCount++;
        }

        // Calcular score (implementación simple; podés ajustar fórmula)
        int score = CalculateScore(fallenCount, impactImpulse, impactRelativeVelocity.magnitude);

        // Armar texto de detalles
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Piezas derribadas: {fallenCount} / {allPieces.Count}");
        sb.AppendLine($"Tiempo de vuelo: {timeOfFlight:F2} s");
        sb.AppendLine($"Punto de impacto: {impactPoint.ToString("F3")}");
        sb.AppendLine($"Velocidad relativa en impacto: {impactRelativeVelocity.magnitude:F2} m/s");
        sb.AppendLine($"Impulso de colisión: {impactImpulse:F2} N·s");
        sb.AppendLine($"Score calculado: {score}");

        // Mostrar UI
        if (panelReport != null)
            panelReport.SetActive(true);

        if (txtScore != null)
            txtScore.text = score.ToString();

        if (txtReportDetails != null)
            txtReportDetails.text = sb.ToString();

        // --- Envío del reporte a Firebase ---
        try
        {
            var report = new FirebaseService.ShotReport()
            {
                id = Guid.NewGuid().ToString(),
                playerName = this.playerName ?? "Player",
                score = score,
                fallenCount = fallenCount,
                timeOfFlight = timeOfFlight,
                impactPoint = new FirebaseService.Vec3(impactPoint),
                impactImpulse = impactImpulse,
                impactRelativeVelocity = impactRelativeVelocity.magnitude,
                timestamp = DateTime.UtcNow.ToString("o") // ISO 8601
            };

            // Guarda en la ruta "game_reports"
            FirebaseService.PostShotReport("game_reports", report,
                onSuccess: () => Debug.Log($"[GameManager] Report subido a Firebase: {report.id}"),
                onError: (ex) => Debug.LogError($"[GameManager] Error al subir report: {ex.Message}")
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameManager] Excepción preparando report para Firebase: {ex.Message}");
        }

        // Reset básico de estado de disparo
        shotFired = false;
        impactRecorded = false;
        evaluateCoroutine = null;
    }

    /// <summary>
    /// Método que calcula el score. Fórmula básica que podés modificar:
    /// - más piezas derribadas -> mayor score
    /// - mayor impulso -> bonus
    /// - penaliza tiempo de vuelo largo (opcional)
    /// </summary>
    private int CalculateScore(int fallenCount, float impactImpulse, float relativeSpeed)
    {
        // Parámetros de tuning
        float basePerPiece = 100f;           // puntos por pieza
        float impulseBonusFactor = 5f;       // multiplicador por impulso
        float speedBonusFactor = 2f;         // multiplicador por velocidad relativa
        float timePenaltyPerSecond = 2f;     // penalización por segundo de vuelo

        float score = fallenCount * basePerPiece;
        score += impactImpulse * impulseBonusFactor;
        score += relativeSpeed * speedBonusFactor;
        score -= timeOfFlight * timePenaltyPerSecond;

        int finalScore = Mathf.Max(0, Mathf.RoundToInt(score));
        return finalScore;
    }

    // --------------------
    // Utilities / debug
    // --------------------

    /// <summary>
    /// Forzar recálculo de la lista de piezas (por si cambiaste la escena en runtime).
    /// </summary>
    public void RefreshTargetPieces()
    {
        allPieces.Clear();
        if (targetsRoot != null)
        {
            foreach (var t in targetsRoot.GetComponentsInChildren<TargetPiece>())
                allPieces.Add(t);
        }
    }

    /// <summary>
    /// Resetea las piezas (llama a su propio método ResetPiece si existe).
    /// Este método asume que TargetPiece tiene un método público ResetState (opcional).
    /// </summary>
    public void ResetAllPieces()
    {
        foreach (var p in allPieces)
        {
            if (p == null) continue;
            try
            {
                p.ResetState(); // si tu TargetPiece no tiene este método, no crashea si lo manejás con if
            }
            catch (Exception)
            {
                // Silenciar si no existe ResetState en TargetPiece
            }
        }
    }

    // --------------------
    // ON GUI / botones
    // --------------------

    /// <summary>
    /// Llamar desde un botón de UI para cerrar el panel de reporte.
    /// </summary>
    public void CloseReportPanel()
    {
        if (panelReport != null)
            panelReport.SetActive(false);
    }
}
