using Silk.NET.Maths;

namespace TheAdventure.Models;

public class PlayerObject : RenderableGameObject
{
    private const int _speed = 128;

    public int MaxHealth { get; private set; } = 100;
    public int CurrentHealth { get; private set; }
    public bool IsInvulnerable { get; private set; } = false;
    private DateTimeOffset _invulnerabilityEndTime = DateTimeOffset.MinValue;
    private bool _isFlashing = false;
    private DateTimeOffset _lastFlashTime = DateTimeOffset.MinValue;
    private const double INVULNERABILITY_DURATION = 1.5;
    private const double FLASH_INTERVAL = 0.15; 

    public enum PlayerStateDirection
    {
        None = 0,
        Down,
        Up,
        Left,
        Right,
    }

    public enum PlayerState
    {
        None = 0,
        Idle,
        Move,
        Attack,
        GameOver
    }

    public (PlayerState State, PlayerStateDirection Direction) State { get; private set; }

    public PlayerObject(SpriteSheet spriteSheet, int x, int y) : base(spriteSheet, (x, y))
    {
        CurrentHealth = MaxHealth;
        SetState(PlayerState.Idle, PlayerStateDirection.Down);
    }

    public void SetState(PlayerState state)
    {
        SetState(state, State.Direction);
    }

    public void SetState(PlayerState state, PlayerStateDirection direction)
    {
        if (State.State == PlayerState.GameOver)
        {
            return;
        }

        if (State.State == state && State.Direction == direction)
        {
            return;
        }

        if (state == PlayerState.None && direction == PlayerStateDirection.None)
        {
            SpriteSheet.ActivateAnimation(null);
        }

        else if (state == PlayerState.GameOver)
        {
            SpriteSheet.ActivateAnimation(Enum.GetName(state));
        }
        else
        {
            var animationName = Enum.GetName(state) + Enum.GetName(direction);
            SpriteSheet.ActivateAnimation(animationName);
        }

        State = (state, direction);
    }

    public void GameOver()
    {
        SetState(PlayerState.GameOver, PlayerStateDirection.None);
    }

    public void TakeDamage(int amount)
    {
        if (IsInvulnerable || State.State == PlayerState.GameOver)
        {
            return;
        }

        CurrentHealth = Math.Max(0, CurrentHealth - amount);

        IsInvulnerable = true;
        _invulnerabilityEndTime = DateTimeOffset.Now.AddSeconds(INVULNERABILITY_DURATION);
        _lastFlashTime = DateTimeOffset.Now;
        _isFlashing = true;

        if (CurrentHealth <= 0)
        {
            GameOver();
        }
    }

    public void UpdateInvulnerability()
    {
        if (!IsInvulnerable)
        {
            return;
        }

        var now = DateTimeOffset.Now;

        if ((now - _lastFlashTime).TotalSeconds >= FLASH_INTERVAL)
        {
            _isFlashing = !_isFlashing;
            _lastFlashTime = now;
        }

        if (now >= _invulnerabilityEndTime)
        {
            IsInvulnerable = false;
            _isFlashing = false;
        }
    }

    public void Attack()
    {
        if (State.State == PlayerState.GameOver)
        {
            return;
        }

        var direction = State.Direction;
        SetState(PlayerState.Attack, direction);
    }

    public void UpdatePosition(double up, double down, double left, double right, int width, int height, double time)
    {
        if (State.State == PlayerState.GameOver)
        {
            return;
        }

        var pixelsToMove = _speed * (time / 1000.0);

        var x = Position.X + (int)(right * pixelsToMove);
        x -= (int)(left * pixelsToMove);

        var y = Position.Y + (int)(down * pixelsToMove);
        y -= (int)(up * pixelsToMove);

        var newState = State.State;
        var newDirection = State.Direction;

        if (x == Position.X && y == Position.Y)
        {
            if (State.State == PlayerState.Attack)
            {
                if (SpriteSheet.AnimationFinished)
                {
                    newState = PlayerState.Idle;
                }
            }
            else
            {
                newState = PlayerState.Idle;
            }
        }
        else
        {
            newState = PlayerState.Move;

            if (y < Position.Y && newDirection != PlayerStateDirection.Up)
            {
                newDirection = PlayerStateDirection.Up;
            }

            if (y > Position.Y && newDirection != PlayerStateDirection.Down)
            {
                newDirection = PlayerStateDirection.Down;
            }

            if (x < Position.X && newDirection != PlayerStateDirection.Left)
            {
                newDirection = PlayerStateDirection.Left;
            }

            if (x > Position.X && newDirection != PlayerStateDirection.Right)
            {
                newDirection = PlayerStateDirection.Right;
            }
        }

        if (newState != State.State || newDirection != State.Direction)
        {
            SetState(newState, newDirection);
        }

        Position = (x, y);
    }

    public bool IsDead()
    {
        return State.State == PlayerState.GameOver || CurrentHealth <= 0;
    }

    public override void Render(GameRenderer renderer)
    {
        if (IsInvulnerable && !_isFlashing)
        {
            return;
        }

        base.Render(renderer);

        RenderHealthBar(renderer);
    }

    private void RenderHealthBar(GameRenderer renderer)
    {
        int barWidth = 48; 
        int barHeight = 6;
        int barYOffset = -15; 

        renderer.SetDrawColor(255, 0, 0, 255);
        var bgRect = new Rectangle<int>(
            Position.X - barWidth / 2,
            Position.Y - barHeight / 2 + barYOffset,
            barWidth,
            barHeight
        );
        renderer.FillRect(bgRect);

        renderer.SetDrawColor(0, 255, 0, 255);
        var healthWidth = (int)((float)CurrentHealth / MaxHealth * barWidth);
        var healthRect = new Rectangle<int>(
            Position.X - barWidth / 2,
            Position.Y - barHeight / 2 + barYOffset,
            healthWidth,
            barHeight
        );
        renderer.FillRect(healthRect);

        renderer.SetDrawColor(255, 255, 255, 255);
        renderer.DrawRect(bgRect);
    }
}