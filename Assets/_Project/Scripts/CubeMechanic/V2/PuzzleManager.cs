using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Events;

namespace PuzzleCubes.Core
{
    /// <summary>
    /// Gestor principal del sistema de puzzles
    /// </summary>
    public class PuzzleManager : MonoBehaviour
    {
        [Header("Puzzle Configuration")]
        [SerializeField] private PuzzleDefinition currentPuzzle;
        [SerializeField] private Transform cubeSpawnPoint;
        [SerializeField] private GameObject cubePrefab;
        [SerializeField] private float spawnDelay = 0.1f;
        
        [Header("Table & Grid")]
        [SerializeField] private Transform puzzleTable;
        [SerializeField] private CubeGrid cubeGrid;
        
        [Header("UI References")]
        [SerializeField] private RawImage referenceImage;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private GameObject completionPanel;
        
        [Header("Visual Feedback")]
        [SerializeField] private Material correctMaterial;
        [SerializeField] private Material incorrectMaterial;
        [SerializeField] private ParticleSystem completionParticles;
        
        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip progressSound;
        [SerializeField] private AudioClip completionSound;
        
        [Header("Debug")]
        [SerializeField] private bool autoValidate = true;
        [SerializeField] private float validationInterval = 0.5f;
        
        [Header("Selection Puzzle")]
        [SerializeField] private PuzzleDefinition[] puzzles;
        
        private List<GameObject> spawnedCubes = new List<GameObject>();
        private Coroutine validationCoroutine;
        private float lastSimilarity = 0f;
        private bool isCompleted = false;
        
        [Header("Eavents")]
        public UnityEvent OnFinishPuzzle;
        
        private void Start()
        {
            if (!cubeGrid)
            {
                cubeGrid = FindObjectOfType<CubeGrid>();
            }
            
                currentPuzzle = puzzles[Random.Range(0, puzzles.Length)];
            
                LoadPuzzle(currentPuzzle);
            
        }
        
        /// <summary>
        /// Carga un nuevo puzzle
        /// </summary>
        public void LoadPuzzle(PuzzleDefinition puzzle)
        {
            if (!puzzle) return;
            
            currentPuzzle = puzzle;
            isCompleted = false;
            
            // Limpiar puzzle anterior
            ClearCurrentPuzzle();
            
            // Mostrar imagen de referencia
            if (referenceImage && puzzle.PuzzleData.ReferenceImage)
            {
                referenceImage.texture = puzzle.PuzzleData.ReferenceImage;
            }
            
            // Actualizar UI
            if (statusText)
            {
                statusText.text = $"Puzzle: {puzzle.PuzzleData.PuzzleName}";
            }
            
            if (progressText)
            {
                progressText.text = "Progress: 0%";
            }
            
            if (completionPanel)
            {
                completionPanel.SetActive(false);
            }
            
            // Spawn cubos necesarios
            StartCoroutine(SpawnCubes(puzzle.PuzzleData.CubePositions.Count));
            
            // Iniciar validación automática
            if (autoValidate && validationCoroutine == null)
            {
                validationCoroutine = StartCoroutine(AutoValidateRoutine());
            }
        }
        
        /// <summary>
        /// Genera los cubos necesarios para el puzzle
        /// </summary>
        private IEnumerator SpawnCubes(int count)
        {
            Vector3 spawnPos = cubeSpawnPoint ? cubeSpawnPoint.position : puzzleTable.position + Vector3.up * 0.2f;
            
            for (int i = 0; i < count; i++)
            {
                GameObject cube = Instantiate(cubePrefab, spawnPos + Random.insideUnitSphere * 0.1f, Random.rotation);
                spawnedCubes.Add(cube);
                
                // Añadir pequeña fuerza inicial para dispersar los cubos
                Rigidbody rb = cube.GetComponent<Rigidbody>();
                if (rb)
                {
                    rb.AddForce(Random.onUnitSphere * 2f, ForceMode.VelocityChange);
                }
                
                yield return new WaitForSeconds(spawnDelay);
            }
        }
        
        /// <summary>
        /// Limpia el puzzle actual
        /// </summary>
        private void ClearCurrentPuzzle()
        {
            // Detener validación
            if (validationCoroutine != null)
            {
                StopCoroutine(validationCoroutine);
                validationCoroutine = null;
            }
            
            // Limpiar grilla
            if (cubeGrid)
            {
                cubeGrid.ClearGrid();
            }
            
            // Destruir cubos sobrantes
            foreach (var cube in spawnedCubes)
            {
                if (cube != null)
                    Destroy(cube);
            }
            spawnedCubes.Clear();
        }
        
        /// <summary>
        /// Rutina de validación automática
        /// </summary>
        private IEnumerator AutoValidateRoutine()
        {
            while (!isCompleted)
            {
                yield return new WaitForSeconds(validationInterval);
                ValidatePuzzle();
            }
        }
        
