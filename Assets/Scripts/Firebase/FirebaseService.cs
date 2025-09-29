using UnityEngine;
using Proyecto26;
using System;

/// <summary>
/// Servicio simple para enviar y guardar datos en Firebase Realtime Database
/// usando Proyecto26 RestClient.
/// </summary>
public static class FirebaseService
{
    // Setear desde inspector al iniciar (ej: GameManager)
    public static string DatabaseUrl = "https://YOUR_DB_URL.firebaseio.com/";

    // Clase auxiliar serializable para Vector3 (JSON-friendly)
    [Serializable]
    public class Vec3
    {
        public float x;
        public float y;
        public float z;
        public Vec3() { }
        public Vec3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    // Estructura con los datos del tiro (será enviada a Firebase)
    [Serializable]
    public class ShotReport
    {
        public string id;
        public string playerName;
        public int score;
        public int fallenCount;
        public float timeOfFlight;
        public Vec3 impactPoint;
        public float impactImpulse;
        public float impactRelativeVelocity;
        public string timestamp; // ISO 8601 string

        public ShotReport() { }
    }

    /// <summary>
    /// Publica un ShotReport en la ruta "nodePath/{report.id}.json".
    /// Genera un id automáticamente si report.id está vacío.
    /// </summary>
    public static void PostShotReport(string nodePath, ShotReport report, System.Action onSuccess = null, System.Action<Exception> onError = null)
    {
        if (string.IsNullOrEmpty(DatabaseUrl))
        {
            Debug.LogError("[FirebaseService] DatabaseUrl no está seteada.");
            onError?.Invoke(new System.Exception("DatabaseUrl vacío"));
            return;
        }

        if (string.IsNullOrEmpty(report.id))
            report.id = System.Guid.NewGuid().ToString();

        string baseUrl = DatabaseUrl.TrimEnd('/');
        string path = nodePath.Trim('/');

        string url = $"{baseUrl}/{path}/{report.id}.json";

        // PUT crea/actualiza la entrada con la clave report.id
        RestClient.Put(url, report).Then(response =>
        {
            Debug.Log($"[FirebaseService] Report enviado: {report.id}");
            onSuccess?.Invoke();
        }).Catch(err =>
        {
            Debug.LogError($"[FirebaseService] Error al enviar report: {err.Message}");
            onError?.Invoke(err);
        });
    }
}
