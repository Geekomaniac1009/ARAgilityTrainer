using UnityEngine;
using UnityEngine.SceneManagement; // Required for scene management

public class GameController : MonoBehaviour
{
    // Static instance to be accessible from anywhere
    public static GameController Instance;

    // --- Game Settings ---
    public enum GameMode { None, PersonalTraining, Challenge }
    public GameMode currentMode = GameMode.None;
    public int currentGameSeed = 0;

    // --- Scene Names (must match your Build Settings) ---
    public string MainMenuScene = "MainMenu";
    public string ARGameScene = "ARAgilityScene";
    public string UserStatsScene = "UserStats";

    void Awake()
    {
  
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Makes this object persist
        }
        else
        {
            Destroy(gameObject); // Destroys any duplicates
        }
    }

    // --- Public Functions to be called by UI Buttons ---

    public void StartPersonalTraining()
    {
        currentMode = GameMode.PersonalTraining;
        currentGameSeed = 0; // Or a new random seed
        SceneManager.LoadScene(ARGameScene);
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene(MainMenuScene);
    }

    public void GoToStats()
    {
        SceneManager.LoadScene(UserStatsScene);
    }
}