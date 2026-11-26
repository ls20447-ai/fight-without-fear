using UnityEngine;
using FWF.Input;
using FWF.Destruction;

namespace FWF.Player
{
    /// <summary>
    /// Mobile third-person controller: move, look, jump, crouch, shoot.
    /// Performance notes:
    ///  - One Update() per player, no LINQ, no GetComponent at runtime.
    ///  - Hit detection uses RaycastNonAlloc with a preallocated buffer.
    ///  - Remote (networked) players should disable this script and be driven by snapshots.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float walkSpeed = 4.5f;
        [SerializeField] float crouchSpeed = 2.2f;
        [SerializeField] float jumpHeight = 1.1f;
        [SerializeField] float gravity = -20f;
        [SerializeField] float turnSmoothTime = 0.08f;

        [Header("Crouch")]
        [SerializeField] float standHeight = 1.8f;
        [SerializeField] float crouchHeight = 1.1f;

        [Header("Camera")]
        [SerializeField] Transform cameraPivot;       // child of player at shoulder height
        [SerializeField] float lookSensitivity = 0.12f;
        [SerializeField] float minPitch = -35f, maxPitch = 65f;
        [SerializeField] float adsSensitivityMult = 0.6f;

        [Header("Shooting (placeholder - replace with weapon system)")]
        [SerializeField] Transform muzzle;
        [SerializeField] float fireRate = 8f;          // shots / sec
        [SerializeField] float range = 80f;
        [SerializeField] float damage = 22f;
        [SerializeField] LayerMask hitMask = ~0;

        CharacterController _cc;
        MobileInputHub _in;
        Transform _cam;

        float _yaw, _pitch, _verticalVel, _turnVel, _nextFireTime;
        bool _crouching;

        // Preallocated buffer: zero GC per shot
        readonly RaycastHit[] _hits = new RaycastHit[4];

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _cam = Camera.main.transform;
            _cc.height = standHeight;
            _cc.center = new Vector3(0, standHeight * 0.5f, 0);
        }

        void Start() => _in = MobileInputHub.Instance;

        void Update()
        {
            if (_in == null) return;

            HandleLook();
            HandleCrouch();
            HandleMove();
            HandleFire();

            _in.ConsumeFrame();
        }

        void HandleLook()
        {
            float sens = lookSensitivity * (_in.AimHeld ? adsSensitivityMult : 1f);
            _yaw   += _in.LookDelta.x * sens;
            _pitch  = Mathf.Clamp(_pitch - _in.LookDelta.y * sens, minPitch, maxPitch);
            cameraPivot.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        void HandleCrouch()
        {
            if (!_in.CrouchTogglePressed) return;

            // Only stand up if there's headroom
            if (_crouching && Physics.SphereCast(transform.position + Vector3.up * crouchHeight * 0.5f,
                    _cc.radius * 0.9f, Vector3.up, out _, standHeight - crouchHeight, hitMask,
                    QueryTriggerInteraction.Ignore))
                return;

            _crouching = !_crouching;
            float h = _crouching ? crouchHeight : standHeight;
            _cc.height = h;
            _cc.center = new Vector3(0, h * 0.5f, 0);
        }

        void HandleMove()
        {
            // Camera-relative movement on the XZ plane
            Vector3 fwd = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
            Vector3 dir = fwd * _in.Move.y + right * _in.Move.x;
            float speed = (_crouching ? crouchSpeed : walkSpeed) * Mathf.Min(1f, dir.magnitude);

            // Face movement direction (or camera direction while aiming/firing)
            bool strafeMode = _in.AimHeld || _in.FireHeld;
            float targetYaw = strafeMode ? _yaw
                            : (dir.sqrMagnitude > 0.01f ? Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg : transform.eulerAngles.y);
            float smoothYaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref _turnVel, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0, smoothYaw, 0);

            // Gravity + jump
            if (_cc.isGrounded && _verticalVel < 0f) _verticalVel = -2f;
            if (_in.JumpPressed && _cc.isGrounded && !_crouching)
                _verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _verticalVel += gravity * Time.deltaTime;

            Vector3 velocity = dir.normalized * speed + Vector3.up * _verticalVel;
            _cc.Move(velocity * Time.deltaTime);
        }

        void HandleFire()
        {
            if (!_in.FireHeld || Time.time < _nextFireTime) return;
            _nextFireTime = Time.time + 1f / fireRate;

            // Shoot from the screen center so the crosshair is accurate in third person
            Ray ray = new Ray(_cam.position, _cam.forward);
            int n = Physics.RaycastNonAlloc(ray, _hits, range, hitMask, QueryTriggerInteraction.Ignore);

            // Find the closest hit that isn't ourselves
            float best = float.MaxValue; int bestIdx = -1;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].transform.root == transform.root) continue;
                if (_hits[i].distance < best) { best = _hits[i].distance; bestIdx = i; }
            }
            if (bestIdx < 0) return;

            var hit = _hits[bestIdx];
            if (hit.collider.TryGetComponent<IDamageable>(out var target))
                target.TakeDamage(damage, hit.point, ray.direction);
            // TODO: tracer/impact VFX from a pool; send hit to server when networking is added
        }
    }
}
