using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io;
using op.io.UI.BlockScripts.BlockUtilities;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class DockingSetupsBlock
    {
        public const string BlockTitle = "Docking Setups";
        public const int MinWidth = 30;
        public const int MinHeight = 0;

        private enum OverlayMode
        {
            None,
            Delete
        }

        private enum DockingCommand
        {
            Save,
            New,
            Rename,
            Delete
        }

        private enum NamePromptMode
        {
            New,
            Rename
        }

        private const int CommandBarHeight = 36;
        private const int ButtonHeight = BlockButtonRowLayout.DefaultButtonHeight;
        private const int ButtonWidth = BlockButtonRowLayout.DefaultButtonWidth;
        private const int ButtonSpacing = BlockButtonRowLayout.DefaultButtonSpacing;
        private const int ContentSpacing = 12;
        private const int OverlayPadding = 10;
        private const int OverlayHeaderHeight = 28;
        private const int OverlayRowHeight = 28;
        private const double FeedbackDurationSeconds = 4.0;
        private const int PromptWidth = 360;
        private const int PromptHeight = 180;
        private const int PromptPadding = 16;
        private const int PromptInputHeight = 34;
        private const int PromptInputHorizontalPadding = 8;
        private const float PromptInputTextNudge = 2f;
        private const int PromptButtonHeight = 28;
        private const int PromptButtonSpacing = 10;
        private const float PromptTitleSpacing = 6f;
        private const float PromptHelperSpacing = 10f;
        private const float PromptFallbackTitleHeight = 26f;
        private const float PromptFallbackHelperHeight = 20f;
        private const string ActiveSetupRowKey = "__ActiveSetup";
        private const string PromptFocusOwner = "DockingSetupsBlock.Prompt";

        private static readonly DockingCommand[] CommandOrder = new[]
        {
            DockingCommand.Save,
            DockingCommand.New,
            DockingCommand.Rename,
            DockingCommand.Delete
        };

        private static readonly Rectangle[] CommandBounds = new Rectangle[CommandOrder.Length];
        private static readonly UIDropdown SetupDropdown = new();
        private static readonly List<SetupEntry> SetupEntries = new();
        private static readonly KeyRepeatTracker PromptRepeater = new();

        private static Rectangle CommandBarBounds;
        private static Rectangle FeedbackBarBounds;
        private static Rectangle DropdownBounds;
        private static Rectangle OverlayBounds;
        private static Rectangle ContentBounds;
        private static Rectangle LastContentBounds;

        private static OverlayMode ActiveOverlay = OverlayMode.None;
        private static int OverlayHoverIndex = -1;
        private static bool SetupListDirty = true;
        private static bool SetupSelectionInitialized;
        private static string SelectedSetupName;

        private static double FeedbackSecondsRemaining;
        private static string FeedbackMessage;

        private static PromptState NamePrompt;
        private static KeyboardState PreviousKeyboardState;
        private static MouseState LastMouseState;

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
            EnsureSetupList();
            EnsureSelectionInitialized();
            UpdateLayout(contentBounds);
            LastContentBounds = contentBounds;
            EnsureDropdownOptions();
            SetupDropdown.Bounds = DropdownBounds;

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.DockingSetups);
            if (blockLocked && ActiveOverlay != OverlayMode.None)
            {
                ActiveOverlay = OverlayMode.None;
                OverlayHoverIndex = -1;
            }

            if (blockLocked && NamePrompt.IsOpen)
            {
                ClosePrompt();
            }

            KeyboardState keyboardState = Keyboard.GetState();
            if (blockLocked)
            {
                SetupDropdown.Close();
            }

            LastMouseState = mouseState;

            if (NamePrompt.IsOpen)
            {
                UpdatePrompt(mouseState, previousMouseState, keyboardState, PreviousKeyboardState, elapsedSeconds);
                FocusModeManager.SetFocusActive(PromptFocusOwner, NamePrompt.IsOpen);
                UpdateFeedbackTimer(gameTime);
                PreviousKeyboardState = keyboardState;
                return;
            }
            else
            {
                FocusModeManager.SetFocusActive(PromptFocusOwner, false);
            }

            bool leftClickStarted = mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            Point mousePoint = mouseState.Position;

            if (!blockLocked)
            {
                bool dropdownChanged = SetupDropdown.Update(mouseState, previousMouseState, keyboardState, PreviousKeyboardState, out string selectedSetup, isDisabled: blockLocked);
                if (dropdownChanged && !string.IsNullOrWhiteSpace(selectedSetup))
                {
                    if (SelectSetup(selectedSetup, applyLayout: true))
                    {
                        PreviousKeyboardState = keyboardState;
                        return;
                    }
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

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.DockingSetups);
            if (blockLocked && ActiveOverlay != OverlayMode.None)
            {
                ActiveOverlay = OverlayMode.None;
                OverlayHoverIndex = -1;
            }

            EnsurePixel(spriteBatch);
            EnsureSetupList();
            EnsureCommandIcons();
            UpdateLayout(contentBounds);
            EnsureDropdownOptions();
            SetupDropdown.Bounds = DropdownBounds;

            DrawCommandBar(spriteBatch, labelFont, blockLocked);
            DrawFeedbackBar(spriteBatch, feedbackFont);

            if (!blockLocked && ActiveOverlay != OverlayMode.None)
            {
                DrawOverlayList(spriteBatch, headerFont, labelFont, blockLocked);
            }

            DrawPlaceholder(spriteBatch, placeholderFont);
            SetupDropdown.DrawOptionsOverlay(spriteBatch);
            DrawPrompt(spriteBatch, contentBounds);
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
            DropdownBounds = dropdownWidth > 0
                ? new Rectangle(dropdownX, dropdownY, dropdownWidth, dropdownHeight)
                : Rectangle.Empty;

            Array.Fill(CommandBounds, Rectangle.Empty);
            int buttonX = dropdownX + (dropdownWidth > 0 ? dropdownWidth + ButtonSpacing : 0);
            Rectangle buttonRow = new(buttonX, CommandBarBounds.Y, Math.Max(0, CommandBarBounds.Right - buttonX), CommandBarHeight);
            IReadOnlyList<Rectangle> buttons = BlockButtonRowLayout.BuildUniformRow(
                buttonRow,
                CommandOrder.Length,
                ButtonWidth,
                ButtonHeight,
                ButtonSpacing);

            int applyCount = Math.Min(CommandBounds.Length, buttons.Count);
            for (int i = 0; i < applyCount; i++)
            {
                CommandBounds[i] = buttons[i];
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

            int contentY = afterFeedbackBarY + overlaySpace;
            int contentHeight = Math.Max(0, contentBounds.Bottom - contentY);
            ContentBounds = new Rectangle(contentBounds.X, contentY, contentBounds.Width, contentHeight);
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

                DockingCommand command = CommandOrder[i];
                if (IsButtonDisabled(command))
                {
                    HandleDisabledCommand(command);
                    return true;
                }

                switch (command)
                {
                    case DockingCommand.Save:
                        SaveSelectedSetup();
                        break;
                    case DockingCommand.New:
                        BeginNewSetup();
                        break;
                    case DockingCommand.Rename:
                        BeginRenameSetup();
                        break;
                    case DockingCommand.Delete:
                        ToggleOverlay(OverlayMode.Delete);
                        break;
                }

                return true;
            }

            return false;
        }

        private static void HandleDisabledCommand(DockingCommand command)
        {
            switch (command)
            {
                case DockingCommand.Save:
                    SetFeedbackMessage("Create a setup before saving.");
                    break;
                case DockingCommand.Rename:
                    SetFeedbackMessage("Select a setup to rename.");
                    break;
                case DockingCommand.Delete:
                    SetFeedbackMessage("No saved setups to delete.");
                    break;
            }
        }

        private static void ToggleOverlay(OverlayMode mode)
        {
            ActiveOverlay = ActiveOverlay == mode ? OverlayMode.None : mode;
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
                if (index >= 0 && index < SetupEntries.Count)
                {
                    OverlayHoverIndex = index;
                    if (leftClickStarted)
                    {
                        SetupEntry entry = SetupEntries[index];
                        if (ActiveOverlay == OverlayMode.Delete)
                        {
                            DeleteSetup(entry.Name);
                        }
                    }
                }
            }
            else if (leftClickStarted && !commandHandled)
            {
                ActiveOverlay = OverlayMode.None;
            }
        }

        private static void DrawCommandBar(SpriteBatch spriteBatch, UIStyle.UIFont font, bool blockLocked)
        {
            BlockToolbarRenderer.Draw(spriteBatch, PixelTexture, CommandBarBounds);

            DrawSetupDropdown(spriteBatch, font, blockLocked);
            for (int i = 0; i < CommandOrder.Length; i++)
            {
                DrawButton(spriteBatch, font, CommandOrder[i], CommandBounds[i], blockLocked);
            }
        }

        private static void DrawButton(SpriteBatch spriteBatch, UIStyle.UIFont font, DockingCommand command, Rectangle bounds, bool blockLocked)
        {
            bool disabled = blockLocked || IsButtonDisabled(command);
            bool hovered = !blockLocked && UIButtonRenderer.IsHovered(bounds, LastMouseState.Position);
            bool isActiveOverlay = command == DockingCommand.Delete && ActiveOverlay == OverlayMode.Delete;

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

        private static Texture2D GetCommandIcon(DockingCommand command)
        {
            return command switch
            {
                DockingCommand.New => IconNew,
                DockingCommand.Save => IconSave,
                DockingCommand.Rename => IconRename,
                DockingCommand.Delete => IconDelete,
                _ => null
            };
        }

        private static void DrawSetupDropdown(SpriteBatch spriteBatch, UIStyle.UIFont font, bool blockLocked)
        {
            if (DropdownBounds == Rectangle.Empty || spriteBatch == null)
            {
                return;
            }

            BlockToolbarRenderer.DrawDropdown(
                spriteBatch,
                PixelTexture,
                SetupDropdown,
                DropdownBounds,
                font,
                blockLocked,
                "No saved setups");
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

            if (!string.IsNullOrWhiteSpace(SelectedSetupName))
            {
                message = $"Active: {SelectedSetupName}";
                color = UIStyle.TextColor;
                return true;
            }

            message = "No docking setup selected";
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

            string header = "Select a setup to delete";
            Vector2 headerSize = headerFont.MeasureString(header);
            Vector2 headerPosition = new(OverlayBounds.X + OverlayPadding, OverlayBounds.Y + (OverlayHeaderHeight - headerSize.Y) / 2f);
            headerFont.DrawString(spriteBatch, header, headerPosition, UIStyle.TextColor);

            Rectangle rowsBounds = new(
                OverlayBounds.X + OverlayPadding,
                OverlayBounds.Y + OverlayHeaderHeight,
                OverlayBounds.Width - (OverlayPadding * 2),
                Math.Max(0, OverlayBounds.Height - OverlayHeaderHeight - OverlayPadding));

            if (SetupEntries.Count == 0)
            {
                string empty = "No saved setups.";
                Vector2 emptySize = rowFont.MeasureString(empty);
                Vector2 emptyPosition = new(rowsBounds.X, rowsBounds.Y + 8);
                rowFont.DrawString(spriteBatch, empty, emptyPosition, UIStyle.MutedTextColor);
                return;
            }

            int rowY = rowsBounds.Y;
            int maxRows = rowsBounds.Height / OverlayRowHeight;
            for (int i = 0; i < SetupEntries.Count && i < maxRows; i++)
            {
                Rectangle rowRect = new(rowsBounds.X, rowY, rowsBounds.Width, OverlayRowHeight - 2);
                bool hovered = i == OverlayHoverIndex;
                Color rowColor = hovered ? UIStyle.AccentMuted : UIStyle.BlockBackground;
                FillRect(spriteBatch, rowRect, rowColor);

                SetupEntry entry = SetupEntries[i];
                Vector2 labelPos = new(rowRect.X + 6, rowRect.Y + 4);
                rowFont.DrawString(spriteBatch, entry.Name, labelPos, UIStyle.TextColor);

                rowY += OverlayRowHeight;
            }
        }

        private static void DrawPlaceholder(SpriteBatch spriteBatch, UIStyle.UIFont font)
        {
            if (ContentBounds == Rectangle.Empty)
            {
                return;
            }

            FillRect(spriteBatch, ContentBounds, UIStyle.BlockBackground * 0.9f);
            DrawRect(spriteBatch, ContentBounds, UIStyle.BlockBorder);

            string placeholder = SetupEntries.Count == 0
                ? "Save a docking setup"
                : "Select a docking setup";
            Vector2 size = TextSpacingHelper.MeasureWithWideSpaces(font, placeholder);
            Vector2 position = new(
                ContentBounds.X + (ContentBounds.Width - size.X) / 2f,
                ContentBounds.Y + (ContentBounds.Height - size.Y) / 2f);
            TextSpacingHelper.DrawWithWideSpaces(font, spriteBatch, placeholder, position, UIStyle.MutedTextColor);
        }
        private static void DrawPrompt(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (!NamePrompt.IsOpen || spriteBatch == null)
            {
                return;
            }

            EnsurePixel(spriteBatch);
            Rectangle viewport = contentBounds == Rectangle.Empty ? LastContentBounds : contentBounds;
            if (viewport == Rectangle.Empty)
            {
                return;
            }

            BuildPromptLayout(viewport);
            FillRect(spriteBatch, viewport, ColorPalette.RebindScrim);

            Rectangle dialog = NamePrompt.Bounds;
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

            string title = NamePrompt.Mode == NamePromptMode.Rename ? "Rename setup" : "New setup";
            Vector2 titleSize = headerFont.MeasureString(title);
            Vector2 titlePos = new(dialog.X + (dialog.Width - titleSize.X) / 2f, dialog.Y + PromptPadding);
            headerFont.DrawString(spriteBatch, title, titlePos, UIStyle.TextColor);

            string helper = NamePrompt.Mode == NamePromptMode.Rename
                ? "Choose a new name for this setup."
                : "Name this setup to save it.";
            Vector2 helperPos = new(dialog.X + PromptPadding, titlePos.Y + titleSize.Y + PromptTitleSpacing);
            bodyFont.DrawString(spriteBatch, helper, helperPos, UIStyle.MutedTextColor);

            Rectangle input = NamePrompt.InputBounds;
            FillRect(spriteBatch, input, UIStyle.BlockBackground * 1.1f);
            DrawRect(spriteBatch, input, UIStyle.BlockBorder);

            string text = string.IsNullOrWhiteSpace(NamePrompt.Buffer) ? "Setup name" : NamePrompt.Buffer;
            Color textColor = string.IsNullOrWhiteSpace(NamePrompt.Buffer) ? UIStyle.MutedTextColor : UIStyle.TextColor;
            Vector2 textSize = TextSpacingHelper.MeasureWithWideSpaces(inputFont, text);
            float textX = input.X + PromptInputHorizontalPadding;
            float textY = input.Y + (input.Height - textSize.Y) / 2f - PromptInputTextNudge;
            Vector2 textPos = new(textX, textY);
            TextSpacingHelper.DrawWithWideSpaces(inputFont, spriteBatch, text, textPos, textColor);

            bool saveHovered = UIButtonRenderer.IsHovered(NamePrompt.ConfirmBounds, LastMouseState.Position);
            bool cancelHovered = UIButtonRenderer.IsHovered(NamePrompt.CancelBounds, LastMouseState.Position);
            bool disableSave = string.IsNullOrWhiteSpace(NamePrompt.Buffer);
            string confirmLabel = NamePrompt.Mode == NamePromptMode.Rename ? "Rename" : "Save";
            UIButtonRenderer.Draw(spriteBatch, NamePrompt.ConfirmBounds, confirmLabel, UIButtonRenderer.ButtonStyle.Blue, saveHovered, disableSave);
            UIButtonRenderer.Draw(spriteBatch, NamePrompt.CancelBounds, "Cancel", UIButtonRenderer.ButtonStyle.Grey, cancelHovered);
        }

        private static void BuildPromptLayout(Rectangle viewport)
        {
            int width = Math.Min(PromptWidth, Math.Max(120, viewport.Width - (PromptPadding * 2)));
            int height = Math.Min(PromptHeight, Math.Max(120, viewport.Height - (PromptPadding * 2)));

            int x = viewport.X + (viewport.Width - width) / 2;
            int y = viewport.Y + (viewport.Height - height) / 2;
            NamePrompt.Bounds = new Rectangle(x, y, width, height);

            int inputWidth = width - (PromptPadding * 2);
            int inputHeight = PromptInputHeight;
            string title = NamePrompt.Mode == NamePromptMode.Rename ? "Rename setup" : "New setup";
            string helper = NamePrompt.Mode == NamePromptMode.Rename
                ? "Choose a new name for this setup."
                : "Name this setup to save it.";
            UIStyle.UIFont headerFont = UIStyle.FontH2;
            UIStyle.UIFont bodyFont = UIStyle.FontBody;
            float titleHeight = PromptFallbackTitleHeight;
            float helperHeight = PromptFallbackHelperHeight;
            if (headerFont.IsAvailable)
            {
                titleHeight = MathF.Max(headerFont.MeasureString(title).Y, PromptFallbackTitleHeight);
            }
            if (bodyFont.IsAvailable)
            {
                helperHeight = MathF.Max(bodyFont.MeasureString(helper).Y, PromptFallbackHelperHeight);
            }

            float introHeight = titleHeight + PromptTitleSpacing + helperHeight + PromptHelperSpacing;
            int inputY = y + PromptPadding + (int)MathF.Ceiling(introHeight);
            NamePrompt.InputBounds = new Rectangle(x + PromptPadding, inputY, inputWidth, inputHeight);

            int buttonsY = NamePrompt.InputBounds.Bottom + PromptPadding;
            int buttonWidth = Math.Max(60, (inputWidth - PromptButtonSpacing) / 2);
            NamePrompt.ConfirmBounds = new Rectangle(x + PromptPadding, buttonsY, buttonWidth, PromptButtonHeight);
            NamePrompt.CancelBounds = new Rectangle(NamePrompt.ConfirmBounds.Right + PromptButtonSpacing, buttonsY, buttonWidth, PromptButtonHeight);
        }

        private static void UpdatePrompt(MouseState mouseState, MouseState previousMouseState, KeyboardState keyboardState, KeyboardState previousKeyboardState, double elapsedSeconds)
        {
            if (!NamePrompt.IsOpen)
            {
                PromptRepeater.Reset();
                return;
            }

            BuildPromptLayout(LastContentBounds);

            bool leftReleased = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;

            if (leftReleased)
            {
                if (NamePrompt.ConfirmBounds.Contains(mouseState.Position))
                {
                    CommitPrompt();
                    return;
                }

                if (NamePrompt.CancelBounds.Contains(mouseState.Position))
                {
                    ClosePrompt();
                    return;
                }
            }

            if (WasKeyPressed(keyboardState, previousKeyboardState, Keys.Enter))
            {
                CommitPrompt();
                return;
            }

            if (WasKeyPressed(keyboardState, previousKeyboardState, Keys.Escape))
            {
                ClosePrompt();
                return;
            }

            HandlePromptInput(keyboardState, previousKeyboardState, elapsedSeconds);
        }

        private static void HandlePromptInput(KeyboardState current, KeyboardState previous, double elapsedSeconds)
        {
            bool shift = current.IsKeyDown(Keys.LeftShift) || current.IsKeyDown(Keys.RightShift);

            foreach (Keys key in PromptRepeater.GetKeysWithRepeat(current, previous, elapsedSeconds))
            {
                if (key == Keys.Back)
                {
                    if (!string.IsNullOrEmpty(NamePrompt.Buffer))
                    {
                        NamePrompt.Buffer = NamePrompt.Buffer[..^1];
                    }
                }
                else if (TryConvertToPromptChar(key, shift, out char value))
                {
                    NamePrompt.Buffer += value;
                }
            }
        }

        private static bool TryConvertToPromptChar(Keys key, bool shift, out char value)
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

        private static void CommitPrompt()
        {
            string name = NamePrompt.Buffer?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                SetFeedbackMessage("Enter a setup name.");
                return;
            }

            if (NamePrompt.Mode == NamePromptMode.Rename)
            {
                if (!TryRenameSetup(name))
                {
                    return;
                }
            }
            else
            {
                if (!TryCreateSetup(name))
                {
                    return;
                }
            }

            ClosePrompt();
        }

        private static void OpenPrompt(NamePromptMode mode, string initialValue = null)
        {
            ActiveOverlay = OverlayMode.None;
            SetupDropdown.Close();
            PromptRepeater.Reset();
            NamePrompt = new PromptState
            {
                IsOpen = true,
                Buffer = initialValue ?? string.Empty,
                Mode = mode
            };
            BuildPromptLayout(LastContentBounds);
            FocusModeManager.SetFocusActive(PromptFocusOwner, true);
        }

        private static void ClosePrompt()
        {
            NamePrompt = default;
            PromptRepeater.Reset();
            FocusModeManager.SetFocusActive(PromptFocusOwner, false);
        }

        private static void SaveSelectedSetup()
        {
            if (string.IsNullOrWhiteSpace(SelectedSetupName))
            {
                BeginNewSetup();
                return;
            }

            string payload = BlockManager.CaptureDockingSetup();
            if (string.IsNullOrWhiteSpace(payload))
            {
                SetFeedbackMessage("Failed to capture the current layout.");
                return;
            }

            BlockDataStore.SetRowData(DockBlockKind.DockingSetups, SelectedSetupName, payload);
            SetupListDirty = true;
            PersistSelectedSetupName(SelectedSetupName);
            SetFeedbackMessage($"Saved '{SelectedSetupName}'.");
        }

        private static void BeginNewSetup()
        {
            OpenPrompt(NamePromptMode.New);
        }

        private static void BeginRenameSetup()
        {
            if (string.IsNullOrWhiteSpace(SelectedSetupName))
            {
                SetFeedbackMessage("Select a setup to rename.");
                return;
            }

            OpenPrompt(NamePromptMode.Rename, SelectedSetupName);
        }

        private static bool TryCreateSetup(string name)
        {
            if (SetupEntries.Any(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                SetFeedbackMessage("That name already exists.");
                return false;
            }

            string payload = BlockManager.CaptureDockingSetup();
            if (string.IsNullOrWhiteSpace(payload))
            {
                SetFeedbackMessage("Failed to capture the current layout.");
                return false;
            }

            BlockDataStore.SetRowData(DockBlockKind.DockingSetups, name, payload);
            SetupListDirty = true;
            SelectedSetupName = name;
            PersistSelectedSetupName(SelectedSetupName);
            SetFeedbackMessage($"Saved '{name}'.");
            return true;
        }

        private static bool TryRenameSetup(string newName)
        {
            if (string.IsNullOrWhiteSpace(SelectedSetupName))
            {
                SetFeedbackMessage("Select a setup to rename.");
                return false;
            }

            if (string.Equals(SelectedSetupName, newName, StringComparison.OrdinalIgnoreCase))
            {
                SetFeedbackMessage("Choose a different name.");
                return false;
            }

            if (SetupEntries.Any(entry => string.Equals(entry.Name, newName, StringComparison.OrdinalIgnoreCase)))
            {
                SetFeedbackMessage("That name already exists.");
                return false;
            }

            SetupEntry current = SetupEntries.FirstOrDefault(entry => string.Equals(entry.Name, SelectedSetupName, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(current.Name))
            {
                SetFeedbackMessage("Unable to find that setup.");
                return false;
            }

            BlockDataStore.SetRowData(DockBlockKind.DockingSetups, newName, current.Payload ?? string.Empty);
            BlockDataStore.DeleteRows(DockBlockKind.DockingSetups, new[] { SelectedSetupName });
            SelectedSetupName = newName;
            PersistSelectedSetupName(SelectedSetupName);
            SetupListDirty = true;
            SetFeedbackMessage($"Renamed to '{newName}'.");
            return true;
        }

        private static void DeleteSetup(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            BlockDataStore.DeleteRows(DockBlockKind.DockingSetups, new[] { name });
            SetupListDirty = true;
            ActiveOverlay = OverlayMode.None;

            if (string.Equals(SelectedSetupName, name, StringComparison.OrdinalIgnoreCase))
            {
                SelectedSetupName = null;
                PersistSelectedSetupName(string.Empty);
                EnsureSetupList();
                EnsureDropdownOptions();
                string next = SetupEntries.FirstOrDefault().Name;
                if (!string.IsNullOrWhiteSpace(next))
                {
                    SelectSetup(next, applyLayout: true);
                }
                else
                {
                    SetFeedbackMessage("Deleted setup.");
                }
                return;
            }

            SetFeedbackMessage($"Deleted '{name}'.");
        }

        private static bool SelectSetup(string name, bool applyLayout)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            SetupEntry entry = SetupEntries.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                return false;
            }

            SelectedSetupName = entry.Name;
            PersistSelectedSetupName(SelectedSetupName);

            if (!applyLayout)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(entry.Payload))
            {
                SetFeedbackMessage("Selected setup has no data.");
                return false;
            }

            if (!BlockManager.QueueDockingSetupApply(entry.Payload))
            {
                SetFeedbackMessage("Failed to load the selected setup.");
                return false;
            }

            SetupListDirty = true;
            SetFeedbackMessage($"Loaded '{SelectedSetupName}'.");
            return true;
        }

        public static bool TryApplyNextSetup(bool allowWhileLocked = false) => TryApplyAdjacentSetup(1, allowWhileLocked);

        public static bool TryApplyPreviousSetup(bool allowWhileLocked = false) => TryApplyAdjacentSetup(-1, allowWhileLocked);

        private static bool TryApplyAdjacentSetup(int direction, bool allowWhileLocked)
        {
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.DockingSetups);
            if (blockLocked && !allowWhileLocked)
            {
                SetFeedbackMessage("Docking setups locked.");
                return false;
            }

            EnsureSetupList();
            EnsureSelectionInitialized();

            if (SetupEntries.Count == 0)
            {
                SetFeedbackMessage("No saved setups.");
                return false;
            }

            int currentIndex = SetupEntries.FindIndex(entry => string.Equals(entry.Name, SelectedSetupName, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                currentIndex = 0;
                SelectedSetupName = SetupEntries[currentIndex].Name;
            }

            int targetIndex = (currentIndex + direction) % SetupEntries.Count;
            if (targetIndex < 0)
            {
                targetIndex += SetupEntries.Count;
            }

            string targetName = SetupEntries[targetIndex].Name;
            return SelectSetup(targetName, applyLayout: true);
        }

        private static void EnsureSetupList()
        {
            if (!SetupListDirty)
            {
                return;
            }

            SetupEntries.Clear();
            Dictionary<string, string> data = BlockDataStore.LoadRowData(DockBlockKind.DockingSetups);
            foreach (KeyValuePair<string, string> pair in data)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                if (pair.Key.StartsWith("__", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SetupEntries.Add(new SetupEntry(pair.Key, pair.Value));
            }

            SetupEntries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            SetupListDirty = false;
        }

        private static void EnsureSelectionInitialized()
        {
            if (SetupSelectionInitialized)
            {
                return;
            }

            SetupSelectionInitialized = true;
            string stored = LoadPreferredSetupName();
            if (!string.IsNullOrWhiteSpace(stored))
            {
                SelectedSetupName = stored;
                SetupListDirty = true;
            }
        }

        private static void EnsureDropdownOptions()
        {
            IEnumerable<UIDropdown.Option> options = SetupEntries.Select(entry => new UIDropdown.Option(entry.Name, entry.Name));
            string desired = !string.IsNullOrWhiteSpace(SelectedSetupName) ? SelectedSetupName : SetupEntries.FirstOrDefault().Name;
            SetupDropdown.SetOptions(options, desired);

            if (SetupDropdown.HasOptions)
            {
                SelectedSetupName = SetupDropdown.SelectedId ?? SelectedSetupName;
            }
        }

        private static string LoadPreferredSetupName()
        {
            try
            {
                Dictionary<string, string> data = BlockDataStore.LoadRowData(DockBlockKind.DockingSetups);
                if (data.TryGetValue(ActiveSetupRowKey, out string stored))
                {
                    return string.IsNullOrWhiteSpace(stored) ? null : stored.Trim();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load active docking setup: {ex.Message}");
            }

            return null;
        }

        private static void PersistSelectedSetupName(string name)
        {
            try
            {
                BlockDataStore.SetRowData(DockBlockKind.DockingSetups, ActiveSetupRowKey, name ?? string.Empty);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to persist active docking setup: {ex.Message}");
            }
        }

        private static bool IsButtonDisabled(DockingCommand command)
        {
            return command switch
            {
                DockingCommand.Save => SetupEntries.Count == 0 && string.IsNullOrWhiteSpace(SelectedSetupName),
                DockingCommand.Rename => string.IsNullOrWhiteSpace(SelectedSetupName),
                DockingCommand.Delete => SetupEntries.Count == 0,
                _ => false
            };
        }

        private static void UpdateFeedbackTimer(GameTime gameTime)
        {
            if (gameTime == null || FeedbackSecondsRemaining <= 0f)
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

        private static bool WasKeyPressed(KeyboardState current, KeyboardState previous, Keys key) =>
            current.IsKeyDown(key) && previous.IsKeyUp(key);

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

        private struct PromptState
        {
            public bool IsOpen;
            public string Buffer;
            public NamePromptMode Mode;
            public Rectangle Bounds;
            public Rectangle InputBounds;
            public Rectangle ConfirmBounds;
            public Rectangle CancelBounds;
        }

        private readonly struct SetupEntry
        {
            public SetupEntry(string name, string payload)
            {
                Name = name;
                Payload = payload;
            }

            public string Name { get; }
            public string Payload { get; }
        }
    }
}
