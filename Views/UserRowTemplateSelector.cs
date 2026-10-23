using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

// Picks the Users list row: one template for active users, one for deactivated users.
// Two small templates replace one big template with hidden twin views (two initials tiles, three badges),
// so every row has fewer views to create, bind and measure while scrolling.
public sealed class UserRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ActiveTemplate { get; set; }
    public DataTemplate? InactiveTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        UserListItem? user = item as UserListItem;
        if (user != null && !user.IsActive)
        {
            return InactiveTemplate!;
        }

        return ActiveTemplate!;
    }
}
