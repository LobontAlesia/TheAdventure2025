using System.Reflection;
using System.Text.Json;
using Silk.NET.Maths;
using TheAdventure.Models;
using TheAdventure.Models.Data;
using TheAdventure.Scripting;

namespace TheAdventure;

public class Engine
{
    private readonly GameRenderer _renderer;
    private readonly Input _input;
    private readonly ScriptEngine _scriptEngine = new();

    private readonly Dictionary<int, GameObject> _gameObjects = new();
    private readonly Dictionary<string, TileSet> _loadedTileSets = new();
    private readonly Dictionary<int, Tile> _tileIdMap = new();

    private Level _currentLevel = new();
    private PlayerObject? _player1; // WASD player
    private PlayerObject? _player2; // Arrow keys player
    
    // Game state tracking
    private bool _isGameOver = false;
    private bool _showRestartMessage = false;
    private DateTimeOffset _gameOverTime = DateTimeOffset.MinValue;
    private const double MESSAGE_BLINK_INTERVAL = 0.8; // seconds

    private DateTimeOffset _lastUpdate = DateTimeOffset.Now;

    public Engine(GameRenderer renderer, Input input)
    {
        _renderer = renderer;
        _input = input;
        _input.OnMouseClick += (_, coords) => AddBomb(coords.x, coords.y);
    }

    public void SetupWorld()
    {
        // Create both players with different starting positions
        _player1 = new PlayerObject(SpriteSheet.Load(_renderer, "Player.json", "Assets"), 100, 100, 1, false); // WASD player
        _player2 = new PlayerObject(SpriteSheet.Load(_renderer, "Player.json", "Assets"), 200, 100, 2, true);  // Arrow keys player

        var levelContent = File.ReadAllText(Path.Combine("Assets", "terrain.tmj"));
        var level = JsonSerializer.Deserialize<Level>(levelContent);
        if (level == null)
        {
            throw new Exception("Failed to load level");
        }

        foreach (var tileSetRef in level.TileSets)
        {
            var tileSetContent = File.ReadAllText(Path.Combine("Assets", tileSetRef.Source));
            var tileSet = JsonSerializer.Deserialize<TileSet>(tileSetContent);
            if (tileSet == null)
            {
                throw new Exception("Failed to load tile set");
            }

            foreach (var tile in tileSet.Tiles)
            {
                tile.TextureId = _renderer.LoadTexture(Path.Combine("Assets", tile.Image), out _);
                _tileIdMap.Add(tile.Id!.Value, tile);
            }

            _loadedTileSets.Add(tileSet.Name, tileSet);
        }

        if (level.Width == null || level.Height == null)
        {
            throw new Exception("Invalid level dimensions");
        }

        if (level.TileWidth == null || level.TileHeight == null)
        {
            throw new Exception("Invalid tile dimensions");
        }

        _renderer.SetWorldBounds(new Rectangle<int>(0, 0, level.Width.Value * level.TileWidth.Value,
            level.Height.Value * level.TileHeight.Value));

        _currentLevel = level;

        _scriptEngine.LoadAll(Path.Combine("Assets", "Scripts"));
    }    private void HandleReviveAttempts()
    {
        if (_player1 == null || _player2 == null)
        {
            return;
        }

        // Ambii jucători folosesc tasta M pentru revival
        if (_input.IsKeyMPressed())
        {
            // Dacă player 1 e viu și player 2 mort, player 1 îl învie pe 2
            if (!_player1.IsDead() && _player2.IsDead() && _player1.CanRevivePlayer(_player2))
            {
                _player1.RevivePlayer(_player2);
            }
            // Dacă player 2 e viu și player 1 mort, player 2 îl învie pe 1
            else if (!_player2.IsDead() && _player1.IsDead() && _player2.CanRevivePlayer(_player1))
            {
                _player2.RevivePlayer(_player1);
            }
        }
    }

