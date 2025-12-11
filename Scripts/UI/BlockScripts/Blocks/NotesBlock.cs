
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class NotesBlock
    {
        public const string BlockTitle = "Notes";
        public const int MinWidth = 30;
        public const int MinHeight = 0;

        private enum OverlayMode
        {
            None,
            Open,
            Delete
        }

        private enum NotesCommand
        {
            New,
            Open,
            Save,
            Close,
            Delete
        }

        private const int CommandBarHeight = 36;
        private const int ButtonWidth = 84;
        private const int ButtonHeight = 26;
        private const int ButtonSpacing = 8;
        private const int ContentSpacing = 12;
        private const int TextPadding = 6;
        private const int OverlayPadding = 10;
        private const int OverlayHeaderHeight = 28;
        private const int OverlayRowHeight = 28;
        private const double CursorBlinkInterval = 0.5;
        private const double StatusDurationSeconds = 4.0;

        private static readonly string ProjectRoot = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName ?? AppContext.BaseDirectory;
        private static readonly string NotesDirectory = Path.Combine(ProjectRoot, "UserNotes");
        private static readonly NotesCommand[] CommandOrder = (NotesCommand[])Enum.GetValues(typeof(NotesCommand));
        private static readonly Rectangle[] CommandBounds = new Rectangle[CommandOrder.Length];
        private static readonly List<NoteFileEntry> NoteFiles = new();
        private static readonly List<LineLayout> LineCache = new();
        private static readonly StringBuilder MeasureBuffer = new();
        private static readonly StringBuilder NoteContent = new();

        private static Rectangle CommandBarBounds;
        private static Rectangle TextViewportBounds;
        private static Rectangle OverlayBounds;

        private static OverlayMode ActiveOverlay = OverlayMode.None;
        private static bool NoteListDirty = true;
        private static bool LineCacheDirty = true;

        private static string ActiveNoteName;
        private static string ActiveNotePath;
        private static bool IsDirty;
        private static int CursorIndex;
        private static int VerticalAnchor = -1;

        private static double CursorBlinkTimer;
        private static bool CursorBlinkVisible = true;

        private static double StatusSecondsRemaining;
        private static string StatusMessage;

        private static KeyboardState PreviousKeyboardState;
        private static MouseState LastMouseState;
        private static int OverlayHoverIndex = -1;

        private static Texture2D PixelTexture;

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            if (gameTime == null)
            {
                return;
            }

            EnsureNoteDirectory();
            EnsureNoteList();
            UpdateLayout(contentBounds);

            LastMouseState = mouseState;
            bool leftClickStarted = mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            Point mousePoint = mouseState.Position;

            bool commandHandled = HandleCommandBarClick(mousePoint, leftClickStarted);

            if (ActiveOverlay != OverlayMode.None)
            {
                HandleOverlayInteraction(mousePoint, leftClickStarted, commandHandled);
            }
            else
            {
                OverlayHoverIndex = -1;
            }

            bool textAreaClicked = leftClickStarted
                && HasActiveNote
                && TextViewportBounds != Rectangle.Empty
                && TextViewportBounds.Contains(mousePoint);
            if (textAreaClicked)
            {
                BlockManager.TryFocusBlock(DockBlockKind.Notes);
            }
            else if (leftClickStarted && BlockManager.BlockHasFocus(DockBlockKind.Notes))
            {
                // Clicking anywhere outside the text viewport should drop focus from the Notes block.
                if (TextViewportBounds == Rectangle.Empty || !TextViewportBounds.Contains(mousePoint))
                {
                    BlockManager.ClearBlockFocus();
                }
            }

            bool editingEnabled = BlockManager.BlockHasFocus(DockBlockKind.Notes) && HasActiveNote;

            KeyboardState keyboardState = Keyboard.GetState();
            if (editingEnabled)
            {
                HandleTextAreaMouse(mousePoint, leftClickStarted);
                HandleTyping(keyboardState);
            }
            else
            {
                VerticalAnchor = -1;
            }

            UpdateCursorBlink(gameTime, editingEnabled);
            UpdateStatusTimer(gameTime);

            PreviousKeyboardState = keyboardState;
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            UIStyle.UIFont headerFont = UIStyle.FontH2;
            UIStyle.UIFont labelFont = UIStyle.FontTech;
            UIStyle.UIFont bodyFont = UIStyle.FontBody;
            UIStyle.UIFont placeholderFont = UIStyle.FontH1;

            if (!labelFont.IsAvailable || !bodyFont.IsAvailable || !headerFont.IsAvailable || !placeholderFont.IsAvailable)
            {
                return;
            }

            EnsurePixel(spriteBatch);
            EnsureNoteList();
            UpdateLayout(contentBounds);

            DrawCommandBar(spriteBatch, labelFont);

            if (ActiveOverlay != OverlayMode.None)
            {
                DrawOverlayList(spriteBatch, headerFont, labelFont);
            }

            if (!HasActiveNote)
            {
                DrawPlaceholder(spriteBatch, placeholderFont);
                return;
            }

            EnsureLineCache();
            DrawEditor(spriteBatch, bodyFont);
        }
        private static void EnsureNoteDirectory()
        {
            try
            {
                Directory.CreateDirectory(NotesDirectory);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to ensure notes directory '{NotesDirectory}': {ex.Message}");
            }
        }

        private static void EnsurePixel(SpriteBatch spriteBatch)
        {
            if (PixelTexture != null || spriteBatch == null)
            {
                return;
            }

            GraphicsDevice device = spriteBatch.GraphicsDevice;
            PixelTexture = new Texture2D(device, 1, 1);
            PixelTexture.SetData(new[] { Color.White });
        }

        private static void UpdateLayout(Rectangle contentBounds)
        {
            CommandBarBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, CommandBarHeight);

            int buttonX = CommandBarBounds.X + ButtonSpacing;
            int buttonY = CommandBarBounds.Y + (CommandBarHeight - ButtonHeight) / 2;
            for (int i = 0; i < CommandOrder.Length; i++)
            {
                CommandBounds[i] = new Rectangle(buttonX, buttonY, ButtonWidth, ButtonHeight);
                buttonX += ButtonWidth + ButtonSpacing;
            }

            OverlayBounds = Rectangle.Empty;
            int overlaySpace = 0;
            int availableHeight = Math.Max(0, contentBounds.Height - CommandBarHeight - ContentSpacing);
            if (ActiveOverlay != OverlayMode.None && availableHeight > 0)
            {
                int overlayHeight = Math.Min(220, availableHeight);
                overlayHeight = Math.Max(Math.Min(overlayHeight, availableHeight), Math.Min(availableHeight, 140));
                overlayHeight = Math.Max(0, overlayHeight);
                OverlayBounds = new Rectangle(contentBounds.X, CommandBarBounds.Bottom + ContentSpacing / 2, contentBounds.Width, overlayHeight);
                overlaySpace = overlayHeight + ContentSpacing;
            }

            int textY = CommandBarBounds.Bottom + ContentSpacing + overlaySpace;
            int textHeight = Math.Max(0, contentBounds.Bottom - textY);
            TextViewportBounds = new Rectangle(contentBounds.X, textY, contentBounds.Width, textHeight);
        }

        private static bool HandleCommandBarClick(Point mousePoint, bool leftClickStarted)
        {
            if (!leftClickStarted)
            {
                return false;
            }

            for (int i = 0; i < CommandOrder.Length; i++)
            {
                Rectangle bounds = CommandBounds[i];
                if (!bounds.Contains(mousePoint))
                {
                    continue;
                }

                NotesCommand command = CommandOrder[i];
                if (IsButtonDisabled(command))
                {
                    HandleDisabledCommand(command);
                    return true;
                }

                switch (command)
                {
                    case NotesCommand.New:
                        BeginNewNote();
                        break;
                    case NotesCommand.Open:
                        ToggleOverlay(OverlayMode.Open);
                        break;
                    case NotesCommand.Save:
                        SaveActiveNote();
                        break;
                    case NotesCommand.Close:
                        CloseNote();
                        break;
                    case NotesCommand.Delete:
                        ToggleOverlay(OverlayMode.Delete);
                        break;
                }

                return true;
            }

            return false;
        }

        private static void HandleDisabledCommand(NotesCommand command)
        {
            switch (command)
            {
                case NotesCommand.Save:
                    SetStatusMessage("Create or open a note before saving.");
                    break;
                case NotesCommand.Close:
                    SetStatusMessage("No active note to close.");
                    break;
                case NotesCommand.Open:
                    SetStatusMessage("No saved notes yet.");
                    break;
                case NotesCommand.Delete:
                    SetStatusMessage("No saved notes to delete.");
                    break;
            }
        }

        private static void ToggleOverlay(OverlayMode mode)
        {
            ActiveOverlay = ActiveOverlay == mode ? OverlayMode.None : mode;
            if (ActiveOverlay != OverlayMode.None)
            {
                EnsureNoteList();
            }

            OverlayHoverIndex = -1;
        }

        private static void HandleOverlayInteraction(Point mousePoint, bool leftClickStarted, bool commandHandled)
        {
            if (OverlayBounds == Rectangle.Empty)
            {
                return;
            }

            OverlayHoverIndex = -1;
            Rectangle rowsBounds = new(
                OverlayBounds.X + OverlayPadding,
                OverlayBounds.Y + OverlayHeaderHeight,
                OverlayBounds.Width - (OverlayPadding * 2),
                Math.Max(0, OverlayBounds.Height - OverlayHeaderHeight - OverlayPadding));

            if (rowsBounds.Height <= 0 || rowsBounds.Width <= 0)
            {
                return;
            }

            if (rowsBounds.Contains(mousePoint))
            {
                int relativeY = mousePoint.Y - rowsBounds.Y;
                int index = relativeY / OverlayRowHeight;
                if (index >= 0 && index < NoteFiles.Count)
                {
                    OverlayHoverIndex = index;
                    if (leftClickStarted)
                    {
                        var entry = NoteFiles[index];
                        if (ActiveOverlay == OverlayMode.Open)
                        {
                            LoadNote(entry);
                        }
                        else if (ActiveOverlay == OverlayMode.Delete)
                        {
                            DeleteNote(entry);
                        }
                    }
                }
            }
            else if (leftClickStarted && !commandHandled)
            {
                ActiveOverlay = OverlayMode.None;
            }
        }

        private static void HandleTextAreaMouse(Point mousePoint, bool leftClickStarted)
        {
            if (!leftClickStarted || !HasActiveNote || TextViewportBounds == Rectangle.Empty)
            {
                return;
            }

            if (!TextViewportBounds.Contains(mousePoint))
            {
                return;
            }

            EnsureLineCache();
            UIStyle.UIFont bodyFont = UIStyle.FontBody;
            float lineHeight = bodyFont.LineHeight;
            if (lineHeight <= 0f || LineCache.Count == 0)
            {
                return;
            }

            float relativeY = mousePoint.Y - TextViewportBounds.Y;
            relativeY = MathHelper.Clamp(relativeY, 0f, Math.Max(0f, TextViewportBounds.Height - 1));
            int lineIndex = (int)Math.Floor(relativeY / lineHeight);
            lineIndex = Math.Clamp(lineIndex, 0, LineCache.Count - 1);

            string lineText = LineCache[lineIndex].Text ?? string.Empty;
            float relativeX = mousePoint.X - (TextViewportBounds.X + TextPadding);
            relativeX = Math.Max(0f, relativeX);
            int column = GetColumnFromX(lineText, relativeX, bodyFont);
            column = Math.Clamp(column, 0, lineText.Length);

            int newIndex = LineCache[lineIndex].StartIndex + column;
            SetCursorIndex(newIndex);
        }

        private static int GetColumnFromX(string text, float relativeX, UIStyle.UIFont font)
        {
            if (font.Font == null || string.IsNullOrEmpty(text))
            {
                return 0;
            }

            if (relativeX <= 0f)
            {
                return 0;
            }

            MeasureBuffer.Clear();
            float previousWidth = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                MeasureBuffer.Append(text[i]);
                float width = font.MeasureString(MeasureBuffer).X;
                if (width >= relativeX)
                {
                    float deltaLeft = relativeX - previousWidth;
                    float deltaRight = width - relativeX;
                    MeasureBuffer.Clear();
                    return deltaRight < deltaLeft ? i + 1 : i;
                }

                previousWidth = width;
            }

            MeasureBuffer.Clear();
            return text.Length;
        }
        private static void HandleTyping(KeyboardState keyboardState)
        {
            var pressed = keyboardState.GetPressedKeys();
            bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            bool control = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);

            foreach (Keys key in pressed)
            {
                if (PreviousKeyboardState.IsKeyDown(key))
                {
                    continue;
                }

                if (control)
                {
                    if (key == Keys.S)
                    {
                        SaveActiveNote();
                    }

                    continue;
                }

                switch (key)
                {
                    case Keys.Enter:
                        InsertString("\n");
                        break;
                    case Keys.Back:
                        RemoveCharacterBeforeCursor();
                        break;
                    case Keys.Delete:
                        RemoveCharacterAtCursor();
                        break;
                    case Keys.Left:
                        MoveCursorHorizontal(-1);
                        break;
                    case Keys.Right:
                        MoveCursorHorizontal(1);
                        break;
                    case Keys.Up:
                        MoveCursorVertical(-1);
                        break;
                    case Keys.Down:
                        MoveCursorVertical(1);
                        break;
                    case Keys.Home:
                        MoveCursorToLineEdge(true);
                        break;
                    case Keys.End:
                        MoveCursorToLineEdge(false);
                        break;
                    case Keys.Tab:
                        InsertString("    ");
                        break;
                    default:
                        if (TryConvertKeyToChar(key, shift, out char character))
                        {
                            InsertCharacter(character);
                        }
                        break;
                }
            }
        }

        private static bool TryConvertKeyToChar(Keys key, bool shift, out char value)
        {
            value = '\0';

            if (key >= Keys.A && key <= Keys.Z)
            {
                char baseChar = (char)('a' + (key - Keys.A));
                value = shift ? char.ToUpperInvariant(baseChar) : baseChar;
                return true;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                int digit = key - Keys.D0;
                value = shift ? GetShiftedDigit(digit) : (char)('0' + digit);
                return true;
            }

            return key switch
            {
                Keys.Space => Assign(' ', out value),
                Keys.OemComma => Assign(shift ? '<' : ',', out value),
                Keys.OemPeriod => Assign(shift ? '>' : '.', out value),
                Keys.OemMinus => Assign(shift ? '_' : '-', out value),
                Keys.OemPlus => Assign(shift ? '+' : '=', out value),
                Keys.OemSemicolon => Assign(shift ? ':' : ';', out value),
                Keys.OemQuotes => Assign(shift ? '"' : '\'', out value),
                Keys.OemOpenBrackets => Assign(shift ? '{' : '[', out value),
                Keys.OemCloseBrackets => Assign(shift ? '}' : ']', out value),
                Keys.OemPipe => Assign(shift ? '|' : '\\', out value),
                Keys.OemTilde => Assign(shift ? '~' : '`', out value),
                Keys.OemQuestion => Assign(shift ? '?' : '/', out value),
                Keys.OemBackslash => Assign(shift ? '|' : '\\', out value),
                _ => false
            };
        }

        private static bool Assign(char character, out char value)
        {
            value = character;
            return true;
        }

        private static char GetShiftedDigit(int digit) => digit switch
        {
            0 => ')',
            1 => '!',
            2 => '@',
            3 => '#',
            4 => '$',
            5 => '%',
            6 => '^',
            7 => '&',
            8 => '*',
            9 => '(',
            _ => ' '
        };

        private static void InsertCharacter(char value)
        {
            InsertString(value.ToString());
        }

        private static void InsertString(string value)
        {
            if (!HasActiveNote || string.IsNullOrEmpty(value))
            {
                return;
            }

            NoteContent.Insert(CursorIndex, value);
            CursorIndex += value.Length;
            LineCacheDirty = true;
            IsDirty = true;
            VerticalAnchor = -1;
            ResetCursorBlink();
        }

        private static void RemoveCharacterBeforeCursor()
        {
            if (CursorIndex == 0)
            {
                return;
            }

            NoteContent.Remove(CursorIndex - 1, 1);
            CursorIndex--;
            LineCacheDirty = true;
            IsDirty = true;
            VerticalAnchor = -1;
            ResetCursorBlink();
        }

        private static void RemoveCharacterAtCursor()
        {
            if (CursorIndex >= NoteContent.Length)
            {
                return;
            }

            NoteContent.Remove(CursorIndex, 1);
            LineCacheDirty = true;
            IsDirty = true;
            VerticalAnchor = -1;
            ResetCursorBlink();
        }

        private static void MoveCursorHorizontal(int delta)
        {
            if (!HasActiveNote)
            {
                return;
            }

            int nextIndex = Math.Clamp(CursorIndex + delta, 0, NoteContent.Length);
            SetCursorIndex(nextIndex);
        }

        private static void MoveCursorVertical(int direction)
        {
            if (!HasActiveNote)
            {
                return;
            }

            EnsureLineCache();
            if (LineCache.Count == 0)
            {
                return;
            }

            var (line, column) = GetCursorLocation();
            if (VerticalAnchor < 0)
            {
                VerticalAnchor = column;
            }

            int targetLine = Math.Clamp(line + direction, 0, LineCache.Count - 1);
            int targetColumn = Math.Clamp(VerticalAnchor, 0, LineCache[targetLine].Text.Length);
            int targetIndex = LineCache[targetLine].StartIndex + targetColumn;
            SetCursorIndex(targetIndex, false);
        }

        private static void MoveCursorToLineEdge(bool toStart)
        {
            if (!HasActiveNote)
            {
                return;
            }

            EnsureLineCache();
            var (line, _) = GetCursorLocation();
            if (line < 0 || line >= LineCache.Count)
            {
                return;
            }

            LineLayout layout = LineCache[line];
            int newIndex = toStart ? layout.StartIndex : layout.StartIndex + layout.Text.Length;
            SetCursorIndex(newIndex);
        }

        private static (int line, int column) GetCursorLocation()
        {
            EnsureLineCache();
            if (LineCache.Count == 0)
            {
                return (0, 0);
            }

            for (int i = 0; i < LineCache.Count; i++)
            {
                LineLayout layout = LineCache[i];
                int start = layout.StartIndex;
                int end = start + layout.Text.Length;
                if (CursorIndex >= start && CursorIndex <= end)
                {
                    return (i, CursorIndex - start);
                }
            }

            LineLayout last = LineCache[^1];
            return (LineCache.Count - 1, last.Text.Length);
        }

        private static void SetCursorIndex(int newIndex, bool resetAnchor = true)
        {
            newIndex = Math.Clamp(newIndex, 0, NoteContent.Length);
            if (CursorIndex == newIndex)
            {
                ResetCursorBlink();
                if (resetAnchor)
                {
                    VerticalAnchor = -1;
                }

                return;
            }

            CursorIndex = newIndex;
            if (resetAnchor)
            {
                VerticalAnchor = -1;
            }

            ResetCursorBlink();
        }
        private static void ResetCursorBlink()
        {
            CursorBlinkTimer = 0;
            CursorBlinkVisible = true;
        }

        private static void UpdateCursorBlink(GameTime gameTime, bool enabled)
        {
            if (!enabled)
            {
                CursorBlinkVisible = false;
                CursorBlinkTimer = 0;
                return;
            }

            CursorBlinkTimer += gameTime.ElapsedGameTime.TotalSeconds;
            if (CursorBlinkTimer >= CursorBlinkInterval)
            {
                CursorBlinkTimer -= CursorBlinkInterval;
                CursorBlinkVisible = !CursorBlinkVisible;
            }
        }

        private static void UpdateStatusTimer(GameTime gameTime)
        {
            if (StatusSecondsRemaining <= 0f)
            {
                return;
            }

            StatusSecondsRemaining = Math.Max(0f, StatusSecondsRemaining - gameTime.ElapsedGameTime.TotalSeconds);
        }

        private static void SetStatusMessage(string message)
        {
            StatusMessage = message;
            StatusSecondsRemaining = StatusDurationSeconds;
        }

        private static bool HasActiveNote => !string.IsNullOrWhiteSpace(ActiveNoteName) && !string.IsNullOrWhiteSpace(ActiveNotePath);

        private static bool IsButtonDisabled(NotesCommand command)
        {
            return command switch
            {
                NotesCommand.Save => !HasActiveNote,
                NotesCommand.Close => !HasActiveNote,
                NotesCommand.Open => NoteFiles.Count == 0,
                NotesCommand.Delete => NoteFiles.Count == 0,
                _ => false
            };
        }

        private static void BeginNewNote()
        {
            string fileName = GenerateDefaultNoteName();
            ActiveNoteName = fileName;
            ActiveNotePath = Path.Combine(NotesDirectory, $"{fileName}.txt");
            NoteContent.Clear();
            CursorIndex = 0;
            LineCacheDirty = true;
            IsDirty = false;
            ActiveOverlay = OverlayMode.None;
            VerticalAnchor = -1;
            SetStatusMessage($"Ready to edit '{fileName}'.");
        }

        private static void CloseNote()
        {
            if (!HasActiveNote)
            {
                SetStatusMessage("No active note to close.");
                return;
            }

            bool hadChanges = IsDirty;
            ActiveNoteName = null;
            ActiveNotePath = null;
            NoteContent.Clear();
            CursorIndex = 0;
            LineCacheDirty = true;
            IsDirty = false;
            ActiveOverlay = OverlayMode.None;
            VerticalAnchor = -1;
            SetStatusMessage(hadChanges ? "Closed note without saving." : "Closed note.");
        }

        private static string GenerateDefaultNoteName()
        {
            string baseName = $"note-{DateTime.Now:yyyyMMdd-HHmmss}";
            string candidate = baseName;
            int counter = 1;
            while (File.Exists(Path.Combine(NotesDirectory, $"{candidate}.txt")))
            {
                candidate = $"{baseName}-{counter++:00}";
            }

            return candidate;
        }

        private static void SaveActiveNote()
        {
            if (!HasActiveNote)
            {
                SetStatusMessage("Create or open a note before saving.");
                return;
            }

            try
            {
                Directory.CreateDirectory(NotesDirectory);
                string normalized = NoteContent.ToString().Replace("\r\n", "\n").Replace("\r", "\n");
                string fileText = normalized.Replace("\n", Environment.NewLine);
                File.WriteAllText(ActiveNotePath, fileText, Encoding.UTF8);
                IsDirty = false;
                NoteListDirty = true;
                SetStatusMessage($"Saved '{ActiveNoteName}'.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to save note '{ActiveNotePath}': {ex.Message}");
                SetStatusMessage("Failed to save note.");
            }
        }

        private static void LoadNote(NoteFileEntry entry)
        {
            try
            {
                string text = File.ReadAllText(entry.FullPath, Encoding.UTF8);
                text = text.Replace("\r\n", "\n").Replace("\r", "\n");
                NoteContent.Clear();
                NoteContent.Append(text);
                ActiveNoteName = entry.DisplayName;
                ActiveNotePath = entry.FullPath;
                CursorIndex = NoteContent.Length;
                LineCacheDirty = true;
                IsDirty = false;
                ActiveOverlay = OverlayMode.None;
                VerticalAnchor = -1;
                SetStatusMessage($"Opened '{entry.DisplayName}'.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to open note '{entry.FullPath}': {ex.Message}");
                SetStatusMessage("Failed to open note.");
            }
        }

        private static void DeleteNote(NoteFileEntry entry)
        {
            try
            {
                if (File.Exists(entry.FullPath))
                {
                    File.Delete(entry.FullPath);
                }

                if (HasActiveNote && string.Equals(ActiveNotePath, entry.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    CloseNote();
                }

                NoteListDirty = true;
                ActiveOverlay = OverlayMode.None;
                SetStatusMessage($"Deleted '{entry.DisplayName}'.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to delete note '{entry.FullPath}': {ex.Message}");
                SetStatusMessage("Failed to delete note.");
            }
        }

        private static void EnsureNoteList()
        {
            if (!NoteListDirty)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(NotesDirectory);
                NoteFiles.Clear();
                foreach (string file in Directory.EnumerateFiles(NotesDirectory, "*.txt"))
                {
                    FileInfo info = new(file);
                    string displayName = Path.GetFileNameWithoutExtension(file);
                    NoteFiles.Add(new NoteFileEntry(displayName, file, info.LastWriteTime));
                }

                NoteFiles.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to enumerate notes in '{NotesDirectory}': {ex.Message}");
            }
            finally
            {
                NoteListDirty = false;
            }
        }

        private static void EnsureLineCache()
        {
            if (!LineCacheDirty)
            {
                return;
            }

            LineCache.Clear();
            if (NoteContent.Length == 0)
            {
                LineCache.Add(new LineLayout(string.Empty, 0));
                LineCacheDirty = false;
                return;
            }

            int lineStart = 0;
            for (int i = 0; i < NoteContent.Length; i++)
            {
                char ch = NoteContent[i];
                if (ch == '\r')
                {
                    continue;
                }

                if (ch == '\n')
                {
                    string lineText = i > lineStart ? NoteContent.ToString(lineStart, i - lineStart) : string.Empty;
                    LineCache.Add(new LineLayout(lineText, lineStart));
                    lineStart = i + 1;
                }
            }

            if (lineStart <= NoteContent.Length)
            {
                string tail = lineStart < NoteContent.Length ? NoteContent.ToString(lineStart, NoteContent.Length - lineStart) : string.Empty;
                LineCache.Add(new LineLayout(tail, lineStart));
            }

            if (LineCache.Count == 0)
            {
                LineCache.Add(new LineLayout(string.Empty, 0));
            }

            LineCacheDirty = false;
        }
        private static void DrawCommandBar(SpriteBatch spriteBatch, UIStyle.UIFont font)
        {
            FillRect(spriteBatch, CommandBarBounds, UIStyle.HeaderBackground);
            DrawRect(spriteBatch, CommandBarBounds, UIStyle.BlockBorder);

            for (int i = 0; i < CommandOrder.Length; i++)
            {
                DrawButton(spriteBatch, font, CommandOrder[i], CommandBounds[i]);
            }

            DrawActiveNoteLabel(spriteBatch, font);
            DrawStatus(spriteBatch, font);
        }

        private static void DrawButton(SpriteBatch spriteBatch, UIStyle.UIFont font, NotesCommand command, Rectangle bounds)
        {
            bool disabled = IsButtonDisabled(command);
            bool hovered = UIButtonRenderer.IsHovered(bounds, LastMouseState.Position);
            bool isActiveOverlay = command switch
            {
                NotesCommand.Open => ActiveOverlay == OverlayMode.Open,
                NotesCommand.Delete => ActiveOverlay == OverlayMode.Delete,
                _ => false
            };

            UIButtonRenderer.ButtonStyle style = isActiveOverlay ? UIButtonRenderer.ButtonStyle.Blue : UIButtonRenderer.ButtonStyle.Grey;
            UIButtonRenderer.Draw(spriteBatch, bounds, command.ToString(), style, hovered, disabled);
        }

        private static void DrawActiveNoteLabel(SpriteBatch spriteBatch, UIStyle.UIFont font)
        {
            if (!HasActiveNote)
            {
                string label = "No note selected";
                Vector2 size = font.MeasureString(label);
                Vector2 position = new(CommandBarBounds.Right - size.X - ButtonSpacing, CommandBarBounds.Y + (CommandBarHeight - size.Y) / 2f);
                font.DrawString(spriteBatch, label, position, UIStyle.MutedTextColor);
                return;
            }

            string labelText = IsDirty ? $"{ActiveNoteName} *" : ActiveNoteName;
            Vector2 textSize = font.MeasureString(labelText);
            Vector2 textPosition = new(CommandBarBounds.Right - textSize.X - ButtonSpacing, CommandBarBounds.Y + (CommandBarHeight - textSize.Y) / 2f);
            font.DrawString(spriteBatch, labelText, textPosition, UIStyle.TextColor);
        }

        private static void DrawStatus(SpriteBatch spriteBatch, UIStyle.UIFont font)
        {
            if (StatusSecondsRemaining <= 0f || string.IsNullOrWhiteSpace(StatusMessage))
            {
                return;
            }

            Vector2 textSize = font.MeasureString(StatusMessage);
            Vector2 position = new(CommandBarBounds.X + ButtonSpacing, CommandBarBounds.Bottom - textSize.Y - 4f);
            font.DrawString(spriteBatch, StatusMessage, position, UIStyle.TextColor);
        }

        private static void DrawOverlayList(SpriteBatch spriteBatch, UIStyle.UIFont headerFont, UIStyle.UIFont rowFont)
        {
            if (OverlayBounds == Rectangle.Empty)
            {
                return;
            }

            FillRect(spriteBatch, OverlayBounds, UIStyle.BlockBackground);
            DrawRect(spriteBatch, OverlayBounds, UIStyle.BlockBorder);

            string header = ActiveOverlay == OverlayMode.Delete ? "Select a note to delete" : "Select a note to open";
            Vector2 headerSize = headerFont.MeasureString(header);
            Vector2 headerPosition = new(OverlayBounds.X + OverlayPadding, OverlayBounds.Y + (OverlayHeaderHeight - headerSize.Y) / 2f);
            headerFont.DrawString(spriteBatch, header, headerPosition, UIStyle.TextColor);

            Rectangle rowsBounds = new(
                OverlayBounds.X + OverlayPadding,
                OverlayBounds.Y + OverlayHeaderHeight,
                OverlayBounds.Width - (OverlayPadding * 2),
                Math.Max(0, OverlayBounds.Height - OverlayHeaderHeight - OverlayPadding));

            if (NoteFiles.Count == 0)
            {
                string empty = "No saved notes.";
                Vector2 emptySize = rowFont.MeasureString(empty);
                Vector2 emptyPosition = new(rowsBounds.X, rowsBounds.Y + 8);
                rowFont.DrawString(spriteBatch, empty, emptyPosition, UIStyle.MutedTextColor);
                return;
            }

            int rowY = rowsBounds.Y;
            int maxRows = rowsBounds.Height / OverlayRowHeight;
            for (int i = 0; i < NoteFiles.Count && i < maxRows; i++)
            {
                Rectangle rowRect = new(rowsBounds.X, rowY, rowsBounds.Width, OverlayRowHeight - 2);
                bool hovered = i == OverlayHoverIndex;
                Color rowColor = hovered ? UIStyle.AccentMuted : UIStyle.BlockBackground;
                FillRect(spriteBatch, rowRect, rowColor);

                NoteFileEntry entry = NoteFiles[i];
                string label = entry.DisplayName;
                string meta = entry.Modified.ToString("g");
                Vector2 labelPos = new(rowRect.X + 6, rowRect.Y + 4);
                rowFont.DrawString(spriteBatch, label, labelPos, UIStyle.TextColor);

                Vector2 metaSize = rowFont.MeasureString(meta);
                Vector2 metaPos = new(rowRect.Right - metaSize.X - 6, rowRect.Y + 4);
                rowFont.DrawString(spriteBatch, meta, metaPos, UIStyle.MutedTextColor);

                rowY += OverlayRowHeight;
            }
        }

        private static void DrawEditor(SpriteBatch spriteBatch, UIStyle.UIFont font)
        {
            if (TextViewportBounds == Rectangle.Empty)
            {
                return;
            }

            FillRect(spriteBatch, TextViewportBounds, UIStyle.BlockBackground * 0.9f);
            DrawRect(spriteBatch, TextViewportBounds, UIStyle.BlockBorder);

            float lineHeight = font.LineHeight;
            float y = TextViewportBounds.Y;
            foreach (LineLayout layout in LineCache)
            {
                if (y + lineHeight > TextViewportBounds.Bottom)
                {
                    break;
                }

                Vector2 position = new(TextViewportBounds.X + TextPadding, y);
                if (!string.IsNullOrEmpty(layout.Text))
                {
                    font.DrawString(spriteBatch, layout.Text, position, UIStyle.TextColor);
                }

                y += lineHeight;
            }

            if (CursorBlinkVisible && BlockManager.BlockHasFocus(DockBlockKind.Notes))
            {
                DrawCursor(spriteBatch, font, lineHeight);
            }
        }

        private static void DrawCursor(SpriteBatch spriteBatch, UIStyle.UIFont font, float lineHeight)
        {
            if (font.Font == null)
            {
                return;
            }

            var (line, column) = GetCursorLocation();
            line = Math.Clamp(line, 0, LineCache.Count - 1);
            float cursorY = TextViewportBounds.Y + (line * lineHeight);
            if (cursorY < TextViewportBounds.Y || cursorY > TextViewportBounds.Bottom)
            {
                return;
            }

            string text = LineCache[line].Text ?? string.Empty;
            string prefix = column <= 0 ? string.Empty : text[..Math.Min(column, text.Length)];
            float offsetX = font.MeasureString(prefix).X;

            Rectangle cursorRect = new(
                (int)(TextViewportBounds.X + TextPadding + offsetX),
                (int)cursorY,
                2,
                (int)Math.Min(lineHeight, TextViewportBounds.Bottom - cursorY));

            FillRect(spriteBatch, cursorRect, UIStyle.AccentColor);
        }

        private static void DrawPlaceholder(SpriteBatch spriteBatch, UIStyle.UIFont font)
        {
            if (TextViewportBounds == Rectangle.Empty)
            {
                return;
            }

            FillRect(spriteBatch, TextViewportBounds, UIStyle.BlockBackground * 0.9f);
            DrawRect(spriteBatch, TextViewportBounds, UIStyle.BlockBorder);

            const string placeholder = "Create a note";
            Vector2 size = font.MeasureString(placeholder);
            Vector2 position = new(
                TextViewportBounds.X + (TextViewportBounds.Width - size.X) / 2f,
                TextViewportBounds.Y + (TextViewportBounds.Height - size.Y) / 2f);
            font.DrawString(spriteBatch, placeholder, position, UIStyle.MutedTextColor);
        }

        private static void FillRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            if (PixelTexture == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            spriteBatch.Draw(PixelTexture, bounds, color);
        }

        private static void DrawRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            if (PixelTexture == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            Rectangle top = new(bounds.X, bounds.Y, bounds.Width, 1);
            Rectangle bottom = new(bounds.X, bounds.Bottom - 1, bounds.Width, 1);
            Rectangle left = new(bounds.X, bounds.Y, 1, bounds.Height);
            Rectangle right = new(bounds.Right - 1, bounds.Y, 1, bounds.Height);

            spriteBatch.Draw(PixelTexture, top, color);
            spriteBatch.Draw(PixelTexture, bottom, color);
            spriteBatch.Draw(PixelTexture, left, color);
            spriteBatch.Draw(PixelTexture, right, color);
        }

        private readonly struct NoteFileEntry
        {
            public NoteFileEntry(string displayName, string fullPath, DateTime modified)
            {
                DisplayName = displayName;
                FullPath = fullPath;
                Modified = modified;
            }

            public string DisplayName { get; }
            public string FullPath { get; }
            public DateTime Modified { get; }
        }

        private readonly struct LineLayout
        {
            public LineLayout(string text, int startIndex)
            {
                Text = text;
                StartIndex = startIndex;
            }

            public string Text { get; }
            public int StartIndex { get; }
        }
    }
}
