using System.Collections.Generic;
using UnityEngine;

/// Capa de parallax que se repite sin fin en X (montañas, primer plano...).
/// parallax: 0 = quieta en el fondo · 0,15 = muy lejos · 1 = se mueve con el escenario · >1 = primer plano.
/// derivaPropia mueve la capa sola aunque la cámara esté quieta (nubes).
/// Los hijos forman UN ciclo (un sprite, o varios seguidos). El script crea las copias necesarias.
public class ParallaxLoop : MonoBehaviour
{
    [Range(0f, 2f)] public float parallax = 0.15f;
    [Tooltip("Movimiento propio en unidades por segundo (p. ej. nubes empujadas por el viento). Negativo = hacia la izquierda.")]
    public float derivaPropia = 0f;
    public Camera cam;
    float anchoCiclo;
    float deriva;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        var ciclo = new GameObject("ciclo_0").transform;
        ciclo.SetParent(transform, false);
        var hijos = new List<Transform>();
        foreach (Transform t in transform) if (t != ciclo) hijos.Add(t);
        float x = 0f;
        foreach (var t in hijos)
        {
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) continue;
            t.SetParent(ciclo, false);
            t.localPosition = new Vector3(x, 0f, 0f);
            x += sr.sprite.bounds.size.x;
        }
        anchoCiclo = x;
        if (anchoCiclo <= 0f) return;
        // copias de sobra para cualquier proporción de pantalla (hasta 21:9), aunque la vista Game
        // tenga otra forma al pulsar Play
        float vista = cam.orthographicSize * 2f * Mathf.Max(cam.aspect, 21f / 9f);
        int copias = Mathf.CeilToInt(vista / anchoCiclo) + 3;
        for (int i = 1; i < copias; i++)
        {
            var c = Instantiate(ciclo.gameObject, transform).transform;
            c.name = "ciclo_" + i;
            c.localPosition = new Vector3(i * anchoCiclo, 0f, 0f);
        }
    }

    void LateUpdate()
    {
        if (anchoCiclo <= 0f) return;
        float camX = cam.transform.position.x;
        float mitadVista = cam.orthographicSize * cam.aspect;
        deriva += derivaPropia * Time.deltaTime;
        float desplazado = camX * parallax - deriva;
        float resto = desplazado - Mathf.Floor(desplazado / anchoCiclo) * anchoCiclo;
        float x = camX - mitadVista - resto - anchoCiclo;
        transform.position = new Vector3(x, transform.position.y, transform.position.z);
    }
}
