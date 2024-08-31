using Dxura.Darkrp;

namespace Dxura.Darkrp;

public enum IdleType
{
	Default,
	WithRifle,
	WithKnife,
	WithPistol,
	Sitting,
	Leaning
}

public sealed class IdleAnimator : Component
{
	[Property] public SkinnedModelRenderer Renderer { get; set; }
	[Property] public GameObject WeaponPrefab { get; set; }
	[Property] public GameObject HoldObject { get; set; }
	[Property] public IdleType IdleType { get; set; }

	/// <summary>
	/// Remove this whole fucking function. Jesus.
	/// </summary>
	/// <param name="eq"></param>
	private async void AsyncOn( Equipment eq )
	{
		await GameTask.DelaySeconds( 0.05f );

		if ( !eq.IsValid() )
		{
			return;
		}

		eq.UpdateRenderMode( true );
	}

	protected override void OnStart()
	{
		var inst = WeaponPrefab?.Clone( new CloneConfig()
		{
			StartEnabled = true, Parent = HoldObject, Transform = new Transform()
		} );
		var eq = inst.Components.Get<Equipment>( FindMode.EverythingInSelfAndDescendants );
		AsyncOn( eq );
	}

	protected override void OnFixedUpdate()
	{
		if ( Renderer.IsValid() )
		{
			Renderer.Set( "idle_type", (int)IdleType );
		}
	}
}
