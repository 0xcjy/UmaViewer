using UnityEngine;

/// <summary>
/// Dibuja en la SCENE VIEW (gizmos) las SONDAS laterales del mechon: puntos virtuales del hueso que
/// la pasada de escape usa para apartar el pelo por el lado que toca, sin engordar la profundidad.
///
/// No hay GameObjects por sonda: el cargador (<c>UmaContainerCharacter</c>) le rellena las listas
/// cada frame con la posicion y el radio ya calculados. Solo editor: no afecta al juego.
/// </summary>
public class CySpringHairProbeVisualizer : MonoBehaviour
{
    /// <summary>Posiciones en mundo, rellenas por el cargador cada frame.</summary>
    public Vector3[] Positions = new Vector3[0];

    /// <summary>Radio a dibujar en cada sonda (el radio propio del hueso, para comparar con las cian).</summary>
    public float[] Radii = new float[0];

    /// <summary>Color del alambre de las sondas.</summary>
    public Color Color = new Color(0.25f, 1.0f, 0.35f, 1.0f);

    private void OnDrawGizmos()
    {
        // (OCULTO) Gizmos COMENTADOS a peticion: las sondas laterales no deben aparecer en la Scene
        // view. El codigo se queda para poder reactivarlo (descomentar este metodo).
        //
        // if (Positions == null)
        //     return;
        //
        // Gizmos.color = Color;
        //
        // for (int i = 0; i < Positions.Length; i++)
        // {
        //     float radius = (Radii != null && i < Radii.Length) ? Radii[i] : 0.0f;
        //     if (radius <= 0.00001f)
        //         continue;
        //
        //     Gizmos.DrawWireSphere(Positions[i], radius);
        // }
        //
        // // Lineas del centro a cada sonda, para ver a que hueso pertenecen.
        // Gizmos.color = new Color(Color.r, Color.g, Color.b, 0.35f);
        // for (int i = 0; i < Positions.Length; i += 3)
        // {
        //     if (i + 1 >= Positions.Length || i + 2 >= Positions.Length)
        //         break;
        //
        //     Gizmos.DrawLine(Positions[i], Positions[i + 1]);
        //     Gizmos.DrawLine(Positions[i], Positions[i + 2]);
        // }
    }
}
