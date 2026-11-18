using System.Collections;
using UnityEngine;

/// <summary>
/// Solo para puertas: anima la hoja y mantiene su bisagra.
/// Se coloca en la raíz que contiene el SpriteRenderer del marco.
/// MayoPrototipo decide el destino según su enlace Entrada o Salida.
/// Los coleccionables no llevan este componente.
/// </summary>
[DisallowMultipleComponent]
public sealed class MayoPuerta : MonoBehaviour
{
    [Tooltip("SpriteRenderer del único hijo móvil de la puerta.")]
    public SpriteRenderer hojaPuerta;
    [Min(.1f)] public float anchoAbierto = .72f;
    private bool poseGuardada;
    private Vector3 posicionCerrada, escalaCerrada;

    /// <summary>Conserva la posición de la bisagra y restaura exactamente la pose cerrada.</summary>
    public IEnumerator AnimarHoja(bool abrir, float duracion, SpriteRenderer banda, float opacidad)
    {
        if (!hojaPuerta || !hojaPuerta.sprite) yield break;
        var hoja = hojaPuerta.transform;
        if (!poseGuardada)
        {
            posicionCerrada = hoja.localPosition;
            escalaCerrada = hoja.localScale;
            poseGuardada = true;
        }
        float tiempo = 0;
        while (true)
        {
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(tiempo / Mathf.Max(.01f, duracion)));
            float apertura = abrir ? t : 1 - t;
            AplicarApertura(apertura);
            if (banda) banda.color = new Color(1, 1, 1, Mathf.Lerp(opacidad, 1, apertura));
            if (tiempo >= duracion) break;
            yield return null;
            tiempo += Time.unscaledDeltaTime;
        }
    }

    private void AplicarApertura(float t)
    {
        var hoja = hojaPuerta.transform;
        var limites = hojaPuerta.sprite.bounds;
        float padreX = hoja.parent ? Mathf.Abs(hoja.parent.lossyScale.x) : 1;
        float anchoSprite = Mathf.Max(.0001f, limites.size.x);
        float objetivo = Mathf.Max(Mathf.Abs(escalaCerrada.x),
            anchoAbierto / (anchoSprite * Mathf.Max(.0001f, padreX)));
        float sx = Mathf.Lerp(Mathf.Abs(escalaCerrada.x), objetivo, t) * Mathf.Sign(escalaCerrada.x);
        float borde = hojaPuerta.flipX ? -limites.max.x : limites.min.x;
        float bisagra = posicionCerrada.x + borde * escalaCerrada.x;
        hoja.localScale = new Vector3(sx, escalaCerrada.y, escalaCerrada.z);
        hoja.localPosition = new Vector3(bisagra - borde * sx, posicionCerrada.y, posicionCerrada.z);
    }
}
