/*
 * This code is part of Arcade Car Physics for Unity by Saarg (2018)
 *
 * This is distributed under the MIT Licence (see LICENSE.md for details)
 */

using UnityEngine;
#if MULTIOSCONTROLS
    using MOSC;
#endif


public interface IVehicle
{
    bool Handbrake { get; }
    float Speed { get; }
}


namespace VehicleBehaviour
{
    [RequireComponent(typeof(Rigidbody))]
    public class WheelVehicle : MonoBehaviour, IVehicle
    {
        public PlayerAvatar playerAvatar;
        [Header("Inputs")]
        #if MULTIOSCONTROLS
        [SerializeField] PlayerNumber playerId;
        #endif
        // If isPlayer is false inputs are ignored
        [SerializeField]
        private bool isPlayer = true;

        public bool IsPlayer
        {
            get => isPlayer;
            set => isPlayer = value;
        }

        // Input names to read using GetAxis
        #pragma warning disable 0414
        [SerializeField] private string throttleInput = "Throttle";
        [SerializeField] private string brakeInput = "Brake";
        [SerializeField] private string turnInput = "Horizontal";
        [SerializeField] private string jumpInput = "Jump";
        [SerializeField] private string driftInput = "Drift";
        [SerializeField] private string boostInput = "Boost";
        [SerializeField] private string blinkersLeftInput = "blinker_left";
        [SerializeField] private string blinkersRightInput = "blinker_right";
        [SerializeField] private string blinkersClearInput = "blinker_clear";
        #pragma warning restore 0414

        /*
         *  Turn input curve: x real input, y value used
         *  My advice (-1, -1) tangent x, (0, 0) tangent 0 and (1, 1) tangent x
         */
        [SerializeField] private AnimationCurve turnInputCurve = AnimationCurve.Linear(-1.0f, -1.0f, 1.0f, 1.0f);

        [Header("Wheels")]
        [SerializeField]
        private WheelCollider[] driveWheel;
        public WheelCollider[] DriveWheel => driveWheel;
        [SerializeField] private WheelCollider[] turnWheel;

        public WheelCollider[] TurnWheel => turnWheel;

        // This code checks if the car is grounded only when needed and the data is old enough
        private bool isGrounded = false;
        private int lastGroundCheck = 0;

        public bool IsGrounded
        {
            get
            {
                if (lastGroundCheck == Time.frameCount)
                {
                    return isGrounded;
                }

                lastGroundCheck = Time.frameCount;
                isGrounded = true;

                foreach (var wheel in wheels)
                {
                    if (!wheel.gameObject.activeSelf || !wheel.isGrounded)
                    {
                        isGrounded = false;
                    }
                }

                return isGrounded;
            }
        }

        [Header("Behaviour")]
        /*
         *  Motor torque represent the torque sent to the wheels by the motor with x: speed in km/h and y: torque
         *  The curve should start at x=0 and y>0 and should end with x>topspeed and y<0
         *  The higher the torque the faster it accelerate
         *  the longer the curve the faster it gets
         */
        [SerializeField]
        private AnimationCurve motorTorque = new(new Keyframe(0, 200), new Keyframe(50, 300), new Keyframe(200, 0));
        [SerializeField] private AnimationCurve deaccelerateMotorTorque = new(new Keyframe(0, 400), new Keyframe(200, 600));

        // Differential gearing ratio
        [Range(2, 16)]
        [SerializeField]
        private float diffGearing = 4.0f;

        public float DiffGearing
        {
            get => diffGearing;
            set => diffGearing = value;
        }

        // Basicaly how hard it brakes
        [SerializeField] private float brakeForce = 1500.0f;

        public float BrakeForce
        {
            get => brakeForce;
            set => brakeForce = value;
        }

        // Max steering hangle, usualy higher for drift car
        [Range(0f, 50.0f)]
        [SerializeField]
        private float steerAngle = 30.0f;

        public float SteerAngle
        {
            get => steerAngle;
            set => steerAngle = Mathf.Clamp(value, 0.0f, 50.0f);
        }

        // The value used in the steering Lerp, 1 is instant (Strong power steering), and 0 is not turning at all
        [Range(0.001f, 1.0f)]
        [SerializeField]
        private float steerSpeed = 0.2f;

        public float SteerSpeed
        {
            get => steerSpeed;
            set => steerSpeed = Mathf.Clamp(value, 0.001f, 1.0f);
        }

        // How hight do you want to jump?
        [Range(1f, 1.5f)]
        [SerializeField]
        private float jumpVel = 1.3f;

        public float JumpVel
        {
            get => jumpVel;
            set => jumpVel = Mathf.Clamp(value, 1.0f, 1.5f);
        }

        // How hard do you want to drift?
        [Range(0.0f, 2f)]
        [SerializeField]
        private float driftIntensity = 1f;

        public float DriftIntensity
        {
            get => driftIntensity;
            set => driftIntensity = Mathf.Clamp(value, 0.0f, 2.0f);
        }

