using System;
using Dalamud.Plugin.Services;

namespace MarketMogul.Services;

public sealed record WorldInfo(uint Id, string Name, string DataCenter);

/// <summary>Suit le monde d'origine et le monde actuel du personnage connecté.</summary>
public sealed class WorldTracker : IDisposable
{
    private readonly IPlayerState playerState;
    private readonly IFramework framework;

    public WorldInfo? Home { get; private set; }
    public WorldInfo? Current { get; private set; }

    /// <summary>Hors de son monde d'origine : c'est là qu'on achète.</summary>
    public bool IsVisiting => Home is not null && Current is not null && Home.Id != Current.Id;

    public bool IsOnOtherDataCenter => IsVisiting && Home!.DataCenter != Current!.DataCenter;

    /// <summary>Déclenché sur le thread du jeu à chaque changement de monde (connexion et déconnexion comprises).</summary>
    public event Action? WorldChanged;

    public WorldTracker(IPlayerState playerState, IFramework framework)
    {
        this.playerState = playerState;
        this.framework = framework;
        framework.Update += OnUpdate;
    }

    private void OnUpdate(IFramework _)
    {
        if (!playerState.IsLoaded)
        {
            if (Current is null)
                return;
            Home = Current = null;
            WorldChanged?.Invoke();
            return;
        }

        // Comparaison d'ids à chaque frame ; la lecture des tables du jeu n'a lieu qu'en cas de changement.
        var homeId = playerState.HomeWorld.RowId;
        var currentId = playerState.CurrentWorld.RowId;
        if (homeId == Home?.Id && currentId == Current?.Id)
            return;

        Home = ToWorldInfo(playerState.HomeWorld);
        Current = ToWorldInfo(playerState.CurrentWorld);
        WorldChanged?.Invoke();
    }

    private static WorldInfo ToWorldInfo(Lumina.Excel.RowRef<Lumina.Excel.Sheets.World> world) =>
        new(world.RowId, world.Value.Name.ExtractText(), world.Value.DataCenter.Value.Name.ExtractText());

    public void Dispose()
    {
        framework.Update -= OnUpdate;
    }
}
