using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NailBalance
{
    public class NailInput : MonoBehaviour
    {
        public GameController game;
        bool dragging, beganOnNail;
        Vector2 start, previous;
        float offset, downwardTravel, maxDistance;
        InputAction press;
        UnityEngine.InputSystem.Pointer pointer;
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        void OnEnable()
        {
            // Keep the press position when several mouse events arrive in one frame.
            InputSystem.settings.disableRedundantEventsMerging = true;
            // Callbacks preserve down/up order even when a quick gesture fits in one frame.
            press = new InputAction("Grab nail", InputActionType.Button, "<Pointer>/press");
            press.started += BeginGesture;
            press.canceled += EndGesture;
            press.Enable();
        }

        void Update()
        {
            Keyboard keys = Keyboard.current;
            if (keys != null && keys.escapeKey.wasPressedThisFrame)
            {
                if (game.State == GameController.Phase.Paused) game.Resume();
                else game.Pause();
            }
            if (!game.CanControl) { dragging = false; return; }
            if (keys != null)
            {
                if (keys.rKey.wasPressedThisFrame) game.RotateNail();
                if (keys.spaceKey.wasPressedThisFrame) game.DropNail();
                if (!game.CanControl) return;
                float direction = (keys.rightArrowKey.isPressed ? 1 : 0) - (keys.leftArrowKey.isPressed ? 1 : 0);
                game.MoveNail(game.HeldNail.transform.position.x + direction * 3f * Time.deltaTime);
                float rotation = (keys.eKey.isPressed ? 1 : 0) - (keys.qKey.isPressed ? 1 : 0);
                if (rotation != 0) game.SetAngle(game.HeldNail.transform.eulerAngles.z + rotation * 70 * Time.deltaTime);
            }

            if (dragging) TrackMovement(pointer.position.ReadValue());
        }

        void BeginGesture(InputAction.CallbackContext context)
        {
            if (!game.CanControl) return;
            pointer = context.control.device as UnityEngine.InputSystem.Pointer;
            if (pointer == null) return;
            Vector2 position = pointer.position.ReadValue();
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
            if (uiHits.Count > 0) return;
            Vector3 world = World(position);
            Vector3 local = game.HeldNail.transform.InverseTransformPoint(world);
            beganOnNail = Mathf.Abs(local.x) < 1.5f && Mathf.Abs(local.y) < 0.4f;
            offset = beganOnNail ? game.HeldNail.transform.position.x - world.x : 0;
            start = previous = position;
            maxDistance = downwardTravel = 0;
            dragging = true;
        }

        void TrackMovement(Vector2 position)
        {
            Vector2 delta = position - previous;
            maxDistance = Mathf.Max(maxDistance, Vector2.Distance(position, start));
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.2f || delta.y > 0) downwardTravel = 0;
            else downwardTravel += -delta.y;
            if (downwardTravel >= Mathf.Max(40, Screen.height * 0.055f))
            {
                game.DropNail();
                dragging = false;
                return;
            }
            if (maxDistance > Screen.height * 0.009f) game.MoveNail(World(position).x + offset);
            previous = position;
        }

        void EndGesture(InputAction.CallbackContext context)
        {
            if (!dragging || !game.CanControl) { dragging = false; return; }
            TrackMovement(pointer.position.ReadValue());
            if (dragging && beganOnNail && maxDistance < Screen.height * 0.012f) game.RotateNail();
            dragging = false;
        }

        Vector3 World(Vector2 point)
        {
            Vector3 world = game.gameCamera.ScreenToWorldPoint(point);
            world.z = 0;
            return world;
        }
        void OnDisable()
        {
            dragging = false;
            press?.Dispose();
        }
    }
}