        // Reset Values
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        /*
         *  The center of mass is set at the start and changes the car behavior A LOT
         *  I recomment having it between the center of the wheels and the bottom of the car's body
         *  Move it a bit to the from or bottom according to where the engine is
         */
        [SerializeField] private Transform centerOfMass;

        // Force aplied downwards on the car, proportional to the car speed
        [Range(0.5f, 10f)]
        [SerializeField]
        private float downforce = 1.0f;

        public float Downforce
        {
            get => downforce;
            set => downforce = Mathf.Clamp(value, 0, 5);
        }

        // When IsPlayer is false you can use this to control the steering
        private float steering;

        public float Steering
        {
            get => steering;
            set => steering = Mathf.Clamp(value, -1f, 1f);
        }

        // When IsPlayer is false you can use this to control the throttle
        private float throttle;

        public float Throttle
        {
            get => throttle;
            set => throttle = Mathf.Clamp(value, -1f, 1f);
        }

        // Like your own car handbrake, if it's true the car will not move
        [SerializeField] private bool handbrake;

        public bool Handbrake
        {
            get => handbrake;
            set => handbrake = value;
        }

        // Use this to disable drifting
        [HideInInspector] public bool allowDrift = true;
        public bool Drift { get; set; }

        // Use this to read the current car speed (you'll need this to make a speedometer)
        [SerializeField] private float speed = 0.0f;
        public float Speed => speed;

        [Header("Particles")]
        // Exhaust fumes
        [SerializeField]
        private ParticleSystem[] gasParticles;

        [Header("Boost")]
        // Disable boost
        [HideInInspector] public bool allowBoost = true;

        // Maximum boost available
        [SerializeField] private float maxBoost = 10f;

        public float MaxBoost
        {
            get => maxBoost;
            set => maxBoost = value;
        }

        // Current boost available
        [SerializeField] private float boost = 10f;

        public float Boost
        {
            get => boost;
            set => boost = Mathf.Clamp(value, 0f, maxBoost);
        }

        // Regen boostRegen per second until it's back to maxBoost
        [Range(0f, 1f)]
        [SerializeField]
        private float boostRegen = 0.2f;

        public float BoostRegen
        {
            get => boostRegen;
            set => boostRegen = Mathf.Clamp01(value);
        }

        /*
         *  The force applied to the car when boosting
         *  NOTE: the boost does not care if the car is grounded or not
         */
        [SerializeField] private float boostForce = 5000;

        public float BoostForce
        {
            get => boostForce;
            set => boostForce = value;
        }

        // Use this to boost when IsPlayer is set to false
        public bool boosting = false;

        public float breaking;
        // Use this to jump when IsPlayer is set to false
        public bool jumping = false;

        // Boost particles and sound
        [SerializeField] private ParticleSystem[] boostParticles;
        [SerializeField] private AudioClip boostClip;
        [SerializeField] private AudioSource boostSource;
        [SerializeField] private CarBlinkers blinkers;

        // Private variables set at the start
        private Rigidbody _rb;
        private WheelCollider[] wheels;


        // Init rigidbody, center of mass, wheels and more
        private void Start()
        {
            #if MULTIOSCONTROLS
            Debug.Log("[ACP] Using MultiOSControls");
            #endif
            if (boostClip != null)
            {
                boostSource.clip = boostClip;
            }

            boost = maxBoost;

            _rb = GetComponent<Rigidbody>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;

            if (_rb != null && centerOfMass != null)
            {
                _rb.centerOfMass = centerOfMass.localPosition;
            }

            wheels = GetComponentsInChildren<WheelCollider>();

            // Set the motor torque to a non null value because 0 means the wheels won't turn no matter what
            foreach (var wheel in wheels)
            {
                wheel.motorTorque = 0.0001f;
            }
        }


        private bool reverse = false;


        // Visual feedbacks and boost regen
        private void Update()
        {
            foreach (var gasParticle in gasParticles)
            {
                gasParticle.Play();
                var em = gasParticle.emission;
                em.rateOverTime = handbrake ? 0 : Mathf.Lerp(em.rateOverTime.constant, Mathf.Clamp(150.0f * throttle, 30.0f, 100.0f), 0.1f);
            }

            if (isPlayer && allowBoost)
            {
                boost += Time.deltaTime * boostRegen;

                if (boost > maxBoost)
                {
                    boost = maxBoost;
                }
            }

            // Get all the inputs!
            if (isPlayer)
            {
                if (Input.GetButtonDown("forward"))
                {
                    reverse = false;
                }
                else if (Input.GetButtonDown("reverse"))
                {
                    reverse = true;
                }

                if (Input.GetButtonDown("blinker_left"))
                {
                    if (blinkers.State != BlinkerState.Left)
                    {
                        blinkers.StartLeftBlinkers();
                    }
                    else
                    {
                        blinkers.Stop();
                    }
                }
                else if (Input.GetButtonDown("blinker_right"))
                {
                    if (blinkers.State != BlinkerState.Right)
                    {
                        blinkers.StartRightBlinkers();
                    }
                    else
                    {
                        blinkers.Stop();
                    }
                }
                else if (Input.GetButtonDown("blinker_clear"))
                {
                    blinkers.Stop();
                }
            }
        }


