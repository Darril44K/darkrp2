using Dxura.Darkrp.UI;
using Sandbox.Events;

namespace Dxura.Darkrp;

public record EquipmentDeployedEvent( Equipment Equipment ) : IGameEvent;

public record EquipmentHolsteredEvent( Equipment Equipment ) : IGameEvent;

public record EquipmentDestroyedEvent( Equipment Equipment ) : IGameEvent;

/// <summary>
/// An equipment component.
/// </summary>
public class Equipment : Component, Component.INetworkListener, IEquipment, IDescription
{
	/// <summary>
	/// A reference to the equipment's <see cref="EquipmentResource"/>.
	/// </summary>
	[Property]
	[Group( "Resources" )]
	public EquipmentResource Resource { get; set; }

	/// <summary>
	/// A tag binder for this equipment.
	/// </summary>
	[RequireComponent]
	public TagBinder TagBinder { get; set; }

	/// <summary>
	/// Shorthand to bind a tag.
	/// </summary>
	/// <param name="tag"></param>
	/// <param name="predicate"></param>
	internal void BindTag( string tag, Func<bool> predicate )
	{
		TagBinder.BindTag( tag, predicate );
	}

	/// <summary>
	/// A reference to the equipment's model renderer.
	/// </summary>
	[Property]
	[Group( "Components" )]
	public SkinnedModelRenderer ModelRenderer { get; set; }

	/// <summary>
	/// The default holdtype for this equipment.
	/// </summary>
	[Property]
	[Group( "Animation" )]
	protected AnimationHelper.HoldTypes HoldType { get; set; } = AnimationHelper.HoldTypes.Rifle;

	/// <summary>
	/// The default holdtype for this equipment.
	/// </summary>
	[Property]
	[Group( "Animation" )]
	public AnimationHelper.Hand Handedness { get; set; } = AnimationHelper.Hand.Right;

	/// <summary>
	/// What sound should we play when taking this gun out?
	/// </summary>
	[Property]
	[Group( "Sounds" )]
	public SoundEvent DeploySound { get; set; }

	/// <summary>
	/// How slower do we walk with this equipment out?
	/// </summary>
	[Property]
	[Group( "Movement" )]
	public float SpeedPenalty { get; set; } = 0f;

	[Property] [Group( "GameObjects" )] public GameObject Muzzle { get; set; }
	[Property] [Group( "GameObjects" )] public GameObject EjectionPort { get; set; }

	/// <summary>
	/// What prefab should we spawn as the mounted version of this piece of equipment?
	/// </summary>
	[Property]
	[Group( "Mount Points" )]
	public GameObject MountedPrefab { get; set; }

	/// <summary>
	/// Should we enable the crosshair?
	/// </summary>
	[Property]
	[Group( "UI" )]
	public bool UseCrosshair { get; set; } = true;
	
	/// <summary>
	/// Should we enable the simple crosshair?
	/// </summary>
	[Property]
	[Group( "UI" )]
	public bool OnlyShowCrosshairDot { get; set; } = false;

	/// <summary>
	/// Cached version of the owner once we fetch it.
	/// </summary>
	private Player? _owner;

	/// <summary>
	/// Who owns this gun?
	/// </summary>
	public Player? Owner => _owner ??= Scene.Directory.FindComponentByGuid( OwnerId ) as Player;

	/// <summary>
	/// The Guid of the owner's <see cref="Player"/>
	/// </summary>
	[HostSync]
	public Guid OwnerId { get; set; }

	// IDescription
	string IDescription.DisplayName => Resource.Name;
	// string IDescription.Icon => Resource.Icon;

	/// <summary>
	/// Is this equipment currently deployed by the player?
	/// </summary>
	[Sync]
	[Change( nameof(OnIsDeployedPropertyChanged) )]
	public bool IsDeployed { get; private set; }

	private bool _wasDeployed { get; set; }
	private bool _hasStarted { get; set; }

	[DeveloperCommand( "Toggle View Model", "Visuals" )]
	private static void ToggleViewModel()
	{
		var player = Player.Local;

		player.CurrentEquipment.ViewModel.ModelRenderer.Enabled =
			!player.CurrentEquipment.ViewModel.ModelRenderer.Enabled;
		player.CurrentEquipment.ViewModel.Arms.Enabled = !player.CurrentEquipment.ViewModel.Arms.Enabled;
	}

	/// <summary>
	/// Updates the render mode, if we're locally controlling a player, we want to hide the world model.
	/// </summary>
	public void UpdateRenderMode( bool force = false )
	{
		var on = force || (Owner.IsValid() && Owner.IsProxy && IsDeployed);

		if ( !Owner.IsValid() && !force )
		{
			on = false;
		}

		ModelRenderer.Enabled = on;
		ModelRenderer.RenderType = on
			? Sandbox.ModelRenderer.ShadowRenderType.On
			: Sandbox.ModelRenderer.ShadowRenderType.ShadowsOnly;
	}

