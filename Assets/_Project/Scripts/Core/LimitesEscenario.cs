using UnityEngine;

/// Calcula, al empezar la partida, los límites del movimiento del personaje y el tope de la cámara
/// a partir de las piezas que haya en el recorrido de zonas (ParallaxSequence).
/// Si se añaden o quitan zonas, los límites se recalculan solos al pulsar Play.
[RequireComponent(typeof(PersonajeController))]
public class LimitesEscenario : MonoBehaviour
{
    [Tooltip("La capa con las zonas y transiciones, una detrás de otra")]
    public ParallaxSequence recorrido;
    [Tooltip("Distancia al borde del escenario a la que se para el personaje (m)")]
    public float margen = 0.6f;

    [Header("Calculado (solo lectura)")]
    public float longitudPiezas;
    public float recorridoCamara;

    void Start() => Aplicar();

    public void Aplicar()
    {
        if (recorrido == null) recorrido = FindAnyObjectByType<ParallaxSequence>();
        var cam = Camera.main;
        if (recorrido == null || cam == null) return;
        recorrido.cam = cam;
        recorrido.Colocar();                                   // suma los anchos de las piezas cargadas
        longitudPiezas = recorrido.Longitud;
        recorridoCamara = recorrido.RecorridoCamara(cam);      // (longitud − pantalla) / parallax
        float mitad = cam.orthographicSize * cam.aspect;
        float x0 = recorrido.camaraInicioX;

        var ctl = GetComponent<PersonajeController>();
        ctl.limiteIzquierdo = x0 - mitad + margen;                   // principio de la primera pieza
        ctl.limiteDerecho = x0 + recorridoCamara + mitad - margen;   // final de la última pieza

        var follow = cam.GetComponent<CameraFollow2D>();
        if (follow != null) { follow.minX = x0; follow.maxX = x0 + recorridoCamara; }
    }
}
