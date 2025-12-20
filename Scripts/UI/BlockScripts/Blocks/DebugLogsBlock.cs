using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;
using FormsClipboard = System.Windows.Forms.Clipboard;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class DebugLogsBlock
    {
        public const string BlockTitle = "Debug Logs";
        public const int MinWidth = 260;
        public const int MinHeight = 160;

        private const int ToolbarHeight = 38;
        private const int ButtonHeight = 30;
        private const int ButtonSpacing = 8;
        private const int Padding = 12;
        private const int TextPadding = 8;
        private const int OpenFolderButtonWidth = 130;
        private const int CopyButtonWidth = 90;
        private const double CopyFeedbackDurationSeconds = 2.0;

        private static readonly BlockScrollPanel ScrollPanel = new();
        private static readonly List<RenderedLine> Lines = new();
        private static int _lastEntryCount = -1;
        private static int _lastTextWidth = -1;
        private static Texture2D _pixel;
        private static MouseState _lastMouseState;
        private static KeyboardState _previousKeyboardState;
        private static double _copyFeedbackSeconds;
        private static bool _isSelecting;
        private static (int Line, int Column)? _selectionAnchor;
        private static (int Line, int Column)? _selectionCaret;

        private readonly struct DebugLogLayout
        {
            public DebugLogLayout(Rectangle toolbar, Rectangle openFolder, Rectangle copy, Rectangle viewport)
            {
                ToolbarBounds = toolbar;
                OpenFolderBounds = openFolder;
                CopyBounds = copy;
                LogViewport = viewport;
            }

            public Rectangle ToolbarBounds { get; }
            public Rectangle OpenFolderBounds { get; }
            public Rectangle CopyBounds { get; }
            public Rectangle LogViewport { get; }
        }

        private readonly struct RenderedLine
        {
            public RenderedLine(string text, Color color)
            {
                Text = text ?? string.Empty;
                Color = color;
            }

            public string Text { get; }
            public Color Color { get; }
        }

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            _lastMouseState = mouseState;
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.DebugLogs);
            UIStyle.UIFont font = UIStyle.GetFontVariant(UIStyle.FontFamilyKey.Xenon, UIStyle.FontVariant.Regular);
            if (!font.IsAvailable)
            {
                ScrollPanel.Update(contentBounds, 0f, mouseState, previousMouseState);
                return;
            }

            DebugLogLayout layout = BuildLayout(contentBounds);
            bool leftClickStarted = mouseState.LeftButton == ButtonState.Pressed &&
                previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickReleased = mouseState.LeftButton == ButtonState.Released &&
                previousMouseState.LeftButton == ButtonState.Pressed;
            bool leftDown = mouseState.LeftButton == ButtonState.Pressed;

            if (blockLocked)
            {
                ClearSelection();
            }

            if (!blockLocked && leftClickStarted && layout.LogViewport.Contains(mouseState.Position))
            {
                BlockManager.TryFocusBlock(DockBlockKind.DebugLogs);
                BeginSelection(mouseState.Position, layout.LogViewport, font);
            }

            if (!blockLocked && _isSelecting && leftDown)
            {
                UpdateSelection(mouseState.Position, layout.LogViewport, font);
            }

            if (_isSelecting && leftClickReleased)
            {
                _isSelecting = false;
            }

            if (leftClickStarted && !layout.LogViewport.Contains(mouseState.Position) &&
                !layout.OpenFolderBounds.Contains(mouseState.Position) &&
                !layout.CopyBounds.Contains(mouseState.Position))
            {
                ClearSelection();
            }

            if (!blockLocked && leftClickStarted && layout.OpenFolderBounds.Contains(mouseState.Position))
            {
                OpenLogsFolder();
            }
            else if (!blockLocked && leftClickStarted && layout.CopyBounds.Contains(mouseState.Position))
            {
                CopyLogsToClipboard();
            }

            int availableWidth = Math.Max(1, layout.LogViewport.Width - (TextPadding * 2));
            RefreshLines(font, availableWidth);

            float contentHeight = (Lines.Count * font.LineHeight) + (TextPadding * 2);
            ScrollPanel.Update(layout.LogViewport, contentHeight, mouseState, previousMouseState);

            Rectangle viewport = ScrollPanel.ContentViewportBounds == Rectangle.Empty
                ? layout.LogViewport
                : ScrollPanel.ContentViewportBounds;

            int adjustedWidth = Math.Max(1, viewport.Width - (TextPadding * 2));
            if (adjustedWidth != _lastTextWidth)
            {
                RefreshLines(font, adjustedWidth);
                contentHeight = (Lines.Count * font.LineHeight) + (TextPadding * 2);
                ScrollPanel.Update(layout.LogViewport, contentHeight, mouseState, previousMouseState);
            }

            HandleKeyboardShortcuts(blockLocked);
            UpdateCopyFeedback(gameTime);
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            UIStyle.UIFont font = UIStyle.GetFontVariant(UIStyle.FontFamilyKey.Xenon, UIStyle.FontVariant.Regular);
            if (!font.IsAvailable)
            {
                return;
            }

            DebugLogLayout layout = BuildLayout(contentBounds);
            DrawToolbar(spriteBatch, layout, font);

            Rectangle viewport = ScrollPanel.ContentViewportBounds == Rectangle.Empty
                ? layout.LogViewport
                : ScrollPanel.ContentViewportBounds;

            float lineHeight = font.LineHeight;
            float y = viewport.Y + TextPadding - ScrollPanel.ScrollOffset;
            int startIndex = Math.Max(0, (int)((ScrollPanel.ScrollOffset - TextPadding) / Math.Max(1f, lineHeight)));
            bool hasSelection = TryGetSelectionRange(out var selection);

            if (Lines.Count == 0)
            {
                string placeholder = "No logs yet.";
                Vector2 size = font.MeasureString(placeholder);
                Vector2 position = new(viewport.X + TextPadding, viewport.Y + TextPadding);
                font.DrawString(spriteBatch, placeholder, position, UIStyle.MutedTextColor);
                ScrollPanel.Draw(spriteBatch);
                return;
            }

            for (int i = startIndex; i < Lines.Count; i++)
            {
                float lineY = y + ((i - startIndex) * lineHeight);
                if (lineY > viewport.Bottom)
                {
                    break;
                }

                if (lineY + lineHeight < viewport.Y)
                {
                    continue;
                }

                RenderedLine line = Lines[i];
                Vector2 position = new(viewport.X + TextPadding, lineY);
                if (hasSelection && IsLineInSelection(i, selection))
                {
                    DrawSelectionHighlight(spriteBatch, line.Text, font, viewport, lineY, lineHeight, i, selection);
                }
                font.DrawString(spriteBatch, line.Text, position, line.Color);
            }

            ScrollPanel.Draw(spriteBatch);
        }

        private static void RefreshLines(UIStyle.UIFont font, int availableWidth)
        {
            if (!font.IsAvailable)
            {
                return;
            }

            DebugLogger.DebugLogEntry[] entries = DebugLogger.GetLogHistorySnapshot();
            bool needsRebuild = _lastEntryCount != entries.Length ||
                _lastTextWidth != availableWidth;

            if (!needsRebuild)
            {
                return;
            }

            _lastEntryCount = entries.Length;
            _lastTextWidth = availableWidth;
            Lines.Clear();

            if (entries.Length == 0)
            {
                ClearSelection();
                return;
            }

            foreach (var entry in entries)
            {
                Color lineColor = ToUiColor(entry.Color);
                string text = entry.Message ?? string.Empty;
                AppendWrappedLine(text, font, availableWidth, lineColor);
            }
        }

        private static void AppendWrappedLine(string text, UIStyle.UIFont font, int availableWidth, Color color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (availableWidth <= 0 || font.MeasureString(text).X <= availableWidth)
            {
                Lines.Add(new RenderedLine(text, color));
                return;
            }

            string[] words = text.Split(' ');
            StringBuilder lineBuilder = new();

            foreach (string word in words)
            {
                string trimmedWord = word ?? string.Empty;
                if (lineBuilder.Length == 0)
                {
                    if (font.MeasureString(trimmedWord).X <= availableWidth)
                    {
                        lineBuilder.Append(trimmedWord);
                    }
                    else
                    {
                        foreach (string chunk in BreakLongWord(trimmedWord, font, availableWidth))
                        {
                            Lines.Add(new RenderedLine(chunk, color));
                        }
                    }
                    continue;
                }

                string candidate = $"{lineBuilder} {trimmedWord}";
                if (font.MeasureString(candidate).X <= availableWidth)
                {
                    lineBuilder.Append(' ').Append(trimmedWord);
                }
                else
                {
                    Lines.Add(new RenderedLine(lineBuilder.ToString(), color));
                    lineBuilder.Clear();

                    if (font.MeasureString(trimmedWord).X <= availableWidth)
                    {
                        lineBuilder.Append(trimmedWord);
                    }
                    else
                    {
                        foreach (string chunk in BreakLongWord(trimmedWord, font, availableWidth))
                        {
                            Lines.Add(new RenderedLine(chunk, color));
                        }
                    }
                }
            }

            if (lineBuilder.Length > 0)
            {
                Lines.Add(new RenderedLine(lineBuilder.ToString(), color));
            }
        }

        private static IEnumerable<string> BreakLongWord(string word, UIStyle.UIFont font, int availableWidth)
        {
            if (string.IsNullOrEmpty(word) || availableWidth <= 0)
            {
                yield break;
            }

            StringBuilder chunk = new();
            foreach (char c in word)
            {
                chunk.Append(c);
                if (font.MeasureString(chunk).X > availableWidth)
                {
                    if (chunk.Length <= 1)
                    {
                        yield return chunk.ToString();
                        chunk.Clear();
                        continue;
                    }

                    string emit = chunk.ToString(0, chunk.Length - 1);
                    yield return emit;
                    chunk.Clear();
                    chunk.Append(c);
                }
            }

            if (chunk.Length > 0)
            {
                yield return chunk.ToString();
            }
        }

        private static void DrawToolbar(SpriteBatch spriteBatch, DebugLogLayout layout, UIStyle.UIFont infoFont)
        {
            EnsurePixel(spriteBatch.GraphicsDevice);
            if (_pixel != null)
            {
                spriteBatch.Draw(_pixel, layout.ToolbarBounds, UIStyle.BlockBackground * 0.92f);
                Rectangle separator = new(layout.ToolbarBounds.X, layout.ToolbarBounds.Bottom - 1, layout.ToolbarBounds.Width, 1);
                spriteBatch.Draw(_pixel, separator, UIStyle.BlockBorder);
            }

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.DebugLogs);
            bool folderHovered = !blockLocked && UIButtonRenderer.IsHovered(layout.OpenFolderBounds, _lastMouseState.Position);
            bool copyHovered = !blockLocked && UIButtonRenderer.IsHovered(layout.CopyBounds, _lastMouseState.Position);
            UIButtonRenderer.Draw(spriteBatch, layout.OpenFolderBounds, "Open folder", UIButtonRenderer.ButtonStyle.Grey, folderHovered, isDisabled: blockLocked);
            UIButtonRenderer.Draw(spriteBatch, layout.CopyBounds, "Copy", UIButtonRenderer.ButtonStyle.Grey, copyHovered, isDisabled: blockLocked);

            if (infoFont.IsAvailable)
            {
                string info = "Read-only (Ctrl+C to copy selection)";
                Vector2 size = infoFont.MeasureString(info);
                float infoX = Math.Max(layout.CopyBounds.Right + ButtonSpacing, layout.ToolbarBounds.Right - size.X);
                Vector2 position = new(infoX, layout.ToolbarBounds.Y + (layout.ToolbarBounds.Height - size.Y) / 2f);
                infoFont.DrawString(spriteBatch, info, position, UIStyle.MutedTextColor);

                if (_copyFeedbackSeconds > 0)
                {
                    string message = "Copied";
                    Vector2 messageSize = infoFont.MeasureString(message);
                    float messageX = Math.Max(layout.ToolbarBounds.Right - messageSize.X, position.X);
                    Vector2 messagePosition = new(messageX, position.Y - messageSize.Y - 2);
                    infoFont.DrawString(spriteBatch, message, messagePosition, UIStyle.AccentColor);
                }
            }
        }

        private static DebugLogLayout BuildLayout(Rectangle contentBounds)
        {
            Rectangle toolbar = new(contentBounds.X, contentBounds.Y, contentBounds.Width, Math.Min(ToolbarHeight, contentBounds.Height));

            int buttonY = toolbar.Y + Math.Max(0, (toolbar.Height - ButtonHeight) / 2);
            int x = toolbar.X;

            Rectangle openFolderBounds = new(x, buttonY, Math.Min(OpenFolderButtonWidth, Math.Max(0, contentBounds.Width - x + contentBounds.X)), ButtonHeight);
            x = openFolderBounds.Right + ButtonSpacing;

            Rectangle copyBounds = new(x, buttonY, Math.Min(CopyButtonWidth, Math.Max(0, contentBounds.Width - x + contentBounds.X)), ButtonHeight);

            int viewportY = toolbar.Bottom + Padding;
            int viewportHeight = Math.Max(0, contentBounds.Bottom - viewportY);
            Rectangle logViewport = new(contentBounds.X, viewportY, contentBounds.Width, viewportHeight);

            return new DebugLogLayout(toolbar, openFolderBounds, copyBounds, logViewport);
        }

        private static void CopyLogsToClipboard()
        {
            string payload = BuildCopyPayload();
            if (string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            try
            {
                FormsClipboard.SetText(payload);
                _copyFeedbackSeconds = CopyFeedbackDurationSeconds;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintWarning($"Failed to copy logs to clipboard: {ex.Message}");
            }
        }

        private static void OpenLogsFolder()
        {
            try
            {
                string logsDirectory = LogFileHandler.GetLogsDirectory();
                if (string.IsNullOrWhiteSpace(logsDirectory))
                {
                    return;
                }

                ProcessStartInfo startInfo = new()
                {
                    FileName = logsDirectory,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintWarning($"Failed to open Logs folder: {ex.Message}");
            }
        }

        private static void HandleKeyboardShortcuts(bool blockLocked)
        {
            if (blockLocked)
            {
                _previousKeyboardState = Keyboard.GetState();
                return;
            }

            KeyboardState keyboardState = Keyboard.GetState();
            bool ctrlDown = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
            bool copyPressed = ctrlDown &&
                keyboardState.IsKeyDown(Keys.C) &&
                (!_previousKeyboardState.IsKeyDown(Keys.C) || !(_previousKeyboardState.IsKeyDown(Keys.LeftControl) || _previousKeyboardState.IsKeyDown(Keys.RightControl)));

            if (BlockManager.BlockHasFocus(DockBlockKind.DebugLogs) && copyPressed)
            {
                CopyLogsToClipboard();
            }

            _previousKeyboardState = keyboardState;
        }

        private static void UpdateCopyFeedback(GameTime gameTime)
        {
            if (_copyFeedbackSeconds <= 0 || gameTime == null)
            {
                return;
            }

            _copyFeedbackSeconds = Math.Max(0, _copyFeedbackSeconds - Math.Max(0, gameTime.ElapsedGameTime.TotalSeconds));
        }

        private static Color ToUiColor(ConsoleColor color)
        {
            return color switch
            {
                ConsoleColor.Black => new Color(16, 16, 16),
                ConsoleColor.DarkBlue => new Color(0, 55, 218),
                ConsoleColor.DarkGreen => new Color(0, 138, 0),
                ConsoleColor.DarkCyan => new Color(58, 150, 221),
                ConsoleColor.DarkRed => new Color(197, 15, 31),
                ConsoleColor.DarkMagenta => new Color(136, 23, 152),
                ConsoleColor.DarkYellow => new Color(193, 156, 0),
                ConsoleColor.Gray => new Color(204, 204, 204),
                ConsoleColor.DarkGray => new Color(118, 118, 118),
                ConsoleColor.Blue => new Color(59, 120, 255),
                ConsoleColor.Green => new Color(22, 198, 12),
                ConsoleColor.Cyan => new Color(97, 214, 214),
                ConsoleColor.Red => new Color(231, 72, 86),
                ConsoleColor.Magenta => new Color(180, 0, 158),
                ConsoleColor.Yellow => new Color(249, 241, 165),
                _ => Color.White
            };
        }

        private static void BeginSelection(Point pointer, Rectangle viewport, UIStyle.UIFont font)
        {
            if (Lines.Count == 0 || !font.IsAvailable)
            {
                ClearSelection();
                return;
            }

            _selectionAnchor = GetSelectionPosition(pointer, viewport, font);
            _selectionCaret = _selectionAnchor;
            _isSelecting = _selectionAnchor.HasValue;
        }

        private static void UpdateSelection(Point pointer, Rectangle viewport, UIStyle.UIFont font)
        {
            if (!_isSelecting || Lines.Count == 0 || !font.IsAvailable)
            {
                return;
            }

            _selectionCaret = GetSelectionPosition(pointer, viewport, font);
        }

        private static (int Line, int Column)? GetSelectionPosition(Point pointer, Rectangle viewport, UIStyle.UIFont font)
        {
            if (Lines.Count == 0 || !font.IsAvailable)
            {
                return null;
            }

            float lineHeight = Math.Max(1f, font.LineHeight);
            float yOffset = (pointer.Y - (viewport.Y + TextPadding)) + ScrollPanel.ScrollOffset;
            int lineIndex = (int)Math.Floor(yOffset / lineHeight);
            lineIndex = Math.Clamp(lineIndex, 0, Lines.Count - 1);

            float xOffset = pointer.X - (viewport.X + TextPadding);
            string lineText = Lines[lineIndex].Text ?? string.Empty;
            int column = GetColumnFromX(lineText, xOffset, font);

            return (lineIndex, column);
        }

        private static int GetColumnFromX(string text, float relativeX, UIStyle.UIFont font)
        {
            if (!font.IsAvailable || string.IsNullOrEmpty(text))
            {
                return 0;
            }

            float cursor = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                string segment = text[i].ToString();
                float width = font.MeasureString(segment).X;
                if (cursor + (width / 2f) >= relativeX)
                {
                    return i;
                }

                cursor += width;
            }

            return text.Length;
        }

        private static bool TryGetSelectionRange(out (int StartLine, int StartColumn, int EndLine, int EndColumn) selection)
        {
            selection = default;
            if (!_selectionAnchor.HasValue || !_selectionCaret.HasValue || Lines.Count == 0)
            {
                return false;
            }

            (int line, int col) start = _selectionAnchor.Value;
            (int line, int col) end = _selectionCaret.Value;

            int lastIndex = Lines.Count - 1;
            start.line = Math.Clamp(start.line, 0, lastIndex);
            end.line = Math.Clamp(end.line, 0, lastIndex);

            if (start.line > end.line || (start.line == end.line && start.col > end.col))
            {
                (start, end) = (end, start);
            }

            start.col = Math.Clamp(start.col, 0, GetLineLength(start.line));
            end.col = Math.Clamp(end.col, 0, GetLineLength(end.line));

            selection = (start.line, start.col, end.line, end.col);
            return true;
        }

        private static bool IsLineInSelection(int lineIndex, (int StartLine, int StartColumn, int EndLine, int EndColumn) selection)
        {
            return lineIndex >= selection.StartLine && lineIndex <= selection.EndLine;
        }

        private static void DrawSelectionHighlight(
            SpriteBatch spriteBatch,
            string lineText,
            UIStyle.UIFont font,
            Rectangle viewport,
            float lineY,
            float lineHeight,
            int lineIndex,
            (int StartLine, int StartColumn, int EndLine, int EndColumn) selection)
        {
            if (_pixel == null || spriteBatch == null || !font.IsAvailable)
            {
                return;
            }

            int startColumn = lineIndex == selection.StartLine ? selection.StartColumn : 0;
            int endColumn = lineIndex == selection.EndLine ? selection.EndColumn : lineText.Length;
            if (startColumn == endColumn)
            {
                return;
            }

            float startX = viewport.X + TextPadding + MeasureColumnWidth(lineText, startColumn, font);
            float endX = viewport.X + TextPadding + MeasureColumnWidth(lineText, endColumn, font);
            float width = Math.Max(1f, endX - startX);

            Rectangle highlight = new((int)Math.Floor(startX), (int)Math.Floor(lineY), (int)Math.Ceiling(width), (int)Math.Ceiling(lineHeight));
            spriteBatch.Draw(_pixel, highlight, UIStyle.AccentColor * 0.25f);
        }

        private static float MeasureColumnWidth(string text, int column, UIStyle.UIFont font)
        {
            column = Math.Clamp(column, 0, text?.Length ?? 0);
            if (column == 0 || string.IsNullOrEmpty(text) || !font.IsAvailable)
            {
                return 0f;
            }

            return font.MeasureString(text.Substring(0, column)).X;
        }

        private static int GetLineLength(int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= Lines.Count)
            {
                return 0;
            }

            return Lines[lineIndex].Text?.Length ?? 0;
        }

        private static void ClearSelection()
        {
            _selectionAnchor = null;
            _selectionCaret = null;
            _isSelecting = false;
        }

        private static string BuildCopyPayload()
        {
            if (TryGetSelectionRange(out var selection) && !(selection.StartLine == selection.EndLine && selection.StartColumn == selection.EndColumn))
            {
                return ExtractSelectionText(selection);
            }

            DebugLogger.DebugLogEntry[] entries = DebugLogger.GetLogHistorySnapshot();
            if (entries.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            foreach (var entry in entries)
            {
                builder.AppendLine(entry.Message ?? string.Empty);
            }

            return builder.ToString();
        }

        private static string ExtractSelectionText((int StartLine, int StartColumn, int EndLine, int EndColumn) selection)
        {
            StringBuilder builder = new();
            for (int line = selection.StartLine; line <= selection.EndLine; line++)
            {
                string text = Lines[line].Text ?? string.Empty;
                int lineStart = line == selection.StartLine ? selection.StartColumn : 0;
                int lineEnd = line == selection.EndLine ? selection.EndColumn : text.Length;
                lineStart = Math.Clamp(lineStart, 0, text.Length);
                lineEnd = Math.Clamp(lineEnd, 0, text.Length);

                if (lineStart < lineEnd)
                {
                    builder.Append(text.Substring(lineStart, lineEnd - lineStart));
                }

                if (line < selection.EndLine)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        private static void EnsurePixel(GraphicsDevice graphicsDevice)
        {
            if (_pixel != null || graphicsDevice == null)
            {
                return;
            }

            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }
    }
}
