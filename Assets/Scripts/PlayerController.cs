using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sneakSpeed = 2f;
    public float jumpHeight = 1.5f;
    public float gravity = -15f;

    [Header("Camera & Looking")]
    public float mouseSensitivity = 200f;
    public Transform cameraTarget;
    public Transform playerCamera; // Drag your Main Camera here!
    
    [Header("Camera Collision")]
    [Tooltip("Which layers should the camera not clip through?")]
    public LayerMask collisionLayers = ~0; // Collides with everything by default
    public float cameraCollisionRadius = 0.2f; // Thickness of the camera's collision ray
    public float minCameraDistance = 0.5f; // Closest the camera can get to the player

    [Header("Inventory & Score")]
    public int keyCount = 0;
    public bool hasStolenSomething = false;
    public int score = 0;
    
    [Tooltip("Drag your UI Text element here to track the score")]
    public TMPro.TextMeshProUGUI scoreText;
    
    [Tooltip("Drag your Keys TextMeshPro element here")]
    public TMPro.TextMeshProUGUI keysText;
    
    [Tooltip("Drag your Game Over TextMeshPro or UI Canvas object here")]
    public GameObject gameOverUI;
    
    [Tooltip("Drag your Game Finish (Victory) TextMeshPro or UI Canvas object here")]
    public GameObject gameFinishUI;
    
    [Tooltip("Drag your Play Again UI Button object here")]
    public GameObject playAgainButton;
    
    [Tooltip("Drag an optional object here to enable when the game ends (e.g. background panel)")]
    public GameObject endBackgroundObject;
    
    [Header("Boost UI Elements")]
    [Tooltip("Text to flash when jump height increases")]
    public TMPro.TextMeshProUGUI jumpBoostText;
    [Tooltip("Text to flash when walk speed increases")]
    public TMPro.TextMeshProUGUI speedBoostText;


    [Header("Audio Effects")]
    public AudioSource movementAudioSource;
    public AudioClip sneakSound;
    public AudioClip runSound;
    public AudioClip jumpSound;
    public AudioClip gameOverSound;

    [Header("Animation")]
    [Tooltip("Drag the child GameObject with the Animator here")]
    public Animator characterAnimator;

    [Header("Alarm UI")]
    [Tooltip("Drag your Red Alarm screen UI Image/Panel here")]
    public GameObject alarmScreenElement;
    public float alarmFlashSpeed = 5f;
    private UnityEngine.UI.Image alarmImage;
    private CanvasGroup alarmCanvasGroup;

    [Header("Camera Overlay UI")]
    [Tooltip("Drag the 2D UI image element to display when in view of a security camera here")]
    public GameObject cameraOverlayUI;

    public void AddScore(int amount)
    {
        score += amount;
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score.ToString();
        }
    }

    private CharacterController controller;
    private Vector3 velocity;
    private float verticalLookRotation = 0f;
    private bool isSneaking = false;
    private Guard[] allGuards; // Cache all guards in the scene
    private SecurityCamera[] allCameras; // Cache all security cameras in the scene

    // To remember the camera's original designated offset
    private Vector3 defaultCameraLocalPos;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        allGuards = FindObjectsOfType<Guard>(); // Find all guards once
        allCameras = FindObjectsOfType<SecurityCamera>(); // Find all cameras once
        
        if (alarmScreenElement != null)
        {
            alarmImage = alarmScreenElement.GetComponent<UnityEngine.UI.Image>();
            alarmCanvasGroup = alarmScreenElement.GetComponent<CanvasGroup>();
            alarmScreenElement.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerCamera != null)
        {
            // Store the initial offset position you set in the editor as our "default" zoom level
            defaultCameraLocalPos = playerCamera.localPosition;
        }
        
        if (gameOverUI != null)
        {
            gameOverUI.SetActive(false); // Ensure it's hidden while playing
        }
        
        if (gameFinishUI != null)
        {
            gameFinishUI.SetActive(false); // Ensure it's hidden while playing
        }
        
        if (playAgainButton != null)
        {
            playAgainButton.SetActive(false);
        }
        
        if (endBackgroundObject != null) endBackgroundObject.SetActive(false);
        
        if (jumpBoostText != null) jumpBoostText.gameObject.SetActive(false);
        if (speedBoostText != null) speedBoostText.gameObject.SetActive(false);
        
    }

    private int lastKeyCount = -1;

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleInventoryUI();
        HandleAlarmUI();
        HandleCameraOverlayUI();
    }

    void HandleCameraOverlayUI()
    {
        if (cameraOverlayUI == null) return;

        bool isInView = false;
        foreach (SecurityCamera cam in allCameras)
        {
            if (cam != null && cam.IsPlayerInView)
            {
                isInView = true;
                break;
            }
        }

        if (cameraOverlayUI.activeSelf != isInView)
        {
            cameraOverlayUI.SetActive(isInView);
        }
    }

    void HandleAlarmUI()
    {
        if (alarmScreenElement == null) return;

        bool isGuardClose = false;
        
        // Check if any guard is chasing and within 12 units
        foreach (Guard guard in allGuards)
        {
            if (guard != null && guard.IsChasing && Vector3.Distance(transform.position, guard.transform.position) <= 12f)
            {
                isGuardClose = true;
                break;
            }
        }

        if (isGuardClose)
        {
            alarmScreenElement.SetActive(true);
            
            // Calculate a pulsing value between 0.2 and 0.8 using sine wave
            float pulse = 0.5f + Mathf.Sin(Time.time * alarmFlashSpeed) * 0.3f;
            
            if (alarmCanvasGroup != null)
            {
                alarmCanvasGroup.alpha = pulse;
            }
            else if (alarmImage != null)
            {
                Color c = alarmImage.color;
                c.a = pulse;
                alarmImage.color = c;
            }
        }
        else
        {
            // Reset and hide when safe
            if (alarmScreenElement.activeSelf)
            {
                alarmScreenElement.SetActive(false);
            }
        }
    }

    void HandleInventoryUI()
    {
        if (keysText != null && keyCount != lastKeyCount)
        {
            lastKeyCount = keyCount;
            
            // Format the UI text depending on if we have keys or not
            if (keyCount > 0)
            {
                keysText.text = "KEYS: " + keyCount.ToString();
            }
            else
            {
                keysText.text = "";
            }
        }
    }



    void LateUpdate()
    {
        // Handle camera calculations in LateUpdate so it executes after the player has finished moving this frame
        HandleCameraCollision();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Rotate the player left and right
        transform.Rotate(Vector3.up * mouseX);

        // Rotate the camera target up and down
        if (cameraTarget != null)
        {
            verticalLookRotation -= mouseY;
            verticalLookRotation = Mathf.Clamp(verticalLookRotation, -60f, 60f); 
            cameraTarget.localRotation = Quaternion.Euler(verticalLookRotation, 0f, 0f);
        }
    }

    void HandleMovement()
    {
        // 1. Check if we're grounded at the VERY START of movement calculation
        bool isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Helps pull the player down slopes
        }

        // 2. Handle Jump input while grounded
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            
            if (characterAnimator != null)
            {
                characterAnimator.SetTrigger("Jump");
            }
            
            if (jumpSound != null)
            {
                AudioSource.PlayClipAtPoint(jumpSound, transform.position);
            }
        }

        // 3. Handle Horizontal Input
        bool isWalking = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isWalking ? walkSpeed : sneakSpeed;

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 move = transform.right * moveX + transform.forward * moveZ;

        // 4. Apply gravity over time
        velocity.y += gravity * Time.deltaTime;

        // 5. Combine horizontal and vertical movement into ONE single Move call
        Vector3 finalMove = (move * currentSpeed) + new Vector3(0, velocity.y, 0);
        controller.Move(finalMove * Time.deltaTime);

        // 6. Handle movement audio
        if (isGrounded && move.sqrMagnitude > 0.01f)
        {
            AudioClip desiredClip = isWalking ? runSound : sneakSound;
            if (movementAudioSource != null && desiredClip != null)
            {
                if (movementAudioSource.clip != desiredClip)
                {
                    movementAudioSource.clip = desiredClip;
                    movementAudioSource.loop = true;
                    movementAudioSource.Play();
                }
                else if (!movementAudioSource.isPlaying)
                {
                    movementAudioSource.Play();
                }
            }
        }
        else
        {
            // Pause footstep sounds if not moving or if in the air
            if (movementAudioSource != null && movementAudioSource.isPlaying)
            {
                movementAudioSource.Pause();
            }
        }

        // 7. Handle Animations
        if (characterAnimator != null)
        {
            float speedPercent = 0f;
            if (move.sqrMagnitude > 0.01f)
            {
                // isWalking in code actually means the fast speed (Run), and default is sneak (Walk)
                speedPercent = isWalking ? 1f : 0.5f; 
            }
            
            // Smoothly blend the Speed parameter
            characterAnimator.SetFloat("Speed", speedPercent, 0.1f, Time.deltaTime);
            
            // Pass grounded state (useful if you want to transition out of Jump animation when landing)
            characterAnimator.SetBool("IsGrounded", isGrounded);
        }
    }

    void HandleCameraCollision()
    {
        if (cameraTarget == null || playerCamera == null) return;

        // Calculate where the camera ideally WANTS to be based on your child hierarchy setup
        Vector3 desiredCameraPos = cameraTarget.TransformPoint(defaultCameraLocalPos);
        
        Vector3 direction = desiredCameraPos - cameraTarget.position;
        float defaultDistance = direction.magnitude;

        // Cast a thick ray (SphereCast) from the back of the player's head towards the camera
        if (Physics.SphereCast(cameraTarget.position, cameraCollisionRadius, direction.normalized, out RaycastHit hit, defaultDistance, collisionLayers))
        {
            // An object is in the way! Move the camera forward based on where we hit the object.
            float newDistance = hit.distance;
            
            // Prevent the camera from zooming inside the player's model
            newDistance = Mathf.Clamp(newDistance, minCameraDistance, defaultDistance);
            
            // Snap camera immediately to prevent clipping
            playerCamera.position = cameraTarget.position + direction.normalized * newDistance;
        }
        else
        {
            // Nothing is blocking the camera, smoothly zoom it back out to the default position
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, defaultCameraLocalPos, Time.deltaTime * 15f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // If the guard enters the trigger radius configured on the child colliders
        if (other.CompareTag("Guard"))
        {
            Debug.Log("Game Over! The player was caught by a guard.");
            
            if (gameOverSound != null)
            {
                AudioSource.PlayClipAtPoint(gameOverSound, cameraTarget != null ? cameraTarget.position : transform.position);
            }
            
            // Enable Game Over UI right before pausing time
            if (gameOverUI != null)
            {
                gameOverUI.SetActive(true);
            }
            
            // Enable the play again button
            if (playAgainButton != null)
            {
                playAgainButton.SetActive(true);
            }
            
            // Enable the background panel
            if (endBackgroundObject != null)
            {
                endBackgroundObject.SetActive(true);
            }
            
            // Unlock mouse so they can click the button
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            Time.timeScale = 0f; // Pauses all time-based movement, animations, and physics
        }
    }

    public void RestartGame()
    {
        // Unfreeze time before loading, otherwise the new scene starts instantly frozen
        Time.timeScale = 1f;
        SceneManager.LoadScene(0); // Load StartScene (index 0)
    }

    public void BoostWalkSpeed(float amount)
    {
        walkSpeed += amount;
        if (speedBoostText != null)
        {
            StartCoroutine(FlashTextRoutine(speedBoostText));
        }
    }

    public void BoostJumpHeight(float amount)
    {
        jumpHeight += amount;
        
        // Let the player quickly tell if their jump was boosted simply by doing a log or using the UI
        if (jumpBoostText != null)
        {
            StartCoroutine(FlashTextRoutine(jumpBoostText));
        }
    }

    private IEnumerator FlashTextRoutine(TMPro.TextMeshProUGUI textElement)
    {
        // Cancel logic can be tricky if they eat two sandwiches too quickly, 
        // but since interaction requires walking to separate objects, standard 3s wait is perfectly stable.
        textElement.gameObject.SetActive(true);
        yield return new WaitForSeconds(3f);
        textElement.gameObject.SetActive(false);
    }
}
