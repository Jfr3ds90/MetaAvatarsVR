using UnityEngine;
using UnityEditor;
using Fusion;
using Fusion.Addons.Physics;
using MetaAvatarsVR.Networking;
using MetaAvatarsVR.Networking.Pragmatic;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

namespace MetaAvatarsVR.Editor
{
    /// <summary>
    /// Herramienta para configurar automáticamente objetos agarrables en VR con Fusion
    /// </summary>
    public class FusionGrabbableSetup : EditorWindow
    {
        private GameObject targetObject;
        private bool usePhysics = true;
        private float objectMass = 1f;
        
        [MenuItem("Tools/VR/Fusion Grabbable Setup")]
        public static void ShowWindow()
        {
            GetWindow<FusionGrabbableSetup>("Fusion VR Grabbable Setup");
        }
        
        void OnGUI()
        {
            GUILayout.Label("Fusion VR Grabbable Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            targetObject = EditorGUILayout.ObjectField(
                "Target Object", 
                targetObject, 
                typeof(GameObject), 
                true
            ) as GameObject;
            
            usePhysics = EditorGUILayout.Toggle("Use Physics", usePhysics);
            
            if (usePhysics)
            {
                objectMass = EditorGUILayout.FloatField("Mass", objectMass);
            }
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("STEP 1: Clean Conflicting Components", GUILayout.Height(30)))
            {
                CleanConflictingComponents();
            }
            
            if (GUILayout.Button("STEP 2: Setup Correct Architecture", GUILayout.Height(30)))
            {
                SetupCorrectArchitecture();
            }
            
            if (GUILayout.Button("STEP 3: Validate Setup", GUILayout.Height(30)))
            {
                ValidateSetup();
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox(
                "This will:\n" +
                "1. Remove conflicting transformers\n" +
                "2. Replace NetworkTransform with NetworkRigidbody3D\n" +
                "3. Setup proper component hierarchy\n" +
                "4. Configure for VR grabbing",
                MessageType.Info
            );
        }
        
        private void CleanConflictingComponents()
        {
            if (targetObject == null)
            {
                Debug.LogError("Please assign a target object!");
                return;
            }
            
            Debug.Log("=== CLEANING CONFLICTING COMPONENTS ===");
            
            // ELIMINAR NetworkTransform (será reemplazado por NetworkRigidbody3D)
            var networkTransform = targetObject.GetComponent<NetworkTransform>();
            if (networkTransform != null)
            {
                DestroyImmediate(networkTransform);
                Debug.Log("✓ Removed NetworkTransform (will use NetworkRigidbody3D instead)");
            }
            
            // ELIMINAR todos los Transformers conflictivos
            var componentsToRemove = new System.Type[]
            {
                typeof(OneGrabFreeTransformer),
                typeof(GrabFreeTransformer),
                typeof(MoveTowardsTargetProvider),
                typeof(TwoGrabFreeTransformer)
            };
            
            foreach (var type in componentsToRemove)
            {
                var components = targetObject.GetComponents(type);
                foreach (var comp in components)
                {
                    DestroyImmediate(comp);
                    Debug.Log($"✓ Removed {type.Name}");
                }
            }
            
            // ELIMINAR scripts de debug anteriores
            var oldDebugger = targetObject.GetComponent<GrabbableDebugger>();
            if (oldDebugger != null)
            {
                DestroyImmediate(oldDebugger);
                Debug.Log("✓ Removed old GrabbableDebugger");
            }
            
            var oldPragmatic = targetObject.GetComponent<PragmaticNetworkedGrabbable>();
            if (oldPragmatic != null)
            {
                DestroyImmediate(oldPragmatic);
                Debug.Log("✓ Removed old PragmaticNetworkedGrabbable");
            }
            
            Debug.Log("=== CLEANUP COMPLETE ===");
            EditorUtility.SetDirty(targetObject);
        }
        
        private void SetupCorrectArchitecture()
        {
            if (targetObject == null)
            {
                Debug.LogError("Please assign a target object!");
                return;
            }
            
            Debug.Log("=== SETTING UP CORRECT ARCHITECTURE ===");
            
            // 1. NetworkObject (ya debe existir)
            var networkObject = targetObject.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                networkObject = targetObject.AddComponent<NetworkObject>();
                Debug.Log("✓ Added NetworkObject");
            }
            
            // Configurar NetworkObject para Shared Mode
            // NOTA: Estas propiedades se configuran en el Inspector
            Debug.Log("⚠ Configure NetworkObject in Inspector:");
            Debug.Log("  - Allow State Authority Override: TRUE (for Shared Mode)");
            Debug.Log("  - Destroy When State Authority Leaves: FALSE");
            Debug.Log("  - Default Area of Interest: Default or Scene");
            Debug.Log("⚠ Ensure NetworkRunner has RunnerSimulatePhysics3D component!");
            
