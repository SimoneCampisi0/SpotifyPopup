using System.Windows.Data;
using System.Windows.Markup;

namespace SpotifyPopup;

// Explicit source also works in detached ToolTips and ContextMenus.
public sealed class TextExtension : MarkupExtension
{
    public string Key { get; set; }
    public TextExtension(string key) => Key = key;
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding("[" + Key + "]") { Source = Localizer.Current, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
