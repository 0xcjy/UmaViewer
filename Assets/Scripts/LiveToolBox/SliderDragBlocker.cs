using UnityEngine;
using UnityEngine.EventSystems;

//Corta el arrastre en los botones que cuelgan del slider de progreso del live (play/pausa y salir).
//
//Unity resuelve el objeto que recibe el arrastre subiendo por la jerarquia hasta encontrar un
//IDragHandler. `Button` (Selectable) no lo implementa, asi que el arrastre acababa en el `Slider`
//padre: pulsar el boton y mover el raton unos pixeles movia la barra. Como el boton esta a la
//izquierda del todo (fuera del rect del slider), el valor se recortaba a 0 y el show volvia al
//principio al reanudar.
//
//Con este componente en el propio boton, el arrastre se queda aqui y no llega al Slider. Es un
//bloqueador vacio a proposito: el boton sigue funcionando con su onClick normal.
public class SliderDragBlocker : MonoBehaviour,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    //Anade el bloqueador al boton si no lo tiene ya.
    public static void Ensure(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponent<SliderDragBlocker>() == null) go.AddComponent<SliderDragBlocker>();
    }

    public void OnInitializePotentialDrag(PointerEventData eventData) { }
    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData) { }
    public void OnEndDrag(PointerEventData eventData) { }
}
