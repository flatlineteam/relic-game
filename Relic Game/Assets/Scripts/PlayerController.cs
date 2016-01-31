﻿using System;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts
{
    public class PlayerController : MonoBehaviour
    {	
        [Range(0, 100)]
        public float Gravity;

        [Range(0, 30)]
        public float MaxHorizontalSpeed;

        [Range(0, 10)]
        public float MaxHorizontalAccelleration;

        [Range(0, 1)]
        public float AirControl;

        [Range(0, 10)]
        public float GroundFriction;

        [Range(1, 5)]
        public float ReverseSpeedFactor = 1;
        
        [Range(0, 10)]
        public float JumpStartThreshhold = 1;

        [Range(0, 5)]
        public float JumpStartAmount = 3;
        
        [Range(0, 30)]
        public float JumpForce;

        [Range(0, 10)]
        public float InstantStopSpeedThreshold;

        [Range(0, 30)]
        public float BounceOtherPlayerForce;

        public enum Direction { Left, Right }

        public Direction LastRequestedDirection { get; set; }

        public PlayerState State { get; set; }

        public enum PlayerState
        {
            Grounded,
            InAir,
            ClimbingUpLedge
        }

		private int playerNumber;
        private bool jumpRequested;
        private float desiredHorizontalAccelleration;
        private bool hitGround;

        private Vector2 currentVelocity;

        private new Rigidbody rigidbody;       

        public void Awake()
        {
            rigidbody = GetComponent<Rigidbody>();
        }

		public void Start()
		{
			playerNumber = GetComponent<RelicPlayer>().PlayerNumber;
		}

        public void Update()
        {
            UpdateInput();
        }

        public void FixedUpdate()
        {
            UpdateState();
        }

        private void UpdateState()
        {
            currentVelocity = new Vector2(rigidbody.velocity.x, rigidbody.velocity.y);

            // If you're on the ground...but you're falling...you're not on the ground
            if (currentVelocity.y < 0.1 && State == PlayerState.Grounded)
                State = PlayerState.InAir;

            // Always apply gravity
            rigidbody.AddForce(0, -Gravity, 0);

            if (hitGround)
            {
                State = PlayerState.Grounded;
                hitGround = false;
            }

            if (State == PlayerState.Grounded)
            {
                if (Mathf.Abs(desiredHorizontalAccelleration) > 0)
                {
                    TryPlayerMoveOnGround();
                }
                else
                {
                    StopInstantly();
                    //SlowDownHorizontalMovement();
                }

                if (Mathf.Abs(currentVelocity.x) > MaxHorizontalSpeed)
                {
                    var difference = currentVelocity.x < 0
                        ? currentVelocity.x + MaxHorizontalSpeed
                        : currentVelocity.x - MaxHorizontalSpeed;

                    var correction = -difference;
                    
                    rigidbody.AddForce(new Vector3(correction, 0, 0), ForceMode.VelocityChange);
                    //SlowDownHorizontalMovement();
                }

                TryJump();
            }
            else if (State == PlayerState.InAir)
            {
                if (Mathf.Abs(desiredHorizontalAccelleration) > 0)
                {
                    // Air control
                    TryPlayerMoveInAir();
                }
            }
        }

        private void TryJump()
        {
            if (!jumpRequested)
                return;

            rigidbody.AddForce(0, JumpForce, 0, ForceMode.Impulse);
            jumpRequested = false;
            State = PlayerState.InAir;
        }

        private void StopInstantly()
        {
            rigidbody.AddForce(new Vector3(-currentVelocity.x, 0, 0), ForceMode.VelocityChange);
        }

        private void TryPlayerMoveOnGround()
        {
            if (Mathf.Abs(currentVelocity.x) < JumpStartThreshhold)
            {
                // We need a jump-start
                rigidbody.AddForce(new Vector3(JumpStartAmount * Mathf.Sign(desiredHorizontalAccelleration), 0, 0),
                    ForceMode.Impulse);
            }

            if ((int) Mathf.Sign(desiredHorizontalAccelleration) != (int) Mathf.Sign(currentVelocity.x))
            {
                //We're trying to reverse direction
                rigidbody.AddForce(new Vector3(desiredHorizontalAccelleration * GroundFriction * ReverseSpeedFactor, 0, 0));
            }
            else
            {
                // Apply force to move
                rigidbody.AddForce(new Vector3(desiredHorizontalAccelleration, 0, 0));
            }
        }

        private void TryPlayerMoveInAir()
        {
            if (Mathf.Abs(currentVelocity.x) < JumpStartThreshhold)
            {
                // We need a jump-start
                var amount = new Vector3(JumpStartAmount * Mathf.Sign(desiredHorizontalAccelleration), 0, 0) *
                             AirControl;
                rigidbody.AddForce(amount, ForceMode.Impulse);
            }

            if ((int)Mathf.Sign(desiredHorizontalAccelleration) != (int)Mathf.Sign(currentVelocity.x))
            {
                //We're trying to reverse direction
                rigidbody.AddForce(new Vector3(desiredHorizontalAccelleration * ReverseSpeedFactor * AirControl, 0, 0));
            }
            else
            {
                // Apply force to move
                rigidbody.AddForce(new Vector3(desiredHorizontalAccelleration * AirControl, 0, 0));
            }
        }

        public void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Player"))
            {
                OnCollisionPlayer(collision);
                return;
            }

            OnCollisionOther(collision);
        }

        private void OnCollisionOther(Collision collision)
        {
            var points = collision.contacts.First(); //todo
            var normal = new Vector2(points.normal.x, points.normal.y);

            var upAmount = Vector2.Dot(new Vector2(0, 1), normal);

            if (upAmount > 0.1) // Facing up?
            {
                hitGround = true;
            }
        }

        private void OnCollisionPlayer(Collision collision)
        {
            var normal = collision.contacts.First().normal;

            if (currentVelocity.y >= 0)
                return; // Moving up

            var amountUp = Vector3.Dot(Vector3.up, normal);

            if (amountUp >= 0.1)
            {
                // The contact normal is "pointing up", which means we stomped someone else
                GetComponent<RelicPlayer>().SquashOtherPlayer(collision.gameObject, collision);
            }
        }

        public void CancelVerticalMomentum()
        {
            var verticalSpeed = currentVelocity.y;

            rigidbody.AddForce(new Vector3(0, -verticalSpeed, 0), ForceMode.VelocityChange);
        }

        public void DoBounceOnOtherPlayer(Collision collision)
        {
            CancelVerticalMomentum();

            rigidbody.AddForce(new Vector3(0, BounceOtherPlayerForce, 0), ForceMode.Impulse);
        }

        public void OnCollisionStay(Collision collision)
        {
            var points = collision.contacts.First(); //todo
            var normal = new Vector2(points.normal.x, points.normal.y);

            var upAmount = Vector2.Dot(new Vector2(0, 1), normal);

            if (upAmount > 0.1) // Facing up?
            {
                hitGround = true;
            }
        }

        private void UpdateInput()
        {
            if (playerNumber == 0)
                throw new InvalidOperationException("Player number hasn't been set.");

            var horizontalAxis = Input.GetAxis("Horizontal" + playerNumber);
            
			var direction = Input.GetAxis("Horizontal" + playerNumber);

            if (playerNumber == 1)
            {
                //Debug.Log(direction);
            }
            
			if(direction != 0)
			{
				direction = direction / Mathf.Abs(direction);
			}

            desiredHorizontalAccelleration = direction * MaxHorizontalAccelleration;

            LastRequestedDirection = direction == -1
                ? Direction.Left
                : direction == 1 ? Direction.Right : LastRequestedDirection;

            if (Input.GetButton("buttonA" + playerNumber)) 
            {
                if(State == PlayerState.Grounded)
                    jumpRequested = true;
            }
        }
    }
}