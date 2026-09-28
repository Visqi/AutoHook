using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AutoHook.World.UpdateModules;

public sealed class PartyUpdateModule : IWorldUpdateModule {
    private readonly List<ulong> _partyScratch = [];

    public void Update(WorldState ws) {
        UpdateTerritory(ws);
        UpdatePartyAndInstance(ws);
        UpdateWeather(ws);
    }

    private static void UpdateTerritory(WorldState ws) {
        var territory = IClientState.Get().TerritoryType;
        if (ws.TerritoryId != territory)
            ws.Execute(new WorldState.OpTerritory(territory));
    }

    private unsafe void UpdatePartyAndInstance(WorldState ws) {
        var inInstance = EventFramework.Instance()->GetInstanceContentDirector() != null;
        if (inInstance != ws.Party.InInstanceContent) {
            // snapshot who we queued with when we enter an instance, clear it when we leave
            ws.Execute(new PartyState.OpQueuedWith(inInstance ? ws.Party.ContentIds : []));
            ws.Execute(new PartyState.OpInInstanceContent(inInstance));
        }

        _partyScratch.Clear();
        var group = GroupManager.Instance()->MainGroup;
        for (var i = 0; i < group.MemberCount; i++) {
            var member = group.GetPartyMemberByIndex(i);
            if (member != null)
                _partyScratch.Add(member->ContentId);
        }

        if (PartyContentIdsEqual(ws.Party.ContentIds, _partyScratch))
            return;

        ws.Execute(new PartyState.OpMembers([.. _partyScratch]));
    }

    private static bool PartyContentIdsEqual(IReadOnlyList<ulong> current, List<ulong> next) {
        if (current.Count != next.Count)
            return false;
        for (var i = 0; i < next.Count; i++) {
            if (current[i] != next[i])
                return false;
        }
        return true;
    }

    private static unsafe void UpdateWeather(WorldState ws) {
        var territory = TerritoryType.GetRow(ws.TerritoryId);
        var currentModified = WeatherManager.Instance()->GetCurrentWeather();
        (var current, var previous, var next) = (territory.GetCurrentWeather().RowId, territory.GetPreviousWeather().RowId, territory.GetNextWeather().RowId);

        if (ws.CurrentModifiedWeatherId == currentModified && ws.CurrentWeatherId == current && ws.PreviousWeatherId == previous && ws.NextWeatherId == next)
            return;

        ws.Execute(new WorldState.OpWeather(currentModified, current, previous, next));
    }
}