        private float steeringWheelAngle = 0;
        public Transform steeringWheel;
        public float steeringWheelMul = -2;


        // Update everything
        private void FixedUpdate()
        {
            GetCurrentSpeed();

            GatherInputs();

            steeringWheelAngle = Mathf.Lerp(steeringWheelAngle, steering * steeringWheelMul, steerSpeed);

            if (steeringWheel != null)
            {
                steeringWheel.localRotation = Quaternion.AngleAxis(steeringWheelAngle, Vector3.forward);
            }

            // Direction
            foreach (var wheel in turnWheel)
            {
                wheel.steerAngle = Mathf.Lerp(wheel.steerAngle, steering, steerSpeed);
            }

            foreach (var wheel in wheels)
            {
                wheel.brakeTorque = 0;
            }

            ApplyHandbrake();

            // Jump
            if (jumping && isPlayer)
            {
                if (!IsGrounded)
                {
                    return;
                }

                _rb.velocity += transform.up * jumpVel;
            }

            CalculateBoost();

            CalculateDrift();

            // Downforce
            _rb.AddForce(-transform.up * (speed * downforce));
        }


        private void GetCurrentSpeed()
        {
            // Measure current speed
            speed = transform.InverseTransformDirection(_rb.velocity).z * 3.6f;
        }


        private void GatherInputs()
        {
            if (!isPlayer)
            {
                return;
            }

            // Accelerate & brake
            if (!string.IsNullOrEmpty(throttleInput))
            {
                throttle = GetInput(throttleInput) * (reverse ? -1f : 1);
                Debug.Log("Throttle value:" + throttle);
            }

            breaking = Mathf.Clamp01(GetInput(brakeInput));
            playerAvatar.SetBreakLights(breaking > 0);

            // Turn
            steering = turnInputCurve.Evaluate(GetInput(turnInput)) * steerAngle;
        }


        private void ApplyHandbrake()
        {
            // Handbrake
            if (Mathf.Abs(speed) < 2 && GetInput(throttleInput) < 0.1f)
            {
                foreach (var wheel in wheels)
                {
                    // Don't zero out this value or the wheel completly lock up
                    wheel.motorTorque = 0.0001f;
                    wheel.brakeTorque = brakeForce;
                }

                return;
            }

            foreach (var wheel in driveWheel)
            {
                if ((speed <= 0 && !reverse && throttle < 0) || (speed >= 0 && reverse && throttle > 0))
                {
                    wheel.motorTorque = 0;
                }
                else
                {
                    wheel.motorTorque = throttle > 0 ? throttle * motorTorque.Evaluate(speed) * diffGearing / driveWheel.Length : throttle * deaccelerateMotorTorque.Evaluate(speed) * diffGearing / driveWheel.Length;
                }
            }

            foreach (var wheel in wheels)
            {
                wheel.brakeTorque = Mathf.Abs(breaking) * brakeForce;
            }
        }


        private void CalculateBoost()
        {
            // Boost
            if (boosting && allowBoost && boost > 0.1f)
            {
                _rb.AddForce(transform.forward * boostForce);

                boost -= Time.fixedDeltaTime;

                if (boost < 0f)
                {
                    boost = 0f;
                }

                if (boostParticles.Length > 0 && !boostParticles[0].isPlaying)
                {
                    foreach (var boostParticle in boostParticles)
                    {
                        boostParticle.Play();
                    }
                }

                if (boostSource != null && !boostSource.isPlaying)
                {
                    boostSource.Play();
                }
            }
            else
            {
                if (boostParticles.Length > 0 && boostParticles[0].isPlaying)
                {
                    foreach (var boostParticle in boostParticles)
                    {
                        boostParticle.Stop();
                    }
                }

                if (boostSource != null && boostSource.isPlaying)
                {
                    boostSource.Stop();
                }
            }
        }


        private void CalculateDrift()
        {
            if (!Drift || !allowDrift)
            {
                return;
            }

            var driftForce = -transform.right;
            driftForce.y = 0.0f;
            driftForce.Normalize();

            if (steering != 0)
            {
                driftForce *= _rb.mass * speed / 7f * throttle * steering / steerAngle;
            }

            var driftTorque = transform.up * (0.1f * steering) / steerAngle;


            _rb.AddForce(driftForce * driftIntensity, ForceMode.Force);
            _rb.AddTorque(driftTorque * driftIntensity, ForceMode.VelocityChange);
        }


        // Reposition the car to the start position
        public void ResetPos()
        {
            transform.position = spawnPosition;
            transform.rotation = spawnRotation;

            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }


        public void toogleHandbrake(bool h)
        {
            handbrake = h;
        }


        // MULTIOSCONTROLS is another package I'm working on ignore it I don't know if it will get a release.
        #if MULTIOSCONTROLS
        private static MultiOSControls _controls;
        #endif


        // Use this method if you want to use your own input manager
        private float GetInput(string input)
        {
            #if MULTIOSCONTROLS
        return MultiOSControls.GetValue(input, playerId);
            #else
            return Input.GetAxis(input);
            #endif
        }
    }
}