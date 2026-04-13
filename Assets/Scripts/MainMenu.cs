using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void StartGame()
    {
        // Ensure time isn't frozen from returning from the game over screen
        Time.timeScale = 1f;
        
        // Load the main heist level (Index 1)
        SceneManager.LoadScene(1);
    }

    // Pro-tip: Included a Quit function just in case you want to easily hook up an Exit button later!
    public void QuitGame()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
    }
}
