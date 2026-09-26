using Cinemachine;
using System;
using System.Collections;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [SerializeField] private float fadeIn = 0.3f;
    [SerializeField] private float fadeOut = 0.3f;

    private Coroutine _currentRoutine;
    private string _currentTutorial;
    private Input InputAction;

    private WorldUIElement _tip;

    public AbstractEntity attackObject, itemToPick;
    public GameObject Musorka;
    public ItemContainer container;

    public CinemachineVirtualCamera tipCam, LustraCam;

    public bool isLustraDeth,isShownTip;

    public void SetLustraState(bool isLustra)
    {
        isLustraDeth = isLustra;
    }

    private void Awake()
    {
        Instance = this;
    }

    public void Start()
    {
        InputAction = InputManager.inputActions;
    }

    public void StartTutor(string name)
    {
        Debug.Log(name + " was Started");
        if (_currentRoutine != null)
        {
            StopCoroutine(_currentRoutine);
            HideTip();
        }

        _currentTutorial = name;

        IEnumerator routine = name switch
        {
            "movement" => Tutorial_Movement(),
            "attack" => Tutorial_Attack(),
            "pickItem" => PickItmeTuttorial(),
            "throw" => ThrowTuttorial(),
            "lustra" => Lustra(),
            _ => null
        };

        if (routine == null)
        {
            Debug.LogWarning($"[TutorialManager] Нет туториала с именем: {name}");
            return;
        }

        _currentRoutine = StartCoroutine(routine);
    }

    public void StopCurrent()
    {
        if (_currentRoutine != null)
        {
            StopCoroutine(_currentRoutine);
            _currentRoutine = null;
        }
        HideTip();
        _currentTutorial = null;
    }

    private IEnumerator Lustra()
    {
        if (!isLustraDeth)
        {
            if (isShownTip == false)
            {
                isShownTip = true;
                tipCam.Priority = 20;

                yield return new WaitForSeconds(4);

                tipCam.Priority = 0;
            }
        }

        LustraCam.Priority = 20;

        yield return new WaitForSeconds(1);

        if (!isLustraDeth)
        {
            bool takes = false;
            void OnTake()
            {
                takes = true;
            }

            container.onTake += OnTake;
            yield return ShowStage(
                "В мусорке можно пополнить запас оружия",
                Musorka.transform,
                () =>
                {
                    if (takes)
                    {
                        container.onTake -= OnTake;
                    }
                    return takes;
                }
            );
        }
        _currentRoutine = null;
    }


    private void ShowTip(string text, Transform target,Vector2 offset = default)
    {
        _tip?.Kill();
        _tip = WorldUIManager.Instance
            .Spawn("dialogue", target, text)
            .FadeIn(fadeIn)
            .Delay(0.2f)
            .MoveUp(-40)
            .FromAbove(200)
            .Keep()
            .Play()
            .SetScale(1);
    }


    private void HideTip()
    {
        _tip?.Hide(fadeOut, onComplete: () => _tip = null);
    }

    private IEnumerator ShowStage(string text, Transform target, Func<bool> condition)
    {
        ShowTip(text, target);
        yield return new WaitUntil(condition);
        HideTip();
    }

    // ==== Тесты ====

    private IEnumerator PickItmeTuttorial()
    {
        yield return ShowStage(
            "E",
            itemToPick.transform,
            () => itemToPick.GetControllerComponent<ItemComponent>()._currentOwner == ContextManager.Instance.player);

        _currentRoutine = null;
        StartTutor("attack");
    }

    private IEnumerator ThrowTuttorial()
    {
        Transform player = ContextManager.Instance.player.transform;
        var throwC = ContextManager.Instance.player.GetControllerComponent<ItemThrowComponent>();
        yield return ShowStage(
            "<color=yellow>Q + ЛКМ </color>для броска",
            player,
            () => throwC.chargingTime >= throwC.timeToMax/1.1f);

        _currentRoutine = null;
    }

    private IEnumerator Tutorial_Movement()
    {
        Transform player = ContextManager.Instance.player.transform;

        yield return ShowStage(
            "<color=yellow>'A' 'D' </color> для движения",
            player,
            () => InputAction.Player.Move.ReadValue<Vector2>() != Vector2.zero);

        const float mouseThreshold = 300f;
        float mouseMoved = 0f;

        yield return ShowStage(
            "Поворачивайте мышь для осмотра",
            player,
            () =>
            {
                mouseMoved += Mouse.current.delta.ReadValue().magnitude;
                return mouseMoved >= mouseThreshold;
            });

        _currentRoutine = null;

        StartTutor("pickItem");
    }

    private IEnumerator Tutorial_Attack()
    {
        yield return ShowStage(
            "<color=yellow>ЛКМ</color> — <color=red>Атака</color>",
            attackObject.transform,
            () => attackObject.GetControllerComponent<HealthComponent>().currHealth <= 0);

        _currentRoutine = null;
    }
}