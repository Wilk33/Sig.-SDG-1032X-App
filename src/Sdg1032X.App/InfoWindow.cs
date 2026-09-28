using System.Windows;
using System.Windows.Controls;

namespace Sdg1032X.App;

public sealed class InfoWindow : Window
{
	public InfoWindow(string title,string text,bool longText)
	{
		Title=title;
		Width=longText ? 720 : 430;
		Height=longText ? 560 : 190;
		MinWidth=longText ? 520 : 430;
		MinHeight=longText ? 360 : 190;
		ResizeMode=longText ? ResizeMode.CanResize : ResizeMode.NoResize;
		ShowInTaskbar=false;
		WindowStartupLocation=WindowStartupLocation.CenterOwner;
		SystemTheme.UseImmersiveDarkMode(this);

		Grid layout=new()
		{
			Background=(System.Windows.Media.Brush)FindResource("WindowBrush")
		};
		if(longText)
		{
			layout.Children.Add(new TextBox
			{
				Text=text,
				IsReadOnly=true,
				AcceptsReturn=true,
				TextWrapping=TextWrapping.Wrap,
				VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
				HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
				Margin=new(14),
				Padding=new(10),
				FontSize=13,
				Background=(System.Windows.Media.Brush)FindResource("ControlBrush"),
				Foreground=(System.Windows.Media.Brush)FindResource("TextBrush")
			});
		}
		else
		{
			StackPanel author=new()
			{
				Margin=new(22),
				VerticalAlignment=VerticalAlignment.Center
			};
			string[] lines=text.Split('\n');
			for(int index=0;index<lines.Length;index++)
			{
				author.Children.Add(new TextBlock
				{
					Text=lines[index],
					Margin=index == 0 ? new(0) : new(0,8,0,0),
					HorizontalAlignment=HorizontalAlignment.Center,
					FontSize=index == 0 ? 22 : 16,
					Foreground=(System.Windows.Media.Brush)FindResource("TextBrush")
				});
			}
			layout.Children.Add(author);
		}
		Content=layout;
	}
}
