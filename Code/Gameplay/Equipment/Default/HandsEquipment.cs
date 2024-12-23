﻿namespace Dxura.Darkrp;

public class HandsEquipment : InputWeaponComponent
{
	[Property] private float InteractRange { get; set; } = 150f;
	[Property] private float ThrowForce { get; set; } = 450f;
	[Property] private float MaxReleaseVelocity { get; set; } = 500f;
	[Property] private float RotateSpeed { get; set; } = 1f;

	[Property] private float HoldDistance { get; set; } = 55f;
	private float _heldDistance;
	private Rotation _heldRotation = Rotation.Identity;
	
	private GameObject? _held;
	private PhysicsBody? _heldBody;

	private float _lastPickupTime;
	private bool _rotating;
	private const float DeltaPickupTime = 0.20f;

	private bool IsHolding() => _held != null;

	protected override void OnStart()
	{
		if ( IsProxy )
		{
			Enabled = false;
		}
	}

	protected override void OnInputUpdate()
	{
		if ( IsHolding() )
		{
			_rotating = false;
			
			// Rotate the object if the player is holding down the rotate button
			if ( Input.Down( "Use" ) )
			{
				RotateHeldObject();
			}
			else if ( Input.Released( "attack2" ) && RealTime.Now - _lastPickupTime > DeltaPickupTime )
			{
				Release(ThrowForce);
			}
			else if ( Input.Released( "attack1" ) && RealTime.Now - _lastPickupTime > DeltaPickupTime )
			{
				Release();
			}
			
			Equipment.Owner.LockCamera = _rotating;
		}else if ( Input.Down( "attack1" ))
		{
			AttemptGrab();
		}	
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		
		if ( _held == null )
		{
			return;
		}

		if ( !_held.IsValid )
		{
			Release();
			return;
		}

		// Calculate the offset from the object's position to its center
		var centerOffset = _heldBody?.MassCenter - _heldBody?.Position ?? Vector3.Zero;

		// Calculate the target position, adjusting for the center offset
		var holdPosition = Player.CameraGameObject!.Transform.Position + Player.CameraGameObject!.Transform.World.Forward * _heldDistance - centerOffset;

		// Check if the object is too far away from the hold position
		var heldDistance = Vector3.DistanceBetween( _held.Transform.Position, holdPosition );
		if ( heldDistance > InteractRange )
		{
			Release();
			return;
		}

		if ( _heldBody == null )
		{
			return;
		}

		var velocity = _heldBody.Velocity;
		Vector3.SmoothDamp( _heldBody.Position, holdPosition, ref velocity, 0.075f, Time.Delta );
		_heldBody.Velocity = velocity;

		var angularVelocity = _heldBody.AngularVelocity;
		Rotation.SmoothDamp( _heldBody.Rotation, _heldRotation, ref angularVelocity, 0.075f, Time.Delta );
		_heldBody.AngularVelocity = angularVelocity;

		// prevent the prop to collide with the player when we are holding it 
		if ( _held.IsValid && _heldBody.Velocity != 0) { _held.Tags.Add( "nocollide" ); }
	}
	

	private void AttemptGrab()
	{
		// Starting position of the line (camera position)
		var start = Player.CameraGameObject!.Transform.Position;

		// Direction of the line (the direction the camera is facing)
		var direction = Player.CameraGameObject!.Transform.World.Forward;

		// Calculate the end position based on direction and interact range
		var end = start + direction * InteractRange;

		// Perform a line trace (raycast) to detect objects in the line of sight (raycast ignore the player)
		var tr = Scene.Trace.Ray( start, end )
			.UseHitboxes()
			.IgnoreGameObject( GameObject )
			.WithAnyTags( "entity", "pickup", "prop" )
			.Run();

		if ( !tr.Hit || !tr.GameObject.IsValid() || tr.GameObject.Tags.Has( "map" ) || tr.StartedSolid ) return;

		var rootObject = tr.GameObject.Root;
		var body = tr.Body;

		if ( !body.IsValid() )
		{
			if ( rootObject.IsValid() )
			{
				var rb = rootObject.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
				if ( rb.IsValid() )
				{
					body = rb.PhysicsBody;
				}
			}
		}

		if ( !body.IsValid() ) return;

		// Don't move keyframed
		if ( body.BodyType == PhysicsBodyType.Keyframed) return;
		
		// PROPs: Don't allow holding other people's props OR frozen props
		if ( rootObject.Tags.Contains( "prop" ) && (!rootObject.Network.IsOwner || body.BodyType == PhysicsBodyType.Static) )
		{
			return;
		}
		
		Grab( tr.GameObject, tr.Body );
	}

	public void Grab( GameObject target, PhysicsBody targetBody )
	{
		target.Network.TakeOwnership();

		var bounds = target.GetBounds();
		var boundsExtents = bounds.Extents;
		_heldDistance = HoldDistance + Math.Max( Math.Max( boundsExtents.x, boundsExtents.y ), boundsExtents.z );
		_heldRotation = target.Transform.Rotation;

		_held = target;
		_heldBody = targetBody;

		_heldBody.BodyType = PhysicsBodyType.Dynamic;

		Player.CantSwitch = true;
		
		_lastPickupTime = RealTime.Now;

	}

	public void Release( float throwingForce = 0 )
	{
		if ( _heldBody.IsValid() )
		{
			_heldBody.AutoSleep = true;

			// Cap the velocity
			var currentVelocity = _heldBody.Velocity;
			if ( currentVelocity.Length > MaxReleaseVelocity )
			{
				currentVelocity = currentVelocity.Normal * MaxReleaseVelocity;
				_heldBody.Velocity = currentVelocity;
			}

			_heldBody.ApplyImpulse( Player.CameraGameObject!.Transform.World.Forward * _heldBody.Mass * throwingForce );
		}

		_held?.Tags.Remove( "nocollide" );
		_held = null;
		_heldBody = null;
		Player.CantSwitch = false;
	}

	private void RotateHeldObject()
	{
		_rotating = true;
		var input = Input.MouseDelta * RotateSpeed;

		var eyeRot = Player.EyeAngles.ToRotation();

		// Create rotation around local X and Y axes
		var rotX = Rotation.FromAxis( eyeRot * Vector3.Right, input.y );
		var rotY = Rotation.FromAxis( eyeRot * Vector3.Up, input.x );

		// Combine rotations
		var newRot = rotY * rotX;

		// Apply to current held rotation
		_heldRotation = newRot * _heldRotation;
	}
}
