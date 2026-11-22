using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// Movimiento lateral del personaje con animaciones de reposo y caminar (derecha / izquierda).
/// Teclas: A/D o flechas para andar (solo mientras se mantienen pulsadas), Mayúsculas para ir más rápido.
/// En los extremos del escenario se da la vuelta.
[RequireComponent(typeof(Rigidbody2D))]
public class PersonajeController : MonoBehaviour
{
    [Header("Movimiento (unidades por segundo)")]
    public float velocidad = 1.6f;
    public float multiplicadorCorrer = 1.8f;

    [Header("Modo automático")]
    [Tooltip("Si está marcado, el personaje anda solo y se da la vuelta al llegar a cada extremo del escenario. El teclado no hace nada.")]
    public bool andarSolo = false;
    [Tooltip("Segundos que se queda quieto al darse la vuelta en modo automático.")]
    public float pausaAlGirar = 0.6f;

    [Header("Límites del escenario (X en el mundo)")]
    public float limiteIzquierdo = -9f;
    public float limiteDerecho = 1000f;

    [Header("Animación")]
    [Tooltip("Velocidad a la que los pies no patinan con el clip a su fps original (12 fps).")]
    public float velocidadNaturalDelClip = 0.95f;

    Rigidbody2D rb;
    Animator anim;
    bool mirandoDerecha = true;
    string estadoActual = "";
    bool esperandoSoltar = false;
    float pausaRestante = 0f;   // tras darse la vuelta en un extremo, espera a que se suelte la tecla

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
    }

    float LeerEje(out bool correr)
    {
        float x = 0f; correr = false;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.rightArrowKey.isPressed || k.dKey.isPressed) x += 1f;
            if (k.leftArrowKey.isPressed || k.aKey.isPressed) x -= 1f;
            correr = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
        }
#else
        x = Input.GetAxisRaw("Horizontal");
        correr = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
        return x;
    }

    void Update()
    {
        bool correr;
        float x;
        if (andarSolo)
        {
            correr = false;
            esperandoSoltar = false;
            if (pausaRestante > 0f) { pausaRestante -= Time.deltaTime; x = 0f; }
            else
            {
                x = mirandoDerecha ? 1f : -1f;
                float pos = rb.position.x;
                if (mirandoDerecha && pos >= limiteDerecho - 0.02f) { mirandoDerecha = false; x = 0f; pausaRestante = pausaAlGirar; }
                else if (!mirandoDerecha && pos <= limiteIzquierdo + 0.02f) { mirandoDerecha = true; x = 0f; pausaRestante = pausaAlGirar; }
            }
        }
        else x = LeerEje(out correr);

        // Al llegar a un extremo del escenario, el personaje se da la vuelta y se queda quieto
        // hasta que se suelte la tecla; después puede volver andando hacia el otro lado.
        float px = rb.position.x;
        bool enDerecha = px >= limiteDerecho - 0.02f;
        bool enIzquierda = px <= limiteIzquierdo + 0.02f;
        if (!andarSolo && esperandoSoltar)
        {
            if (Mathf.Abs(x) < 0.01f) esperandoSoltar = false;
            else if ((x > 0f && !mirandoDerecha) || (x < 0f && mirandoDerecha)) x = 0f; // sigue empujando hacia el borde
            else esperandoSoltar = false;                                           // ya pulsa hacia el otro lado
        }
        if (!andarSolo)
        {
            if (x > 0.01f && enDerecha) { x = 0f; mirandoDerecha = false; esperandoSoltar = true; }
            else if (x < -0.01f && enIzquierda) { x = 0f; mirandoDerecha = true; esperandoSoltar = true; }
        }

        float v = x * velocidad * (correr ? multiplicadorCorrer : 1f);

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = new Vector2(v, rb.linearVelocity.y);
#else
        rb.velocity = new Vector2(v, rb.velocity.y);
#endif
        // límites del escenario
        var p = rb.position;
        if (p.x < limiteIzquierdo) rb.position = new Vector2(limiteIzquierdo, p.y);
        if (p.x > limiteDerecho) rb.position = new Vector2(limiteDerecho, p.y);

        if (!esperandoSoltar)
        {
            if (x > 0.01f) mirandoDerecha = true;
            else if (x < -0.01f) mirandoDerecha = false;
        }

        bool andando = Mathf.Abs(v) > 0.01f;
        string lado = mirandoDerecha ? "right" : "left";
        CambiarEstado((andando ? "walk_" : "idle_") + lado);

        // los pies acompañan a la velocidad real
        if (anim != null) anim.speed = andando ? Mathf.Abs(v) / velocidadNaturalDelClip : 1f;
    }

    void CambiarEstado(string estado)
    {
        if (anim == null || estado == estadoActual) return;
        if (anim.runtimeAnimatorController == null || !anim.HasState(0, Animator.StringToHash(estado))) return; // aún no existe ese clip
        anim.Play(estado, 0, 0f);
        estadoActual = estado;
    }
}
