using UnityEngine;
using TMPro;

/// <summary>
/// Place on any trigger collider that acts as a level exit.
/// The player can only trigger game-complete if their score meets
/// <see cref="minimumScore"/>. Set this per-level in the Inspector.
/// 
/// Optional: assign <see cref="lockedPromptText"/> to show a screen-space
/// TMP message explaining how much score is still needed.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ExitZone : MonoBehaviour
{
    [Header("Score Requirement")]
    [Tooltip("Minimum score the player must reach before this exit becomes active. " +
             "Set this per-level in the Inspector.")]
    public int minimumScore = 0;

    [Header("UI Prompt")]
    [Tooltip("Screen-space TextMeshProUGUI shown while the player is inside the exit zone " +
             "but has not yet met the score requirement.")]
    public TextMeshProUGUI lockedPromptText;

    [Header("Audio")]
    public AudioClip gameFinishSound;
    public AudioClip exitLockedSound;

    [Header("Win Screen")]
    [Tooltip("Drag the 2D UI image/panel to show as the win screen background here.")]
    public GameObject winScreenUI;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private bool triggered = false;  // prevent double-triggering

    // ── Unity Messages ────────────────────────────────────────────────────────
    void Start()
    {
        GetComponent<Collider>().isTrigger = true;
        if (lockedPromptText != null) lockedPromptText.gameObject.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (player.score >= minimumScore)
        {
            // Score requirement met — complete the level
            TriggerComplete(player);
        }
        else
        {
            // Not enough score — show feedback
            int needed = minimumScore - player.score;

            if (lockedPromptText != null)
            {
                lockedPromptText.text = $"You need {needed} more score to escape!";
                lockedPromptText.gameObject.SetActive(true);
            }

            if (exitLockedSound != null)
                AudioSource.PlayClipAtPoint(exitLockedSound, transform.position);

            Debug.Log($"[ExitZone] Player tried to escape but only has {player.score}/{minimumScore} score.");
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Hide the "not enough score" prompt when player walks away
        if (other.GetComponent<PlayerController>() != null && lockedPromptText != null)
            lockedPromptText.gameObject.SetActive(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    void TriggerComplete(PlayerController player)
    {
        triggered = true;

        if (lockedPromptText != null) lockedPromptText.gameObject.SetActive(false);

        Debug.Log($"[ExitZone] Escaped with score {player.score}! Triggering End Screen.");

        if (gameFinishSound != null)
            AudioSource.PlayClipAtPoint(gameFinishSound, transform.position);

        // Show the win screen image
        if (winScreenUI != null)
            winScreenUI.SetActive(true);

        if (player.gameFinishUI != null)
            player.gameFinishUI.SetActive(true);

        if (player.playNextLevelButton != null)
        {
            player.playNextLevelButton.SetActive(true);
            player.playNextLevelButton.transform.SetAsLastSibling();
        }

        player.isGameOver = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 0f;
    }
}