        /// <summary>
        /// Valida el estado actual del puzzle
        /// </summary>
        public void ValidatePuzzle()
        {
            if (!currentPuzzle || !cubeGrid || isCompleted) return;
            
            // Obtener estructura actual
            PuzzleData currentData = cubeGrid.GetCurrentPuzzleData();
            
            // Si no hay cubos, no validar
            if (currentData.CubePositions.Count == 0)
            {
                UpdateProgress(0f);
                return;
            }
            
            // Obtener todas las rotaciones posibles del puzzle objetivo
            List<PuzzleData> targetRotations = currentPuzzle.PuzzleData.GetAllYRotations();
            
            float maxSimilarity = 0f;
            bool exactMatch = false;
            
            // Comparar con cada rotación posible
            foreach (var rotation in targetRotations)
            {
                if (currentData.Equals(rotation))
                {
                    exactMatch = true;
                    maxSimilarity = 1f;
                    break;
                }
                
                float similarity = currentData.CalculateSimilarity(rotation);
                maxSimilarity = Mathf.Max(maxSimilarity, similarity);
            }
            
            // Actualizar progreso
            UpdateProgress(maxSimilarity);
            
            // Verificar completitud
            if (exactMatch || maxSimilarity >= currentPuzzle.CompletionThreshold)
            {
                OnPuzzleCompleted();
            }
        }
        
        /// <summary>
        /// Actualiza el feedback visual del progreso
        /// </summary>
        private void UpdateProgress(float similarity)
        {
            // Actualizar texto
            if (progressText)
            {
                progressText.text = $"Progress: {Mathf.RoundToInt(similarity * 100)}%";
            }
            
            // Cambiar color según progreso
            if (similarity > lastSimilarity && similarity > 0.5f)
            {
                // Progreso positivo
                if (audioSource && progressSound && similarity - lastSimilarity > 0.1f)
                {
                    audioSource.PlayOneShot(progressSound);
                }
                
                // Flash visual en cubos
                StartCoroutine(FlashCubes(correctMaterial, 0.3f));
            }
            else if (similarity < lastSimilarity - 0.1f)
            {
                // Retroceso
                StartCoroutine(FlashCubes(incorrectMaterial, 0.3f));
            }
            
            lastSimilarity = similarity;
        }
        
        /// <summary>
        /// Flash visual en todos los cubos
        /// </summary>
        private IEnumerator FlashCubes(Material flashMaterial, float duration)
        {
            List<(MeshRenderer renderer, Material originalMat)> cubeRenderers = new List<(MeshRenderer, Material)>();
            
            // Obtener todos los cubos en la grilla
            foreach (var kvp in cubeGrid.Grid)
            {
                if (kvp.Value != null)
                {
                    MeshRenderer renderer = kvp.Value.GetComponent<MeshRenderer>();
                    if (renderer)
                    {
                        cubeRenderers.Add((renderer, renderer.material));
                        renderer.material = flashMaterial;
                    }
                }
            }
            
            yield return new WaitForSeconds(duration);
            
            // Restaurar materiales
            foreach (var (renderer, originalMat) in cubeRenderers)
            {
                if (renderer)
                    renderer.material = originalMat;
            }
        }
        
        /// <summary>
        /// Se llama cuando el puzzle se completa
        /// </summary>
        private void OnPuzzleCompleted()
        {
            if (isCompleted) return;
            
            isCompleted = true;
            
            // Detener validación
            if (validationCoroutine != null)
            {
                StopCoroutine(validationCoroutine);
                validationCoroutine = null;
            }
            
            // Efectos de completitud
            if (completionPanel)
            {
                completionPanel.SetActive(true);
            }
            
            if (completionParticles)
            {
                completionParticles.Play();
            }
            
            if (audioSource && completionSound)
            {
                audioSource.PlayOneShot(completionSound);
            }
            
            // Haptic feedback en ambos controles
            OVRInput.SetControllerVibration(1f, 1f, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(1f, 1f, OVRInput.Controller.RTouch);
            
            Debug.Log($"Puzzle '{currentPuzzle.PuzzleData.PuzzleName}' completed!");
            OnFinishPuzzle?.Invoke();
        }
        
        /// <summary>
        /// Reinicia el puzzle actual
        /// </summary>
        public void RestartPuzzle()
        {
            if (currentPuzzle)
            {
                LoadPuzzle(currentPuzzle);
            }
        }
        
        /// <summary>
        /// Carga el siguiente puzzle (si hay múltiples)
        /// </summary>
        public void LoadNextPuzzle()
        {
            // Implementar lógica para cargar siguiente puzzle
            Debug.Log("LoadNextPuzzle not implemented yet");
        }
        
        #region Debug Methods
        
        [ContextMenu("Force Validate")]
        public void ForceValidate()
        {
            ValidatePuzzle();
        }
        
        [ContextMenu("Complete Puzzle")]
        public void ForceComplete()
        {
            OnPuzzleCompleted();
        }
        
        #endregion
    }
}