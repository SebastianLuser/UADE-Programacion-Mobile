using UnityEngine;
using System;

public enum GameState
{
    Menu,
    Playing,
    Paused,
    GameOver,
    Victory
}

public class GameStateManager : BaseManager, IUpdatable
{
    private GameState currentState = GameState.Menu;
    
    public GameState CurrentState => currentState;
    public event Action<GameState, GameState> OnStateChanged;
    
    public bool IsActive => isInitialized && enabled;
    
    protected override void OnInitialize()
    {
        ServiceLocator.Register<GameStateManager>(this);
        
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.RegisterUpdatable(this);
    }
    
    public void ChangeState(GameState newState)
    {
        if (currentState == newState) return;
        
        GameState previousState = currentState;
        OnExitState(currentState);
        currentState = newState;
        OnEnterState(newState);
        
        OnStateChanged?.Invoke(previousState, newState);
        Debug.Log($"Game state changed from {previousState} to {newState}");
    }
    
    private void OnEnterState(GameState state)
    {
        switch (state)
        {
            case GameState.Menu:
                Time.timeScale = 1f;
                break;
                
            case GameState.Playing:
                Time.timeScale = 1f;
                break;
                
            case GameState.Paused:
                Time.timeScale = 0f;
                break;
                
            case GameState.GameOver:
                Time.timeScale = 0f;
                HandleGameOver();
                break;
                
            case GameState.Victory:
                Time.timeScale = 0f;
                HandleVictory();
                break;
        }
    }
    
    private void OnExitState(GameState state)
    {
        switch (state)
        {
            case GameState.Paused:
            case GameState.GameOver:
            case GameState.Victory:
                Time.timeScale = 1f;
                break;
        }
    }
    
    public void StartGame()
    {
        ChangeState(GameState.Playing);
    }
    
    public void PauseGame()
    {
        if (currentState == GameState.Playing)
        {
            ChangeState(GameState.Paused);
        }
    }
    
    public void ResumeGame()
    {
        if (currentState == GameState.Paused)
        {
            ChangeState(GameState.Playing);
        }
    }
    
    public void GameOver()
    {
        ChangeState(GameState.GameOver);
    }
    
    public void Victory()
    {
        ChangeState(GameState.Victory);
    }
    
    public void ReturnToMenu()
    {
        ChangeState(GameState.Menu);
    }
    
    private void HandleGameOver()
    {
        Debug.Log("Game Over!");
    }
    
    private void HandleVictory()
    {
        Debug.Log("Victory!");
    }
    
    public void OnUpdate(float deltaTime)
    {
        CheckGameConditions();
        
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentState == GameState.Playing)
            {
                PauseGame();
            }
            else if (currentState == GameState.Paused)
            {
                ResumeGame();
            }
        }
    }
    
    private void CheckGameConditions()
    {
        if (currentState != GameState.Playing) return;
        
        var characterManager = ServiceLocator.Get<CharacterManager>();
        if (characterManager == null) return;
        
        if (!characterManager.IsMainCharacterAlive())
        {
            GameOver();
        }
        else if (characterManager.GetAliveEnemiesCount() == 0)
        {
            Victory();
        }
    }
    
    protected override void OnShutdown()
    {
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.UnregisterUpdatable(this);
        
        Time.timeScale = 1f;
        ServiceLocator.Unregister<GameStateManager>();
    }
}