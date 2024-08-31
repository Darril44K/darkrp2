﻿using Dxura.Darkrp;
using Sandbox.Events;

namespace Dxura.Darkrp;

public abstract class GivePlayerStateComponent<T> : Component,
	IGameEventHandler<EnterStateEvent>,
	IGameEventHandler<LeaveStateEvent>,
	IGameEventHandler<PlayerJoinedEvent>
	where T : Component, new()
{
	private void CreateForPlayer( PlayerState player )
	{
		if ( player.Components.Get<T>( FindMode.EverythingInChildren ) is { } existing )
		{
			if ( !player.IsConnected )
			{
				return;
			}

			existing.GameObject.Network.AssignOwnership( player.Connection );

			return;
		}

		var obj = Scene.CreateObject( false );

		obj.Name = typeof(T).Name;

		obj.SetParent( player.GameObject, false );
		obj.Components.Create<T>();
		obj.Enabled = true;

		if ( player.IsConnected )
		{
			obj.NetworkSpawn( player.Connection );
		}
		else
		{
			obj.NetworkSpawn();
		}
	}

	void IGameEventHandler<EnterStateEvent>.OnGameEvent( EnterStateEvent eventArgs )
	{
		foreach ( var player in GameUtils.AllPlayers )
		{
			CreateForPlayer( player );
		}
	}

	void IGameEventHandler<LeaveStateEvent>.OnGameEvent( LeaveStateEvent eventArgs )
	{
		foreach ( var player in GameUtils.AllPlayers )
		{
			player.Components.Get<T>()?.Destroy();
		}
	}

	void IGameEventHandler<PlayerJoinedEvent>.OnGameEvent( PlayerJoinedEvent eventArgs )
	{
		CreateForPlayer( eventArgs.Player );
	}
}
