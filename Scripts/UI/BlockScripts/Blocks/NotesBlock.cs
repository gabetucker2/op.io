
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io;
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
            Delete
        }

        private enum NotesCommand
        {
            New,
            Save,
            Rename,
            Close,
            Delete
        }

        private enum NamePromptMode
        {
            Save,
            Rename
        }

        private const int CommandBarHeight = 36;
        private const int ButtonHeight = 26;
        private const int ButtonWidth = ButtonHeight;
        private const int ButtonSpacing = 8;
        private const int ContentSpacing = 12;
        private const int TextPadding = 6;
        private const int OverlayPadding = 10;
        private const int OverlayHeaderHeight = 28;
        private const int OverlayRowHeight = 28;
        private const double CursorBlinkInterval = 0.5;
        private const double FeedbackDurationSeconds = 4.0;
        private const int SavePromptWidth = 360;
        private const int SavePromptHeight = 170;
        private const int SavePromptPadding = 16;
        private const int SavePromptInputHeight = 28;
        private const int SavePromptButtonHeight = 28;
        private const int SavePromptButtonSpacing = 10;
        private const float SavePromptTitleSpacing = 6f;
        private const float SavePromptHelperSpacing = 10f;
        private const float SavePromptFallbackTitleHeight = 26f;
        private const float SavePromptFallbackHelperHeight = 20f;
        private const string LastNoteRowKey = "__LastNote";

        private static readonly string NotesDirectory = NotesFileSystem.NotesDirectoryPath;
        private static readonly NotesCommand[] CommandOrder = new[]
        {
            NotesCommand.Save,
            NotesCommand.New,
            NotesCommand.Rename,
            NotesCommand.Delete
        };
        private static readonly Rectangle[] CommandBounds = new Rectangle[CommandOrder.Length];
        private static readonly List<NoteFileEntry> NoteFiles = new();
        private static readonly List<LineLayout> LineCache = new();
        private static readonly StringBuilder MeasureBuffer = new();
        private static readonly StringBuilder NoteContent = new();
        private static readonly UIDropdown NoteDropdown = new();
        private static readonly KeyRepeatTracker TextInputRepeater = new();
        private static readonly KeyRepeatTracker SavePromptRepeater = new();

        private static Rectangle CommandBarBounds;
        private static Rectangle FeedbackBarBounds;
        private static Rectangle TextViewportBounds;
        private static Rectangle OverlayBounds;
        private static Rectangle NoteDropdownBounds;
        private static Rectangle LastContentBounds;

        private static OverlayMode ActiveOverlay = OverlayMode.None;
        private static bool NoteListDirty = true;
        private static bool LineCacheDirty = true;
        private static bool DefaultNoteEnsured;
        private static bool LastNoteRestored;
        private static string PreferredNoteId;

        private static string ActiveNoteName;
        private static string ActiveNotePath;
        private static bool ActiveNoteSaved;
        private static bool IsDirty;
        private static int CursorIndex;
        private static int VerticalAnchor = -1;

        private static double CursorBlinkTimer;
        private static bool CursorBlinkVisible = true;

        private static double FeedbackSecondsRemaining;
        private static string FeedbackMessage;

        private static KeyboardState PreviousKeyboardState;
        private static MouseState LastMouseState;
        private static int OverlayHoverIndex = -1;
        private static SavePromptState SavePrompt;

        private static Texture2D PixelTexture;
        private static Texture2D IconNew;
        private static Texture2D IconSave;
        private static Texture2D IconRename;
        private static Texture2D IconDelete;

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            if (gameTime == null)
            {
                return;
            }

            double elapsedSeconds = Math.Max(gameTime.ElapsedGameTime.TotalSeconds, 0d);
            EnsureNoteDirectory();
            EnsureNoteList();
            EnsureLastNoteOpen();
            UpdateLayout(contentBounds);
            LastContentBounds = contentBounds;
            EnsureNoteDropdownOptions();
            NoteDropdown.Bounds = NoteDropdownBounds;
            EnsureDefaultNoteOpen();

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Notes);
            if (blockLocked && ActiveOverlay != OverlayMode.None)
            {
                ActiveOverlay = OverlayMode.None;
                OverlayHoverIndex = -1;
            }
            if (blockLocked && SavePrompt.IsOpen)
            {
                CloseSavePrompt();
            }

            KeyboardState keyboardState = Keyboard.GetState();
            if (blockLocked)
            {
                NoteDropdown.Close();
            }

            LastMouseState = mouseState;
            if (SavePrompt.IsOpen)
            {
                TextInputRepeater.Reset();
                UpdateSavePrompt(mouseState, previousMouseState, keyboardState, PreviousKeyboardState, elapsedSeconds);
                UpdateFeedbackTimer(gameTime);
                PreviousKeyboardState = keyboardState;
                return;
            }

            bool leftClickStarted = mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            Point mousePoint = mouseState.Position;

            string selectedNote = null;
            bool dropdownChanged = false;
            if (!blockLocked)
            {
                dropdownChanged = NoteDropdown.Update(mouseState, previousMouseState, keyboardState, PreviousKeyboardState, out selectedNote, isDisabled: blockLocked);
            }
            if (dropdownChanged && !string.IsNullOrWhiteSpace(selectedNote))
            {
                NoteFileEntry match = NoteFiles.FirstOrDefault(n => string.Equals(n.DisplayName, selectedNote, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(match.DisplayName))
                {
                    LoadNote(match);
                }
            }

            bool commandHandled = !blockLocked && HandleCommandBarClick(mousePoint, leftClickStarted);

            if (!blockLocked && ActiveOverlay != OverlayMode.None)
            {
                HandleOverlayInteraction(mousePoint, leftClickStarted, commandHandled);
            }
            else
            {
                OverlayHoverIndex = -1;
            }

            bool textAreaClicked = !blockLocked
                && leftClickStarted
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

            bool editingEnabled = !blockLocked && BlockManager.BlockHasFocus(DockBlockKind.Notes) && HasActiveNote;

            if (editingEnabled)
            {
                HandleTextAreaMouse(mousePoint, leftClickStarted);
                HandleTyping(keyboardState, PreviousKeyboardState, elapsedSeconds);
            }
            else
            {
                VerticalAnchor = -1;
                TextInputRepeater.Reset();
            }

            UpdateCursorBlink(gameTime, editingEnabled);
            UpdateFeedbackTimer(gameTime);

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
            UIStyle.UIFont feedbackFont = UIStyle.FontBody;

            if (!labelFont.IsAvailable || !bodyFont.IsAvailable || !headerFont.IsAvailable || !placeholderFont.IsAvailable)
            {
                return;
            }

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Notes);
            if (blockLocked && ActiveOverlay != OverlayMode.None)
            {
                ActiveOverlay = OverlayMode.None;
                OverlayHoverIndex = -1;
            }

            EnsurePixel(spriteBatch);
            EnsureNoteList();
            EnsureCommandIcons();
            UpdateLayout(contentBounds);

            DrawCommandBar(spriteBatch, labelFont, blockLocked);
            DrawFeedbackBar(spriteBatch, feedbackFont);

            if (!blockLocked && ActiveOverlay != OverlayMode.None)
            {
                DrawOverlayList(spriteBatch, headerFont, labelFont, blockLocked);
            }

            if (!HasActiveNote)
            {
                DrawPlaceholder(spriteBatch, placeholderFont);
            }
            else
            {
                EnsureLineCache();
                DrawEditor(spriteBatch, bodyFont);
            }

            NoteDropdown.DrawOptionsOverlay(spriteBatch);
            DrawSavePrompt(spriteBatch, contentBounds);
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

        private static void EnsureCommandIcons()
        {
            IconNew = EnsureIcon(IconNew, "Icon_New.png");
            IconSave = EnsureIcon(IconSave, "Icon_Save.png");
            IconRename = EnsureIcon(IconRename, "Icon_Rename.png");
            IconDelete = EnsureIcon(IconDelete, "Icon_Delete.png");
        }

        private static Texture2D EnsureIcon(Texture2D icon, string fileName)
        {
            if (icon != null && !icon.IsDisposed)
            {
                return icon;
            }

            return BlockIconProvider.GetIcon(fileName);
        }

        private static void UpdateLayout(Rectangle contentBounds)
        {
            CommandBarBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, CommandBarHeight);
            FeedbackBarBounds = BlockFeedbackBarRenderer.CalculateBounds(contentBounds, CommandBarBounds);

            int totalButtonsWidth = CommandOrder.Length * ButtonWidth + Math.Max(0, (CommandOrder.Length - 1) * ButtonSpacing);
            int dropdownHeight = ButtonHeight;
            int dropdownY = CommandBarBounds.Y + (CommandBarHeight - dropdownHeight) / 2;

            int availableDropdown = CommandBarBounds.Width - (ButtonSpacing * 3 + totalButtonsWidth);
            int dropdownWidth = Math.Max(0, availableDropdown);

            int dropdownX = CommandBarBounds.X + ButtonSpacing;
            NoteDropdownBounds = dropdownWidth > 0
                ? new Rectangle(dropdownX, dropdownY, dropdownWidth, dropdownHeight)
                : Rectangle.Empty;

            int buttonX = dropdownX + (dropdownWidth > 0 ? dropdownWidth + ButtonSpacing : 0);
            int buttonY = CommandBarBounds.Y + (CommandBarHeight - ButtonHeight) / 2;
            for (int i = 0; i < CommandOrder.Length; i++)
            {
                CommandBounds[i] = new Rectangle(buttonX, buttonY, ButtonWidth, ButtonHeight);
                buttonX += ButtonWidth + ButtonSpacing;
            }

            OverlayBounds = Rectangle.Empty;
            int overlaySpace = 0;
            int afterFeedbackBarY = (FeedbackBarBounds == Rectangle.Empty ? CommandBarBounds.Bottom : FeedbackBarBounds.Bottom) + ContentSpacing;
            int availableHeight = Math.Max(0, contentBounds.Height - (afterFeedbackBarY - contentBounds.Y));
            if (ActiveOverlay != OverlayMode.None && availableHeight > 0)
            {
                int overlayHeight = Math.Min(220, availableHeight);
                overlayHeight = Math.Max(Math.Min(overlayHeight, availableHeight), Math.Min(availableHeight, 140));
                overlayHeight = Math.Max(0, overlayHeight);
                OverlayBounds = new Rectangle(contentBounds.X, afterFeedbackBarY, contentBounds.Width, overlayHeight);
                overlaySpace = overlayHeight + ContentSpacing;
            }

            int textY = afterFeedbackBarY + overlaySpace;
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
                    case NotesCommand.Save:
                        SaveActiveNote();
                        break;
                    case NotesCommand.Rename:
                        BeginRename();
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
                    SetFeedbackMessage("Create or open a note before saving.");
                    break;
                case NotesCommand.Close:
                    SetFeedbackMessage("No active note to close.");
                    break;
                case NotesCommand.Delete:
                    SetFeedbackMessage("No saved notes to delete.");
                    break;
                case NotesCommand.Rename:
                    SetFeedbackMessage("Open a note before renaming.");
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
                        if (ActiveOverlay == OverlayMode.Delete)
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
                float width = TextSpacingHelper.MeasureWithWideSpaces(font, MeasureBuffer).X;
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
        private static void HandleTyping(KeyboardState keyboardState, KeyboardState previousKeyboardState, double elapsedSeconds)
        {
            bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            bool control = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);

            foreach (Keys key in TextInputRepeater.GetKeysWithRepeat(keyboardState, previousKeyboardState, elapsedSeconds))
            {
                bool newPress = !previousKeyboardState.IsKeyDown(key);

                if (control)
                {
                    if (key == Keys.S && newPress)
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

        private static void UpdateFeedbackTimer(GameTime gameTime)
        {
            if (FeedbackSecondsRemaining <= 0f)
            {
                return;
            }

            FeedbackSecondsRemaining = Math.Max(0f, FeedbackSecondsRemaining - gameTime.ElapsedGameTime.TotalSeconds);
        }

        private static void SetFeedbackMessage(string message)
        {
            FeedbackMessage = message;
            FeedbackSecondsRemaining = FeedbackDurationSeconds;
        }

        private static bool HasActiveNote => !string.IsNullOrWhiteSpace(ActiveNoteName) && !string.IsNullOrWhiteSpace(ActiveNotePath);

        private static bool IsButtonDisabled(NotesCommand command)
        {
            return command switch
            {
                NotesCommand.Save => !HasActiveNote,
                NotesCommand.Close => !HasActiveNote,
                NotesCommand.Delete => NoteFiles.Count == 0,
                NotesCommand.Rename => !HasActiveNote,
                _ => false
            };
        }

        private static void BeginNewNote()
        {
            CloseSavePrompt();
            string fileName = GenerateDefaultNoteName();
            ActiveNoteName = fileName;
            ActiveNotePath = Path.Combine(NotesDirectory, $"{fileName}.txt");
            ActiveNoteSaved = false;
            NoteContent.Clear();
            CursorIndex = 0;
            LineCacheDirty = true;
            IsDirty = false;
            ActiveOverlay = OverlayMode.None;
            VerticalAnchor = -1;
            SetFeedbackMessage($"Ready to edit '{fileName}'.");
        }

        private static void ClearActiveNoteState()
        {
            ActiveNoteName = null;
            ActiveNotePath = null;
            ActiveNoteSaved = false;
            NoteContent.Clear();
            CursorIndex = 0;
            LineCacheDirty = true;
            IsDirty = false;
            VerticalAnchor = -1;
        }

        private static void CloseNote()
        {
            CloseSavePrompt();
            if (!HasActiveNote)
            {
                SetFeedbackMessage("No active note to close.");
                return;
            }

            bool hadChanges = IsDirty;
            ClearActiveNoteState();
            ActiveOverlay = OverlayMode.None;
            SetFeedbackMessage(hadChanges ? "Closed note without saving." : "Closed note.");
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
                SetFeedbackMessage("Create or open a note before saving.");
                return;
            }

            if (SavePrompt.IsOpen)
            {
                return;
            }

            if (!ActiveNoteSaved)
            {
                OpenNamePrompt(NamePromptMode.Save);
                return;
            }

            PersistActiveNote(ActiveNoteName, ActiveNotePath);
        }

        private static void BeginRename()
        {
            if (!HasActiveNote)
            {
                SetFeedbackMessage("Open a note before renaming.");
                return;
            }

            OpenNamePrompt(NamePromptMode.Rename);
        }

        private static void PersistActiveNote(string noteName, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(noteName) || string.IsNullOrWhiteSpace(targetPath))
            {
                SetFeedbackMessage("Enter a name for the note.");
                return;
            }

            try
            {
                Directory.CreateDirectory(NotesDirectory);
                string normalized = NoteContent.ToString().Replace("\r\n", "\n").Replace("\r", "\n");
                string fileText = normalized.Replace("\n", Environment.NewLine);
                File.WriteAllText(targetPath, fileText, Encoding.UTF8);
                ActiveNoteName = noteName;
                ActiveNotePath = targetPath;
                ActiveNoteSaved = true;
                IsDirty = false;
                NoteListDirty = true;
                PersistLastOpenedNote(ActiveNoteName);
                SetFeedbackMessage($"Saved '{noteName}'.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to save note '{targetPath}': {ex.Message}");
                SetFeedbackMessage("Failed to save note.");
            }
        }

        private static void OpenNamePrompt(NamePromptMode mode)
        {
            NoteDropdown.Close();
            ActiveOverlay = OverlayMode.None;
            OverlayHoverIndex = -1;
            SavePromptRepeater.Reset();
            SavePrompt = new SavePromptState
            {
                IsOpen = true,
                Buffer = (ActiveNoteName ?? string.Empty).Trim(),
                Mode = mode
            };
            BuildSavePromptLayout(LastContentBounds);
        }

        private static void CloseSavePrompt()
        {
            SavePrompt = default;
            SavePromptRepeater.Reset();
        }

        private static void UpdateSavePrompt(MouseState mouseState, MouseState previousMouseState, KeyboardState keyboardState, KeyboardState previousKeyboardState, double elapsedSeconds)
        {
            if (!SavePrompt.IsOpen)
            {
                SavePromptRepeater.Reset();
                return;
            }

            BuildSavePromptLayout(LastContentBounds);

            bool leftReleased = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased)
            {
                if (SavePrompt.SaveBounds.Contains(mouseState.Position))
                {
                    CommitSavePrompt();
                    return;
                }

                if (SavePrompt.CancelBounds.Contains(mouseState.Position))
                {
                    CloseSavePrompt();
                    return;
                }
            }

            if (WasKeyPressed(keyboardState, previousKeyboardState, Keys.Enter))
            {
                CommitSavePrompt();
                return;
            }

            if (WasKeyPressed(keyboardState, previousKeyboardState, Keys.Escape))
            {
                CloseSavePrompt();
                return;
            }

            HandleSavePromptInput(keyboardState, previousKeyboardState, elapsedSeconds);
        }

        private static void BuildSavePromptLayout(Rectangle contentBounds)
        {
            Rectangle viewport = contentBounds == Rectangle.Empty ? LastContentBounds : contentBounds;
            if (viewport == Rectangle.Empty)
            {
                SavePrompt.Bounds = Rectangle.Empty;
                SavePrompt.InputBounds = Rectangle.Empty;
                SavePrompt.SaveBounds = Rectangle.Empty;
                SavePrompt.CancelBounds = Rectangle.Empty;
                return;
            }

            int maxWidth = Math.Max(120, viewport.Width - (SavePromptPadding * 2));
            int maxHeight = Math.Max(140, viewport.Height - (SavePromptPadding * 2));
            int width = Math.Min(SavePromptWidth, maxWidth);
            int height = Math.Min(SavePromptHeight, maxHeight);
            int x = viewport.X + (viewport.Width - width) / 2;
            int y = viewport.Y + (viewport.Height - height) / 2;
            SavePrompt.Bounds = new Rectangle(x, y, width, height);

            UIStyle.UIFont headerFont = UIStyle.FontH2;
            UIStyle.UIFont bodyFont = UIStyle.FontBody;
            float headerHeight = headerFont.IsAvailable ? headerFont.LineHeight : SavePromptFallbackTitleHeight;
            float helperHeight = bodyFont.IsAvailable ? bodyFont.LineHeight : SavePromptFallbackHelperHeight;
            int introHeight = (int)Math.Ceiling(headerHeight + SavePromptTitleSpacing + helperHeight + SavePromptHelperSpacing);

            int inputWidth = Math.Max(60, width - (SavePromptPadding * 2));
            int inputY = y + SavePromptPadding + introHeight;
            SavePrompt.InputBounds = new Rectangle(x + SavePromptPadding, inputY, inputWidth, SavePromptInputHeight);

            int buttonsY = SavePrompt.InputBounds.Bottom + SavePromptPadding;
            int buttonWidth = Math.Max(60, (inputWidth - SavePromptButtonSpacing) / 2);
            SavePrompt.SaveBounds = new Rectangle(x + SavePromptPadding, buttonsY, buttonWidth, SavePromptButtonHeight);
            SavePrompt.CancelBounds = new Rectangle(SavePrompt.SaveBounds.Right + SavePromptButtonSpacing, buttonsY, buttonWidth, SavePromptButtonHeight);
        }

        private static void CommitSavePrompt()
        {
            string sanitized = SanitizeNoteName(SavePrompt.Buffer);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                SetFeedbackMessage("Enter a name for the note.");
                return;
            }

            string targetPath = Path.Combine(NotesDirectory, $"{sanitized}.txt");
            if (SavePrompt.Mode == NamePromptMode.Rename)
            {
                CommitRename(sanitized, targetPath);
            }
            else
            {
                CommitSave(sanitized, targetPath);
            }
        }

        private static void CommitSave(string noteName, string targetPath)
        {
            bool targetExists = File.Exists(targetPath);
            bool switchingTarget = !string.Equals(targetPath, ActiveNotePath, StringComparison.OrdinalIgnoreCase);
            if (switchingTarget && targetExists)
            {
                SetFeedbackMessage("A note with that name already exists.");
                return;
            }

            ActiveNoteName = noteName;
            ActiveNotePath = targetPath;
            CloseSavePrompt();
            PersistActiveNote(ActiveNoteName, ActiveNotePath);
        }

        private static void CommitRename(string noteName, string targetPath)
        {
            if (!HasActiveNote)
            {
                SetFeedbackMessage("Open a note before renaming.");
                return;
            }

            bool pathUnchanged = string.Equals(targetPath, ActiveNotePath, StringComparison.OrdinalIgnoreCase);
            if (!pathUnchanged && File.Exists(targetPath))
            {
                SetFeedbackMessage("A note with that name already exists.");
                return;
            }

            string previousName = ActiveNoteName;
            string previousPath = ActiveNotePath;
            CloseSavePrompt();

            try
            {
                Directory.CreateDirectory(NotesDirectory);
                if (!pathUnchanged && File.Exists(previousPath))
                {
                    File.Move(previousPath, targetPath);
                }

                ActiveNoteName = noteName;
                ActiveNotePath = targetPath;
                NoteListDirty = true;
                PersistLastOpenedNote(ActiveNoteName);

                string feedbackMessage = pathUnchanged
                    ? "Name unchanged."
                    : $"Renamed '{previousName}' to '{noteName}'.";
                SetFeedbackMessage(feedbackMessage);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to rename note '{previousPath}' to '{targetPath}': {ex.Message}");
                SetFeedbackMessage("Failed to rename note.");
            }
        }

        private static void HandleSavePromptInput(KeyboardState current, KeyboardState previous, double elapsedSeconds)
        {
            bool shift = current.IsKeyDown(Keys.LeftShift) || current.IsKeyDown(Keys.RightShift);

            foreach (Keys key in SavePromptRepeater.GetKeysWithRepeat(current, previous, elapsedSeconds))
            {
                if (key == Keys.Back)
                {
                    if (!string.IsNullOrEmpty(SavePrompt.Buffer))
                    {
                        SavePrompt.Buffer = SavePrompt.Buffer[..^1];
                    }
                }
                else if (TryConvertToNoteNameChar(key, shift, out char value))
                {
                    SavePrompt.Buffer += value;
                }
            }
        }

        private static bool TryConvertToNoteNameChar(Keys key, bool shift, out char value)
        {
            value = default;

            if (key is >= Keys.A and <= Keys.Z)
            {
                char baseChar = (char)('a' + (key - Keys.A));
                value = shift ? char.ToUpperInvariant(baseChar) : baseChar;
                return true;
            }

            if (key is >= Keys.D0 and <= Keys.D9)
            {
                value = (char)('0' + (key - Keys.D0));
                return true;
            }

            if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
            {
                value = (char)('0' + (key - Keys.NumPad0));
                return true;
            }

            if (key == Keys.Space)
            {
                value = ' ';
                return true;
            }

            if (key == Keys.OemMinus || key == Keys.Subtract)
            {
                value = shift ? '_' : '-';
                return true;
            }

            if (key == Keys.OemPeriod)
            {
                value = '.';
                return true;
            }

            return false;
        }

        private static string SanitizeNoteName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string trimmed = name.Trim();
            if (trimmed.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[..^4];
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            StringBuilder builder = new(trimmed.Length);
            foreach (char ch in trimmed)
            {
                if (invalidChars.Contains(ch))
                {
                    continue;
                }

                builder.Append(ch);
            }

            string sanitized = builder.ToString().Trim();
            while (sanitized.EndsWith(".", StringComparison.Ordinal))
            {
                sanitized = sanitized[..^1].TrimEnd();
            }

            return sanitized;
        }

        private static bool WasKeyPressed(KeyboardState current, KeyboardState previous, Keys key)
        {
            return current.IsKeyDown(key) && !previous.IsKeyDown(key);
        }

        private static void LoadNote(NoteFileEntry entry, bool announce = true)
        {
            CloseSavePrompt();
            try
            {
                string text = File.ReadAllText(entry.FullPath, Encoding.UTF8);
                text = text.Replace("\r\n", "\n").Replace("\r", "\n");
                NoteContent.Clear();
                NoteContent.Append(text);
                ActiveNoteName = entry.DisplayName;
                ActiveNotePath = entry.FullPath;
                ActiveNoteSaved = true;
                CursorIndex = NoteContent.Length;
                LineCacheDirty = true;
                IsDirty = false;
                ActiveOverlay = OverlayMode.None;
                VerticalAnchor = -1;
                PersistLastOpenedNote(entry.DisplayName);
                if (announce)
                {
                    SetFeedbackMessage($"Opened '{entry.DisplayName}'.");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to open note '{entry.FullPath}': {ex.Message}");
                SetFeedbackMessage("Failed to open note.");
            }
        }

        private static void DeleteNote(NoteFileEntry entry)
        {
            CloseSavePrompt();
            string nextNotePath = GetNextNotePath(entry.FullPath);
            try
            {
                if (File.Exists(entry.FullPath))
                {
                    File.Delete(entry.FullPath);
                }

                if (HasActiveNote && string.Equals(ActiveNotePath, entry.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    ClearActiveNoteState();
                }

                NoteListDirty = true;
                ActiveOverlay = OverlayMode.None;
                string feedbackMessage = $"Deleted '{entry.DisplayName}'.";
                if (!HasActiveNote)
                {
                    TryOpenNextAvailableNote(nextNotePath, feedbackMessage);
                }
                else
                {
                    SetFeedbackMessage(feedbackMessage);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to delete note '{entry.FullPath}': {ex.Message}");
                SetFeedbackMessage("Failed to delete note.");
            }
        }

        private static string GetNextNotePath(string referencePath)
        {
            if (NoteFiles.Count == 0)
            {
                return null;
            }

            int index = NoteFiles.FindIndex(n => string.Equals(n.FullPath, referencePath, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                if (index + 1 < NoteFiles.Count)
                {
                    return NoteFiles[index + 1].FullPath;
                }

                if (index - 1 >= 0)
                {
                    return NoteFiles[index - 1].FullPath;
                }
            }

            return NoteFiles[0].FullPath;
        }

        private static void TryOpenNextAvailableNote(string preferredPath, string prefixFeedback)
        {
            EnsureNoteList();
            if (NoteFiles.Count == 0)
            {
                ClearLastNotePreference();
                if (!string.IsNullOrWhiteSpace(prefixFeedback))
                {
                    SetFeedbackMessage(prefixFeedback);
                }

                return;
            }

            NoteFileEntry next = NoteFiles.FirstOrDefault(n => string.Equals(n.FullPath, preferredPath, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(next.DisplayName))
            {
                next = NoteFiles[0];
            }

            LoadNote(next, announce: false);
            if (!string.IsNullOrWhiteSpace(prefixFeedback))
            {
                SetFeedbackMessage($"{prefixFeedback} Opened '{next.DisplayName}'.");
            }
            else
            {
                SetFeedbackMessage($"Opened '{next.DisplayName}'.");
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

        private static void EnsureLastNoteOpen()
        {
            if (HasActiveNote || LastNoteRestored)
            {
                return;
            }

            LastNoteRestored = true;
            PreferredNoteId ??= LoadLastNoteId();
            if (string.IsNullOrWhiteSpace(PreferredNoteId) || NoteFiles.Count == 0)
            {
                return;
            }

            NoteFileEntry entry = NoteFiles.FirstOrDefault(n => string.Equals(n.DisplayName, PreferredNoteId, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(entry.DisplayName))
            {
                return;
            }

            LoadNote(entry, announce: false);
            DefaultNoteEnsured = true;
        }

        private static string LoadLastNoteId()
        {
            try
            {
                Dictionary<string, string> data = BlockDataStore.LoadRowData(DockBlockKind.Notes);
                if (data.TryGetValue(LastNoteRowKey, out string stored) && !string.IsNullOrWhiteSpace(stored))
                {
                    return stored.Trim();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load last opened note: {ex.Message}");
            }

            return null;
        }

        private static void PersistLastOpenedNote(string noteName)
        {
            if (string.IsNullOrWhiteSpace(noteName))
            {
                return;
            }

            PreferredNoteId = noteName.Trim();
            try
            {
                BlockDataStore.SetRowData(DockBlockKind.Notes, LastNoteRowKey, PreferredNoteId);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to persist last opened note: {ex.Message}");
            }
        }

        private static void ClearLastNotePreference()
        {
            PreferredNoteId = null;
            try
            {
                BlockDataStore.SetRowData(DockBlockKind.Notes, LastNoteRowKey, string.Empty);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to clear last opened note: {ex.Message}");
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

        private static void EnsureNoteDropdownOptions()
        {
            List<UIDropdown.Option> options = new(NoteFiles.Count + 1);
            foreach (NoteFileEntry entry in NoteFiles)
            {
                string label = string.IsNullOrWhiteSpace(entry.DisplayName) ? "Note" : entry.DisplayName;
                options.Add(new UIDropdown.Option(entry.DisplayName, label));
            }

            if (HasActiveNote && !options.Any(o => string.Equals(o.Id, ActiveNoteName, StringComparison.OrdinalIgnoreCase)))
            {
                options.Insert(0, new UIDropdown.Option(ActiveNoteName, ActiveNoteName));
            }

            string desired = HasActiveNote
                ? ActiveNoteName
                : (!string.IsNullOrWhiteSpace(PreferredNoteId) ? PreferredNoteId : options.FirstOrDefault().Id);
            NoteDropdown.SetOptions(options, desired);
        }

        private static void EnsureDefaultNoteOpen()
        {
            if (DefaultNoteEnsured || HasActiveNote)
            {
                return;
            }

            DefaultNoteEnsured = true;

            if (!NoteDropdown.HasOptions || NoteFiles.Count == 0)
            {
                return;
            }

            string targetId = NoteDropdown.SelectedId ?? NoteFiles[0].DisplayName;
            NoteFileEntry entry = NoteFiles.FirstOrDefault(n => string.Equals(n.DisplayName, targetId, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(entry.DisplayName) && NoteFiles.Count > 0)
            {
                entry = NoteFiles[0];
            }

            if (!string.IsNullOrWhiteSpace(entry.DisplayName))
            {
                LoadNote(entry, announce: false);
            }
        }
        private static void DrawCommandBar(SpriteBatch spriteBatch, UIStyle.UIFont font, bool blockLocked)
        {
            FillRect(spriteBatch, CommandBarBounds, UIStyle.DragBarBackground);
            DrawRect(spriteBatch, CommandBarBounds, UIStyle.BlockBorder);

            DrawNoteDropdown(spriteBatch, font, blockLocked);
            for (int i = 0; i < CommandOrder.Length; i++)
            {
                DrawButton(spriteBatch, font, CommandOrder[i], CommandBounds[i], blockLocked);
            }
        }

        private static void DrawButton(SpriteBatch spriteBatch, UIStyle.UIFont font, NotesCommand command, Rectangle bounds, bool blockLocked)
        {
            bool disabled = blockLocked || IsButtonDisabled(command);
            bool hovered = !blockLocked && UIButtonRenderer.IsHovered(bounds, LastMouseState.Position);
            bool isActiveOverlay = command switch
            {
                NotesCommand.Delete => ActiveOverlay == OverlayMode.Delete,
                _ => false
            };

            UIButtonRenderer.ButtonStyle style = isActiveOverlay ? UIButtonRenderer.ButtonStyle.Blue : UIButtonRenderer.ButtonStyle.Grey;
            Texture2D icon = GetCommandIcon(command);
            if (icon != null && !icon.IsDisposed)
            {
                UIButtonRenderer.DrawIcon(spriteBatch, bounds, icon, style, hovered, disabled);
            }
            else
            {
                UIButtonRenderer.Draw(spriteBatch, bounds, command.ToString(), style, hovered, disabled);
            }
        }

        private static Texture2D GetCommandIcon(NotesCommand command)
        {
            return command switch
            {
                NotesCommand.New => IconNew,
                NotesCommand.Save => IconSave,
                NotesCommand.Rename => IconRename,
                NotesCommand.Delete => IconDelete,
                _ => null
            };
        }

        private static void DrawNoteDropdown(SpriteBatch spriteBatch, UIStyle.UIFont font, bool blockLocked)
        {
            if (NoteDropdownBounds == Rectangle.Empty || spriteBatch == null)
            {
                return;
            }

            if (NoteDropdown.HasOptions)
            {
                NoteDropdown.Draw(spriteBatch, drawOptions: false);
            }
            else
            {
                FillRect(spriteBatch, NoteDropdownBounds, UIStyle.BlockBackground);
                DrawRect(spriteBatch, NoteDropdownBounds, UIStyle.BlockBorder);
                string placeholder = "No saved notes";
                Vector2 size = font.MeasureString(placeholder);
                Vector2 pos = new(NoteDropdownBounds.X + 8, NoteDropdownBounds.Y + (NoteDropdownBounds.Height - size.Y) / 2f);
                font.DrawString(spriteBatch, placeholder, pos, UIStyle.MutedTextColor);
            }

            if (blockLocked)
            {
                FillRect(spriteBatch, NoteDropdownBounds, UIStyle.BlockBackground * 0.45f);
            }
        }

        private static void DrawFeedbackBar(SpriteBatch spriteBatch, UIStyle.UIFont font)
        {
            if (!TryGetFeedbackLabel(out string message, out Color color))
            {
                return;
            }

            BlockFeedbackBarRenderer.Draw(spriteBatch, FeedbackBarBounds, font, message, color);
        }

        private static bool TryGetFeedbackLabel(out string message, out Color color)
        {
            bool hasFeedback = FeedbackSecondsRemaining > 0f && !string.IsNullOrWhiteSpace(FeedbackMessage);
            if (hasFeedback)
            {
                message = FeedbackMessage;
                color = UIStyle.TextColor;
                return true;
            }

            if (HasActiveNote)
            {
                message = $"Active: {ActiveNoteName}{(IsDirty ? " *" : string.Empty)}";
                color = UIStyle.TextColor;
                return true;
            }

            message = "No note selected";
            color = UIStyle.MutedTextColor;
            return true;
        }

        private static void DrawOverlayList(SpriteBatch spriteBatch, UIStyle.UIFont headerFont, UIStyle.UIFont rowFont, bool blockLocked)
        {
            if (blockLocked || OverlayBounds == Rectangle.Empty)
            {
                return;
            }

            FillRect(spriteBatch, OverlayBounds, UIStyle.BlockBackground);
            DrawRect(spriteBatch, OverlayBounds, UIStyle.BlockBorder);

            string header = "Select a note to delete";
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

        private static void DrawSavePrompt(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (!SavePrompt.IsOpen || spriteBatch == null)
            {
                return;
            }

            EnsurePixel(spriteBatch);
            Rectangle viewport = contentBounds == Rectangle.Empty ? LastContentBounds : contentBounds;
            if (viewport == Rectangle.Empty)
            {
                return;
            }

            BuildSavePromptLayout(viewport);
            FillRect(spriteBatch, viewport, ColorPalette.RebindScrim);

            Rectangle dialog = SavePrompt.Bounds;
            if (dialog == Rectangle.Empty)
            {
                return;
            }

            FillRect(spriteBatch, dialog, UIStyle.BlockBackground);
            DrawRect(spriteBatch, dialog, UIStyle.BlockBorder);

            UIStyle.UIFont headerFont = UIStyle.FontH2;
            UIStyle.UIFont bodyFont = UIStyle.FontBody;
            UIStyle.UIFont inputFont = UIStyle.FontTech;
            if (!headerFont.IsAvailable || !bodyFont.IsAvailable || !inputFont.IsAvailable)
            {
                return;
            }

            string title = SavePrompt.Mode == NamePromptMode.Rename ? "Rename note" : "Save note";
            Vector2 titleSize = headerFont.MeasureString(title);
            Vector2 titlePos = new(dialog.X + (dialog.Width - titleSize.X) / 2f, dialog.Y + SavePromptPadding);
            headerFont.DrawString(spriteBatch, title, titlePos, UIStyle.TextColor);

            string helper = SavePrompt.Mode == NamePromptMode.Rename
                ? "Choose a new name for this note."
                : "Name this note to save it.";
            Vector2 helperPos = new(dialog.X + SavePromptPadding, titlePos.Y + titleSize.Y + SavePromptTitleSpacing);
            bodyFont.DrawString(spriteBatch, helper, helperPos, UIStyle.MutedTextColor);

            Rectangle input = SavePrompt.InputBounds;
            FillRect(spriteBatch, input, UIStyle.BlockBackground * 1.1f);
            DrawRect(spriteBatch, input, UIStyle.BlockBorder);

            string text = string.IsNullOrWhiteSpace(SavePrompt.Buffer) ? "Note name" : SavePrompt.Buffer;
            Color textColor = string.IsNullOrWhiteSpace(SavePrompt.Buffer) ? UIStyle.MutedTextColor : UIStyle.TextColor;
            Vector2 textSize = TextSpacingHelper.MeasureWithWideSpaces(inputFont, text);
            Vector2 textPos = new(input.X + 8, input.Y + (input.Height - textSize.Y) / 2f);
            TextSpacingHelper.DrawWithWideSpaces(inputFont, spriteBatch, text, textPos, textColor);

            bool saveHovered = UIButtonRenderer.IsHovered(SavePrompt.SaveBounds, LastMouseState.Position);
            bool cancelHovered = UIButtonRenderer.IsHovered(SavePrompt.CancelBounds, LastMouseState.Position);
            bool disableSave = string.IsNullOrWhiteSpace(SavePrompt.Buffer);
            string confirmLabel = SavePrompt.Mode == NamePromptMode.Rename ? "Rename" : "Save";
            UIButtonRenderer.Draw(spriteBatch, SavePrompt.SaveBounds, confirmLabel, UIButtonRenderer.ButtonStyle.Blue, saveHovered, disableSave);
            UIButtonRenderer.Draw(spriteBatch, SavePrompt.CancelBounds, "Cancel", UIButtonRenderer.ButtonStyle.Grey, cancelHovered);
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
                    TextSpacingHelper.DrawWithWideSpaces(font, spriteBatch, layout.Text, position, UIStyle.TextColor);
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
            float offsetX = TextSpacingHelper.MeasureWithWideSpaces(font, prefix).X;

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
            Vector2 size = TextSpacingHelper.MeasureWithWideSpaces(font, placeholder);
            Vector2 position = new(
                TextViewportBounds.X + (TextViewportBounds.Width - size.X) / 2f,
                TextViewportBounds.Y + (TextViewportBounds.Height - size.Y) / 2f);
            TextSpacingHelper.DrawWithWideSpaces(font, spriteBatch, placeholder, position, UIStyle.MutedTextColor);
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

        private struct SavePromptState
        {
            public bool IsOpen;
            public string Buffer;
            public NamePromptMode Mode;
            public Rectangle Bounds;
            public Rectangle InputBounds;
            public Rectangle SaveBounds;
            public Rectangle CancelBounds;
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
