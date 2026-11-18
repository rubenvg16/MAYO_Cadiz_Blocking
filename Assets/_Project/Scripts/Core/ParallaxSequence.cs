using UnityEngine;

/// Capa de parallax formada por una secuencia de piezas una tras otra (calle → transición → trasera ...).
/// Los hijos (sprites con pivote abajo-izquierda) se colocan en orden al empezar.
public class ParallaxSequence : MonoBehaviour
{
    [Range(0f, 2f)] public float parallax = 0.45f;
    public Camera cam;
    [Tooltip("X de la cámara en la que la primera pieza empieza en el borde izquierdo de la pantalla")]
    public float camaraInicioX = 0f;
    public float Longitud { get; private set; }

    void Start() { if (cam == null) cam = Camera.main; Colocar(); }

    public void Colocar()
    {
        float x = 0f;
        foreach (Transform t in transform)
        {
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) continue;
            t.localPosition = new Vector3(x, 0f, 0f);
            x += sr.sprite.bounds.size.x;
        }
        Longitud = x;
    }

    void LateUpdate()
    {
        float camX = cam.transform.position.x;
        float mitadVista = cam.orthographicSize * cam.aspect;
        float x = camaraInicioX - mitadVista + (camX - camaraInicioX) * (1f - parallax);
        transform.position = new Vector3(x, transform.position.y, transform.position.z);
    }

    /// Distancia que recorre la cámara hasta ver la última pieza entera.
    public float RecorridoCamara(Camera c)
    {
        float vista = c.orthographicSize * 2f * c.aspect;
        return Mathf.Max(0f, (Longitud - vista) / Mathf.Max(0.0001f, parallax));
    }
}
