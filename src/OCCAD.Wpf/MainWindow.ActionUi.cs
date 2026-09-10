using WpfItemCollection = System.Windows.Controls.ItemCollection;
using WpfMenuItem = System.Windows.Controls.MenuItem;

namespace OCCAD.Wpf;

public partial class MainWindow
{
    private void RefreshLanguageUi()
    {
        ChineseLanguageMenuItem.IsChecked =
            string.Equals(
                CadLanguageManager.CurrentLanguage,
                "zh-CN",
                StringComparison.OrdinalIgnoreCase);
        EnglishLanguageMenuItem.IsChecked =
            string.Equals(
                CadLanguageManager.CurrentLanguage,
                "en-US",
                StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshActionUi()
    {
        foreach (var item in DescendantMenuItems(MainMenu.Items))
        {
            if (item.Tag is not string actionId)
                continue;

            var action = _workspace.Actions.Find(actionId);
            if (action is not null)
                item.IsEnabled = action.CanExecute();
        }
    }

    private static IEnumerable<WpfMenuItem> DescendantMenuItems(
        WpfItemCollection items)
    {
        foreach (var item in items.OfType<WpfMenuItem>())
        {
            yield return item;
            foreach (var child in DescendantMenuItems(item.Items))
                yield return child;
        }
    }
}
