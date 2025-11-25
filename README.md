# AR Agility Trainer — Unity + AR Foundation + Firebase

The **AR Agility Trainer** is an augmented-reality movement training system designed to improve agility, reaction speed, and perception–action coordination.  
Built with **Unity**, **AR Foundation**, **Firebase**, and an optional **Python ML pipeline**, it turns any safe, open space into an adaptive, data-driven training environment.

---

## 📌 Features

### 1. Personal Training Mode
- Tap AR cones spawned within a 4m radius.
- Adaptive difficulty based on reaction-time performance.
- Optional **Sports Pattern Mode** using real athlete movement extracted from video via Python ML.
- All session data stored to Firebase for progress tracking.

### 2. Challenge Mode (Asynchronous Multiplayer)
- Player 1 generates a 5-digit challenge code.
- Player 2 joins using the same code.
- Firebase syncs:
  - Shared game seed  
  - Challenge state  
  - Final scores
- Both players play identical cone sequences independently.

### 3. User Stats Dashboard
- Displays the last 20 training sessions.
- Shows score, difficulty, date.
- Helps track long-term agility progression.

---

## 📱 AR Gameplay Flow

### Main Menu
- Personal Training  
- Create Challenge  
- Join Challenge  
- User Stats  

### AR Training Scene
- Plane detection → Set arena → Countdown  
- Cones spawn (random / real sports pattern)  
- Score + timer UI  
- Final scorecard  

---

## 🧩 System Architecture

### Unity C# Managers
- **GameController.cs** — global game state  
- **AgilityGameManager.cs** — AR initialization, cone spawn, difficulty adaptation  
- **SportsPattern.cs** — loads ML-generated patterns  
- **UserStatsManager.cs** — Firebase history fetch  
- **MainMenuManager.cs** — UI flow  

### Firebase Backend
- Anonymous authentication
- `/game_history/{userId}/` — per-session stats  
- `/challenges/{code}/` — async multiplayer  

### Optional ML Pipeline (Python)
- YOLO-based pose tracking from sports footage  
- Movement vector extraction  
- Pattern JSON exported → consumed by Unity  

---

## 🏃 Sports Pattern Mode — Data Pipeline Overview

From video → movement detection → JSON → AR training pattern:

1. Pose estimation (YOLO, Ultralytics)  
2. Orientation & movement vector extraction  
3. Rally segmentation  
4. Pattern smoothing  
5. JSON export for SportsPattern.cs  

This allows the trainer to recreate realistic footwork sequences from sports like **tennis**, **badminton**, etc.

---

## 📈 Research Alignment & Future Scope

The project aligns with evidence-based motor learning principles across:

- **Children** → locomotor skill development & perceptual–motor integration  
- **Older adults** → balance, fall-prevention, cognitive-motor improvement  
- **Athletes** → agility, reaction time, perception–action coupling  

Future enhancements include:
- Upper-limb reflex training  
- Biomechanical modelling  
- ML-based movement classification (LSTM/CNN)  
- Advanced difficulty modelling  

---

## 🛠️ Tech Stack

### Unity
- Unity 2021+  
- AR Foundation (ARCore/ARKit)  
- TextMeshPro  
- JSON Integration

### Firebase
- Realtime Database  
- Anonymous Auth

### Python (optional)
- YOLO / Ultralytics  
- NumPy, OpenCV  
- Movement pattern extraction Jupyter notebook

---

## 🚀 How to Run

1. Clone the repo  
2. Open in Unity (AR Foundation-compatible version)  
3. Replace Firebase config with your own  
4. Build to:
   - Android (ARCore)  
   - iOS (ARKit)

Sports mode requires running the Python notebook and exporting JSON patterns.

---

## 📂 Repository Structure

```
ARAgilityTrainer/
 ├── Assets/
 │   ├── Scripts/
 │   │   ├── GameController.cs
 │   │   ├── AgilityGameManager.cs
 │   │   ├── SportsPattern.cs
 │   │   ├── FirebaseManager.cs
 │   │   └── UserStatsManager.cs
 │   ├── Prefabs/
 │   └── UI/
 ├── ML/
 │   └── sports_train.ipynb
 ├── ProjectSettings/
 ├── README.md
```

---

## 📜 License

MIT License / Custom — add your preference.

---