	private ViewModel viewModel;

	/// <summary>
	/// A reference to the equipment's <see cref="Darkrp.ViewModel"/> if it has one.
	/// </summary>
	public ViewModel ViewModel
	{
		get => viewModel;
		set
		{
			viewModel = value;

			if ( viewModel.IsValid() )
			{
				viewModel.Equipment = this;
			}
		}
	}

	void INetworkListener.OnDisconnected( Connection connection )
	{
		if ( !Networking.IsHost )
		{
			return;
		}
		
		// TODO:  Drop printers e.g?
		// var player = GameUtils.Players.FirstOrDefault( x => x.Network.OwnerConnection == connection );
		// if ( !player.IsValid() )
		// {
		// 	return;
		// }
		//
		// DroppedEquipment.Create( Resource, player.Transform.Position + Vector3.Up * 32f, Rotation.Identity, this );
	}

	/// <summary>
	/// Deploy this equipment.
	/// </summary>
	[Authority]
	public void Deploy()
	{
		if ( IsDeployed )
		{
			return;
		}

		// We must first holster all other equipment items.
		if ( Owner.IsValid() )
		{
			var equipment = Owner.Inventory.Equipment.ToList();

			foreach ( var item in equipment )
			{
				item.Holster();
			}
		}

		IsDeployed = true;
	}

	/// <summary>
	/// Holster this equipment.
	/// </summary>
	[Authority]
	public void Holster()
	{
		if ( !IsDeployed )
		{
			return;
		}

		IsDeployed = false;
	}

	/// <summary>
	/// Allow equipment to override holdtypes at any notice.
	/// </summary>
	/// <returns></returns>
	public virtual AnimationHelper.HoldTypes GetHoldType()
	{
		return HoldType;
	}

	private void OnIsDeployedPropertyChanged( bool oldValue, bool newValue )
	{
		// Conna: If `OnStart` hasn't been called yet, don't do anything. It'd be nice to have a property on
		// a Component that can indicate this.
		if ( !_hasStarted )
		{
			return;
		}

		UpdateDeployedState();
	}

	private void UpdateDeployedState()
	{
		if ( IsDeployed == _wasDeployed )
		{
			return;
		}

		switch ( _wasDeployed )
		{
			case false when IsDeployed:
				OnDeployed();
				break;
			case true when !IsDeployed:
				OnHolstered();
				break;
		}

		_wasDeployed = IsDeployed;
	}

	public void ClearViewModel()
	{
		if ( ViewModel.IsValid() )
		{
			ViewModel.GameObject.Destroy();
		}
	}

	/// <summary>
	/// Creates a viewmodel for the player to use.
	/// </summary>
	public void CreateViewModel( bool playDeployEffects = true )
	{
		var player = Owner;
		if ( !player.IsValid() )
		{
			return;
		}

		var resource = Resource;

		ClearViewModel();
		UpdateRenderMode();

		if ( resource.ViewModelPrefab.IsValid() )
		{
			// Create the equipment prefab and put it on the equipment gameobject.
			var viewModelGameObject = resource.ViewModelPrefab.Clone( new CloneConfig()
			{
				Transform = new Transform(), Parent = player.ViewModelGameObject, StartEnabled = true
			} );

			var viewModelComponent = viewModelGameObject.Components.Get<ViewModel>();
			viewModelComponent.PlayDeployEffects = playDeployEffects;

			// equipment needs to know about the ViewModel
			ViewModel = viewModelComponent;

			viewModelGameObject.BreakFromPrefab();
		}

		if ( !playDeployEffects )
		{
			return;
		}

		if ( DeploySound is null )
		{
			return;
		}

		var snd = Sound.Play( DeploySound, Transform.Position );
		if ( !snd.IsValid() )
		{
			return;
		}

		snd.ListenLocal = !IsProxy;
	}

	protected override void OnStart()
	{
		_wasDeployed = IsDeployed;
		_hasStarted = true;

		if ( IsDeployed )
		{
			OnDeployed();
		}
		else
		{
			OnHolstered();
		}
	}

	private bool HasCreatedViewModel { get; set; } = false;

	protected virtual void OnDeployed()
	{
		if ( Owner.IsValid() && Owner.CameraMode == CameraMode.FirstPerson )
		{
			CreateViewModel( !HasCreatedViewModel );
		}

		HasCreatedViewModel = true;

		UpdateRenderMode();

		GameObject.Root.Dispatch( new EquipmentDeployedEvent( this ) );
	}

	protected virtual void OnHolstered()
	{
		UpdateRenderMode();
		ClearViewModel();

		GameObject.Root.Dispatch( new EquipmentHolsteredEvent( this ) );
	}

	protected override void OnDestroy()
	{
		ClearViewModel();

		GameObject.Root.Dispatch( new EquipmentDestroyedEvent( this ) );
	}
}
