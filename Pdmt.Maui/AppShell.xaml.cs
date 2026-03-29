using Pdmt.Maui.Views;

namespace Pdmt.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("addEvent", typeof(NewEventPage));
    }
}
