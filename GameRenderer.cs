using Silk.NET.Maths;
using Silk.NET.SDL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TheAdventure.Models;
using Point = Silk.NET.SDL.Point;

namespace TheAdventure;

public unsafe class GameRenderer
{
    private Sdl _sdl;
    private Renderer* _renderer;
    private GameWindow _window;
    private Camera _camera;

    private Dictionary<int, IntPtr> _texturePointers = new();
    private Dictionary<int, TextureData> _textureData = new();
    private int _textureId;

    public GameRenderer(Sdl sdl, GameWindow window)
    {
        _sdl = sdl;
        
        _renderer = (Renderer*)window.CreateRenderer();
        _sdl.SetRenderDrawBlendMode(_renderer, BlendMode.Blend);
        
        _window = window;
        var windowSize = window.Size;
        _camera = new Camera(windowSize.Width, windowSize.Height);
    }

    public void SetWorldBounds(Rectangle<int> bounds)
    {
        _camera.SetWorldBounds(bounds);
    }

    public void CameraLookAt(int x, int y)
    {
        _camera.LookAt(x, y);
    }

    public int LoadTexture(string fileName, out TextureData textureInfo)
    {
        using (var fStream = new FileStream(fileName, FileMode.Open))
        {
            var image = Image.Load<Rgba32>(fStream);
            textureInfo = new TextureData()
            {
                Width = image.Width,
                Height = image.Height
            };
            var imageRAWData = new byte[textureInfo.Width * textureInfo.Height * 4];
            image.CopyPixelDataTo(imageRAWData.AsSpan());
            fixed (byte* data = imageRAWData)
            {
                var imageSurface = _sdl.CreateRGBSurfaceWithFormatFrom(data, textureInfo.Width,
                    textureInfo.Height, 8, textureInfo.Width * 4, (uint)PixelFormatEnum.Rgba32);
                if (imageSurface == null)
                {
                    throw new Exception("Failed to create surface from image data.");
                }
                
                var imageTexture = _sdl.CreateTextureFromSurface(_renderer, imageSurface);
                if (imageTexture == null)
                {
                    _sdl.FreeSurface(imageSurface);
                    throw new Exception("Failed to create texture from surface.");
                }
                
                _sdl.FreeSurface(imageSurface);
                
                _textureData[_textureId] = textureInfo;
                _texturePointers[_textureId] = (IntPtr)imageTexture;
            }
        }

        return _textureId++;
    }

    public void RenderTexture(int textureId, Rectangle<int> src, Rectangle<int> dst,
        RendererFlip flip = RendererFlip.None, double angle = 0.0, Point center = default)
    {
        if (_texturePointers.TryGetValue(textureId, out var imageTexture))
        {
            var translatedDst = _camera.ToScreenCoordinates(dst);
            _sdl.RenderCopyEx(_renderer, (Texture*)imageTexture, in src,
                in translatedDst,
                angle,
                in center, flip);
        }
    }

    public Vector2D<int> ToWorldCoordinates(int x, int y)
    {
        return _camera.ToWorldCoordinates(new Vector2D<int>(x, y));
    }

    public void SetDrawColor(byte r, byte g, byte b, byte a)
    {
        _sdl.SetRenderDrawColor(_renderer, r, g, b, a);
    }

    public void ClearScreen()
    {
        _sdl.RenderClear(_renderer);
    }

    public void PresentFrame()
    {
        _sdl.RenderPresent(_renderer);
    }

    public int GetWidth()
    {
        return _window.Size.Width;
    }
    
    public int GetHeight()
    {
        return _window.Size.Height;
    }    public Vector2D<int> MeasureText(string text, string fontPath)
    {
        int spacing = 20;
        return new Vector2D<int>(text.Length * spacing, 32);
    }    public void DrawText(string text, int x, int y, string fontPath, byte r, byte g, byte b, byte a)
    {
        SetDrawColor(r, g, b, a);
        int charWidth = 16;
        int charHeight = 32;
        int spacing = 20;
        int currentX = x;
        
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == ' ')
            {
                currentX += spacing;
                continue;
            }
            
