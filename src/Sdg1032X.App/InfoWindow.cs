using System.Windows;
using System.Windows.Controls;

namespace Sdg1032X.App;

public sealed class InfoWindow : Window
{
	public InfoWindow(string title,string text,bool longText)
	{
		Title=title;
		Width=longText ? 720 : 390;
		Height=longText ? 560 : 230;
		MinWidth=330;
		MinHeight=190;
		WindowStartupLocation=WindowStartupLocation.CenterOwner;
		SystemTheme.UseImmersiveDarkMode(this);

		Grid layout=new();
		layout.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
		layout.RowDefinitions.Add(new(){Height=GridLength.Auto});
		TextBox content=new()
		{
			Text=text,
			IsReadOnly=true,
			AcceptsReturn=true,
			TextWrapping=longText ? TextWrapping.Wrap : TextWrapping.NoWrap,
			VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility=longText ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto,
			Margin=new(10),
			Padding=new(8)
		};
		Button close=new()
		{
			Content="Zamknij",
			Width=100,
			Margin=new(10,0,10,10),
			HorizontalAlignment=HorizontalAlignment.Right,
			IsDefault=true,
			IsCancel=true
		};
		close.Click+=(_, _)=>Close();
		Grid.SetRow(content,0);
		Grid.SetRow(close,1);
		layout.Children.Add(content);
		layout.Children.Add(close);
		Content=layout;
	}
}