            // 2. NetworkRigidbody3D (REEMPLAZA a NetworkTransform)
            var networkRb = targetObject.GetComponent<NetworkRigidbody3D>();
            if (networkRb == null)
            {
                networkRb = targetObject.AddComponent<NetworkRigidbody3D>();
                Debug.Log("✓ Added NetworkRigidbody3D");
            }
            
            // 3. Rigidbody (requerido por NetworkRigidbody3D)
            var rb = targetObject.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = targetObject.AddComponent<Rigidbody>();
                Debug.Log("✓ Added Rigidbody");
            }
            
            // Configurar Rigidbody para VR
            rb.mass = objectMass;
            rb.linearDamping = 1f;
            rb.angularDamping = 1f;
            rb.useGravity = usePhysics;
            rb.isKinematic = false; // Importante: NO kinemático por defecto
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            Debug.Log("✓ Configured Rigidbody for VR");
            
            // 4. Collider (si no existe)
            var collider = targetObject.GetComponent<Collider>();
            if (collider == null)
            {
                // Intentar obtener de MeshFilter
                var meshFilter = targetObject.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    var meshCollider = targetObject.AddComponent<MeshCollider>();
                    meshCollider.convex = true; // Debe ser convex para Rigidbody
                    Debug.Log("✓ Added MeshCollider (convex)");
                }
                else
                {
                    // Fallback a BoxCollider
                    targetObject.AddComponent<BoxCollider>();
                    Debug.Log("✓ Added BoxCollider");
                }
            }
            
            // 5. Grabbable de Meta (mantener si existe)
            var grabbable = targetObject.GetComponent<Grabbable>();
            if (grabbable == null)
            {
                grabbable = targetObject.AddComponent<Grabbable>();
                Debug.Log("✓ Added Grabbable (Meta)");
            }
            
            // 6. HandGrabInteractable (opcional pero recomendado)
            var handGrab = targetObject.GetComponent<HandGrabInteractable>();
            if (handGrab == null)
            {
                handGrab = targetObject.AddComponent<HandGrabInteractable>();
                Debug.Log("✓ Added HandGrabInteractable");
            }
            
            Debug.Log("⚠ RunnerSimulatePhysics3D should be added to NetworkRunner prefab, not to individual objects!");
            
            Debug.Log("=== ARCHITECTURE SETUP COMPLETE ===");
            EditorUtility.SetDirty(targetObject);
        }
        
        private void ValidateSetup()
        {
            if (targetObject == null)
            {
                Debug.LogError("Please assign a target object!");
                return;
            }
            
            Debug.Log("=== VALIDATING SETUP ===");
            
            bool hasIssues = false;
            
            // Verificar componentes requeridos
            CheckComponent<NetworkObject>("NetworkObject", ref hasIssues);
            CheckComponent<NetworkRigidbody3D>("NetworkRigidbody3D", ref hasIssues);
            CheckComponent<Rigidbody>("Rigidbody", ref hasIssues);
            CheckComponent<Collider>("Collider", ref hasIssues);
            CheckComponent<Grabbable>("Grabbable (Meta)", ref hasIssues);
            
            // Verificar que NO tenga NetworkTransform
            if (targetObject.GetComponent<NetworkTransform>() != null)
            {
                Debug.LogError("✗ NetworkTransform found! Should use NetworkRigidbody3D instead");
                hasIssues = true;
            }
            
            // Verificar que NO tenga transformers conflictivos
            var badTransformers = new System.Type[]
            {
                typeof(OneGrabFreeTransformer),
                typeof(GrabFreeTransformer),
                typeof(MoveTowardsTargetProvider)
            };
            
            foreach (var type in badTransformers)
            {
                if (targetObject.GetComponent(type) != null)
                {
                    Debug.LogError($"✗ {type.Name} found! This will cause conflicts");
                    hasIssues = true;
                }
            }
            
            // Verificar configuración de Rigidbody
            var rb = targetObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (rb.isKinematic)
                {
                    Debug.LogWarning("⚠ Rigidbody is kinematic - object won't respond to physics");
                }
                if (rb.collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic)
                {
                    Debug.LogWarning("⚠ Collision detection should be ContinuousDynamic for VR");
                }
                if (rb.interpolation == RigidbodyInterpolation.None)
                {
                    Debug.LogWarning("⚠ Rigidbody interpolation should be enabled for smooth VR");
                }
            }
            
            if (!hasIssues)
            {
                Debug.Log("✓✓✓ VALIDATION PASSED - Object is ready for VR networking! ✓✓✓");
            }
            else
            {
                Debug.LogError("✗✗✗ VALIDATION FAILED - Please fix issues above ✗✗✗");
            }
        }
        
        private void CheckComponent<T>(string name, ref bool hasIssues) where T : Component
        {
            if (targetObject.GetComponent<T>() == null)
            {
                Debug.LogError($"✗ Missing {name}");
                hasIssues = true;
            }
            else
            {
                Debug.Log($"✓ Has {name}");
            }
        }
    }
}