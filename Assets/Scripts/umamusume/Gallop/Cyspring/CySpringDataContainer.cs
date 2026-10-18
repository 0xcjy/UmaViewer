using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop
{
    [Serializable]
    public class CySpringDataContainer : MonoBehaviour
    {
        public const int CATEGORY_NUM = 5;

        public List<CySpringCollisionData> collisionParam = new List<CySpringCollisionData>();
        public List<CySpringParamDataElement> springParam = new List<CySpringParamDataElement>();
        public List<ConnectedBoneData> ConnectedBoneList = new List<ConnectedBoneData>();

        public bool enableVerticalWind;
        public bool enableHorizontalWind;

        public float centerWindAngleSlow;
        public float centerWindAngleFast;

        public float verticalCycleSlow = 1f;
        public float horizontalCycleSlow = 1f;

        public float verticalAngleWidthSlow;
        public float horizontalAngleWidthSlow;

        public float verticalCycleFast = 1f;
        public float horizontalCycleFast = 1f;

        public float verticalAngleWidthFast;
        public float horizontalAngleWidthFast;

        public bool IsEnableHipMoveParam;
        public float HipMoveInfluenceDistance;
        public float HipMoveInfluenceMaxDistance;

        public bool UseCorrectScaleCalc;

        public void SetWindParam(CySpringParamDataAsset asset)
        {
            if (asset == null)
                return;

            enableVerticalWind = asset.EnableVerticalWind;
            enableHorizontalWind = asset.EnableHorizontalWind;

            centerWindAngleSlow = asset.CenterWindAngleSlow;
            centerWindAngleFast = asset.CenterWindAngleFast;

            verticalCycleSlow = asset.VerticalCycleSlow;
            horizontalCycleSlow = asset.HorizontalCycleSlow;

            verticalAngleWidthSlow = asset.VerticalAngleWidthSlow;
            horizontalAngleWidthSlow = asset.HorizontalAngleWidthSlow;

            verticalCycleFast = asset.VerticalCycleFast;
            horizontalCycleFast = asset.HorizontalCycleFast;

            verticalAngleWidthFast = asset.VerticalAngleWidthFast;
            horizontalAngleWidthFast = asset.HorizontalAngleWidthFast;
        }

        public void SetMoveSpringParam(CySpringParamDataAsset asset)
        {
            if (asset == null)
                return;

            IsEnableHipMoveParam = asset.IsEnableHipMoveParam;
            HipMoveInfluenceDistance = asset.HipMoveInfluenceDistance;
            HipMoveInfluenceMaxDistance = asset.HipMoveInfluenceMaxDistance;
            UseCorrectScaleCalc = asset.UseCorrectScaleCalc;
        }

        public void SetConnectedBonePara(CySpringParamDataAsset asset)
        {
            if (asset == null || asset.ConnectedBoneList == null)
            {
                ConnectedBoneList = new List<ConnectedBoneData>();
                return;
            }

            ConnectedBoneList = new List<ConnectedBoneData>(asset.ConnectedBoneList);
        }

        public CySpringDataContainer()
        {
            collisionParam = new List<CySpringCollisionData>();
            springParam = new List<CySpringParamDataElement>();
            ConnectedBoneList = new List<ConnectedBoneData>();

            verticalCycleSlow = 1f;
            horizontalCycleSlow = 1f;
            verticalCycleFast = 1f;
            horizontalCycleFast = 1f;
        }

        private void OnValidate()
        {
            if (collisionParam == null)
                collisionParam = new List<CySpringCollisionData>();

            if (springParam == null)
                springParam = new List<CySpringParamDataElement>();

            if (ConnectedBoneList == null)
                ConnectedBoneList = new List<ConnectedBoneData>();

            if (verticalCycleSlow <= 0f)
                verticalCycleSlow = 1f;

            if (horizontalCycleSlow <= 0f)
                horizontalCycleSlow = 1f;

            if (verticalCycleFast <= 0f)
                verticalCycleFast = 1f;

            if (horizontalCycleFast <= 0f)
                horizontalCycleFast = 1f;
        }

        public enum Category
        {
            Invalid = -1,
            Live = 0,
            Race = 1,
            Story = 2,
            Home = 3,
            Training = 4
        }

        // ====================================================================
        // (P1 — port del camino ORIGINAL de física / DynamicBone)
        //
        // Este contenedor era un contenedor de datos PASIVO: la simulación la llevaba el
        // puente nativo `CySpringController` (que necesita CySpringPlugin.dll). El proyecto
        // ORIGINAL NO usa ese puente: su contenedor se auto-simula aquí, añadiendo un
        // componente `DynamicBone` por hueso y los colliders correspondientes. Eso es lo que
        // hace que pelo / orejas / cola funcionen, y funciona SIN la DLL nativa.
        //
        // Se conserva el camino pasivo de abajo (SetWindParam, etc.) porque el puente nativo
        // todavía compila contra él; simplemente ya no es el que se usa para el pelo.
        // ====================================================================

        public List<DynamicBone> DynamicBones = new List<DynamicBone>();

        /// <summary>
        /// Crea un GameObject hijo por cada colisión y le añade el collider de DynamicBone
        /// correspondiente (cápsula / esfera / plano). Devuelve el mapa nombre→Transform.
        /// </summary>
        public Dictionary<string, Transform> InitiallizeCollider(Dictionary<string, Transform> bones)
        {
            var colliders = new Dictionary<string, Transform>();

            if (collisionParam == null || bones == null)
                return colliders;

            foreach (CySpringCollisionData collider in collisionParam)
            {
                if (collider == null) continue;
                if (collider.IsInner) continue;
                if (!bones.TryGetValue(collider.TargetObjectName, out Transform bone) || bone == null)
                    continue;

                var child = new GameObject(collider.CollisionName);
                child.transform.SetParent(bone);
                child.transform.localPosition = Vector3.zero;
                child.transform.localRotation = Quaternion.identity;
                child.transform.localScale = Vector3.one;
                colliders.Add(child.name, child.transform);

                //(Original) Ajustes de radio por tipo de colisión, para que la ropa/pelo
                //no se meta en el cuerpo. Se conservan tal cual.
                float radius = collider.Radius;

                if (collider.CollisionName == "Col_B_Hip_Tail") radius *= 0.96f;
                else if (collider.CollisionName == "Col_B_Chest_Tail") radius *= 1.14f;
                else if (collider.CollisionName.Contains("Col_B_Hip_Skirt")) radius *= 0.8f;
                else if (collider.CollisionName.Contains("Col_B_Hip_Jacket")) radius *= 1f;
                else if (collider.CollisionName == "Col_Elbow_R_Hair") radius *= 0.3f;
                else if (collider.CollisionName == "Col_Elbow_L_Hair") radius *= 0.3f;
                else radius *= 0.9f;

                switch (collider.Type)
                {
                    case CySpringCollisionData.CollisionType.Capsule:
                        var capsule = child.AddComponent<DynamicBoneCollider>();
                        capsule.ColliderName = collider.CollisionName;
                        child.transform.localPosition = (collider.Offset + collider.Offset2) / 2;
                        child.transform.LookAt(child.transform.TransformPoint(collider.Offset2));
                        capsule.m_Direction = DynamicBoneColliderBase.Direction.Z;
                        capsule.m_Height = (collider.Offset - collider.Offset2).magnitude + radius;
                        capsule.m_Radius = radius;
                        capsule.m_Bound = collider.IsInner
                            ? DynamicBoneColliderBase.Bound.Inside
                            : DynamicBoneColliderBase.Bound.Outside;
                        break;

                    case CySpringCollisionData.CollisionType.Sphere:
                        var sphere = child.AddComponent<DynamicBoneCollider>();
                        sphere.ColliderName = collider.CollisionName;
                        child.transform.localPosition = collider.Offset;
                        sphere.m_Radius = radius;
                        sphere.m_Height = collider.Distance;
                        sphere.m_Bound = collider.IsInner
                            ? DynamicBoneColliderBase.Bound.Inside
                            : DynamicBoneColliderBase.Bound.Outside;
                        break;

                    case CySpringCollisionData.CollisionType.Plane:
                        var plane = child.AddComponent<DynamicBonePlaneCollider>();
                        plane.ColliderName = collider.CollisionName;
                        child.transform.localPosition = collider.Offset;
                        plane.m_Bound = collider.IsInner
                            ? DynamicBoneColliderBase.Bound.Inside
                            : DynamicBoneColliderBase.Bound.Outside;
                        break;

                    case CySpringCollisionData.CollisionType.None:
                        break;
                }
            }

            return colliders;
        }

        /// <summary>
        // (P1, PRUEBA) Peso de la BASE de la cola, en escala de m_Stiffness de DynamicBone.
// 0 = sin anclaje (el hueso se va libre), 1 = clavado al padre (no se mueve).
// 0.95 es "peso extremadamente alto": el hueso conserva como mucho un 10 % de la
// separación que el original le permitía (que era ~1.8x su longitud, es decir, podía
// estirarse al 184 %). La punta se queda con el valor normal del original.
private const float TailBaseStiffness = 0.75f;

// (P1, AFINADO) El usuario reports: "ahora está muchísimo mejor, pero aún se mueve un
// poco demasiado". Se SUBE el extremo de la punta para que el gradiente llegue hasta el
// final de la cadena: antes la punta conservaba el valor degenerado del original
// (~0.08 en la cola) y por eso el último tramo quedaba libre.
//
// Ojo con el rango de DynamicBone: maxlen = restLen * (1 - stiffness) * 2. Es decir
//   0.0 → puede estirarse al 200 %  (el original: 176-184 %, de ahí el "exagerado")
//   0.5 → no puede estirarse (1.0x)  <-- "peso normal", el hueso queda a su largo
//   1.0 → no puede separarse del padre (0 %)
//
// MEDIDO en vivo: con 0.95 la base quedó al 52 % de su largo y la punta al 31 %, o sea
// la cola se enrolla sobre sí misma en vez de ondear. Por eso el peso EXTREMO no sirve:
// baja de 0.95 a 0.75 (límite 50 %) y la punta sube de 0.08 a 0.45.
private const float TailTipStiffness = 0.45f;

// (P1, AFINADO) El pelo usa un gradiente algo más suave que la cola: tiene más huesos y
// arrancarlo con 0.75 lo dejaría tieso contra la cabeza.
//
// Gradiente del pelo en TRES tramos, pedido del usuario: "aumenta ligeramente el gradiente
// un 30-40 %, pero en el último hueso, que es el más alejado del cuerpo, haz un salto del
// doble".
//   1) rampa suave   HairBaseStiffness → HairRampTipStiffness
//   2) último hueso  salto doble → HairTipStiffness
// El salto del último va en sentido contrario al resto de la rampa: el hueso más
// alejado del cuerpo es el más rígido. Es lo que pidió el usuario, no lo que hace el juego
// (en los datos del juego el drag SUBE hacia la punta), así que queda como ajuste y no
// como traducción fiel del dato.
//
// (04/10/2026) Subidos todos +0.10 por "aún hay mucho movimiento", y arreglado el
// defecto de que la rampa solo recorría el rango completo en las cadenas largas.
// MEDIDO: de 154 cadenas, 92 tienen 2 huesos y recibían un único valor fijo (0.45), y 30
// tienen 3 y recibían [0.45, 0.85] sin tramo intermedio. Con la normalización por índice
// (`t = i / lastIdx` en vez de `i / (lastIdx - 1)`) TODAS las cadenas recorren el rango
// completo, incluidas las de 2 huesos, que son el 60 % del pelo.
private const float HairBaseStiffness = 0.85f;
private const float HairRampTipStiffness = 0.55f;
private const float HairTipStiffness = 0.92f;

// (P1) Orejas: "aumenta mucho más los pesos; en la base de la cabeza prácticamente no
// deberían moverse". Con la tabla de DynamicBone, 0.97 deja el hueso a un 6 % de su longitud
// de reposo (maxlen = restLen × (1 − 0.97) × 2 = 0.06 × restLen): prácticamente clavado a
// la cabeza, que es lo pedido. Antes iban 0.75 → 0.45.
private const float EarBaseStiffness = 0.97f;
private const float EarTipStiffness = 0.65f;

/// Crea un DynamicBone por cada resorte de este contenedor y lo parametriza según el
        /// tipo de prenda (cola / pelo-cabeza / ropa). Los valores son los del original.
        /// </summary>
        public void InitializePhysics(Dictionary<string, Transform> bones, Dictionary<string, Transform> colliders)
        {
            DynamicBones.Clear();

            if (springParam == null || bones == null)
                return;

            //(Original) El tipo de prenda se decide por el NOMBRE del GameObject del
            //contenedor: "tail" → cola, "chr" → pelo/cabeza, resto → ropa.
            string nameLower = gameObject.name.ToLower();
            bool isTailObject = nameLower.Contains("tail");
            bool isHairObject = !isTailObject && nameLower.Contains("chr");

            foreach (CySpringParamDataElement spring in springParam)
            {
                if (spring == null) continue;
                if (!bones.TryGetValue(spring._boneName, out Transform bone) || bone == null)
                    continue;

                var dynamic = bone.gameObject.AddComponent<DynamicBone>();
                dynamic.m_Root = bone;

                dynamic.m_Gravity = new Vector3(0, Mathf.Clamp01(-30f / spring._gravity), 0);
                dynamic.m_LimitAngel_Min = spring._limitAngleMin;
                dynamic.m_LimitAngel_Max = spring._limitAngleMax;

                if (isTailObject)
                {
                    dynamic.m_Damping = 0.05f;
                    dynamic.m_Stiffness = Mathf.Clamp01(20f / spring._stiffnessForce);
                    dynamic.m_Elasticity = Mathf.Clamp01(20f / spring._dragForce);
                    dynamic.m_Radius = spring._collisionRadius * 0.9f;
                    dynamic.m_Inert = spring.MoveSpringApplyRate / 4f;
                }
                else if (isHairObject)
                {
                    dynamic.m_Damping = 0.1f;
                    dynamic.m_Stiffness = Mathf.Clamp01(15f / spring._stiffnessForce);
                    dynamic.m_Elasticity = Mathf.Clamp01(20f / spring._dragForce);
                    dynamic.m_Radius = spring._collisionRadius * 0.9f;
                    dynamic.m_Inert = spring.MoveSpringApplyRate / 2.5f;
                }
                else
                {
                    dynamic.m_Damping = 0.1f;
                    dynamic.m_Stiffness = Mathf.Clamp01(20f / spring._stiffnessForce);
                    dynamic.m_Elasticity = Mathf.Clamp01(30f / spring._dragForce);
                    dynamic.m_Radius = spring._collisionRadius * 0.8f;
                    dynamic.m_Inert = spring.MoveSpringApplyRate / 3f;
                }

                dynamic.SetupParticles();
                DynamicBones.Add(dynamic);

                if (spring._collisionNameList != null && colliders != null)
                {
                    foreach (string collisionName in spring._collisionNameList)
                    {
                        if (collisionName != null && colliders.TryGetValue(collisionName, out Transform tmp))
                        {
                            var col = tmp.GetComponent<DynamicBoneColliderBase>();
                            if (col != null)
                                dynamic.Particles[0].m_Colliders.Add(col);
                        }
                    }
                }

                if (spring._childElements == null)
                    continue;

                foreach (var child in spring._childElements)
                {
                    if (child == null) continue;

                    var tempParticle = dynamic.Particles.Find(p => p.m_Transform.gameObject.name == child._boneName);
                    if (tempParticle == null) continue;

                    if (isTailObject)
                    {
                        tempParticle.m_Damping = 0.05f;
                        tempParticle.m_Stiffness = Mathf.Clamp01(20f / child._stiffnessForce);
                        tempParticle.m_Elasticity = Mathf.Clamp01(20f / child._dragForce);
                        tempParticle.m_Radius = child._collisionRadius * 1f;
                        tempParticle.m_Inert = child.MoveSpringApplyRate / 4f;

                        // (P1) El gradiente de peso NO se aplica aquí: se aplica DESPUÉS, por
                        // índice, a TODOS los huesos. Ver ApplyWeightGradientByIndex.
                    }
                    else if (isHairObject)
                    {
                        tempParticle.m_Damping = 0.1f;
                        tempParticle.m_Stiffness = Mathf.Clamp01(15f / child._stiffnessForce);
                        tempParticle.m_Elasticity = Mathf.Clamp01(20f / child._dragForce);
                        tempParticle.m_Radius = child._collisionRadius * 1f;
                        tempParticle.m_Inert = child.MoveSpringApplyRate / 2.5f;

                        // (P1) El gradiente de peso se aplica por índice después, a todos los
                        // huesos. Ver ApplyWeightGradientByIndex.
                    }
                    else
                    {
                        tempParticle.m_Damping = 0.1f;
                        tempParticle.m_Stiffness = Mathf.Clamp01(20f / child._stiffnessForce);
                        tempParticle.m_Elasticity = Mathf.Clamp01(30f / child._dragForce);
                        tempParticle.m_Radius = child._collisionRadius * 0.8f;
                        tempParticle.m_Inert = child.MoveSpringApplyRate / 2f;
                    }

                    tempParticle.m_LimitAngel_Min = child._limitAngleMin;
                    tempParticle.m_LimitAngel_Max = child._limitAngleMax;

                    if (child._collisionNameList == null || colliders == null)
                        continue;

                    foreach (string collisionName in child._collisionNameList)
                    {
                        if (collisionName != null && colliders.TryGetValue(collisionName, out Transform tmp))
                        {
                            var col = tmp.GetComponent<DynamicBoneColliderBase>();
                            if (col != null)
                                tempParticle.m_Colliders.Add(col);
                        }
                    }
                }

                // (P1, COLISIÓN - T012a) Los colisores que el resorte declara se ataban SOLO a
                // `Particles[0]`, que es la partícula RAÍZ y va fijada a la animación. En
                // DynamicBone la raíz nunca colisiona (el bucle de colisión de UpdateParticles2
                // empieza en i=1), así que esos colisores eran MUERTOS: el pelo "tenía colisores
                // pero no frenaba" y la cola (sus 2 colisores iban en la raíz) se atravesaba.
                // Se propaga el conjunto de colisores del resorte (raíz + hijos) a TODAS las
                // partículas. Es la MISMA solución para pelo, orejas y cola.
                AttachCollidersToAllParticles(dynamic, spring, colliders);

                // (P1) Gradiente de peso por ÍNDICE, no por _childElements. Motivo medido: el
                // resorte de la oreja (`Sp_He_Ear0_R_00`, 6 huesos) solo declara 2 hijos, así
                // que aplicar el gradiente dentro del bucle de hijos dejaba los huesos 1-3
                // con la rigidez original ~0.02 (que permite estirarse al ~196 %) y solo 4-5
                // lo recibían. Resultado: las orejas ondeaban de más, con el mismo síntoma
                // "exagerado" que tenía la cola. Aplicar por índice cubre TODOS los huesos.
                ApplyWeightGradientByIndex(dynamic, spring._boneName);
            }

            if (DynamicBones.Count > 0)
            {
                // (T012d) Anclaje de pose: las partículas de DynamicBone se quedan en la pose que
                // había al inicializar, mientras los huesos siguen la animación y el giro manual de
                // cabeza. Medido: la partícula de Sp_He_Hair4_L_01 quedaba a 0.078 m de su hueso.
                // Como la malla sigue al HUESO y la física vive en la PARTÍCULA, la colisión
                // protegía donde no se ve. Este componente re-ancla las partículas a la pose real
                // por evento (giro de raíz) + umbral de desfase. Vale para pelo, orejas y cola.
                var poseAnchor = gameObject.GetComponent<global::DynamicBonePoseAnchor>();
                if (poseAnchor == null)
                    poseAnchor = gameObject.AddComponent<global::DynamicBonePoseAnchor>();
                poseAnchor.bones = new List<DynamicBone>(DynamicBones);
            }
        }

        /// <summary>
        /// (P1, COLISIÓN - T012a) Ata a TODAS las partículas del DynamicBone los colisores que
        /// declara el resorte (los de la raíz y los de sus `_childElements`). Antes cada lista
        /// se ataba solo a la partícula que la nombraba, y la raíz (`Particles[0]`) va fijada a
        /// la animación y nunca colisiona → pelo y cola se atravesaban. Dedupe por partícula.
        /// </summary>
        private static void AttachCollidersToAllParticles(
            DynamicBone dynamic,
            CySpringParamDataElement spring,
            Dictionary<string, Transform> colliders)
        {
            if (dynamic == null || spring == null || colliders == null)
                return;

            if (dynamic.Particles == null || dynamic.Particles.Count == 0)
                return;

            var names = new HashSet<string>(StringComparer.Ordinal);
            AddCollisionNames(names, spring._collisionNameList);

            if (spring._childElements != null)
            {
                for (int i = 0; i < spring._childElements.Count; i++)
                {
                    var child = spring._childElements[i];
                    if (child != null)
                        AddCollisionNames(names, child._collisionNameList);
                }
            }

            if (names.Count == 0)
                return;

            var resolved = new List<DynamicBoneColliderBase>();
            foreach (string name in names)
            {
                if (name != null && colliders.TryGetValue(name, out Transform tmp) && tmp != null)
                {
                    var collider = tmp.GetComponent<DynamicBoneColliderBase>();
                    if (collider != null && !resolved.Contains(collider))
                        resolved.Add(collider);
                }
            }

            if (resolved.Count == 0)
                return;

            for (int i = 0; i < dynamic.Particles.Count; i++)
            {
                var particle = dynamic.Particles[i];
                if (particle == null)
                    continue;

                if (particle.m_Colliders == null)
                    particle.m_Colliders = new List<DynamicBoneColliderBase>();

                for (int j = 0; j < resolved.Count; j++)
                {
                    if (!particle.m_Colliders.Contains(resolved[j]))
                        particle.m_Colliders.Add(resolved[j]);
                }
            }
        }

        private static void AddCollisionNames(HashSet<string> set, List<string> names)
        {
            if (set == null || names == null)
                return;

            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (!string.IsNullOrEmpty(name))
                    set.Add(name);
            }
        }

        /// <summary>
        /// (P1) Aplica el gradiente de peso por hueso según la posición en la cadena:
        /// base "pesada" (pegada al cuerpo) y punta más libre pero ya sin quedar suelta.
        ///
        /// El peso en DynamicBone se expresa con m_Stiffness porque
        /// `maxlen = restLen × (1 − stiffness) × 2`:
        ///   0.0 → puede estirarse al 200 %  ·  0.5 → queda a su largo  ·  1.0 → clavado
        /// Ese era el "movimiento exagerado": el original dejaba los huesos en 0.02-0.12,
        /// es decir, casi al 200 % de su longitud.
        ///
        /// Pelo, orejas y cola tienen cada una su propio gradiente (constantes al principio
        /// del fichero), ajustado por el usuario a ojo sobre el live.
        /// </summary>
        private static void ApplyWeightGradientByIndex(DynamicBone dynamic, string boneName)
        {
            if (dynamic == null || dynamic.Particles == null || dynamic.Particles.Count < 2)
                return;

            bool esOreja = !string.IsNullOrEmpty(boneName) && boneName.Contains("Ear");

            // (Original) Solo pelo, orejas y cola; la ropa la simula el puente nativo.
            if (!dynamic.m_Root.name.Contains("Sp_He_") && !dynamic.m_Root.name.Contains("Sp_Hi_Tail"))
                return;

            int lastIdx = dynamic.Particles.Count - 1;

            if (esOreja)
            {
                // (P1) Orejas: base clavada a la cabeza (prácticamente sin movimiento) y
                // punta algo menos rígida. Rampa simple, sin salto final.
                for (int i = 1; i <= lastIdx; i++)
                {
                    float t = (float)i / lastIdx;
                    var p = dynamic.Particles[i];
                    p.m_Stiffness = Mathf.Lerp(EarBaseStiffness, EarTipStiffness, t);
                    p.m_Damping = Mathf.Lerp(0.6f, 0.2f, t);
                }
            }
            else if (dynamic.m_Root.name.Contains("Sp_Hi_Tail"))
            {
                // (P1) Cola: rampa simple 0.75 → 0.45, validada por el usuario.
                for (int i = 1; i <= lastIdx; i++)
                {
                    float t = (float)i / lastIdx;
                    var p = dynamic.Particles[i];
                    p.m_Stiffness = Mathf.Lerp(TailBaseStiffness, TailTipStiffness, t);
                    p.m_Damping = Mathf.Lerp(0.4f, 0.08f, t);
                }
            }
            else
            {
                // (P1) Pelo: rampa suave y SALTO DOBLE en el último hueso.
                //
                // Normalización por índice COMPLETO (`t = i / lastIdx`) y no hasta el
                // penúltimo (`i / (lastIdx - 1)`). Con la versión anterior las cadenas cortas
                // no recorrían el rango: una de 2 huesos recibía un único valor fijo y una de
                // 3 recibía [finDeRampa, salto] sin tramo intermedio. MEDIDO: 92 de 154
                // cadenas tienen 2 huesos, o sea el 60 % del pelo se quedaba sin gradiente.
                for (int i = 1; i <= lastIdx; i++)
                {
                    var p = dynamic.Particles[i];

                    if (i == lastIdx)
                    {
                        p.m_Stiffness = HairTipStiffness;
                        p.m_Damping = 0.3f;
                        continue;
                    }

                    float t = (float)i / lastIdx;
                    p.m_Stiffness = Mathf.Lerp(HairBaseStiffness, HairRampTipStiffness, t);
                    p.m_Damping = Mathf.Lerp(0.35f, 0.12f, t);
                }
            }

            // La raíz (índice 0) es la que pega con el cuerpo: se deja con su valor original
            // porque su rigidez la aporta el hueso padre, no el gradiente.
        }

        public void EnablePhysics(bool isOn)
        {
            foreach (DynamicBone dynamic in DynamicBones)
            {
                if (dynamic != null)
                    dynamic.enabled = isOn;
            }
        }

        public void ResetPhysics()
        {
            foreach (DynamicBone dynamic in DynamicBones)
            {
                if (dynamic != null)
                    dynamic.ResetParticlesPosition();
            }
        }
    }
}