using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI; 
using System;

public class UserStatsManager : MonoBehaviour
{
    [Header("UI Component")]
    public TextMeshProUGUI StatsText; 
    public Button returnToMenuButton; /

    private FirebaseManager firebaseManager;
    private GameController gameController;

    void Start()
    {
        // Find persistent managers
        firebaseManager = FindObjectOfType<FirebaseManager>();
        gameController = GameController.Instance;

        // Link button click event in code
        if (returnToMenuButton != null)
        {
            returnToMenuButton.onClick.AddListener(OnReturnToMenuClicked);
        }
        

        // Start the data fetching and display process
        LoadAndDisplayStats();
    }

    /// <summary>
    /// Handles the "Return to Home" button click.
    /// </summary>
    public void OnReturnToMenuClicked()
    {
        if (gameController != null)
        {
            gameController.GoToMainMenu();
        }
    }

    private async void LoadAndDisplayStats()
    {
        if (firebaseManager == null)
        {
            DisplaySummary("Error: Firebase Manager object not found in scene.", Color.red);
            return;
        }

        // Give the user feedback while waiting
        DisplaySummary("Connecting to services and verifying user...", Color.white);

        
        await firebaseManager.WaitUntilUserSignedIn();

        // Check the final status AFTER waiting
        if (!FirebaseManager.IsInitialized || string.IsNullOrEmpty(FirebaseManager.UserId))
        {
            DisplaySummary("Error: Could not establish Firebase connection or sign-in failed.", Color.red);
            return;
        }

        // Execution continues ONLY when Firebase is ready:
        DisplaySummary("Loading personal game history...", Color.white);

        // Await the asynchronous data fetching from Firebase
        List<FirebaseManager.GameSummary> history = await firebaseManager.GetLastGameHistory(20);

        if (history.Count == 0)
        {
            DisplaySummary("No game history found. Play some Personal Training games!", Color.yellow);
            return;
        }

        // Build the tabular summary
        string table = FormatDataAsTable(history);

        // Display the result
        DisplaySummary(table, Color.white);
    }

    /// <summary>
    /// Formats the list of game summaries into a neat, readable string table.
    /// </summary>
    private string FormatDataAsTable(List<FirebaseManager.GameSummary> history)
    {
        // Use rich text tags to simulate table columns and headers
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // Title and Header Row
        sb.AppendLine("<b><size=120%>PERSONAL GAME HISTORY</size></b>\n");

        // Header
        sb.Append($"<align=\"left\">{"<color=#E45A12><b> # </b></color>",-5}");
        sb.Append($"<align=\"left\">{"<color=#E45A12><b> SCORE </b></color>",-10}");
        sb.Append($"<align=\"left\">{"<color=#E45A12><b> DIFFICULTY (s)</b></color>",-20}");
        sb.AppendLine($"<align=\"left\">{"<color=#E45A12><b> DATE </b></color>"}");
        sb.AppendLine("<size=10%>----------------------------------------------------</size>");

        // Data Rows
        for (int i = 0; i < history.Count; i++)
        {
            // Game number (from newest, so 1 is the latest game)
            int gameNumber = i + 1;

            // Format the timestamp (milliseconds since epoch) into a readable DateTime
            DateTime dateTime = DateTimeOffset.FromUnixTimeMilliseconds(history[i].timestamp).LocalDateTime;
            string dateString = dateTime.ToString("MMM dd, HH:mm");

            // Append the data row
            sb.Append($"<align=\"left\"><color=#105930>{gameNumber,-5}</color>");
            sb.Append($"<align=\"left\"><color=#105930>{history[i].score,-10:F0}</color>");
            sb.Append($"<align=\"left\"><color=#105930>{history[i].difficultyLevel,-20:F2}</color>");
            sb.AppendLine($"<align=\"left\"><color=#105930>{dateString}</color>");
        }

        return sb.ToString();
    }

    private void DisplaySummary(string message, Color color)
    {
        if (StatsText != null)
        {
            StatsText.color = color;
            StatsText.text = message;
        }
        Debug.Log($"[Stats]: {message}");
    }
}