using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class ChatBlock
    {
        public const string BlockTitle = "Chat";
        public const int MinWidth = 200;
        public const int MinHeight = 120;

        private enum ChatChannel { All, Party, Proximity, Whisper }

        private sealed class ChatMessage
        {
            public ChatMessage(string sender, string text, ChatChannel channel, bool isPlayer)
            {
                Sender = sender;
                Text = text;
                Channel = channel;
                IsPlayer = isPlayer;
            }

            public string Sender { get; }
            public string Text { get; }
            public ChatChannel Channel { get; }
            public bool IsPlayer { get; }
        }

        private const int MaxMessages = 500;
        private const int InputBarHeight = 32;
        private const int ChannelBadgeMinWidth = 32;
        private const int ChannelBadgePadX = 14;
        private const int TextPadding = 6;
        private const double CursorBlinkInterval = 0.5d;
        private const string InputFocusOwner = "ChatBlock.Input";
        private const string PlayerName = "You";

        private static readonly Color ColorAll       = new Color(220, 220, 225);
        private static readonly Color ColorParty     = new Color(100, 210, 120);
        private static readonly Color ColorProximity = new Color(240, 210, 80);
        private static readonly Color ColorWhisper   = new Color(200, 120, 220);
        private static readonly Color InputBarBg     = new Color(22, 22, 26);
        private static readonly Color InputFieldBg   = new Color(35, 35, 40);

        private static readonly List<ChatMessage> _messages = new();
        private static readonly StringBuilder _inputBuffer = new();
        private static readonly KeyRepeatTracker _inputRepeater = new();
        private static readonly BlockScrollPanel _scrollPanel = new();

        private static ChatChannel _activeChannel = ChatChannel.All;
        private static bool _inputFocused;
        private static double _cursorBlinkTimer;
        private static bool _cursorVisible = true;
        private static KeyboardState _prevKeyboardState;
        private static Texture2D _pixel;

        private static Color ChannelColor(ChatChannel ch) => ch switch
        {
            ChatChannel.Party     => ColorParty,
            ChatChannel.Proximity => ColorProximity,
            ChatChannel.Whisper   => ColorWhisper,
            _                     => ColorAll
        };

        private static string ChannelLabel(ChatChannel ch) => ch switch
        {
            ChatChannel.Party     => "Party",
            ChatChannel.Proximity => "Near",
            ChatChannel.Whisper   => "Whisper",
            _                     => "All"
        };

        private static int GetBadgeWidth()
        {
            UIStyle.UIFont font = UIStyle.FontBody;
            if (!font.IsAvailable)
                return ChannelBadgeMinWidth;
            float textW = font.MeasureString(ChannelLabel(_activeChannel)).X;
            return Math.Max(ChannelBadgeMinWidth, (int)MathF.Ceiling(textW) + ChannelBadgePadX);
        }

        private static Rectangle GetBadgeBounds(Rectangle inputBar)
        {
            int badgeW = GetBadgeWidth();
            return new Rectangle(inputBar.X + TextPadding, inputBar.Y + (inputBar.Height - 20) / 2, badgeW, 20);
        }

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
                return;

            double elapsed = Math.Max(gameTime?.ElapsedGameTime.TotalSeconds ?? 0d, 0d);
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Chat);

            (Rectangle displayArea, Rectangle inputBar) = BuildLayout(contentBounds);

            bool leftClickStarted = mouseState.LeftButton == ButtonState.Pressed &&
                                    previousMouseState.LeftButton == ButtonState.Released;

            // Focus on click in input bar; click the channel badge to cycle channel
            if (!blockLocked && leftClickStarted)
            {
                Rectangle badgeBounds = GetBadgeBounds(inputBar);
                if (badgeBounds.Contains(mouseState.Position))
                {
                    CycleChannel();
                    _inputFocused = true;
                    BlockManager.TryFocusBlock(DockBlockKind.Chat);
                }
                else if (inputBar.Contains(mouseState.Position))
                {
                    _inputFocused = true;
                    BlockManager.TryFocusBlock(DockBlockKind.Chat);
                }
                else if (!displayArea.Contains(mouseState.Position))
                {
                    _inputFocused = false;
                }
            }

            if (blockLocked)
                _inputFocused = false;

            // Scroll
            UIStyle.UIFont font = UIStyle.FontBody;
            float lineHeight = font.IsAvailable ? font.LineHeight : 14f;
            float contentHeight = _messages.Count * lineHeight + TextPadding * 2;
            _scrollPanel.Update(displayArea, contentHeight, blockLocked ? previousMouseState : mouseState, previousMouseState);
            _scrollPanel.ScrollToMax();

            // Keyboard input when focused
            if (_inputFocused && !blockLocked)
            {
                KeyboardState ks = Keyboard.GetState();
                bool shift = ks.IsKeyDown(Keys.LeftShift) || ks.IsKeyDown(Keys.RightShift);

                foreach (Keys key in _inputRepeater.GetKeysWithRepeat(ks, _prevKeyboardState, elapsed))
                {
                    bool newPress = !_prevKeyboardState.IsKeyDown(key);

                    switch (key)
                    {
                        case Keys.Tab when newPress:
                            CycleChannel();
                            break;
                        case Keys.Enter when newPress:
                            SendMessage();
                            break;
                        case Keys.Back:
                            if (_inputBuffer.Length > 0)
                                _inputBuffer.Remove(_inputBuffer.Length - 1, 1);
                            break;
                        case Keys.Escape when newPress:
                            _inputFocused = false;
                            break;
                        default:
                            if (TryConvertKey(key, shift, out char ch))
                                _inputBuffer.Append(ch);
                            break;
                    }
                }

                _prevKeyboardState = ks;
            }
            else
            {
                KeyboardState ks = Keyboard.GetState();
                // When the chat block has focus but the input bar was not clicked directly,
                // pressing Enter auto-focuses the input so the user can start typing.
                if (!blockLocked && BlockManager.BlockHasFocus(DockBlockKind.Chat))
                {
                    if (ks.IsKeyDown(Keys.Enter) && !_prevKeyboardState.IsKeyDown(Keys.Enter))
                    {
                        _inputFocused = true;
                        BlockManager.TryFocusBlock(DockBlockKind.Chat);
                    }
                }
                _prevKeyboardState = ks;
                _inputRepeater.Reset();
            }

            // Cursor blink
            if (_inputFocused)
            {
                _cursorBlinkTimer += elapsed;
                if (_cursorBlinkTimer >= CursorBlinkInterval)
                {
                    _cursorBlinkTimer -= CursorBlinkInterval;
                    _cursorVisible = !_cursorVisible;
                }
            }
            else
            {
                _cursorVisible = false;
                _cursorBlinkTimer = 0;
            }
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null || contentBounds.Width <= 0 || contentBounds.Height <= 0)
                return;

            EnsurePixel(spriteBatch.GraphicsDevice);
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Chat);
            (Rectangle displayArea, Rectangle inputBar) = BuildLayout(contentBounds);
            UIStyle.UIFont font = UIStyle.FontBody;

            DrawMessages(spriteBatch, displayArea, font);
            _scrollPanel.Draw(spriteBatch, blockLocked);
            DrawInputBar(spriteBatch, inputBar, font);
        }

        private static void DrawMessages(SpriteBatch spriteBatch, Rectangle displayArea, UIStyle.UIFont font)
        {
            if (!font.IsAvailable)
                return;

            float lineHeight = font.LineHeight;
            float y = displayArea.Y + TextPadding - _scrollPanel.ScrollOffset;
            int startIndex = Math.Max(0, (int)((_scrollPanel.ScrollOffset - TextPadding) / Math.Max(1f, lineHeight)));

            if (_messages.Count == 0)
            {
                font.DrawString(spriteBatch, "No messages yet.", new Vector2(displayArea.X + TextPadding, displayArea.Y + TextPadding), UIStyle.MutedTextColor);
                return;
            }

            for (int i = startIndex; i < _messages.Count; i++)
            {
                float lineY = y + i * lineHeight;
                if (lineY > displayArea.Bottom) break;
                if (lineY + lineHeight < displayArea.Y) continue;

                ChatMessage msg = _messages[i];
                Color col = ChannelColor(msg.Channel);

                string channelTag = $"[{ChannelLabel(msg.Channel)}] ";
                string senderPart = $"{msg.Sender}: ";
                string full = channelTag + senderPart + msg.Text;

                Vector2 pos = new(displayArea.X + TextPadding, lineY);
                font.DrawString(spriteBatch, full, pos, col);
            }
        }

        private static void DrawInputBar(SpriteBatch spriteBatch, Rectangle inputBar, UIStyle.UIFont font)
        {
            if (_pixel == null) return;

            // Bar background
            spriteBatch.Draw(_pixel, inputBar, InputBarBg);

            // Top separator
            Rectangle sep = new(inputBar.X, inputBar.Y, inputBar.Width, 1);
            spriteBatch.Draw(_pixel, sep, UIStyle.BlockBorder);

            // Channel badge (width adapts to current label text)
            Rectangle badgeBounds = GetBadgeBounds(inputBar);
            Color badgeColor = ChannelColor(_activeChannel);
            spriteBatch.Draw(_pixel, badgeBounds, badgeColor * 0.25f);
            DrawRectOutline(spriteBatch, badgeBounds, badgeColor * 0.7f, 1);

            if (font.IsAvailable)
            {
                string label = ChannelLabel(_activeChannel);
                Vector2 labelSize = font.MeasureString(label);
                Vector2 labelPos = new(
                    badgeBounds.X + (badgeBounds.Width - labelSize.X) / 2f,
                    badgeBounds.Y + (badgeBounds.Height - labelSize.Y) / 2f);
                font.DrawString(spriteBatch, label, labelPos, badgeColor);
            }

            // Input field
            int fieldX = badgeBounds.Right + 6;
            int fieldW = Math.Max(0, inputBar.Right - fieldX - TextPadding);
            Rectangle fieldBounds = new(fieldX, inputBar.Y + (inputBar.Height - 22) / 2, fieldW, 22);
            spriteBatch.Draw(_pixel, fieldBounds, InputFieldBg);
            DrawRectOutline(spriteBatch, fieldBounds, _inputFocused ? UIStyle.AccentColor * 0.8f : UIStyle.BlockBorder, 1);

            if (font.IsAvailable && fieldW > 0)
            {
                string displayText = _inputBuffer.ToString();
                string cursor = (_inputFocused && _cursorVisible) ? "|" : "";
                string fullText = displayText + cursor;

                // Truncate from left if too wide
                float maxTextW = Math.Max(0f, fieldW - TextPadding * 2);
                while (fullText.Length > 0 && font.MeasureString(fullText).X > maxTextW)
                    fullText = fullText.Substring(1);

                Color textColor = _inputFocused ? Color.White : UIStyle.MutedTextColor;
                if (displayText.Length == 0 && !_inputFocused)
                    font.DrawString(spriteBatch, "Say something...", new Vector2(fieldBounds.X + TextPadding, fieldBounds.Y + (fieldBounds.Height - font.LineHeight) / 2f), UIStyle.MutedTextColor * 0.5f);
                else
                    font.DrawString(spriteBatch, fullText, new Vector2(fieldBounds.X + TextPadding, fieldBounds.Y + (fieldBounds.Height - font.LineHeight) / 2f), textColor);
            }
        }

        private static (Rectangle displayArea, Rectangle inputBar) BuildLayout(Rectangle contentBounds)
        {
            int inputY = Math.Max(contentBounds.Y, contentBounds.Bottom - InputBarHeight);
            int displayH = Math.Max(0, inputY - contentBounds.Y);
            Rectangle displayArea = new(contentBounds.X, contentBounds.Y, contentBounds.Width, displayH);
            Rectangle inputBar = new(contentBounds.X, inputY, contentBounds.Width, InputBarHeight);
            return (displayArea, inputBar);
        }

        private static void CycleChannel()
        {
            _activeChannel = (ChatChannel)(((int)_activeChannel + 1) % 4);
        }

        private static void SendMessage()
        {
            string text = _inputBuffer.ToString().Trim();
            if (string.IsNullOrEmpty(text))
                return;

            AddMessage(PlayerName, text, _activeChannel, isPlayer: true);
            _inputBuffer.Clear();
        }

        private static void AddMessage(string sender, string text, ChatChannel channel, bool isPlayer)
        {
            _messages.Add(new ChatMessage(sender, text, channel, isPlayer));
            while (_messages.Count > MaxMessages)
                _messages.RemoveAt(0);
        }

        private static void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            if (_pixel == null || rect.Width <= 0 || rect.Height <= 0) return;
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        private static void EnsurePixel(GraphicsDevice gd)
        {
            if (_pixel != null || gd == null) return;
            _pixel = new Texture2D(gd, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        private static bool TryConvertKey(Keys key, bool shift, out char value)
        {
            value = '\0';
            if (key >= Keys.A && key <= Keys.Z)
            {
                char b = (char)('a' + (key - Keys.A));
                value = shift ? char.ToUpperInvariant(b) : b;
                return true;
            }
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                int digit = key - Keys.D0;
                if (shift)
                {
                    value = digit switch
                    {
                        0 => ')', 1 => '!', 2 => '@', 3 => '#', 4 => '$',
                        5 => '%', 6 => '^', 7 => '&', 8 => '*', 9 => '(',
                        _ => '\0'
                    };
                }
                else
                {
                    value = (char)('0' + digit);
                }
                return value != '\0';
            }
            value = key switch
            {
                Keys.Space        => ' ',
                Keys.OemPeriod    => shift ? '>' : '.',
                Keys.OemComma     => shift ? '<' : ',',
                Keys.OemMinus     => shift ? '_' : '-',
                Keys.OemPlus      => shift ? '+' : '=',
                Keys.OemQuestion  => shift ? '?' : '/',
                Keys.OemSemicolon => shift ? ':' : ';',
                Keys.OemQuotes    => shift ? '"' : '\'',
                Keys.OemOpenBrackets  => shift ? '{' : '[',
                Keys.OemCloseBrackets => shift ? '}' : ']',
                Keys.OemBackslash     => shift ? '|' : '\\',
                Keys.OemTilde         => shift ? '~' : '`',
                _                     => '\0'
            };
            return value != '\0';
        }
    }
}
