using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Shinzui.View.Interaction;

namespace DoorMove.Temp
{
    // ドアのタイプ
    public enum DoorType
    {
        Rotate,
        Slide
    }

    [System.Serializable]
    public class DoorPart
    {
        [Tooltip("動かしたいドアパーツ of Transform")]
        [SerializeField] private Transform partTransform;

        [Tooltip("このパーツの動作タイプ")]
        [SerializeField] private DoorType doorType = DoorType.Slide;

        [Header("Rotation Settings")]
        [Tooltip("回転の基準となる軸")]
        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        [Tooltip("開いている時の回転角度")]
        [SerializeField] private float openAngle = 90f;
        [Tooltip("閉じている時の回転角度")]
        [SerializeField] private float closeAngle = 0f;

        [Header("Slide Settings")]
        [Tooltip("移動の基準となる方向（ローカル座標）")]
        [SerializeField] private Vector3 slideDirection = Vector3.right;
        [Tooltip("スライドして開く距離（メートル）")]
        [SerializeField] private float slideDistance = 1f;

        [Header("Sequence Settings")]
        [Tooltip("開くときのアニメーション開始ディレイ（秒）")]
        [SerializeField] private float openDelay = 0f;
        [Tooltip("閉じるときのアニメーション開始ディレイ（秒）")]
        [SerializeField] private float closeDelay = 0f;

        [Header("Speed Settings")]
        [Tooltip("このパーツの動くスピード。0以下の場合は全体のTransition Speedが使用されます。")]
        [SerializeField] private float transitionSpeed = 0f;

        public Transform PartTransform => partTransform;
        public DoorType DoorType => doorType;
        public Vector3 RotationAxis => rotationAxis;
        public float OpenAngle => openAngle;
        public float CloseAngle => closeAngle;
        public Vector3 SlideDirection => slideDirection;
        public float SlideDistance => slideDistance;
        public float OpenDelay => openDelay;
        public float CloseDelay => closeDelay;
        public float TransitionSpeed => transitionSpeed;

        // 初期状態を記憶する内部変数
        [HideInInspector] public Vector3 initialPosition;
        [HideInInspector] public Quaternion initialRotation;

        public DoorPart(Transform transform, DoorType type, Vector3 rotAxis, float opAngle, float clAngle, Vector3 slideDir, float slideDist, float opDelay, float clDelay, float transSpeed = 0f)
        {
            partTransform = transform;
            doorType = type;
            rotationAxis = rotAxis;
            openAngle = opAngle;
            closeAngle = clAngle;
            slideDirection = slideDir;
            slideDistance = slideDist;
            openDelay = opDelay;
            closeDelay = clDelay;
            transitionSpeed = transSpeed;
        }
    }
    
    public class TempDoor : InteractableComponent
    {
        [Header("Close Message")]
        [SerializeField] 
        private string afterMessage = "door closed";

        [Header("Door Parts Configuration")]
        [Tooltip("動かしたい扉のパーツをここに登録します。")]
        [SerializeField] private List<DoorPart> doorParts = new List<DoorPart>();
        
        private string _beforeMessage;
        public new string InteractMessage => isOpen ? afterMessage : _beforeMessage;

        private bool isOpen = false;
        private bool isMoving = false;
        private Coroutine moveCoroutine;

        private void Start()
        {
            _beforeMessage = base.InteractMessage;

            if (doorParts == null || doorParts.Count == 0)
            {
                Debug.LogWarning($"[TempDoor] No door parts registered on {gameObject.name}.", this);
            }
            else
            {
                // 全パーツの初期状態を記憶
                foreach (var part in doorParts)
                {
                    if (part.PartTransform != null)
                    {
                        part.initialPosition = part.PartTransform.localPosition;
                        part.initialRotation = part.PartTransform.localRotation;
                    }
                }
            }
            
            SyncMessageToBase();
        }

        /// <summary>
        /// プレイヤーのインタラクトによって呼び出されるドア開閉のトリガー
        /// </summary>
        public void Interact()
        {
            if (isMoving) return;

            isOpen = !isOpen;
            
            if (moveCoroutine != null)
            {
                StopCoroutine(moveCoroutine);
            }

            moveCoroutine = StartCoroutine(AnimateDoors(isOpen));
            
            SyncMessageToBase();
        }

        private IEnumerator AnimateDoors(bool targetOpen)
        {
            isMoving = true;

            List<Coroutine> activeCoroutines = new List<Coroutine>();

            foreach (var part in doorParts)
            {
                if (part.PartTransform == null) continue;

                float delay = targetOpen ? part.OpenDelay : part.CloseDelay;
                Coroutine co = StartCoroutine(AnimatePart(part, targetOpen, delay));
                activeCoroutines.Add(co);
            }

            // すべての個別パーツの移動完了を待つ
            foreach (var co in activeCoroutines)
            {
                yield return co;
            }

            isMoving = false;
        }

        private IEnumerator AnimatePart(DoorPart part, bool targetOpen, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            float speed = part.TransitionSpeed;
            if (speed <= 0f)
            {
                Debug.LogWarning($"[TempDoor] Transition speed of door part '{part.PartTransform?.name}' on {gameObject.name} is 0 or less. Falling back to 3.0f.", this);
                speed = 3f;
            }

            if (part.DoorType == DoorType.Rotate)
            {
                float targetAngle = targetOpen ? part.OpenAngle : part.CloseAngle;
                Quaternion targetRotation = Quaternion.Euler(part.RotationAxis * targetAngle);

                while (Quaternion.Angle(part.PartTransform.localRotation, targetRotation) > 0.1f)
                {
                    part.PartTransform.localRotation = Quaternion.Slerp(
                        part.PartTransform.localRotation, 
                        targetRotation, 
                        Time.deltaTime * speed
                    );
                    yield return null;
                }

                part.PartTransform.localRotation = targetRotation;
            }
            else
            {
                Vector3 targetPosition = targetOpen 
                    ? part.initialPosition + (part.SlideDirection.normalized * part.SlideDistance) 
                    : part.initialPosition;

                while (Vector3.Distance(part.PartTransform.localPosition, targetPosition) > 0.001f)
                {
                    part.PartTransform.localPosition = Vector3.Lerp(
                        part.PartTransform.localPosition, 
                        targetPosition, 
                        Time.deltaTime * speed
                    );
                    yield return null;
                }

                part.PartTransform.localPosition = targetPosition;
            }
        }
        
        /// <summary>
        /// ベースクラスのインタラクトメッセージを同期
        /// </summary>
        private void SyncMessageToBase()
        {
            string currentText = isOpen ? afterMessage : _beforeMessage;

            var field = typeof(InteractableComponent).GetField("interactMessage", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
            if (field != null)
            {
                field.SetValue(this, currentText);
            }
        }
    }
}
