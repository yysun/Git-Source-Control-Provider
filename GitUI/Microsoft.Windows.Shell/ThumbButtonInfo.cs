/**************************************************************************\
    Copyright Microsoft Corporation. All Rights Reserved.
\**************************************************************************/

namespace Microsoft.Windows.Shell
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.ComponentModel;
    using Standard;

    // This class doesn't lend itself to designer support, but still decorating the DefaultEvent.
    [DefaultEvent("Click")]
    public sealed class ThumbButtonInfo : Freezable, ICommandSource
    {
        // The CommandManager keeps weak references on delegates
        // hooked for CanExecuteChanged.  We need to explicitly keep a
        // strong reference, because just adding an instance method causes
        // the auto-generated handler to be GC'd.
        private EventHandler _commandEvent;

        protected override Freezable CreateInstanceCore()
        {
            return new ThumbButtonInfo();
        }

        #region Dependency Properties and support methods

        public static readonly DependencyProperty VisibilityProperty = DependencyProperty.Register(
            "Visibility",
            typeof(Visibility),
            typeof(ThumbButtonInfo),
            new PropertyMetadata(Visibility.Visible));

        /// <summary>
        /// Gets or sets the whether this should be visible in the UI.
        /// </summary>
        public Visibility Visibility
        {
            get { return (Visibility)GetValue(VisibilityProperty); }
            set { SetValue(VisibilityProperty, value); }
        }

        public static readonly DependencyProperty DismissWhenClickedProperty = DependencyProperty.Register(
            "DismissWhenClicked",
            typeof(bool),
            typeof(ThumbButtonInfo),
            new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets the DismissWhenClicked property.  This dependency property 
        /// indicates whether the thumbnail window should disappear as a result
        /// of the user clicking this button.
        /// </summary>
        public bool DismissWhenClicked
        {
            get { return (bool)GetValue(DismissWhenClickedProperty); }
            set { SetValue(DismissWhenClickedProperty, value); }
        }

        public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
            "ImageSource",
            typeof(ImageSource),
            typeof(ThumbButtonInfo),
            new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets the ImageSource property.  This dependency property 
        /// indicates the ImageSource to use for this button's display.
        /// </summary>
        public ImageSource ImageSource
        {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            set { SetValue(ImageSourceProperty, value); }
        }

        public static readonly DependencyProperty IsBackgroundVisibleProperty = DependencyProperty.Register(
            "IsBackgroundVisible",
            typeof(bool),
            typeof(ThumbButtonInfo),
            new PropertyMetadata(true));

        /// <summary>
        /// Gets or sets the IsBackgroundVisible property.  This dependency property 
        /// indicates whether the default background should be shown.
        /// </summary>
        public bool IsBackgroundVisible
        {
            get { return (bool)GetValue(IsBackgroundVisibleProperty); }
            set { SetValue(IsBackgroundVisibleProperty, value); }
        }

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
            "Description",
            typeof(string),
            typeof(ThumbButtonInfo),
            new PropertyMetadata(
                string.Empty,
                null,
                _CoerceDescription));

        /// <summary>
        /// Gets or sets the Description property.  This dependency property 
        /// indicates the text to display in the tooltip for this button.
        /// </summary>
        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

        // The THUMBBUTTON struct has a hard-coded length for this field of 260.
        private static object _CoerceDescription(DependencyObject d, object value)
        {
            var text = (string)value;

            if (text != null && text.Length >= 260)
            {
                // Account for the NULL in native LPWSTRs
                text = text.Substring(0, 259);
            }

            return text;
        }

        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.Register(
            "IsEnabled",
            typeof(bool),
            typeof(ThumbButtonInfo),
            new PropertyMetadata(
                true,
                null,
                (d, e) => ((ThumbButtonInfo)d)._CoerceIsEnabledValue(e)));

        private object _CoerceIsEnabledValue(object value)
        {
            var enabled = (bool)value;
            return enabled && _CanExecute;
        }

        /// <summary>
        /// Gets or sets the IsEnabled property.
        /// </summary>
        /// <remarks>
        /// This dependency property
        /// indicates whether the button is receptive to user interaction and
        /// should appear as such.  The button will not raise click events from
        /// the user when this property is false.
        /// See also IsInteractive.
        /// </remarks>
        public bool IsEnabled
        {
            get { return (bool)GetValue(IsEnabledProperty); }
            set { SetValue(IsEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsInteractiveProperty = DependencyProperty.Register(
            "IsInteractive",
            typeof(bool),
            typeof(ThumbButtonInfo),
            new PropertyMetadata(true));

        /// <summary>
        /// Gets or sets the IsInteractive property.
        /// </summary>
        /// <remarks>
        /// This dependency property allows an enabled button, as determined
        /// by the IsEnabled property, to not raise click events.  Buttons that
        /// have IsInteractive=false can be used to indicate status.
        /// IsEnabled=false takes precedence over IsInteractive=false.
        /// </remarks>
        public bool IsInteractive
        {
            get { return (bool)GetValue(IsInteractiveProperty); }
            set { SetValue(IsInteractiveProperty, value); }
        }

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
            "Command",
            typeof(ICommand),
            typeof(ThumbButtonInfo),
            new PropertyMetadata(
                null,
                (d, e) => ((ThumbButtonInfo)d)._OnCommandChanged(e)));

        private void _OnCommandChanged(DependencyPropertyChangedEventArgs e)
        {
            var oldCommand = (ICommand)e.OldValue;
            var newCommand = (ICommand)e.NewValue;

            if (oldCommand == newCommand)
            {
                return;
            }

            if (oldCommand != null)
            {
                _UnhookCommand(oldCommand);
            }
            if (newCommand != null)
            {
                _HookCommand(newCommand);
            }
        }

        /// <summary>
        /// CommandParameter Dependency Property
        /// </summary>
        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
            "CommandParameter",
            typeof(object),
            typeof(ThumbButtonInfo),
            new PropertyMetadata(
                null,
                (d, e) => ((ThumbButtonInfo)d)._UpdateCanExecute()));

        // .Net property deferred to ICommandSource region.

        /// <summary>
        /// CommandTarget Dependency Property
        /// </summary>
        public static readonly DependencyProperty CommandTargetProperty = DependencyProperty.Register(
            "CommandTarget",
            typeof(IInputElement),
            typeof(ThumbButtonInfo),
            new PropertyMetadata(
                null,
                (d, e) => ((ThumbButtonInfo)d)._UpdateCanExecute()));

        // .Net property deferred to ICommandSource region.

        private static readonly DependencyProperty _CanExecuteProperty = DependencyProperty.Register(
            "_CanExecute",
            typeof(bool),
            typeof(ThumbButtonInfo),
            new PropertyMetadata(
                true,
                (d, e) => d.CoerceValue(IsEnabledProperty)));

        private bool _CanExecute
        {
            get { return (bool)GetValue(_CanExecuteProperty); }
            set { SetValue(_CanExecuteProperty, value); }
        }

        #endregion

        public event EventHandler Click;

        internal void InvokeClick()
        {
            EventHandler local = Click;
            if (local != null)
            {
                local(this, EventArgs.Empty);
            }

            _InvokeCommand();
        }

        private void _InvokeCommand()
        {
            ICommand command = Command;
            if (command != null)
            {
                object parameter = CommandParameter;
                IInputElement target = CommandTarget;

                RoutedCommand routedCommand = command as RoutedCommand;
                if (routedCommand != null)
                {
                    if (routedCommand.CanExecute(parameter, target))
                    {
                        routedCommand.Execute(parameter, target);
                    }
                }
                else if (command.CanExecute(parameter))
                {
                    command.Execute(parameter);
                }
            }
        }

        private void _UnhookCommand(ICommand command)
        {
            Assert.IsNotNull(command);
            command.CanExecuteChanged -= _commandEvent;
            _commandEvent = null;
            _UpdateCanExecute();
        }

        private void _HookCommand(ICommand command)
        {
            _commandEvent = (sender, e) => _UpdateCanExecute();
            command.CanExecuteChanged += _commandEvent;
            _UpdateCanExecute();
        }

        private void _UpdateCanExecute()
        {
            if (Command != null)
            {
                object parameter = CommandParameter;
                IInputElement target = CommandTarget;

                RoutedCommand routed = Command as RoutedCommand;
                if (routed != null)
                {
                    _CanExecute = routed.CanExecute(parameter, target);
                }
                else
                {
                    _CanExecute = Command.CanExecute(parameter);
                }
            }
            else
            {
                _CanExecute = true;
            }
        }

        #region ICommandSource Members

        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        public object CommandParameter
        {
            get { return (object)GetValue(CommandParameterProperty); }
            set { SetValue(CommandParameterProperty, value); }
        }

        public IInputElement CommandTarget
        {
            get { return (IInputElement)GetValue(CommandTargetProperty); }
            set { SetValue(CommandTargetProperty, value); }
        }

        #endregion
    }
}
