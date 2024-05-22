﻿using System.Numerics;

namespace SpinBladeArena.LogicCenter.AI;

public abstract class AIPlayer(int userId, string userName, Vector2 position) : Player(userId, userName, position)
{
    static int _nextAIUserId = -1;

    public abstract float ReactionTimeMS { get; }

    public abstract AIPreference Preference { get; }

    private float _accumulatedTime = 0;

    public override AddPlayerRequest CreateRespawnRequest() => new AddAIPlayerRequest(Preference, UserId, UserName);

    public static AIPlayer CreateRandom(Vector2 position, HashSet<string> knownNames)
    {
        AIPreference preference = (AIPreference)Random.Shared.Next(_names.Count);
        string userName = RandomName(preference, knownNames);
        int userId = _nextAIUserId--;
        return Create(position, preference, userName, userId);
    }

    private static AIPlayer Create(Vector2 position, AIPreference preference, string userName, int userId)
    {
        return preference switch
        {
            AIPreference.Peaceful => new PeacefulAIPlayer(userId, userName, position),
            AIPreference.Aggressive => new AggressiveAIPlayer(userId, userName, position),
            AIPreference.Defensive => new DefensiveAIPlayer(userId, userName, position),
            _ => throw new NotImplementedException()
        };
    }

    public static AIPlayer CreateRespawn(AddAIPlayerRequest req, Vector2 position)
    {
        return Create(position, req.AIPreference, req.UserName, req.UserId);
    }

    private static string RandomName(AIPreference preference, HashSet<string> knownNames)
    {
        var nameBatch = _names[preference];
        string name;
        do
        {
            name = nameBatch[Random.Shared.Next(nameBatch.Length)];
        } while (knownNames.Contains(name));

        return name;
    }

    private static readonly Dictionary<AIPreference, string[]> _names = new()
    {
        [AIPreference.Peaceful] = ["小乔", "刘禅", "袁绍", "刘表", "貂蝉", "刘备", "糜竺", "鲁肃", "陶谦", "孙尚香"],
        [AIPreference.Aggressive] = ["吕布", "曹操", "张飞", "马超", "孟获", "邢道荣", "许褚", "张郃", "魏延", "关羽"],
        [AIPreference.Defensive] = ["孙权", "赵云", "诸葛亮", "曹仁", "司马懿", "周瑜", "陆逊", "姜维", "邓艾", "钟会"]
    };

    public void Think(float deltaTime, Lobby lobby)
    {
        _accumulatedTime += deltaTime;
        if (_accumulatedTime < ReactionTimeMS) return;
        _accumulatedTime = 0;

        // common: if no weapon, goes for random bonus
        CloseastThings things = GetCloseastThings(lobby);
        if (Weapon.Count == 0 && things.Bonuses.Length != 0)
        {
            Position = things.Bonuses.First().Bonus.Position;
            return;
        }

        Think(lobby, things);
    }

    protected abstract void Think(Lobby lobby, CloseastThings things);

    protected CloseastThings GetCloseastThings(Lobby lobby, int maxCount = 4)
    {
        PlayerDistance[] closestPlayers = lobby.Players
            .Where(p => p.UserId != UserId)
            .Select(p => new PlayerDistance(p, Vector2.Distance(Position, p.Position) - Size - p.SafeDistance))
            .OrderBy(p => p.Distance)
            .Take(maxCount)
            .ToArray();
        BonusDistance[] closestBonuses = lobby.PickableBonuses
            .Select(b => new BonusDistance(b, Vector2.Distance(Position, b.Position) - Size))
            .OrderBy(b => b.Distance)
            .Take(maxCount)
            .ToArray();
        return new CloseastThings(closestPlayers, closestBonuses);
    }

    protected void RunAwayFromDanger(Player danger)
    {
        Destination = Position + Vector2.Normalize(Position - danger.Position) * MovementSpeedPerSecond * ReactionTimeMS * 2;
    }

    protected PlayerDistance? FindCloseastDanger(CloseastThings closestThings)
    {
        return closestThings.Players.FirstOrDefault(p => p.Player.IsDangerousToPlayer(this, ReactionTimeMS));
    }

    protected PlayerDistance? FindApprochingDanger(CloseastThings closestThings)
    {
        return closestThings.Players.FirstOrDefault(p => p.Distance < MovementSpeedPerSecond);
    }
}