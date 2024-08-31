using Sandbox.Diagnostics;
using Sandbox.Events;

namespace Dxura.Darkrp;

public record OnPlayerRagdolledEvent : IGameEvent
{
	public float DestroyTime { get; set; } = 0f;
}

public partial class Player
{
	
	/// <summary>
	/// An accessor for health component if we have one.
	/// </summary>
	[Property]
	public HealthComponent HealthComponent { get; set; }
	
	/// <summary>
	/// The player's health component
	/// </summary>
	[RequireComponent]
	public ArmorComponent ArmorComponent { get; private set; }

	/// <summary>
	/// The player's inventory, items, etc.
	/// </summary>
	[RequireComponent]
	public PlayerInventory Inventory { get; private set; }

	/// <summary>
	/// How long since the player last respawned?
	/// </summary>
	[HostSync]
	public TimeSince TimeSinceLastRespawn { get; private set; }

	public void OnKill( DamageInfo damageInfo )
	{
		if ( Networking.IsHost )
		{
			ArmorComponent.HasHelmet = false;
			ArmorComponent.Armor = 0f;

			PlayerState.RespawnState = RespawnState.Requested;

			Inventory.Clear();
			CreateRagdoll();
		}

		PlayerBoxCollider.Enabled = false;

		if ( IsProxy )
		{
			return;
		}

		PlayerState.OnKill( damageInfo );

		Holster();

		_previousVelocity = Vector3.Zero;
		CameraController.Mode = CameraMode.ThirdPerson;
	}

	public void SetSpawnPoint( SpawnPointInfo spawnPoint )
	{
		SpawnPosition = spawnPoint.Position;
		SpawnRotation = spawnPoint.Rotation;

		SpawnPointTags.Clear();

		foreach ( var tag in spawnPoint.Tags )
		{
			SpawnPointTags.Add( tag );
		}
	}

	public void OnRespawn()
	{
		Assert.True( Networking.IsHost );

		OnHostRespawn();
		OnClientRespawn();
	}

	private void OnHostRespawn()
	{
		Assert.True( Networking.IsHost );

		_previousVelocity = Vector3.Zero;

		// Leave a seat if we are in one.
		if ( CurrentSeat.IsValid() )
		{
			CurrentSeat.Leave( this );
		}

		Teleport( SpawnPosition, SpawnRotation );

		if ( Body is not null )
		{
			Body.DamageTakenForce = Vector3.Zero;
		}

		if ( HealthComponent.State != LifeState.Alive )
		{
			ArmorComponent.HasHelmet = false;
			ArmorComponent.Armor = 0f;
		}

		HealthComponent.Health = HealthComponent.MaxHealth;

		TimeSinceLastRespawn = 0f;

		ResetBody();
		Scene.Dispatch( new PlayerSpawnedEvent( this ) );
	}

	[Authority]
	private void OnClientRespawn()
	{
		if ( !PlayerState.IsValid() )
		{
			return;
		}

		SteamId = Connection.Local.SteamId;

		CameraController.SetActive( true );
		CameraController.Mode = CameraMode.FirstPerson;
	}

	public void Teleport( Transform transform )
	{
		Teleport( transform.Position, transform.Rotation );
	}

	[Authority]
	public void Teleport( Vector3 position, Rotation rotation )
	{
		Transform.World = new Transform( position, rotation );
		Transform.ClearInterpolation();
		EyeAngles = rotation.Angles();

		if ( CharacterController.IsValid() )
		{
			CharacterController.Velocity = Vector3.Zero;
			CharacterController.IsOnGround = true;
		}
	}

	[Broadcast( NetPermission.HostOnly )]
	private void CreateRagdoll()
	{
		if ( !Body.IsValid() )
		{
			return;
		}

		Body.SetRagdoll( true );
		Body.GameObject.SetParent( null, true );
		Body.GameObject.Name = $"Ragdoll ({DisplayName})";

		var ev = new OnPlayerRagdolledEvent();
		Scene.Dispatch( ev );

		if ( ev.DestroyTime > 0f )
		{
			var comp = Body.Components.Create<TimedDestroyComponent>();
			comp.Time = ev.DestroyTime;
		}

		Body = null;
	}

	private void ResetBody()
	{
		if ( Body is not null )
		{
			Body.DamageTakenForce = Vector3.Zero;
		}

		PlayerBoxCollider.Enabled = true;

		Components.Get<HumanOutfitter>( FindMode.EnabledInSelfAndDescendants )?
			.UpdateFromJob( Job );
	}
}
