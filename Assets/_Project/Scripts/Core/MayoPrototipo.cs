using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public sealed class MayoPrototipo : MonoBehaviour
{
    public int nivel;
    public string titulo;
    public InputActionAsset controles;
    public UIDocument interfaz;
    public StyleSheet hojaEstilo;
    public Rigidbody2D rafaela;
    public Camera camara;
    public Transform fondo, clavel, salida, entrada;
    public Transform[] macetas;
    public Transform[] macetasDeRiego = Array.Empty<Transform>();
    public Rect[] escaleras = Array.Empty<Rect>();
    public Rect chorro;
    public float longitud;
    public float techo;
    private InputActionAsset acciones;
    private InputAction mover, saltar, correr, usar, pausa;
    private Animator animador;
    private Collider2D cuerpo;
    private readonly List<Collider2D> plataformasEscalera = new List<Collider2D>();
    private readonly Collider2D[] contactosEscalera = new Collider2D[24];
    private Vector2 movimiento;
    private bool salto, derecha = true, pausado, abriendoPuerta;
    private string estado;
    private Label estadoUi, ayuda;
    private VisualElement raiz, panel;
    private float opacidad;
    private SpriteRenderer bandaEntrada, bandaSalida;
    private BoxCollider2D zonaEntrada, zonaSalida;
    private const float OpacidadZona = .76f;
    private const string AccesoCenital = "MAYO_CenitalDesbloqueado";
    private string destino;
    private static int ultimo = 1;
    private static int llegadaNivel, llegadaDesde;
    private static int claveles;
    private static readonly HashSet<string> macetasRegadas = new HashSet<string>();
    private int recogidas;
    private bool finCenital;
    [SerializeField] private MayoCenitalBlocking zaguan;
    private InputAction canaCenital;
    private readonly Stack<Transform> recogidaCenital = new Stack<Transform>();
    private VisualElement fundido;

    private void OnEnable()
    {
        acciones = Instantiate(controles);
        mover = acciones.FindAction("Player/Move", true);
        saltar = acciones.FindAction("Player/Jump", true);
        correr = acciones.FindAction("Player/Sprint", true);
        usar = acciones.FindAction("Player/Interact", true);
        pausa = acciones.FindAction("Player/Pause", true);
        if (nivel == 6)
        {
            canaCenital = new InputAction("Caña cenital", InputActionType.Button);
            canaCenital.AddBinding("<Keyboard>/r");
            canaCenital.AddBinding("<Gamepad>/rightShoulder");
            canaCenital.Enable();
        }
        acciones.FindActionMap("Player", true).Enable();
        if (rafaela)
        {
            animador = rafaela.GetComponentInChildren<Animator>();
            cuerpo = rafaela.GetComponent<Collider2D>();
        }
    }

    private void Start()
    {
        raiz = interfaz.rootVisualElement;
        raiz.styleSheets.Add(hojaEstilo);
        if (nivel >= 1 && nivel <= 5 && clavel && (claveles & (1 << (nivel - 1))) != 0) clavel.gameObject.SetActive(false);
        if (nivel == 0)
        {
            Menu();
        }
        else
        {
            if (nivel <= 5) ultimo = nivel;
            Hud();
            AsegurarPuertaDeRegreso();
            DarEspacioAlUmbral();
            PrepararZonasDePaso();
            PrepararRampasDeObra();
            PrepararLlegada();
            foreach (var maceta in macetasDeRiego)
                if (maceta && macetasRegadas.Contains(ClaveMaceta(maceta))) MarcarRegada(maceta);
        }
    }

    private void OnDisable()
    {
        RestaurarPlataformas();
        Time.timeScale = 1;
        canaCenital?.Dispose();
        if (acciones) { acciones.Disable(); Destroy(acciones); }
        LiberarZona(bandaEntrada);
        LiberarZona(bandaSalida);
    }

    private void Menu()
    {
        raiz.Clear();
        panel = new VisualElement();
        panel.AddToClassList("panel");
        raiz.Add(panel);
        panel.Add(new Label("CÓRDOBA  ·  MAPA DE BITS") { name = "subtitulo" });
        panel.Add(new Label("MAYO") { name = "titulo" });
        Boton("Jugar", () => { claveles = 0; macetasRegadas.Clear(); ultimo = 1; Ir("Nivel_1"); });
        Boton("Continuar nivel", () => Ir("Nivel_" + ultimo));
        BotonesCenital();
        Boton("Opciones", Opciones);
        Boton("Seleccionar escena de prueba", Seleccion);
    }

    private static bool SceneDisponible(string escena) => Application.CanStreamedLevelBeLoaded(escena);

    private void BotonesCenital()
    {
        bool disponible = SceneDisponible("Cenital");
        bool desbloqueado = PlayerPrefs.GetInt(AccesoCenital,0) == 1;
        Boton(desbloqueado ? "Cenital" : "Probar cenital · Blocking",
            () => Ir("Cenital")).SetEnabled(disponible);
        if (!disponible) panel.Add(new Label("Falta registrar Cenital en la lista de escenas."));
        else if (!desbloqueado) panel.Add(new Label("En la partida se desbloquea al terminar con los cinco claveles."));
    }

    private Button Boton(string texto, Action accion)
    {
        var boton = new Button(accion) { text = texto };
        panel.Add(boton);
        return boton;
    }

    private void Seleccion()
    {
        panel.Clear();
        for (int n = 1; n <= 5; n++)
        {
            var escena = "Nivel_" + n;
            Boton("Probar nivel " + n, () => Ir(escena));
        }
        Boton("Probar cenital", () => Ir("Cenital")).SetEnabled(SceneDisponible("Cenital"));
        Boton("Volver", Menu);
    }

    private void Opciones()
    {
        panel.Clear();
        var volumen = new Slider("Volumen general", 0, 1) { value = AudioListener.volume };
        volumen.RegisterValueChangedCallback(evt => AudioListener.volume = evt.newValue);
        panel.Add(volumen);
        var pantalla = new Toggle("Pantalla completa") { value = Screen.fullScreen };
        pantalla.RegisterValueChangedCallback(evt => Screen.fullScreen = evt.newValue);
        panel.Add(pantalla);
        Boton("Volver", nivel == 0 ? (Action)Menu : MostrarPausa);
    }

    private void Hud()
    {
        raiz.Clear();
        var barra = new VisualElement();
        barra.AddToClassList("hud");
        raiz.Add(barra);
        var rotulo = new Label(nivel == 6 ? titulo : $"Nivel {nivel} · {titulo}");
        rotulo.AddToClassList("nivel-actual");
        barra.Add(rotulo);
        estadoUi = new Label(TextoContador());
        barra.Add(estadoUi);
        barra.Add(new Button(MostrarPausa) { text = "Ⅱ" });
        ayuda = new Label(nivel == 6 ? "Muévete en planta · E recoge · R riega en la dirección de marcha" : "A/D o ←/→ · Espacio salta · ↑/↓ en escaleras · E interactúa o riega");
        ayuda.AddToClassList("ayuda");
        raiz.Add(ayuda);
    }

    private void MostrarPausa()
    {
        if (abriendoPuerta || destino != null) return;
        pausado = true;
        Time.timeScale = 0;
        raiz.Clear();
        panel = new VisualElement();
        panel.AddToClassList("panel");
        raiz.Add(panel);
        panel.Add(new Label("Pausa") { name = "titulo" });
        Boton("Continuar", Continuar);
        Boton("Reiniciar nivel", () => Ir(SceneManager.GetActiveScene().name));
        Boton("Opciones", Opciones);
        Boton("Menú", () => Ir("Menu"));
    }

    private void Continuar()
    {
        pausado = false;
        movimiento = Vector2.zero;
        salto = false;
        Time.timeScale = 1;
        Hud();
    }

    private void Ir(string escena, Color? colorPaso = null)
    {
        if (destino != null) return;
        if (!SceneDisponible(escena)) { Debug.LogError("Escena no disponible: " + escena); return; }
        destino = escena;
        fundido = new VisualElement();
        fundido.AddToClassList("fundido");
        if (colorPaso.HasValue) fundido.style.backgroundColor = colorPaso.Value;
        raiz.Add(fundido);
    }

    private void Update()
    {
        if (destino != null)
        {
            opacidad += Time.unscaledDeltaTime * 3.5f;
            fundido.style.opacity = Mathf.Clamp01(opacidad);
            if (opacidad >= 1) { Time.timeScale = 1; SceneManager.LoadScene(destino); }
            return;
        }
        if (nivel == 0 || abriendoPuerta || finCenital) return;
        if (pausa.WasPressedThisFrame())
        {
            if (pausado) Continuar(); else MostrarPausa();
        }
        if (pausado) return;
        movimiento = mover.ReadValue<Vector2>();
        salto |= saltar.WasPressedThisFrame();
        estadoUi.text = TextoContador();
        if (zaguan && canaCenital.WasPressedThisFrame()) zaguan.Regar();
        if (usar.WasPressedThisFrame()) Interactuar();
    }

    private string TextoContador()
    {
        return nivel == 6
            ? $"Macetas {recogidas}/{macetas.Length}" + (zaguan ? "   ·   Cancela " + zaguan.Reloj : "")
            : $"Claveles {ContarClaveles()}/5";
    }

    private int ContarClaveles()
    {
        int total = 0;
        for (int i = 0; i < 5; i++) if ((claveles & (1 << i)) != 0) total++;
        return total;
    }

    private bool Cerca(Transform objeto)
    {
        if (!objeto || !objeto.gameObject.activeInHierarchy || !rafaela) return false;
        Vector2 punto = rafaela.position + (nivel == 6 ? Vector2.zero : Vector2.up * .45f);
        var zona = objeto == entrada ? zonaEntrada : objeto == salida ? zonaSalida : null;
        return zona ? zona.OverlapPoint(punto) : Vector2.Distance(punto, MayoElementoVisual.PosicionDe(objeto)) <= 1.35f;
    }

    private void Interactuar()
    {
        if (Cerca(clavel))
        {
            claveles |= 1 << (nivel - 1);
            clavel.gameObject.SetActive(false);
            return;
        }
        if (nivel == 6)
        {
            foreach (var maceta in macetas)
            {
                if (!Cerca(maceta)) continue;
                maceta.gameObject.SetActive(false);
                recogidas++;
                recogidaCenital.Push(maceta);
                if (recogidas == macetas.Length) CompletarCenital();
                return;
            }
            return;
        }
        if (nivel > 1 && Cerca(entrada))
        {
            StartCoroutine(AbrirPuertaYViajar(entrada, nivel - 1));
            return;
        }
        if (!Cerca(salida)) { RegarMacetaCercana(); return; }
        if (nivel < 5) StartCoroutine(AbrirPuertaYViajar(salida, nivel + 1));
        else
        {
            MostrarPausa();
            panel.Clear();
            panel.Add(new Label("Recorrido completado") { name = "titulo" });
            if (claveles == 31)
            {
                PlayerPrefs.SetInt(AccesoCenital, 1);
                PlayerPrefs.Save();
            }
            BotonesCenital();
            Boton("Menú", () => Ir("Menu"));
        }
    }

    public void RegresarAPuertaCenital()
    {
        if (nivel != 6 || pausado) return;
        if (recogidaCenital.Count > 0)
        {
            recogidaCenital.Pop().gameObject.SetActive(true);
            recogidas--;
        }
        rafaela.position = new Vector2(3,3);
        rafaela.linearVelocity = Vector2.zero;
    }

    public void CerrarPorGatoCenital()
    {
        if (nivel != 6 || finCenital) return;
        finCenital = true;
        rafaela.linearVelocity = Vector2.zero;
        MostrarPausa();
        panel.Clear();
        panel.Add(new Label("¡El gato te ha pillado!") { name = "titulo" });
        panel.Add(new Label("Vuelve a empezar el zaguán."));
        Boton("Volver a empezar", () => Ir("Cenital")).Focus();
    }

    public void CerrarPorTiempoCenital()
    {
        MostrarPausa();
        panel.Clear();
        panel.Add(new Label("Se abre la cancela") { name = "titulo" });
        panel.Add(new Label($"Macetas recogidas: {recogidas}/{macetas.Length}"));
        Boton("Reintentar", () => Ir("Cenital"));
        Boton("Volver al menú", () => Ir("Menu"));
    }

    private void CompletarCenital()
    {
        MostrarPausa();
        panel.Clear();
        panel.Add(new Label("Zaguán completado") { name = "titulo" });
        panel.Add(new Label($"Macetas recogidas: {recogidas}/{macetas.Length}"));
        Boton("Volver al menú", () => Ir("Menu"));
    }

    private string ClaveMaceta(Transform maceta) => nivel + ":" + maceta.name;

    private void RegarMacetaCercana()
    {
        Transform candidata = null;
        float mejor = float.PositiveInfinity;
        foreach (var maceta in macetasDeRiego)
        {
            if (!maceta || macetasRegadas.Contains(ClaveMaceta(maceta))) continue;
            Vector2 delta = (Vector2)MayoElementoVisual.PosicionDe(maceta) - rafaela.position;
            if (Mathf.Abs(delta.x) > 1.12f || delta.y < -3.1f || delta.y > 3.1f) continue;
            float distancia = (delta - Vector2.up * .65f).sqrMagnitude;
            if (distancia >= mejor) continue;
            mejor = distancia;
            candidata = maceta;
        }
        if (!candidata) return;
        macetasRegadas.Add(ClaveMaceta(candidata));
        MarcarRegada(candidata);
    }

    private static void MarcarRegada(Transform maceta)
    {
        if (maceta.TryGetComponent<MayoElementoVisual>(out var elemento))
            elemento.MarcarRegada();
    }

    private void AsegurarPuertaDeRegreso()
    {
        // La puerta debe existir en la escena y ser visible para el alumno.
        // Ya no se construye una jerarquía de piezas geométricas al pulsar Play.
        if (nivel >= 2 && nivel <= 5 && !entrada)
            Debug.LogWarning("Falta enlazar Entrada en MayoPrototipo. " +
                "Coloca la puerta de regreso en la escena y arrástrala a ese campo.", this);
    }

    private void PrepararLlegada()
    {
        if (llegadaNivel != nivel) return;
        var puerta = llegadaDesde < nivel ? entrada : salida;

        llegadaNivel = llegadaDesde = 0;
        if (!puerta || !rafaela) return;

        float ladoInterior = puerta == entrada ? 1f : -1f;
        rafaela.position = new Vector2(MayoElementoVisual.PosicionDe(puerta).x + ladoInterior * 1.05f, .08f);
        if (camara)
        {
            float mitad = Mathf.Min(longitud / 2f, camara.orthographicSize * camara.aspect);
            camara.transform.position = new Vector3(Mathf.Clamp(rafaela.position.x, mitad, longitud - mitad),
                2.6f, camara.transform.position.z);
        }
        StartCoroutine(MostrarLlegada(puerta));
    }

    private IEnumerator MostrarLlegada(Transform puerta)
    {
        abriendoPuerta = true;
        var banda = BandaDe(puerta);
        if (banda) banda.color = Color.white;
        yield return MoverHoja(puerta, false, banda);

        abriendoPuerta = false;
    }

    private IEnumerator AbrirPuertaYViajar(Transform puerta, int siguiente)
    {
        abriendoPuerta = true;
        movimiento = Vector2.zero;
        salto = false;
        rafaela.linearVelocity = Vector2.zero;

        derecha = MayoElementoVisual.PosicionDe(puerta).x >= rafaela.position.x;
        if (animador)
        {
            estado = derecha ? "idle_right" : "idle_left";
            animador.Play(estado,0,0);
        }
        var colorDestino = ColorEscena(siguiente);
        var banda = BandaDe(puerta);
        yield return MoverHoja(puerta, true, banda);
        yield return CruzarUmbral(puerta);
        llegadaNivel = siguiente;
        llegadaDesde = nivel;
        Ir("Nivel_" + siguiente, colorDestino);
    }

    private void DarEspacioAlUmbral()
    {
        if (nivel < 1 || nivel > 5) return;
        if (entrada) MayoElementoVisual.Ubicar(entrada, new Vector3(.95f, MayoElementoVisual.PosicionDe(entrada).y, MayoElementoVisual.PosicionDe(entrada).z));
        if (entrada) AsegurarBloqueo(entrada);
        if (nivel < 5 && salida) AsegurarBloqueo(salida);
        if (nivel < 5 && salida) MayoElementoVisual.Ubicar(salida, new Vector3(longitud-.95f, MayoElementoVisual.PosicionDe(salida).y, MayoElementoVisual.PosicionDe(salida).z));
    }

    private static void AsegurarBloqueo(Transform puerta)
    {
        if (puerta.GetComponent<BoxCollider2D>()) return;
        var bloqueo = puerta.gameObject.AddComponent<BoxCollider2D>();
        var escala = puerta.lossyScale;
        bloqueo.size = new Vector2(.22f / Mathf.Abs(escala.x), 1.75f / Mathf.Abs(escala.y));
        bloqueo.offset = (Vector2)puerta.InverseTransformPoint(MayoElementoVisual.PosicionDe(puerta))
            + new Vector2(0,.40f / Mathf.Abs(escala.y));
    }

    private void PrepararRampasDeObra()
    {
        foreach (var raiz in gameObject.scene.GetRootGameObjects())
        foreach (var borde in raiz.GetComponentsInChildren<EdgeCollider2D>())
        {
            if (!borde.name.StartsWith("Escalera de obra") || borde.pointCount != 2) continue;
            var puntos = borde.points;
            float fondo = Mathf.Min(puntos[0].y,puntos[1].y) - .18f;
            var solida = borde.GetComponent<PolygonCollider2D>();
            if (!solida) solida = borde.gameObject.AddComponent<PolygonCollider2D>();
            solida.SetPath(0,new[]{new Vector2(puntos[0].x,fondo),puntos[0],puntos[1],
                new Vector2(puntos[1].x,fondo)});
            borde.enabled = false;
        }
    }

    private IEnumerator CruzarUmbral(Transform puerta)
    {
        float sentido = puerta == entrada ? -1f : 1f;
        Vector2 inicio = rafaela.position;
        var final = new Vector2(MayoElementoVisual.PosicionDe(puerta).x + sentido * .54f, inicio.y);
        if (animador)
        {
            derecha = sentido > 0;
            estado = derecha ? "walk_right" : "walk_left";
            animador.Play(estado,0,0);
        }

        const float duracion = .50f;
        var gravedad = rafaela.gravityScale;
        rafaela.gravityScale = 0;
        float tiempo = 0;
        while (tiempo < duracion)
        {
            yield return new WaitForFixedUpdate();
            tiempo += Time.fixedDeltaTime;
            rafaela.MovePosition(Vector2.Lerp(inicio, final, Mathf.Clamp01(tiempo/duracion)));
        }
        yield return new WaitForFixedUpdate();
        rafaela.linearVelocity = Vector2.zero;
        rafaela.gravityScale = gravedad;
    }

    public static Color ColorEscena(int nivel)
    {
        switch (nivel)
        {
            case 1: return new Color32(52, 70, 97, 255);
            case 2: return new Color32(54, 91, 83, 255);
            case 3: return new Color32(96, 69, 95, 255);
            case 4: return new Color32(111, 76, 59, 255);
            case 5: return new Color32(106, 70, 88, 255);
            case 6: return new Color32(67, 85, 76, 255);
            default: return new Color32(28, 40, 61, 255);
        }
    }

    private void PrepararZonasDePaso()
    {
        if (nivel < 1 || nivel > 5) return;
        var actual = camara ? camara.backgroundColor : ColorEscena(nivel);
        if (entrada) bandaEntrada = CrearZonaDePaso(entrada, ColorEscena(nivel-1), actual, out zonaEntrada);
        if (nivel < 5 && salida) bandaSalida = CrearZonaDePaso(salida, actual, ColorEscena(nivel+1), out zonaSalida);
    }

    private SpriteRenderer BandaDe(Transform puerta) => puerta == entrada ? bandaEntrada : bandaSalida;

    private static SpriteRenderer CrearZonaDePaso(Transform puerta, Color izquierda, Color derecha,
        out BoxCollider2D zona)
    {
        const int muestras = 96;
        const float ancho = 2.45f;
        const float alto = 3.05f;
        var textura = new Texture2D(muestras,1,TextureFormat.RGBA32,false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        for (int x = 0; x < muestras; x++)
        {
            float u = x/(float)(muestras-1);
            Color color = Color.Lerp(izquierda,derecha,Mathf.SmoothStep(0,1,u));
            color = Color.Lerp(color,new Color(.84f,.89f,.84f),.16f);
            color.a = Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Min(u,1-u)*14f));
            textura.SetPixel(x,0,color);
        }
        textura.Apply(false);
        var sprite = Sprite.Create(textura,new Rect(0,0,muestras,1),new Vector2(.5f,.5f),
            muestras,0,SpriteMeshType.FullRect);
        var objeto = new GameObject("Zona de paso · Degradado y trigger");
        objeto.transform.position = MayoElementoVisual.PosicionDe(puerta) + new Vector3(0,alto/2-.55f,1);
        zona = objeto.AddComponent<BoxCollider2D>();
        zona.isTrigger = true;
        zona.size = new Vector2(ancho,alto);

        var banda = objeto.AddComponent<SpriteRenderer>();
        banda.sprite = sprite;
        banda.drawMode = SpriteDrawMode.Sliced;
        banda.size = new Vector2(ancho,alto);
        banda.sortingOrder = -60;
        banda.color = new Color(1,1,1,OpacidadZona);
        return banda;
    }

    private static void LiberarZona(SpriteRenderer banda)
    {
        if (!banda) return;
        var sprite = banda.sprite;
        if (sprite)
        {
            Destroy(sprite.texture);
            Destroy(sprite);
        }
        Destroy(banda.gameObject);
    }

    private IEnumerator MoverHoja(Transform puerta, bool abrir, SpriteRenderer banda)
    {
        // Entrada y Salida son referencias distintas. Ambas tienen MayoPuerta;
        // el clavel y las macetas solo tienen MayoElementoVisual.
        if (!puerta.TryGetComponent<MayoPuerta>(out var visual) || !visual.hojaPuerta)
            yield break;
        var bloqueo = puerta.GetComponent<BoxCollider2D>();
        if (bloqueo) bloqueo.enabled = false;
        yield return visual.AnimarHoja(abrir, abrir ? .68f : .46f, banda, OpacidadZona);
        if (!abrir && bloqueo) bloqueo.enabled = true;
    }

    private void FixedUpdate()
    {
        if (!rafaela || pausado || abriendoPuerta || destino != null) return;
        Vector2 pos = rafaela.position;
        if (nivel == 6)
        {
            rafaela.linearVelocity = zaguan ? zaguan.Velocidad(movimiento,rafaela.linearVelocity) : Vector2.ClampMagnitude(movimiento, 1) * 4;
            return;
        }
        float x = movimiento.x * (correr.IsPressed() ? 6f : 4f);
        float y = rafaela.linearVelocity.y;
        bool escalera = false;
        Rect zonaEscalera = default;
        foreach (var zona in escaleras)
        {
            if (!zona.Contains(pos)) continue;
            bool soltarse = Mathf.Abs(movimiento.x) > .45f && Mathf.Abs(movimiento.y) < .1f;
            if (soltarse || (Mathf.Abs(movimiento.y) <= .1f && rafaela.gravityScale != 0)) continue;
            escalera = true;
            zonaEscalera = zona;
            break;
        }
        rafaela.gravityScale = escalera ? 0 : 2.5f;
        if (escalera)
        {
            AbrirPasoEscalera(zonaEscalera, pos);
            float inferior = zonaEscalera.yMin + .15f;
            float superior = zonaEscalera.yMax - .48f;
            float destinoY = Mathf.Clamp(pos.y + movimiento.y * 4f * Time.fixedDeltaTime, inferior, superior);
            y = (destinoY - pos.y) / Time.fixedDeltaTime;
            x = Mathf.Clamp((zonaEscalera.center.x - pos.x) / Time.fixedDeltaTime, -4f, 4f);
        }
        else
        {
            RestaurarPlataformas();
        }
        if (!escalera && chorro.Contains(pos)) y = 8f;
        else if (!escalera && salto)
        {
            var suelo = Physics2D.Raycast(pos + Vector2.up * .04f, Vector2.down, .21f, 1);
            if (suelo.collider) y = 8.8f;
        }
        salto = false;
        rafaela.linearVelocity = new Vector2(x, y);
        if (!escalera && Mathf.Abs(x) > .1f) derecha = x > 0;
        string nuevo = (Mathf.Abs(x) > .1f ? "walk_" : "idle_") + (derecha ? "right" : "left");
        if (animador)
        {
            var sentido = derecha ? "right" : "left";
            var avance = Mathf.Abs(y) > .08f;
            if (escalera && avance)
                nuevo = (y > 0 ? "mano_subir_" : "mano_bajar_") + sentido;
            else if (!escalera && Mathf.Abs(x) > .1f)
            {
                var apoyo = Physics2D.Raycast(pos + Vector2.up * .04f, Vector2.down, .24f, 1);
                if (apoyo.collider && apoyo.collider.name.StartsWith("Escalera de obra"))
                    nuevo = (x > 0 ? "obra_subir_" : "obra_bajar_") + sentido;
            }
            if (!animador.HasState(0, Animator.StringToHash(nuevo)))
                nuevo = (Mathf.Abs(x) > .1f ? "walk_" : "idle_") + sentido;
        }
        if (animador && nuevo != estado && animador.HasState(0, Animator.StringToHash(nuevo)))
        {
            animador.Play(nuevo, 0, 0);
            estado = nuevo;
        }
        if (pos.y < -5) { rafaela.position = new Vector2(2,.1f); rafaela.linearVelocity = Vector2.zero; }
    }

    private void AbrirPasoEscalera(Rect zona, Vector2 posicion)
    {
        if (!cuerpo) return;
        var filtro = new ContactFilter2D { useTriggers = false };
        float baseY = zona.yMin + .12f;
        float tope = zona.yMax - .60f;
        float desde = Mathf.Max(baseY - .40f, posicion.y - .55f);
        float hasta = Mathf.Min(tope + .25f, posicion.y + 1.65f);
        if (hasta <= desde) return;
        int cantidad = Physics2D.OverlapBox(new Vector2(zona.center.x, (desde+hasta)/2),
            new Vector2(zona.width, hasta-desde), 0, filtro, contactosEscalera);
        for (int i = 0; i < cantidad; i++)
        {
            var plataforma = contactosEscalera[i];
            if (!plataforma.usedByEffector || !plataforma.TryGetComponent<PlatformEffector2D>(out _)
                || plataformasEscalera.Contains(plataforma)) continue;
            Physics2D.IgnoreCollision(cuerpo, plataforma);
            plataformasEscalera.Add(plataforma);
        }
    }

    private void RestaurarPlataformas()
    {
        if (cuerpo)
            foreach (var plataforma in plataformasEscalera)
                if (plataforma) Physics2D.IgnoreCollision(cuerpo, plataforma, false);
        plataformasEscalera.Clear();
    }

    private void LateUpdate()
    {
        if (!rafaela || !camara) return;
        var pos = rafaela.position;
        float mitad = Mathf.Min(longitud / 2f, camara.orthographicSize * camara.aspect);
        var objetivo = new Vector3(Mathf.Clamp(pos.x, mitad, longitud - mitad), nivel == 6
            ? Mathf.Clamp(pos.y, camara.orthographicSize, techo - camara.orthographicSize)
            : Mathf.Clamp(pos.y + 2.6f, 2.6f, techo), -10);
        camara.transform.position = Vector3.Lerp(camara.transform.position, objetivo, 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
        if (fondo) fondo.position = new Vector3(camara.transform.position.x * .84f + 7f, camara.transform.position.y * .83f, 2);
    }
}


















