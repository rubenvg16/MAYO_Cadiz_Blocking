using UnityEngine;

public sealed class MayoCenitalBlocking : MonoBehaviour
{
    public MayoPrototipo juego;
    public Transform gato, paloma, bandada, figuraRafaela;
    public Sprite spriteCharco;
    [SerializeField] private float segundos = 180;
    [SerializeField] private float radioPaloma = 2.5f;
    [SerializeField] private float radioBandada = 6f;
    [SerializeField] private float radioGato = 5f;
    private static readonly Vector2[] ronda = {
        new Vector2(35.5f,19.5f), new Vector2(36.8f,15), new Vector2(36.8f,3),
        new Vector2(5,2), new Vector2(2,7.5f), new Vector2(2,10.8f),
        new Vector2(19.5f,10.8f), new Vector2(20,19.5f)
    };
    private Vector2 direccion = Vector2.down;
    private Vector2 rumboGato = Vector2.left;
    private Vector2 origenBandada;
    private Vector2 origenPaloma;
    private int punto;
    private float alerta, dispersaHasta, inmunidad;
    private Transform charco;
    private float venceCharco;
    private bool terminado;

    public string Reloj
    {
        get
        {
            int resta = Mathf.Max(0,Mathf.CeilToInt(segundos));
            return (resta/60).ToString("00") + ":" + (resta%60).ToString("00");
        }
    }

    private void Start()
    {
        // En esta versión el sprite cenital está en la raíz de Rafaela.
        if (!figuraRafaela && juego && juego.rafaela && juego.rafaela.GetComponent<SpriteRenderer>())
            figuraRafaela = juego.rafaela.transform;
        if (bandada) origenBandada = bandada.position;
        // Corrige también las escenas guardadas con la posición anterior.
        foreach (var maceta in juego.macetas)
            if (maceta && maceta.name == "Maceta volcada 5" &&
                Vector2.Distance(maceta.position,new Vector2(21,16.6f)) < .05f)
                maceta.position = new Vector3(17.4f,16.6f,maceta.position.z);
        if (paloma) origenPaloma = paloma.position;
    }

    public Vector2 Velocidad(Vector2 movimiento,Vector2 anterior)
    {
        if (movimiento.sqrMagnitude > .01f)
        {
            direccion = movimiento.normalized;
            if (figuraRafaela)
            {
                float angulo = Mathf.Atan2(direccion.x,-direccion.y)*Mathf.Rad2Deg;
                if (figuraRafaela == juego.rafaela.transform)
                    juego.rafaela.rotation = angulo;
                else figuraRafaela.rotation = Quaternion.Euler(0,0,angulo);
            }
        }
        Vector2 prevista = Vector2.ClampMagnitude(movimiento,1) * 4;
        if (charco && Vector2.Distance(juego.rafaela.position,charco.position)<.72f)
            return Vector2.Lerp(anterior,prevista,Time.fixedDeltaTime*2.2f);
        return prevista;
    }

    public void Regar()
    {
        if (terminado || !spriteCharco || Time.timeScale == 0) return;
        Vector2 origen = juego.rafaela.position;
        var golpe = Physics2D.Raycast(origen,direccion,1.28f,1);
        Vector2 fin = golpe.collider ? origen + direccion * Mathf.Max(0,golpe.distance-.18f) : origen+direccion*1.28f;
        if (!charco)
        {
            charco = new GameObject("Charco · Suelo mojado").transform;
            charco.SetParent(transform,false);
            var vista = charco.gameObject.AddComponent<SpriteRenderer>();
            vista.sprite = spriteCharco;
            vista.color = new Color(.35f,.77f,.85f,.58f);
            vista.sortingOrder = -3;
            charco.localScale = new Vector3(1.42f,1.42f,1);
        }
        charco.position = fin;
        venceCharco = Time.time+8;
        if (bandada && Vector2.Distance(fin,bandada.position)<radioBandada &&
            !Physics2D.Linecast(fin,bandada.position,1))
            dispersaHasta = Time.time+6;
    }

    private void Update()
    {
        if (terminado || Time.timeScale == 0 || !juego || !juego.rafaela) return;
        segundos = Mathf.Max(0,segundos-Time.deltaTime);
        if (segundos <= 0)
        {
            terminado = true;
            juego.CerrarPorTiempoCenital();
            return;
        }
        if (charco && Time.time >= venceCharco) { Destroy(charco.gameObject); charco = null; }
        Vector2 jugador = juego.rafaela.position;
        if (paloma)
        {
            Vector2 delta = (Vector2)paloma.position-jugador;
            if (delta.magnitude < radioPaloma && juego.rafaela.linearVelocity.sqrMagnitude > 8)
            {
                Vector2 destino = (Vector2)paloma.position + delta.normalized * (3.3f*Time.deltaTime);
                if (!Physics2D.Linecast(paloma.position,destino,1)) paloma.position = Limitar(destino);
            }
            else paloma.position = Vector2.MoveTowards(paloma.position,origenPaloma,Time.deltaTime*.65f);
        }
        if (bandada)
        {
            bool dispersa = Time.time < dispersaHasta;
            var destino = dispersa ? origenBandada + Vector2.up * 1.1f : origenBandada;
            bandada.position = Vector2.MoveTowards(bandada.position,destino,Time.deltaTime*1.6f);
            bandada.localScale = Vector3.Lerp(bandada.localScale,Vector3.one*(dispersa ? 1.7f : 1f),Time.deltaTime*7f);
            if (!dispersa && Time.time >= inmunidad && Ve(bandada.position,Vector2.down,jugador,radioBandada,.65f))
                Captura();
        }
        if (gato)
        {
            Vector2 paso = Vector2.MoveTowards(gato.position,ronda[punto],Time.deltaTime*2f);
            var avance = paso-(Vector2)gato.position;
            if (avance.sqrMagnitude > .00001f) rumboGato = avance.normalized;
            gato.position = paso;
            gato.rotation = Quaternion.Euler(0,0,Mathf.Atan2(rumboGato.x,-rumboGato.y)*Mathf.Rad2Deg);
            if (Vector2.Distance(paso,ronda[punto]) < .05f) punto = (punto+1)%ronda.Length;
            if (Time.time >= inmunidad && Ve(gato.position,rumboGato,jugador,radioGato,.60f))
                alerta += Time.deltaTime;
            else alerta = Mathf.Max(0,alerta-Time.deltaTime*2);
            if (alerta > .60f)
            {
                terminado = true;
                juego.CerrarPorGatoCenital();
            }
        }
    }

    private void Captura()
    {
        inmunidad = Time.time+1.5f;
        alerta = 0;
        juego.RegresarAPuertaCenital();
    }

    private static Vector2 Limitar(Vector2 punto) =>
        new Vector2(Mathf.Clamp(punto.x,.8f,37.6f),Mathf.Clamp(punto.y,.8f,20.8f));

    private static bool Ve(Vector2 origen,Vector2 direccion,Vector2 objetivo,float radio,float coseno)
    {
        Vector2 delta = objetivo-origen;
        return delta.sqrMagnitude < radio*radio && Vector2.Dot(direccion,delta.normalized)>coseno
            && !Physics2D.Linecast(origen,objetivo,1);
    }
}




