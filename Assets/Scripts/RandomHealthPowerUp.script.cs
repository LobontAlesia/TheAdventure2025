using System;
using TheAdventure;
using TheAdventure.Models;
using TheAdventure.Scripting;

public class RandomHealthPowerUp : IScript
{    
    private DateTimeOffset _nextSpawnTime = DateTimeOffset.MinValue;    
    private readonly Random _random = new();
    private const double SPAWN_INTERVAL = 15.0; //la fiecare 15 sec
    private const int MAX_POWERUPS = 3; //max 3 power-up-uri simultan

    public void Initialize()
    {
        _nextSpawnTime = DateTimeOffset.Now.AddSeconds(SPAWN_INTERVAL);
    }

    public void Execute(Engine engine)
    {
        if (DateTimeOffset.Now < _nextSpawnTime)
        {
            return;
        }

        int activePowerUps = 0;
        foreach (var obj in engine.GetRenderables())
        {
            if (obj is TemporaryGameObject temp && temp.Tag == "HealthPowerUp")
            {
                activePowerUps++;
            }
        }

        if (activePowerUps < MAX_POWERUPS)
        {
            _nextSpawnTime = DateTimeOffset.Now.AddSeconds(SPAWN_INTERVAL);
            int x = _random.Next(50, 400);
            int y = _random.Next(50, 400);            engine.AddHealthPowerUp(x, y, false);
        }
    }
}
