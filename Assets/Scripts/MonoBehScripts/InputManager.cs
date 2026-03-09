using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour, IGameService
{
    // Статический экземпляр InputManager для доступа к rebindingOperation
    public static InputManager Instance { get; private set; }
    // Статический экземпляр MainController для общего доступа
    public static Input inputActions { get; private set; }
    // Переменная для хранения операции rebinding
    private InputActionRebindingExtensions.RebindingOperation rebindingOperation;
    // Ивент для оповещения о завершении ребайндинга
    public static event Action OnRebindComplete;
    public static event Action<Vector2> OnMouseSensivityChange;


    private void OnDisable()
    {
        Cleanup();
    }

    public static void RebindAction(string actionName, int bindingIndex)
    {
        if (Instance == null || inputActions == null)
        {
            Debug.LogError("InputManager: Instance or InputActions is null! Ensure InputManager is initialized before calling RebindAction.");
            return;
        }

        InputAction action = inputActions.FindAction(actionName);
        if (action == null)
        {
            Debug.LogWarning($"InputManager: Action '{actionName}' не найден!");
            return;
        }

        action.Disable();
        Debug.Log($"InputManager: Начинается ребайндинг для {actionName} (bindingIndex: {bindingIndex})");

        if (Instance.rebindingOperation != null)
        {
            Debug.LogWarning("InputManager: Previous rebinding operation is still active. Cancelling it.");
            Instance.rebindingOperation.Dispose();
            Instance.rebindingOperation = null;
        }

        Instance.rebindingOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsHavingToMatchPath("<Keyboard>")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(operation =>
            {
                action.Enable();
                operation.Dispose();
                Instance.rebindingOperation = null;
                Debug.Log($"InputManager: Rebind завершён! Новое значение: {action.bindings[bindingIndex].effectivePath}");
                Instance.SaveRebinds();
                Debug.Log($"InputManager: Rebind завершён! Вызов OnRebindComplete события");
                OnRebindComplete?.Invoke();
            })
            .OnCancel(operation =>
            {
                action.Enable();
                operation.Dispose();
                Instance.rebindingOperation = null;
                Debug.Log("InputManager: Rebinding cancelled");
            })
            .Start();
    }

    private void SaveRebinds()
    {
        string rebinds = inputActions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString("rebinds", rebinds);
        PlayerPrefs.Save();
        Debug.Log("InputManager: Rebindings saved to PlayerPrefs");
    }

    private void LoadRebinds()
    {
        string savedRebinds = PlayerPrefs.GetString("rebinds", "");
        if (!string.IsNullOrEmpty(savedRebinds))
        {
            inputActions.LoadBindingOverridesFromJson(savedRebinds);
            Debug.Log("InputManager: Rebindings loaded from PlayerPrefs");
        }
    }

    public static void Cleanup()
    {
        if (Instance != null && Instance.rebindingOperation != null)
        {
            Instance.rebindingOperation.Dispose();
            Instance.rebindingOperation = null;
            Debug.Log("InputManager: Rebinding operation disposed");
        }
    }

    public static void TriggerMouseSensitivityChange(Vector2 newSensitivity)
    {
        OnMouseSensivityChange?.Invoke(newSensitivity);
    }

    public void Init()
    {
        if (Instance == null)
        {
            Instance = this;
            inputActions = new Input();
            inputActions.Player.Enable();
            LoadRebinds();
            Debug.Log("InputManager: Initialized with MainController instance");
        }
        else
        {
            Destroy(gameObject);
        }
    }
}