    public void ProcessFrame()
    {
        var currentTime = DateTimeOffset.Now;
        var msSinceLastFrame = (currentTime - _lastUpdate).TotalMilliseconds;
        _lastUpdate = currentTime;

        if (_player1 == null || _player2 == null)
        {
            return;
        }
        
        if (_isGameOver && _input.IsKeyRPressed())
        {
            RestartGame();
            return;
        }
        
        _player1.UpdateInvulnerability();
        _player2.UpdateInvulnerability();

        HandleReviveAttempts();

        // Player 1 (WASD)
        double p1Up = _input.IsKeyWPressed() ? 1.0 : 0.0;
        double p1Down = _input.IsKeySPressed() ? 1.0 : 0.0;
        double p1Left = _input.IsKeyAPressed() ? 1.0 : 0.0;
        double p1Right = _input.IsKeyDPressed() ? 1.0 : 0.0;

        // Player 2 (Arrow Keys)
        double p2Up = _input.IsUpPressed() ? 1.0 : 0.0;
        double p2Down = _input.IsDownPressed() ? 1.0 : 0.0;
        double p2Left = _input.IsLeftPressed() ? 1.0 : 0.0;
        double p2Right = _input.IsRightPressed() ? 1.0 : 0.0;

        // Update both players
        _player1.UpdatePosition(p1Up, p1Down, p1Left, p1Right, 48, 48, msSinceLastFrame);
        _player2.UpdatePosition(p2Up, p2Down, p2Left, p2Right, 48, 48, msSinceLastFrame);
        
        _scriptEngine.ExecuteAll(this);

        // Game over if both players are dead
        if (_player1.IsDead() && _player2.IsDead())
        {
            _isGameOver = true;
            _gameOverTime = currentTime;
        }
    }

    public void RenderFrame()
    {
        _renderer.SetDrawColor(0, 0, 0, 255);
        _renderer.ClearScreen();

        // Camera follows midpoint between players
        if (_player1 != null && _player2 != null)
        {
            var midX = (_player1.Position.X + _player2.Position.X) / 2;
            var midY = (_player1.Position.Y + _player2.Position.Y) / 2;
            _renderer.CameraLookAt(midX, midY);
        }

        RenderTerrain();
        RenderAllObjects();

        if (_isGameOver)
        {
            RenderGameOverMessage();
        }

        _renderer.PresentFrame();
    }

    public void AddBomb(int X, int Y, bool translateCoordinates = true)
    {
        var worldCoords = translateCoordinates ? _renderer.ToWorldCoordinates(X, Y) : new Vector2D<int>(X, Y);

        SpriteSheet spriteSheet = SpriteSheet.Load(_renderer, "BombExploding.json", "Assets");
        spriteSheet.ActivateAnimation("Explode");

        TemporaryGameObject bomb = new(spriteSheet, 2.1, (worldCoords.X, worldCoords.Y));
        _gameObjects.Add(bomb.Id, bomb);
    }    public void AddHealthPowerUp(int X, int Y, bool translateCoordinates = true)
    {
        var worldCoords = translateCoordinates ? _renderer.ToWorldCoordinates(X, Y) : new Vector2D<int>(X, Y);        // Folosim un SpriteSheet gol pentru că desenăm direct în Render
        SpriteSheet spriteSheet = SpriteSheet.CreateEmpty(_renderer);
        var powerUp = new HealthPowerUpObject(spriteSheet, (worldCoords.X, worldCoords.Y));
        _gameObjects.Add(powerUp.Id, powerUp);
        Console.WriteLine($"Power-up added at position: {worldCoords.X}, {worldCoords.Y}");
    }

    private void HandlePowerUpCollision(PlayerObject player, TemporaryGameObject powerUp)
    {
        if (powerUp.Tag != "HealthPowerUp")
        {
            return;
        }

        var deltaX = Math.Abs(player.Position.X - powerUp.Position.X);
        var deltaY = Math.Abs(player.Position.Y - powerUp.Position.Y);
        
        if (deltaX < 32 && deltaY < 32)
        {
            player.Heal(25); // Adaugă 25 puncte de viață
            powerUp.ForceExpire(); // Face power-up-ul să dispară
        }
    }    public void RenderAllObjects()
    {
        var toRemove = new List<int>();
        var toCollisionCheck = new List<TemporaryGameObject>();
        
        // Mai întâi renderăm toate obiectele și le adăugăm pentru verificări
        foreach (var gameObject in GetRenderables())
        {
            gameObject.Render(_renderer);
            if (gameObject is TemporaryGameObject tempObject)
            {
                if (tempObject.IsExpired)
                {
                    toRemove.Add(tempObject.Id);
                }
                else
                {
                    toCollisionCheck.Add(tempObject);
                }
            }
        }

        // Check collisions with both players
        foreach (var obj in toCollisionCheck)
        {
            if (_player1 != null)
            {
                var deltaX1 = Math.Abs(_player1.Position.X - obj.Position.X);
                var deltaY1 = Math.Abs(_player1.Position.Y - obj.Position.Y);
                if (deltaX1 < 32 && deltaY1 < 32)
                {
                    if (obj.Tag == "HealthPowerUp")
                    {
                        _player1.Heal(25);
                        obj.ForceExpire();
                        toRemove.Add(obj.Id);
                    }
                    else
                    {
                        _player1.TakeDamage(25);
                    }
                }
            }

            if (_player2 != null)
            {
                var deltaX2 = Math.Abs(_player2.Position.X - obj.Position.X);
                var deltaY2 = Math.Abs(_player2.Position.Y - obj.Position.Y);
                if (deltaX2 < 32 && deltaY2 < 32)
                {
                    if (obj.Tag == "HealthPowerUp")
                    {
                        _player2.Heal(25);
                        obj.ForceExpire();
                        toRemove.Add(obj.Id);
                    }
                    else
                    {
                        _player2.TakeDamage(25);
                    }
                }
            }
        }

        // Remove expired objects after collision checks
        foreach (var id in toRemove)
        {
            _gameObjects.Remove(id);
        }

        // Render both players
        _player1?.Render(_renderer);
        _player2?.Render(_renderer);    }

