using System.Collections;
using System.Threading.Tasks; // Required for async/await
using TMPro; /
using UnityEngine;
using UnityEngine.SceneManagement; 
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Main Menu Buttons")]
    public Button trainButton;
    public Button createChallengeButton;
    public Button joinChallengeButton;
    public Button statsButton;

    [Header("Join Challenge Popup")]
    public GameObject joinPopupPanel; 
    public TMP_InputField codeInputField;
    public Button submitCodeButton;
    public TextMeshProUGUI joinFeedbackText;

    [Header("Create Challenge Popup")]
    public GameObject createPopupPanel; 
    public TextMeshProUGUI challengeCodeText;
    public Button cancelCreateButton;
    [Header("Firebase")]
    public FirebaseManager firebaseManager; 

    private GameController gameController;

    void Start()
    {
        // Find the persistent GameController
        gameController = GameController.Instance;

        // Find the persistent FirebaseManager
        firebaseManager = FindObjectOfType<FirebaseManager>();

        // Hook up listeners in code 
        trainButton.onClick.AddListener(OnTrainClicked);
        statsButton.onClick.AddListener(OnStatsClicked);

        //  Use a lambda expression for async methods 
        createChallengeButton.onClick.AddListener(async () => await OnCreateChallengeClickedAsync());
        joinChallengeButton.onClick.AddListener(OnJoinChallengeClicked);
        cancelCreateButton.onClick.AddListener(async() => await OnCancelCreateChallenge());

        //  Hook up popup listeners 
        
        submitCodeButton.onClick.AddListener(async () => await OnSubmitJoinCodeAsync());

        // ---  Set default UI state ---
        joinPopupPanel.SetActive(false);
        createPopupPanel.SetActive(false);
    }

    // --- Button Functions ---

    void OnTrainClicked()
    {
        Debug.Log("Train button clicked");
        if (gameController != null)
        {
            gameController.StartPersonalTraining();
        }
    }

    void OnStatsClicked()
    {
        Debug.Log("Stats button clicked");
        if (gameController != null)
        {
            gameController.GoToStats();
        }
    }

    // --- CHALLENGE CREATION LOGIC (Player 1) ---
    
    async Task OnCreateChallengeClickedAsync()
    {
        Debug.Log("Create Challenge clicked");
        if (firebaseManager == null)
        {
            Debug.LogError("FirebaseManager not found!");
            return;
        }

        // Show "Creating..." panel
        createPopupPanel.SetActive(true);
        joinPopupPanel.SetActive(false);
        challengeCodeText.text = "Generating code...";

        //  Call the Firebase function
        (string challengeCode, int seed) = await firebaseManager.CreateChallengeSession();

        if (string.IsNullOrEmpty(challengeCode))
        {
            challengeCodeText.text = "Error creating challenge. Try again.";
            await Task.Delay(2000);
            createPopupPanel.SetActive(false);
            return;
        }

        //  Update the GameController with the new seed (for P1)
        if (gameController != null)
        {
            gameController.currentMode = GameController.GameMode.Challenge;
            gameController.currentGameSeed = seed;
        }

        //  Show the code and start listening
        challengeCodeText.text = $"Share this code:\n<b>{challengeCode}</b>\n\nWaiting for opponent...";

        // Listen for P2 to join (the handshake)
        firebaseManager.ListenForOpponentToJoin(challengeCode, () =>
        {
            Debug.Log("Opponent joined! Loading AR scene for Player 1.");
            challengeCodeText.text = "Opponent found! Starting game...";

            // Load the AR scene
            SceneManager.LoadScene(gameController.ARGameScene);
        });
    }

    public async Task OnCancelCreateChallenge()
    {
        //  Hide the panel, regardless of its current state
        createPopupPanel.SetActive(false);

        // IMPORTANT: Stop the coroutine that is waiting for the opponent.
       
        StopAllCoroutines();

        // 3. Reset the GameController state
        if (gameController != null)
        {
            gameController.currentMode = GameController.GameMode.None;
            gameController.currentGameSeed = 0;

            // Load the Main Menu scene again to fully reset the UI/Scene state.
            await Task.Delay(500); // Small delay to ensure UI updates
            gameController.GoToMainMenu(); 
        }

        Debug.Log("Challenge creation cancelled. Resetting scene state.");
    }
    void OnJoinChallengeClicked()
    {
        Debug.Log("Join Challenge clicked");
        // Show the popup where the user can enter a code
        joinPopupPanel.SetActive(true);
        joinFeedbackText.text = "Enter a 5-digit challenge code.";
    }

    
    async Task OnSubmitJoinCodeAsync()
    {
        string code = codeInputField.text;
        if (string.IsNullOrEmpty(code))
        {
            joinFeedbackText.text = "Code cannot be empty.";
            return;
        }

        if (firebaseManager == null)
        {
            Debug.LogError("FirebaseManager not found!");
            return;
        }

        joinFeedbackText.text = "Verifying code...";
        // Call the Firebase function to join
        (bool success, string message, int seed) = await firebaseManager.JoinChallengeSession(code);

        if (success)
        {
            joinFeedbackText.text = "Success! Joining game...";

            // Set up the GameController for the challenge
            if (gameController != null)
            {
                gameController.currentMode = GameController.GameMode.Challenge;
                gameController.currentGameSeed = seed;
            }

            // Load the AR scene after a brief delay
            await Task.Delay(500);
            SceneManager.LoadScene(gameController.ARGameScene);
        }
        else
        {
            // Show the error (e.g., "Invalid Challenge Code.")
            joinFeedbackText.text = message;
            await Task.Delay(2000);
            gameController.GoToMainMenu();
        }
    }
}