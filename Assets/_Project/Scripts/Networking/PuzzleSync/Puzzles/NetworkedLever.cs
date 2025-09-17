using UnityEngine;
using Fusion;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkedLever : NetworkBehaviour, ITransformer
    {
        [Header("Configuration")]
        [SerializeField] private int _leverIndex;
        [SerializeField] private string _leverLetter = "A";
        
        [Header("Angles")]
        [SerializeField] private float _restAngle = 120f;
        [SerializeField] private float _activationAngle = 55f;
        [SerializeField] private float _minAngle = 45f;
        [SerializeField] private float _maxAngle = 130f;
        
        [Header("References")]
        [SerializeField] private NetworkedLeverPuzzle _puzzleController;
        
        [Header("Events")]
        public UnityEvent<string> OnLeverActivated;
        public UnityEvent<string> OnLeverDeactivated;
        
        [Networked] public float NetworkedAngle { get; set; }
        [Networked] public NetworkBool IsActivated { get; set; }
        [Networked] public NetworkBool IsGrabbed { get; set; }
        
        private IGrabbable _grabbable;
        private Transform _parentReference; 
        private Vector3 _fixedWorldPosition;
        
        private bool _isTransforming = false;
        private float _grabStartAngle;
        private Vector3 _grabStartDirectionLocal; 
        private bool _wasActivated = false;
        
        private void Awake()
        {
            _parentReference = transform.parent;
            if (_parentReference == null)
            {
                Debug.LogError($"[Lever {_leverLetter}] Must be child of a parent transform!");
                GameObject parent = new GameObject($"LeverParent_{_leverLetter}");
                parent.transform.position = transform.position;
                parent.transform.rotation = transform.rotation;
                transform.SetParent(parent.transform);
                _parentReference = parent.transform;
            }
            
            _fixedWorldPosition = transform.position;
            
            SetupComponents();
            ApplyLocalAngle(_restAngle);
        }
        
        private void SetupComponents()
        {
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            
            var grabbable = GetComponent<Grabbable>();
            if (grabbable == null)
            {
                grabbable = gameObject.AddComponent<Grabbable>();
            }
            
            grabbable.InjectOptionalOneGrabTransformer(this);
            
            var handGrab = GetComponent<HandGrabInteractable>();
            if (handGrab == null)
            {
                handGrab = gameObject.AddComponent<HandGrabInteractable>();
                handGrab.InjectOptionalPointableElement(grabbable);
                handGrab.InjectRigidbody(rb);
            }
        }
        
        #region ITransformer Implementation
        
        public IGrabbable Grabbable => _grabbable;
        
        public void Initialize(IGrabbable grabbable)
        {
            _grabbable = grabbable;
        }
        
        public void BeginTransform()
        {
            _isTransforming = true;
            _grabStartAngle = transform.localEulerAngles.z;
            
            // Obtener dirección inicial en espacio LOCAL del padre
            if (_grabbable.GrabPoints.Count > 0)
            {
                Vector3 handWorldPos = _grabbable.GrabPoints[0].position;
                // Convertir a espacio local del padre
                Vector3 handLocalPos = _parentReference.InverseTransformPoint(handWorldPos);
                // Proyectar en plano XY local
                _grabStartDirectionLocal = new Vector3(handLocalPos.x, handLocalPos.y, 0).normalized;
            }
            
            if (Runner && Runner.IsRunning)
            {
                RPC_SetGrabbed(true);
            }
            
            Debug.Log($"[Lever {_leverLetter}] Grab started at local angle {_grabStartAngle:F1}°");
        }
        
        public void UpdateTransform()
        {
            if (!_isTransforming) return;
            
            // MANTENER posición mundial fija
            transform.position = _fixedWorldPosition;
            
            // Calcular rotación basada en espacio LOCAL del padre
            if (_grabbable.GrabPoints.Count > 0)
            {
                Vector3 handWorldPos = _grabbable.GrabPoints[0].position;
                
                // Convertir posición de mano a espacio local del padre
                Vector3 handLocalPos = _parentReference.InverseTransformPoint(handWorldPos);
                
                // Proyectar en plano XY local del padre
                Vector3 currentDirectionLocal = new Vector3(handLocalPos.x, handLocalPos.y, 0).normalized;
                
                // Calcular ángulo entre direcciones EN ESPACIO LOCAL
                float angleDelta = Vector3.SignedAngle(
                    _grabStartDirectionLocal, 
                    currentDirectionLocal, 
                    Vector3.forward  // Eje Z local
                );
                
                float targetAngle = Mathf.Clamp(_grabStartAngle + angleDelta, _minAngle, _maxAngle);
                
                // Aplicar rotación LOCAL
                ApplyLocalAngle(targetAngle);
            }
        }
        
        public void EndTransform()
        {
            _isTransforming = false;
            
            // Asegurar posición fija
            transform.position = _fixedWorldPosition;
            
            if (Runner && Runner.IsRunning)
            {
                float finalAngle = transform.localEulerAngles.z;
                RPC_SetGrabbed(false);
                RPC_UpdateAngle(finalAngle);
            }
            
            Debug.Log($"[Lever {_leverLetter}] Grab ended at local angle {transform.localEulerAngles.z:F1}°");
        }
        
        #endregion
        
        private void ApplyLocalAngle(float angle)
        {
            transform.localRotation = Quaternion.Euler(0, 0, angle);
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                NetworkedAngle = _restAngle;
                IsActivated = false;
            }
            
            ApplyLocalAngle(NetworkedAngle);
            
            if (_puzzleController != null)
            {
                _puzzleController.RegisterLever(this, _leverIndex);
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            
            transform.position = _fixedWorldPosition;
            
            if (IsGrabbed && _isTransforming)
            {
                NetworkedAngle = transform.localEulerAngles.z;
            }
            
            bool shouldBeActive = NetworkedAngle <= _activationAngle;
            if (shouldBeActive != _wasActivated)
            {
                IsActivated = shouldBeActive;
                _wasActivated = shouldBeActive;
                
                if (_puzzleController != null)
                {
                    _puzzleController.OnLeverStateChanged(_leverIndex, IsActivated);
                }
                
                RPC_NotifyActivation(IsActivated);
            }
        }
        
        public override void Render()
        {
            transform.position = _fixedWorldPosition;
            
            if (!_isTransforming)
            {
                float current = transform.localEulerAngles.z;
                float target = NetworkedAngle;
                ApplyLocalAngle(Mathf.LerpAngle(current, target, Time.deltaTime * 10f));
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_SetGrabbed(NetworkBool grabbed) => IsGrabbed = grabbed;
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_UpdateAngle(float angle) => NetworkedAngle = angle;
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyActivation(NetworkBool activated)
        {
            if (activated)
                OnLeverActivated?.Invoke(_leverLetter);
            else
                OnLeverDeactivated?.Invoke(_leverLetter);
        }
        
        public void ResetLever()
        {
            if (HasStateAuthority)
            {
                NetworkedAngle = _restAngle;
                IsActivated = false;
                IsGrabbed = false;
                _wasActivated = false;
            }
            ApplyLocalAngle(_restAngle);
        }
        
        public int GetLeverIndex() => _leverIndex;
        public string GetLeverLetter() => _leverLetter;
        public bool GetIsActivated() => IsActivated;
        
        #region Debug Visualization (Solo Visual)

private void OnDrawGizmosSelected()
{
    if (_parentReference == null && transform.parent != null)
        _parentReference = transform.parent;
        
    if (_parentReference == null) return;
    
    DrawLeverDebugVisuals();
}

private void OnDrawGizmos()
{
    if (_parentReference != null)
    {
        Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.05f);
        
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + _parentReference.up * 0.2f, 
            _leverLetter,
            new GUIStyle() { 
                normal = { textColor = Color.white }, 
                fontSize = 12, 
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            }
        );
        #endif
    }
}

private void DrawLeverDebugVisuals()
{
    Vector3 pivotPos = transform.position;
    float radius = 0.7f;
    
    float currentAngle = Application.isPlaying ? transform.localEulerAngles.z : _restAngle;
    
    #if UNITY_EDITOR
    
    
    UnityEditor.Handles.color = new Color(0.8f, 0.8f, 0.8f, 0.3f);
    
    Vector3 minDirection = _parentReference.rotation * Quaternion.Euler(0, 0, _minAngle) * Vector3.up;
    float totalArc = _maxAngle - _minAngle;
    
    UnityEditor.Handles.DrawWireArc(
        pivotPos,
        _parentReference.forward,  
        minDirection,               
        totalArc,                   
        radius
    );
    
    UnityEditor.Handles.color = new Color(0f, 1f, 0f, 0.15f);
    Vector3 activationStart = _parentReference.rotation * Quaternion.Euler(0, 0, _minAngle) * Vector3.up;
    float activationArc = _activationAngle - _minAngle;
    
    UnityEditor.Handles.DrawSolidArc(
        pivotPos,
        _parentReference.forward,
        activationStart,
        activationArc,
        radius * 0.95f
    );
    
    UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.1f);
    Vector3 deactivationStart = _parentReference.rotation * Quaternion.Euler(0, 0, _activationAngle) * Vector3.up;
    float deactivationArc = _maxAngle - _activationAngle;
    
    UnityEditor.Handles.DrawSolidArc(
        pivotPos,
        _parentReference.forward,
        deactivationStart,
        deactivationArc,
        radius * 0.95f
    );
    
    #endif
    
    
    DrawAngleLine(pivotPos, _minAngle, radius * 0.8f, Color.cyan, "MIN", 2f);
    
    DrawAngleLine(pivotPos, _maxAngle, radius * 0.8f, Color.blue, "MAX", 2f);
    
    DrawAngleLine(pivotPos, _restAngle, radius * 1.0f, Color.red, "REST", 3f);
    
    Vector3 restEndPoint = pivotPos + (_parentReference.rotation * Quaternion.Euler(0, 0, _restAngle) * Vector3.up) * radius;
    Gizmos.color = Color.red;
    Gizmos.DrawWireSphere(restEndPoint, 0.02f);
    
    DrawAngleLine(pivotPos, _activationAngle, radius * 1.0f, Color.yellow, "ACTIVATE", 3f);
    
    bool isCurrentlyActive = currentAngle <= _activationAngle;
    Color currentColor = isCurrentlyActive ? Color.green : Color.magenta;
    DrawAngleLine(pivotPos, currentAngle, radius * 1.2f, currentColor, $"{currentAngle:F1}°", 4f);
    
    Vector3 currentEndPoint = pivotPos + (_parentReference.rotation * Quaternion.Euler(0, 0, currentAngle) * Vector3.up) * (radius * 1.2f);
    Gizmos.color = currentColor;
    float sphereSize = IsGrabbed ? 0.04f : 0.025f;
    Gizmos.DrawWireSphere(currentEndPoint, sphereSize);
    
    DrawInfoPanel(pivotPos, currentAngle, isCurrentlyActive);
    
    if (Application.isPlaying && IsGrabbed)
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(pivotPos, Vector3.one * 0.15f);
    }
}

