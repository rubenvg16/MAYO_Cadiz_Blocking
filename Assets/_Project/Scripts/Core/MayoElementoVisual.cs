using TMPro;
using UnityEngine;

/// <summary>
/// Elementos con los que se interactúa.
/// </summary>
[DisallowMultipleComponent]
public sealed class MayoElementoVisual : MonoBehaviour
{
    public enum TipoElemento { General, Coleccionable, PuertaAvance, PuertaRegreso, MacetaDeRiego, MetaFinal }
    public TipoElemento tipo;
    public SpriteRenderer spritePrincipal;
    public TMP_Text rotulo;

    [Tooltip("Punto local utilizado por las distancias de interacción. Conserva el punto original al centrar el sprite en la raíz.")]
    public Vector3 puntoInteraccionLocal;

    public Vector3 PuntoInteraccion => transform.TransformPoint(puntoInteraccionLocal);

    /// <summary>Usa el punto lógico, aunque el dibujo tenga otro centro visual.</summary>
    public static Vector3 PosicionDe(Transform objeto)
    {
        if (objeto.TryGetComponent<MayoElementoVisual>(out var visual))
            return visual.PuntoInteraccion;
        return objeto.position;
    }

    /// <summary>Reubica el punto lógico sin alterar el ajuste del dibujo ni del collider.</summary>
    public static void Ubicar(Transform objeto, Vector3 punto)
    {
        objeto.position += punto - PosicionDe(objeto);
    }

    public void MarcarRegada()
    {
        if (spritePrincipal) spritePrincipal.color = new Color32(163, 235, 155, 255);
    }
}
