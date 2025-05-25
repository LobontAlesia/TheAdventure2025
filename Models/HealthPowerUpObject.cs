using Silk.NET.Maths;

namespace TheAdventure.Models;

public class HealthPowerUpObject : TemporaryGameObject
{
    private readonly float _glowSpeed = 2.0f;
    private float _time = 0;
    private float _glowIntensity = 1.0f;

    public HealthPowerUpObject(SpriteSheet spriteSheet, (int X, int Y) position)
        : base(spriteSheet, float.MaxValue, position)
    {
        Tag = "HealthPowerUp";
    }    public override void Render(GameRenderer renderer)
    {
        _time += 0.016f; 
        _glowIntensity = 0.6f + (float)Math.Sin(_time * _glowSpeed) * 0.4f;
        
        renderer.SetDrawColor(0, 0, (byte)(200 * _glowIntensity), (byte)(100 * _glowIntensity));
        DrawCircle(renderer, Position.X, Position.Y, 20);
        
        renderer.SetDrawColor(50, 150, 255, (byte)(200 * _glowIntensity));
        DrawCircle(renderer, Position.X, Position.Y, 12);
    }    private void DrawCircle(GameRenderer renderer, int centerX, int centerY, int radius)
    {
        int x = radius;
        int y = 0;
        int error = 0;

        while (x >= y)
        {
            FillCirclePoints(renderer, centerX, centerY, x, y);
            FillCirclePoints(renderer, centerX, centerY, y, x);
            
            y++;
            error += 1 + 2 * y;
            if (2 * (error - x) + 1 > 0)
            {
                x--;
                error += 1 - 2 * x;
            }
        }
    }

    private void FillCirclePoints(GameRenderer renderer, int centerX, int centerY, int x, int y)
    {
        for (int i = -x; i <= x; i++)
        {
            renderer.FillRect(new Rectangle<int>(centerX + i, centerY + y, 1, 1));
            renderer.FillRect(new Rectangle<int>(centerX + i, centerY - y, 1, 1));
        }
    }
}
