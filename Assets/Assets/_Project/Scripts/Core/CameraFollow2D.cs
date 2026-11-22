using UnityEngine;

/// Cámara que sigue al personaje solo en horizontal, con suavizado y límites.
public class CameraFollow2D : MonoBehaviour
{
    public Transform objetivo;
    public float suavizado = 0.15f;
    [Tooltip("Adelanto de la cámara hacia donde mira el personaje")]
    public float adelanto = 2f;
    public float minX = 0f;
    public float maxX = 1000f;
    float velX;
    float ultimaX;
    float dir = 1f;

    void Start() { if (objetivo != null) ultimaX = objetivo.position.x; }

    void LateUpdate()
    {
        if (objetivo == null) return;
        float dx = objetivo.position.x - ultimaX;
        if (Mathf.Abs(dx) > 0.0005f) dir = Mathf.Sign(dx);
        ultimaX = objetivo.position.x;
        float destino = Mathf.Clamp(objetivo.position.x + dir * adelanto, minX, maxX);
        float x = Mathf.SmoothDamp(transform.position.x, destino, ref velX, suavizado);
        transform.position = new Vector3(x, transform.position.y, transform.position.z);
    }
}