private void DrawAngleLine(Vector3 origin, float angle, float length, Color color, string label, float width)
{
    Vector3 direction = _parentReference.rotation * Quaternion.Euler(0, 0, angle) * Vector3.up;
    Vector3 endPoint = origin + direction * length;
    
    Gizmos.color = color;
    
    Gizmos.DrawLine(origin, endPoint);
    
    if (width > 1f)
    {
        Vector3 perpendicular = _parentReference.right * 0.01f;
        for (int i = 1; i <= (int)width; i++)
        {
            float offset = i * 0.005f;
            Gizmos.DrawLine(origin + perpendicular * offset, endPoint + perpendicular * offset);
            Gizmos.DrawLine(origin - perpendicular * offset, endPoint - perpendicular * offset);
        }
    }
    
    #if UNITY_EDITOR
    if (!string.IsNullOrEmpty(label))
    {
        GUIStyle style = new GUIStyle();
        style.normal.textColor = color;
        style.fontSize = 10;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        
        UnityEditor.Handles.Label(endPoint + direction * 0.1f, label, style);
    }
    #endif
}

private void DrawInfoPanel(Vector3 position, float currentAngle, bool isActive)
{
    #if UNITY_EDITOR
    string info = $"═══ Lever {_leverLetter} [{_leverIndex}] ═══\n";
    info += $"Current: {currentAngle:F1}°\n";
    info += $"Status: {(isActive ? "▶ ACTIVE" : "■ INACTIVE")}\n";
    
    if (Application.isPlaying)
    {
        info += $"Grabbed: {(IsGrabbed ? "YES" : "NO")}\n";
        info += $"Network: {NetworkedAngle:F1}°\n";
        
        float progress = Mathf.InverseLerp(_maxAngle, _activationAngle, currentAngle);
        info += $"Progress: {(progress * 100f):F0}%";
    }
    else
    {
        info += "[Editor Mode]";
    }
    
    GUIStyle panelStyle = new GUIStyle();
    panelStyle.normal.textColor = Color.white;
    panelStyle.fontSize = 11;
    panelStyle.alignment = TextAnchor.MiddleLeft;
    panelStyle.fontStyle = FontStyle.Normal;
    
    Vector3 labelPos = position + _parentReference.up * 0.4f + _parentReference.right * 0.3f;
    UnityEditor.Handles.Label(labelPos, info, panelStyle);
    #endif
}

private void OnGUI()
{
    if (!Application.isEditor || !Input.GetKey(KeyCode.L)) return;
    
    float currentAngle = transform.localEulerAngles.z;
    
    GUI.Box(new Rect(10, Screen.height - 100, 180, 80), $"Lever {_leverLetter}");
    
    GUI.Label(new Rect(15, Screen.height - 75, 170, 20), 
        $"Angle: {currentAngle:F1}° / {_activationAngle}°");
    
    float progress = Mathf.InverseLerp(_maxAngle, _minAngle, currentAngle);
    GUI.color = currentAngle <= _activationAngle ? Color.green : Color.red;
    GUI.HorizontalSlider(new Rect(15, Screen.height - 50, 160, 20), progress, 0f, 1f);
    GUI.color = Color.white;
}

#endregion
    }
}