using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using States;
using Systems;
using Unity.VisualScripting;
using UnityEngine;

namespace Controllers
{
    public class PlayerController : EntityController
    {
        public IInputProvider input;
        protected MoveSystem _moveSystem = new MoveSystem();
        private readonly JumpSystem _jumpSystem = new JumpSystem();
        private readonly InventorySystem _inventorySystem = new InventorySystem();
        private readonly SpriteFlipSystem _flipSystem = new SpriteFlipSystem();
        private readonly ColorPositioningSystem _colorPositioningSystem = new ColorPositioningSystem();
        private readonly LedgeClimbSystem _ledgeClimbSystem = new LedgeClimbSystem();
        private readonly FrictionSystem _frictionSystem = new FrictionSystem();
        private readonly FSMSystem _fsmSystem = new FSMSystem();
        private readonly DashSystem _dashSystem = new DashSystem();
        private readonly SlideSystem _slideSystem = new SlideSystem();
        private readonly SlideDashSystem _slideDashSystem = new SlideDashSystem();
        private readonly WallRunSystem _wallRunSystem = new WallRunSystem();
        private readonly HookSystem _hookSystem = new HookSystem();
        
        [SerializeField] private MoveComponent moveComponent;
        [SerializeField] private JumpComponent jumpComponent;
        [SerializeField] private AttackComponent attackComponent = new AttackComponent();
        [SerializeField] private InventoryComponent inventoryComponent = new InventoryComponent(); 
        [SerializeField] private ColorPositioningComponent colorPositioningComponent = new ColorPositioningComponent();
        [SerializeField] public WallEdgeClimbComponent wallEdgeClimbComponent = new WallEdgeClimbComponent();
        [SerializeField] public  DashComponent dashComponent= new DashComponent();
        [SerializeField] public  FsmComponent fsmComponent = new FsmComponent();
        [SerializeField] public  AnimationComponent animationComponent = new AnimationComponent();
        private readonly SpriteFlipComponent _flipComponent = new SpriteFlipComponent();
        [SerializeField] public SlideComponent slideComponent = new SlideComponent();
        [SerializeField] public WallRunComponent wallRunComponent = new WallRunComponent();
        [SerializeField] public HookComponent hookComponent = new HookComponent();
        
        public PlayerCustomizer playerCustomizer;

        private  AttackSystem _attackSystem = new AttackSystem();
        private Vector2 cachedVelocity;
        private Vector2 LateVelocity;
        
