using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Uptime.Core;

namespace Uptime.Presentation.Localization;

/// <summary>
/// Applies localization directly to controls and refreshes them from one explicit language event.
/// This avoids nested DataContext/indexer bindings for static UI copy.
/// </summary>
public sealed class Localize
{
    public static readonly AttachedProperty<string?> TextKeyProperty = AvaloniaProperty.RegisterAttached<Localize, Control, string?>("TextKey");
    public static readonly AttachedProperty<string?> ContentKeyProperty = AvaloniaProperty.RegisterAttached<Localize, Control, string?>("ContentKey");
    public static readonly AttachedProperty<string?> TipKeyProperty = AvaloniaProperty.RegisterAttached<Localize, Control, string?>("TipKey");
    public static readonly AttachedProperty<string?> OnKeyProperty = AvaloniaProperty.RegisterAttached<Localize, Control, string?>("OnKey");
    public static readonly AttachedProperty<string?> OffKeyProperty = AvaloniaProperty.RegisterAttached<Localize, Control, string?>("OffKey");
    public static readonly AttachedProperty<string?> AutomationNameKeyProperty = AvaloniaProperty.RegisterAttached<Localize, Control, string?>("AutomationNameKey");
    private static readonly AttachedProperty<bool> IsHookedProperty =
        AvaloniaProperty.RegisterAttached<Localize, Control, bool>("IsHooked");
    private static readonly HashSet<Control> AttachedControls = new();

    static Localize()
    {
        TextKeyProperty.Changed.AddClassHandler<Control, string?>((control, _) => Configure(control));
        ContentKeyProperty.Changed.AddClassHandler<Control, string?>((control, _) => Configure(control));
        TipKeyProperty.Changed.AddClassHandler<Control, string?>((control, _) => Configure(control));
        OnKeyProperty.Changed.AddClassHandler<Control, string?>((control, _) => Configure(control));
        OffKeyProperty.Changed.AddClassHandler<Control, string?>((control, _) => Configure(control));
        AutomationNameKeyProperty.Changed.AddClassHandler<Control, string?>((control, _) => Configure(control));
        Localizer.Instance.LanguageChanged += (_, _) =>
        {
            foreach (var control in AttachedControls.ToArray()) Apply(control);
        };
    }

    public static string? GetTextKey(Control control) => control.GetValue(TextKeyProperty);
    public static void SetTextKey(Control control, string? value) => control.SetValue(TextKeyProperty, value);
    public static string? GetContentKey(Control control) => control.GetValue(ContentKeyProperty);
    public static void SetContentKey(Control control, string? value) => control.SetValue(ContentKeyProperty, value);
    public static string? GetTipKey(Control control) => control.GetValue(TipKeyProperty);
    public static void SetTipKey(Control control, string? value) => control.SetValue(TipKeyProperty, value);
    public static string? GetOnKey(Control control) => control.GetValue(OnKeyProperty);
    public static void SetOnKey(Control control, string? value) => control.SetValue(OnKeyProperty, value);
    public static string? GetOffKey(Control control) => control.GetValue(OffKeyProperty);
    public static void SetOffKey(Control control, string? value) => control.SetValue(OffKeyProperty, value);
    public static string? GetAutomationNameKey(Control control) => control.GetValue(AutomationNameKeyProperty);
    public static void SetAutomationNameKey(Control control, string? value) => control.SetValue(AutomationNameKeyProperty, value);

    private static void Configure(Control control)
    {
        if (!control.GetValue(IsHookedProperty))
        {
            control.SetValue(IsHookedProperty, true);
            control.AttachedToVisualTree += OnAttached;
            control.DetachedFromVisualTree += OnDetached;
        }
        Apply(control);
    }

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
        {
            AttachedControls.Add(control);
            Apply(control);
        }
    }

    private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control) AttachedControls.Remove(control);
    }

    private static void Apply(Control control)
    {
        var l = Localizer.Instance;
        if (GetTextKey(control) is { } textKey && control is TextBlock textBlock)
            textBlock.Text = l[textKey];
        if (GetContentKey(control) is { } contentKey && control is ContentControl contentControl)
            contentControl.Content = l[contentKey];
        if (GetTipKey(control) is { } tipKey)
            ToolTip.SetTip(control, l[tipKey]);
        if (GetOnKey(control) is { } onKey && control is ToggleSwitch toggleOn)
            toggleOn.OnContent = l[onKey];
        if (GetOffKey(control) is { } offKey && control is ToggleSwitch toggleOff)
            toggleOff.OffContent = l[offKey];
        if (GetAutomationNameKey(control) is { } automationKey)
            AutomationProperties.SetName(control, l[automationKey]);
    }
}