    public void RenderTerrain()
    {
        foreach (var currentLayer in _currentLevel.Layers)
        {
            for (int i = 0; i < _currentLevel.Width; ++i)
            {
                for (int j = 0; j < _currentLevel.Height; ++j)
                {
                    int? dataIndex = j * currentLayer.Width + i;
                    if (dataIndex == null)
                    {
                        continue;
                    }

                    var currentTileId = currentLayer.Data[dataIndex.Value] - 1;
                    if (currentTileId == null)
                    {
                        continue;
                    }

                    var currentTile = _tileIdMap[currentTileId.Value];

                    var tileWidth = currentTile.ImageWidth ?? 0;
                    var tileHeight = currentTile.ImageHeight ?? 0;

                    var sourceRect = new Rectangle<int>(0, 0, tileWidth, tileHeight);
                    var destRect = new Rectangle<int>(i * tileWidth, j * tileHeight, tileWidth, tileHeight);
                    _renderer.RenderTexture(currentTile.TextureId, sourceRect, destRect);
                }
            }
        }
    }

    public IEnumerable<RenderableGameObject> GetRenderables()
    {
        foreach (var gameObject in _gameObjects.Values)
        {
            if (gameObject is RenderableGameObject renderableGameObject)
            {
                yield return renderableGameObject;
            }
        }
    }

    public (int X, int Y) GetPlayerPosition()
    {
        return _player1!.Position;
    }

    private void RenderGameOverMessage()
    {
        double elapsedSeconds = (_lastUpdate - _gameOverTime).TotalSeconds;
        if (elapsedSeconds < 0)
        {
            return;
        }

        _renderer.SetDrawColor(0, 0, 0, 200);
        var overlayRect = new Rectangle<int>(0, 0, _renderer.GetWidth(), _renderer.GetHeight());
        _renderer.FillRect(overlayRect);

        bool showMessage = (_showRestartMessage = elapsedSeconds % (MESSAGE_BLINK_INTERVAL * 2) < MESSAGE_BLINK_INTERVAL);

        if (showMessage)
        {
            string gameOverMsg = "GAME OVER";
            Vector2D<int> gameOverSize = _renderer.MeasureText(gameOverMsg, "");
            int gameOverX = (_renderer.GetWidth() - gameOverSize.X) / 2;
            int gameOverY = (_renderer.GetHeight() - gameOverSize.Y) / 2 - 30;
            _renderer.DrawText(gameOverMsg, gameOverX, gameOverY, "", 255, 0, 0, 255);

            string restartMsg = "Press R to Restart";
            Vector2D<int> restartSize = _renderer.MeasureText(restartMsg, "");
            int restartX = (_renderer.GetWidth() - restartSize.X) / 2;
            int restartY = (_renderer.GetHeight() - restartSize.Y) / 2 + 30;
            _renderer.DrawText(restartMsg, restartX, restartY, "", 255, 255, 255, 255);
        }
    }private void RestartGame()
    {
        _scriptEngine.UnloadAll();
        
        _gameObjects.Clear();
        _tileIdMap.Clear();
        _loadedTileSets.Clear();
        _isGameOver = false;
        _showRestartMessage = false;
        _gameOverTime = DateTimeOffset.MinValue;
        _lastUpdate = DateTimeOffset.Now;

        SetupWorld();
    }
}