        private Vector2 MoveDirection
        {
            get
            {
                Vector2 raw = input.GetState().movementDirection;
                Vector2 result = Vector2.zero;

                result.x = Mathf.Abs(raw.x) < 0.5f ? 0f : Mathf.Sign(raw.x);
                result.y = Mathf.Abs(raw.y) < 0.5f ? 0f : Mathf.Sign(raw.y);

                return result;
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
        }
        protected unsafe override void Awake()
        {
            input = new NavigationSystem();
            base.Awake();
            HAHAHAHA obj = new HAHAHAHA();
            void* ptr = obj.GetPointer();
            void* vtable = *(void**)ptr;
            void* klassPtr = *(void**)vtable;
            BruteforceScanMonoClass(klassPtr);

        }
        protected void Start()
        {
            Subscribe();
            States();
        }
        public void EnableAllActions()
        {
            input.GetState().inputActions.Player.Move.Enable();
            input.GetState().inputActions.Player.Jump.Enable();
            input.GetState().inputActions.Player.Interact.Enable();
            input.GetState().inputActions.Player.OnDrop.Enable();
            input.GetState().inputActions.Player.Next.Enable();
            input.GetState().inputActions.Player.Previous.Enable();
            input.GetState().inputActions.Player.Attack.Enable();
            input.GetState().inputActions.Player.Dash.Enable();
            input.GetState().inputActions.Player.Slide.Enable();
            input.GetState().inputActions.Player.GrablingHook.Enable();
            input.GetState().inputActions.Player.WeaponWheel.Enable();
        }
        private void Subscribe()
        {
            EnableAllActions();
            input.GetState().inputActions.Player.Interact.started += _inventorySystem.TakeItem;
            input.GetState().inputActions.Player.OnDrop.started += _inventorySystem.ThrowItem;
            input.GetState().inputActions.Player.Jump.started += c =>
            {
                jumpComponent.isJumpButtonPressed = true;
                if(slideComponent.isCeilOpen && (jumpComponent.isGround || jumpComponent.coyotTime > 0))
                    _fsmSystem.SetState(new JumpState(this));
                else
                {
                    _jumpSystem.StartJumpBuffer();
                }
            };
            input.GetState().inputActions.Player.Jump.canceled += c =>
            {
                jumpComponent.isJumpButtonPressed = false;
                if(slideComponent.isCeilOpen && wallRunComponent.wallRunProcess == null && wallRunComponent.isJumped == false)
                    _fsmSystem.SetState(new JumpUpState(this));
            };

            input.GetState().inputActions.Player.WeaponWheel.performed += context =>
            {
                if (context.ReadValue<Vector2>().y > 0)
                    _inventorySystem.NextItem(context);
            };
            input.GetState().inputActions.Player.WeaponWheel.performed += context =>
            {
                if (context.ReadValue<Vector2>().y < 0)
                    _inventorySystem.PreviousItem(context);
            };
            input.GetState().inputActions.Player.Dash.started += c =>
            {
                if(dashComponent.allowDash && dashComponent.DashProcess == null && wallEdgeClimbComponent.EdgeStuckProcess == null )
                    _fsmSystem.SetState(new DashState(this));
                
            };
            input.GetState().inputActions.Player.Slide.started += c =>
            {
                if (jumpComponent.isGround) 
                    _fsmSystem.SetState(new SlideState(this));
            };
            
            input.GetState().inputActions.Player.GrablingHook.started += c =>
            {
                if(!slideComponent.isCeilOpen && slideComponent.SlideProcess != null)
                    return;
                _fsmSystem.SetState(new GrablingHookState(this));
            };
            /*input.GetState().inputActions.Player.Attack.started += _ => _attackSystem.Update();*/
        }
        private void Unsubscribe()
        {
            input.GetState().inputActions.Player.Interact.started -= _inventorySystem.TakeItem;
            input.GetState().inputActions.Player.OnDrop.started -= _inventorySystem.ThrowItem;
            
            input.GetState().inputActions.Player.Attack.started -= _ => (_attackSystem).OnUpdate();
        }
        private void States()
        {

            var idle = new IdleState(this);
            var walk = new WalkState(this);
            var fall = new FallState(this);
            var wallEdge = new WallLeangeClimb(this);
            var wallRun = new WallRunState(this);
            var fallUp = new FallUpState(this);
            
            _fsmSystem.AddAnyTransition(wallRun, () => _wallRunSystem.CanStartWallRun() && ((cachedVelocity.y >= 2 && Mathf.Abs(LateVelocity.x) >= 5f) || !dashComponent.allowDash)  && wallRunComponent.canWallRun && wallRunComponent.wallRunProcess == null 
                                                       && moveComponent.direction.x == transform.localScale.x && slideComponent.SlideProcess == null  && dashComponent.isDash == false && !hookComponent.IsHooked);
            _fsmSystem.AddAnyTransition(fall, () => !jumpComponent.isGround && cachedVelocity.y < -1 && wallRunComponent.wallRunProcess == null && wallEdgeClimbComponent.EdgeStuckProcess == null 
                                                    && !hookComponent.IsHooked);
            _fsmSystem.AddAnyTransition(fallUp, () => !jumpComponent.isGround && cachedVelocity.y > 1 && wallRunComponent.wallRunProcess == null && wallEdgeClimbComponent.EdgeStuckProcess == null 
                                                      && !hookComponent.IsHooked);
            _fsmSystem.AddAnyTransition(walk, () =>Mathf.Abs(cachedVelocity.x) > 1.5f && jumpComponent.isGround && Mathf.Abs(cachedVelocity.y) < 1.5f 
                                                   && !dashComponent.isDash && slideComponent.SlideProcess == null && wallRunComponent.wallRunProcess == null && !hookComponent.IsHooked);
            _fsmSystem.AddTransition(fall,wallEdge, () => _ledgeClimbSystem.CanGrabLedge(out var _, out var _));
            _fsmSystem.AddAnyTransition(idle, () => Mathf.Abs(cachedVelocity.x) <= 1.5f  && Mathf.Abs(cachedVelocity.y) < 1.5f
                                                                                         && !dashComponent.isDash && wallEdgeClimbComponent.EdgeStuckProcess == null && jumpComponent.isGround 
                                                                                         && slideComponent.SlideProcess == null && wallRunComponent.wallRunProcess == null && dashComponent.DashProcess == null 
                                                                                         && !hookComponent.IsHooked);
            
            _fsmSystem.SetState(idle);
        }

        public override void Update()
        {
            base.Update();
            _flipComponent.direction = MoveDirection;
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            moveComponent.direction = new Vector2(MoveDirection.x,moveComponent.direction.y);

            LateVelocity = cachedVelocity;
            cachedVelocity = baseFields.rb.linearVelocity;
        }

        public void LateUpdate()
        {
            _colorPositioningSystem.OnUpdate();
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube((Vector2)baseFields.collider.bounds.center + Vector2.down * baseFields.collider.bounds.extents.y, jumpComponent.groundCheackSize);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
        unsafe void BruteforceScanMonoClass(void* klassPtr, int scanSize = 0x200)
        {
            Debug.Log($"=== Bruteforcing MonoClass at 0x{(ulong)klassPtr:X16} ===");

            for (int offset = 0; offset < scanSize; offset += 8)
            {
                byte* currentPos = (byte*)klassPtr + offset;

                // 1. Пробуем прочитать как указатель (void*)
                void* possiblePtr = *(void**)currentPos;
                if (possiblePtr == null) continue;

                // 2. Проверяем, не строка ли это (имя класса/метода/поля)
                if (TryReadString(possiblePtr, out string str))
                {
                    Debug.Log($"+0x{offset:X3} | Ptr: 0x{(ulong)possiblePtr:X16} | Str: {str}");
                    continue;
                }

                // 3. Проверяем, не массив ли это (поля/методы/интерфейсы)
                if (IsPossibleArrayOfPointers(possiblePtr, out int count))
                {
                    Debug.Log($"+0x{offset:X3} | Possible array of {count} pointers");
                    DumpPointerArray(possiblePtr, count);
                }

                // 4. Проверяем, не VTable ли это (первые 2 слота часто указывают на методы)
                if (IsPossibleVTable(possiblePtr))
                {
                    Debug.Log($"+0x{offset:X3} | Possible VTable");
                    DumpVTable(possiblePtr);
                }
            }
        }

        // Проверяет, можно ли прочитать память как строку
        unsafe bool TryReadString(void* ptr, out string str)
        {
            str = null;
            if (ptr == null) return false;

            try
            {
                byte* p = (byte*)ptr;
                int len = 0;
                while (len < 256 && p[len] != 0) len++;
                if (len == 0) return false;

                str = Encoding.UTF8.GetString(p, len);
                return str.All(c => c >= 32 && c <= 126); // Только печатные ASCII
            }
            catch { return false; }
        }

        // Проверяет, выглядит ли указатель как массив указателей
        unsafe bool IsPossibleArrayOfPointers(void* ptr, out int count)
        {
            count = 0;
            if (ptr == null) return false;

            void** array = (void**)ptr;
            for (int i = 0; i < 5; i++) // Проверяем первые 5 элементов
            {
                if (array[i] == null) break;
                count++;
            }

            return count > 1; // Если хотя бы 2 указателя — похоже на массив
        }

        // Проверяет, похоже ли это на VTable (первые 2 метода обычно виртуальные)
        unsafe bool IsPossibleVTable(void* ptr)
        {
            if (ptr == null) return false;
            void** vtable = (void**)ptr;
            return vtable[0] != null && vtable[1] != null;
        }

        // Дамп первых N элементов массива указателей
        unsafe void DumpPointerArray(void* ptr, int max = 10)
        {
            void** array = (void**)ptr;
            for (int i = 0; i < max && array[i] != null; i++)
            {
                Debug.Log($"  [{i}] -> 0x{(ulong)array[i]:X16}");
            }
        }

        // Дамп VTable (первые 5 методов)
        unsafe void DumpVTable(void* vtable)
        {
            void** methods = (void**)vtable;
            for (int i = 0; i < 5 && methods[i] != null; i++)
            {
                Debug.Log($"  VTable[{i}] -> 0x{(ulong)methods[i]:X16}");
                if (TryReadString(methods[i], out string methodName))
                {
                    Debug.Log($"    Possible method: {methodName}");
                }
            }
        }

        unsafe void FindInterfacesBruteforce(void* klassPtr)
        {
            for (int offset = 0x30; offset < 0x100; offset += 8)
            {
                void** candidate = (void**)((byte*)klassPtr + offset);
                void* firstInterface = candidate[0];

                if (firstInterface != null)
                {
                    // Проверяем, что это похоже на MonoClass (имя читается)
                    TryReadString(firstInterface, 0x48, $"Candidate at +0x{offset:X}");
                }
            }
        }
        unsafe void DumpMemory(string label, void* ptr, int size)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- {label} (0x{(ulong)ptr:X16}) ---");

            for (int offset = 0; offset < size; offset += 8)
            {
                try
                {
                    ulong value = *(ulong*)((byte*)ptr + offset);
                    sb.AppendLine($"+0x{offset:X3} | 0x{value:X16}");
                }
                catch { sb.AppendLine($"+0x{offset:X3} | <ACCESS VIOLATION>"); }
            }

            Debug.Log(sb.ToString());
        }

        unsafe void TryReadString(void* ptr, int offset, string fieldName)
        {
            try
            {
                byte* strPtr = *(byte**)((byte*)ptr + offset);
                if (strPtr != null)
                {
                    int len = 0;
                    while (strPtr[len] != 0 && len < 256) len++;
                    string str = Encoding.UTF8.GetString(strPtr, len);
                    Debug.Log($"{fieldName}: {str} (0x{(ulong)strPtr:X16})");
                }
            }
            catch { Debug.Log($"{fieldName}: <ERROR>"); }
        }

        unsafe void DumpFields(void* fieldsPtr)
        {
            if (fieldsPtr == null)
            {
                Debug.Log("Fields Ptr is NULL");
                return;
            }

            for (int i = 0; i < 20; i++)
            {
                void* fieldPtr = ((void**)fieldsPtr)[i]; // Здесь может быть NRE
                if (fieldPtr == null) break;

                TryReadString(fieldPtr, 0x10, $"Field {i}");
            }
        }

        unsafe void DumpMethods(void* methodsPtr)
        {
            // MonoMethod — имя метода часто по +0x8
            for (int i = 0; i < 20; i++) // Первые 20 методов
            {
                void* methodPtr = ((byte**)methodsPtr)[i];
                if (methodPtr == null) break;
                TryReadString(methodPtr, 0x8, $"Method {i}");
            }
        }
    }
}


[StructLayout(LayoutKind.Sequential)]
struct MonoHeader {
    public IntPtr vtable;
    public IntPtr syncBlock;
}


class HAHAHAHA:IComponent
{
    private int b = 33;
}

