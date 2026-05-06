using System;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.TextInput;

namespace EffectViewer.Controls
{
    internal static class BrowserTextBoxInputSync
    {
        public static readonly AttachedProperty<bool> ReportSelectionChangesProperty =
            AvaloniaProperty.RegisterAttached<TextBox, bool>(
                "ReportSelectionChanges",
                typeof(BrowserTextBoxInputSync),
                defaultValue: false);

        private static readonly FieldInfo InputMethodClientField =
            typeof(TextBox).GetField("_imClient", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo RaiseSurroundingTextChangedMethod =
            typeof(TextInputMethodClient).GetMethod(
                "RaiseSurroundingTextChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);

        static BrowserTextBoxInputSync()
        {
            ReportSelectionChangesProperty.Changed.AddClassHandler<TextBox>(OnReportSelectionChangesChanged);
        }

        public static void SetReportSelectionChanges(TextBox element, bool value)
        {
            element.SetValue(ReportSelectionChangesProperty, value);
        }

        public static bool GetReportSelectionChanges(TextBox element)
        {
            return element.GetValue(ReportSelectionChangesProperty);
        }

        private static void OnReportSelectionChangesChanged(
            TextBox textBox,
            AvaloniaPropertyChangedEventArgs args)
        {
            if (!OperatingSystem.IsBrowser())
            {
                return;
            }

            if (args.GetOldValue<bool>())
            {
                textBox.PropertyChanged -= TextBox_PropertyChanged;
            }

            if (args.GetNewValue<bool>())
            {
                textBox.PropertyChanged += TextBox_PropertyChanged;
            }
        }

        private static void TextBox_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (sender is not TextBox textBox ||
                e.Property != TextBox.SelectionStartProperty &&
                e.Property != TextBox.SelectionEndProperty &&
                e.Property != TextBox.CaretIndexProperty)
            {
                return;
            }

            RaiseSurroundingTextChanged(textBox);
        }

        private static void RaiseSurroundingTextChanged(TextBox textBox)
        {
            if (InputMethodClientField?.GetValue(textBox) is TextInputMethodClient client)
            {
                RaiseSurroundingTextChangedMethod?.Invoke(client, []);
            }
        }
    }
}
