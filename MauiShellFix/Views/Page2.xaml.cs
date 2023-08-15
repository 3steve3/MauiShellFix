namespace MauiShellFix.Views;

public partial class Page2 : ContentPage
{
	public Page2()
	{
		InitializeComponent();
	}

    private async void Button_Clicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}