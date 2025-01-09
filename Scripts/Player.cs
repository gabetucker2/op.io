using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using op.io.Scripts;

namespace op_io
{
    public class Player
    {
        private GameObject _gameObject;
        private Vector2 _position;
        private float _speed;
        private int _radius;
        private Color _color;
        private Texture2D _texture;
        private int _viewportWidth;
        private int _viewportHeight;

        public Player(GameObject gameObject, float x, float y, int radius, float speed, Color color, int viewportWidth, int viewportHeight)
        {
            _gameObject = gameObject;
            _position = new Vector2(x, y);
            _radius = radius;
            _speed = speed;
            _color = color;
            _viewportWidth = viewportWidth;
            _viewportHeight = viewportHeight;
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            _texture = new Texture2D(graphicsDevice, _radius * 2, _radius * 2);
            Color[] data = new Color[_radius * _radius * 4];
            for (int y = 0; y < _radius * 2; y++)
            {
                for (int x = 0; x < _radius * 2; x++)
                {
                    int dx = x - _radius;
                    int dy = y - _radius;
                    if (dx * dx + dy * dy <= _radius * _radius)
                        data[y * _radius * 2 + x] = _color;
                    else
                        data[y * _radius * 2 + x] = Color.Transparent;
                }
            }
            _texture.SetData(data);
        }

        public void Update(float deltaTime)
        {
            // Move GameObject
            ActionHandler.Move(_gameObject, InputManager.MoveVector(), _speed, deltaTime);

            // Sync Player position with GameObject position TODO: move to GameObject functionality
            _position = _gameObject.Position;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(_texture, _position - new Vector2(_radius), Color.White);
        }

        public Vector2 Position => _position;
        public int Radius => _radius;
    }
}