            switch (char.ToUpper(text[i]))
            {                case 'O':
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth/4, y, currentX + charWidth*3/4, y);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth*3/4, y, currentX + charWidth, y + charHeight/4);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth, y + charHeight/4, currentX + charWidth, y + charHeight*3/4);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth, y + charHeight*3/4, currentX + charWidth*3/4, y + charHeight);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth*3/4, y + charHeight, currentX + charWidth/4, y + charHeight);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth/4, y + charHeight, currentX, y + charHeight*3/4);
                    _sdl.RenderDrawLine(_renderer, currentX, y + charHeight*3/4, currentX, y + charHeight/4);
                    _sdl.RenderDrawLine(_renderer, currentX, y + charHeight/4, currentX + charWidth/4, y);
                    break;

                case 'G':
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth/4, y, currentX + charWidth*3/4, y);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth*3/4, y, currentX + charWidth, y + charHeight/4);
                    _sdl.RenderDrawLine(_renderer, currentX, y + charHeight/4, currentX, y + charHeight*3/4);
                    _sdl.RenderDrawLine(_renderer, currentX, y + charHeight*3/4, currentX + charWidth/4, y + charHeight);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth/4, y + charHeight, currentX + charWidth*3/4, y + charHeight);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth, y + charHeight/2, currentX + charWidth, y + charHeight);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth/2, y + charHeight/2, currentX + charWidth, y + charHeight/2);
                    break;                case 'V':
                    _sdl.RenderDrawLine(_renderer, currentX, y, currentX + charWidth/4, y + charHeight/2);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth/4, y + charHeight/2, currentX + charWidth/2, y + charHeight);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth, y, currentX + charWidth*3/4, y + charHeight/2);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth*3/4, y + charHeight/2, currentX + charWidth/2, y + charHeight);
                    break;

                case 'P':
                    _sdl.RenderDrawLine(_renderer, currentX, y, currentX, y + charHeight);
                    _sdl.RenderDrawLine(_renderer, currentX, y, currentX + charWidth, y);
                    _sdl.RenderDrawLine(_renderer, currentX, y + charHeight/2, currentX + charWidth, y + charHeight/2);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth, y, currentX + charWidth, y + charHeight/2);
                    break;
                      case 'S':
                    _sdl.RenderDrawLine(_renderer, currentX, y, currentX + charWidth, y);
                    _sdl.RenderDrawLine(_renderer, currentX, y + charHeight/2, currentX + charWidth, y + charHeight/2);
                    _sdl.RenderDrawLine(_renderer, currentX, y + charHeight, currentX + charWidth, y + charHeight);
                    _sdl.RenderDrawLine(_renderer, currentX, y, currentX, y + charHeight/2);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth, y + charHeight/2, currentX + charWidth, y + charHeight);
                    break;
                    
                case 'T':
                    _sdl.RenderDrawLine(_renderer, currentX, y, currentX + charWidth, y);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth/2, y, currentX + charWidth/2, y + charHeight);
                    break;
                    
                case 'R':
                    _sdl.RenderDrawLine(_renderer, currentX, y, currentX, y + charHeight);
                    _sdl.RenderDrawLine(_renderer, currentX, y, currentX + charWidth, y);
                    _sdl.RenderDrawLine(_renderer, currentX, y + charHeight/2, currentX + charWidth, y + charHeight/2);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth, y, currentX + charWidth, y + charHeight/2);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth/2, y + charHeight/2, currentX + charWidth, y + charHeight);
                    break;
                      case 'E':
                    _sdl.RenderDrawLine(_renderer, currentX, y, currentX, y + charHeight);
                    _sdl.RenderDrawLine(_renderer, currentX, y, currentX + charWidth, y);
                    _sdl.RenderDrawLine(_renderer, currentX, y + charHeight/2, currentX + charWidth, y + charHeight/2);
                    _sdl.RenderDrawLine(_renderer, currentX, y + charHeight, currentX + charWidth, y + charHeight);
                    break;
                    
                case 'A':
                    _sdl.RenderDrawLine(_renderer, currentX, y + charHeight, currentX + charWidth/2, y);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth/2, y, currentX + charWidth, y + charHeight);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth/4, y + charHeight/2, currentX + charWidth*3/4, y + charHeight/2);
                    break;
                    
                case 'M':
                    _sdl.RenderDrawLine(_renderer, currentX, y + charHeight, currentX, y);
                    _sdl.RenderDrawLine(_renderer, currentX, y, currentX + charWidth/2, y + charHeight/2);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth/2, y + charHeight/2, currentX + charWidth, y);
                    _sdl.RenderDrawLine(_renderer, currentX + charWidth, y, currentX + charWidth, y + charHeight);
                    break;
                      default:
                    var rect = new Rectangle<int>(currentX, y, charWidth, charHeight);
                    _sdl.RenderDrawRect(_renderer, &rect);
                    break;
            }
            
            currentX += spacing;
        }
    }

    public void FillRect(Rectangle<int> rect)
    {
        var translatedRect = _camera.ToScreenCoordinates(rect);
        _sdl.RenderFillRect(_renderer, &translatedRect);
    }
    
    public void DrawRect(Rectangle<int> rect)
    {
        var translatedRect = _camera.ToScreenCoordinates(rect);
        _sdl.RenderDrawRect(_renderer, &translatedRect);
    }
}
