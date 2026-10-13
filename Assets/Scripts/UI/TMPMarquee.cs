using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Marquesina horizontal para un <see cref="TMP_Text"/> que no cabe en el ancho de su rect:
/// el texto se desplaza hacia la izquierda (los primeros caracteres salen por la izquierda) y
/// reaparece por la derecha, en bucle. Si el texto cabe, se queda quieto.
///
/// Se usa en las etiquetas de los items de los desplegables para que un nombre largo no se parta en
/// dos líneas y se solape con la fila de al lado: una sola línea que se desplaza. El recorte lo hace
/// el RectMask2D de la fila.
/// </summary>
public class TMPMarquee : MonoBehaviour
{
    [Tooltip("Texto a desplazar. Si se deja vacío (o apunta fuera de este GameObject) se coge el TMP_Text de este mismo GameObject.")]
    public TMP_Text Target;

    [Tooltip("Velocidad de desplazamiento, en píxeles por segundo.")]
    public float Speed = 40f;

    [Tooltip("Tiempo quieto al aparecer, para poder leer el principio.")]
    public float StartDelay = 1f;

    [Tooltip("Hueco que se deja antes de que el texto vuelva a entrar por la derecha.")]
    public float Gap = 48f;

    private RectTransform _rt;
    private float _baseX;
    private bool _hasBase;
    private float _timer;
    private string _lastText;

    private void Awake()
    {
        _rt = transform as RectTransform;
        if (Target == null || Target.gameObject != gameObject)
            Target = GetComponent<TMP_Text>();

        if (Target != null)
        {
            //Una sola línea, sin partirse; el recorte lo hace el RectMask2D de la fila.
            Target.enableWordWrapping = false;
            Target.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private void OnEnable()
    {
        //Cada vez que la fila se (re)activa, se vuelve a empezar por el principio.
        _hasBase = false;
        _timer = StartDelay;
        _lastText = null;
    }

    private void LateUpdate()
    {
        if (Target == null || _rt == null) return;

        if (!_hasBase)
        {
            _baseX = _rt.anchoredPosition.x;
            _hasBase = true;
        }

        //El texto cambió (la lista se rellenó / se filtró): vuelve a empezar por el principio.
        if (Target.text != _lastText)
        {
            _lastText = Target.text;
            _timer = StartDelay;
            _rt.anchoredPosition = new Vector2(_baseX, _rt.anchoredPosition.y);
        }

        //Se mide con el layout YA calculado por TMP (textInfo), sin forzar un reparseo de la fuente.
        float textW = MeasureFirstLineWidth();
        if (textW <= 0f) return;
        float viewW = ViewWidth();

        //Cabe entero: quieto.
        if (textW <= viewW + 0.5f)
        {
            if (!Mathf.Approximately(_rt.anchoredPosition.x, _baseX))
                _rt.anchoredPosition = new Vector2(_baseX, _rt.anchoredPosition.y);
            return;
        }

        if (_timer > 0f)
        {
            _timer -= Time.unscaledDeltaTime;
            return;
        }

        float x = _rt.anchoredPosition.x - Speed * Time.unscaledDeltaTime;

        //Cuando el texto ha salido del todo por la izquierda, vuelve a entrar por la derecha.
        if (x < _baseX - (textW + Gap))
            x = _baseX + viewW;

        _rt.anchoredPosition = new Vector2(x, _rt.anchoredPosition.y);
    }

    //Ancho visible: el del recorte (RectMask2D del padre) si lo hay, porque la etiqueta puede ser
    //más estrecha que la fila; si no, el propio rect de la etiqueta.
    private float ViewWidth()
    {
        var parent = _rt.parent as RectTransform;
        if (parent != null && parent.GetComponent<RectMask2D>() != null)
        {
            float w = parent.rect.width;
            if (w > 0f) return w;
        }
        return _rt.rect.width;
    }

    //Ancho real de la primera línea según el textInfo ya generado (sin ForceMeshUpdate ni
    //preferredWidth, que forzarían un parseo extra de la fuente).
    private float MeasureFirstLineWidth()
    {
        TMP_TextInfo info = Target.textInfo;
        if (info == null || info.characterCount <= 0 || info.lineCount <= 0) return 0f;

        TMP_LineInfo line = info.lineInfo[0];
        int first = line.firstCharacterIndex;
        int last = line.lastCharacterIndex;
        if (first < 0 || last < first || last >= info.characterCount) return 0f;

        float min = info.characterInfo[first].bottomLeft.x;
        float max = info.characterInfo[last].topRight.x;
        float w = max - min;
        return w > 0f ? w : 0f;
    }
}
