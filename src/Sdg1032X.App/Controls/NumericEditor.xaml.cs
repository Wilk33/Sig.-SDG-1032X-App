using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Sdg1032X.Core;

namespace Sdg1032X.App.Controls;

public sealed class NumericValueCommittedEventArgs(double value) : EventArgs
{
	public double Value { get; }=value;
}

public partial class NumericEditor : UserControl
{
	public static readonly DependencyProperty LabelProperty=
		DependencyProperty.Register(
			nameof(Label),
			typeof(string),
			typeof(NumericEditor),
			new PropertyMetadata(string.Empty));
	public static readonly DependencyProperty UnitProperty=
		DependencyProperty.Register(
			nameof(Unit),
			typeof(string),
			typeof(NumericEditor),
			new PropertyMetadata(string.Empty));
	public static readonly DependencyProperty ValueProperty=
		DependencyProperty.Register(
			nameof(Value),
			typeof(double),
			typeof(NumericEditor),
			new FrameworkPropertyMetadata(
				0d,
				FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
				ValueChanged));
	public static readonly DependencyProperty StepProperty=
		DependencyProperty.Register(
			nameof(Step),
			typeof(double),
			typeof(NumericEditor),
			new PropertyMetadata(1d));
	public static readonly DependencyProperty MinimumProperty=
		DependencyProperty.Register(
			nameof(Minimum),
			typeof(double),
			typeof(NumericEditor),
			new PropertyMetadata(double.NegativeInfinity));
	public static readonly DependencyProperty MaximumProperty=
		DependencyProperty.Register(
			nameof(Maximum),
			typeof(double),
			typeof(NumericEditor),
			new PropertyMetadata(double.PositiveInfinity));
	public static readonly DependencyProperty ScaleProperty=
		DependencyProperty.Register(
			nameof(Scale),
			typeof(double),
			typeof(NumericEditor),
			new PropertyMetadata(1d,DisplayPropertyChanged));
	public static readonly DependencyProperty DecimalPlacesProperty=
		DependencyProperty.Register(
			nameof(DecimalPlaces),
			typeof(int),
			typeof(NumericEditor),
			new PropertyMetadata(3,DisplayPropertyChanged));

	public NumericEditor()
	{
		InitializeComponent();
		Loaded+=(_, _)=>RefreshText();
	}

	public event EventHandler<NumericValueCommittedEventArgs>? ValueCommitted;

	public string Label
	{
		get => (string)GetValue(LabelProperty);
		set => SetValue(LabelProperty,value);
	}

	public string Unit
	{
		get => (string)GetValue(UnitProperty);
		set => SetValue(UnitProperty,value);
	}

	public double Value
	{
		get => (double)GetValue(ValueProperty);
		set => SetValue(ValueProperty,value);
	}

	public double Step
	{
		get => (double)GetValue(StepProperty);
		set => SetValue(StepProperty,value);
	}

	public double Minimum
	{
		get => (double)GetValue(MinimumProperty);
		set => SetValue(MinimumProperty,value);
	}

	public double Maximum
	{
		get => (double)GetValue(MaximumProperty);
		set => SetValue(MaximumProperty,value);
	}

	public double Scale
	{
		get => (double)GetValue(ScaleProperty);
		set => SetValue(ScaleProperty,value);
	}

	public int DecimalPlaces
	{
		get => (int)GetValue(DecimalPlacesProperty);
		set => SetValue(DecimalPlacesProperty,value);
	}

	private static void ValueChanged(DependencyObject sender,DependencyPropertyChangedEventArgs eventArgs)
	{
		((NumericEditor)sender).RefreshText();
	}

	private static void DisplayPropertyChanged(DependencyObject sender,DependencyPropertyChangedEventArgs eventArgs)
	{
		((NumericEditor)sender).RefreshText();
	}

	private void EditorPreviewKeyDown(object sender,KeyEventArgs eventArgs)
	{
		if(eventArgs.Key == Key.Enter)
		{
			CommitText();
			eventArgs.Handled=true;
			Root.Focus();
		}
		else if(eventArgs.Key == Key.Escape)
		{
			RefreshText();
			eventArgs.Handled=true;
			Root.Focus();
		}
		else if(eventArgs.Key == Key.Up)
		{
			Adjust(Step);
			eventArgs.Handled=true;
		}
		else if(eventArgs.Key == Key.Down)
		{
			Adjust(-Step);
			eventArgs.Handled=true;
		}
	}

	private void EditorLostKeyboardFocus(object sender,KeyboardFocusChangedEventArgs eventArgs)
	{
		RefreshText();
	}

	private void IncrementClick(object sender,RoutedEventArgs eventArgs)
	{
		Adjust(Step);
	}

	private void DecrementClick(object sender,RoutedEventArgs eventArgs)
	{
		Adjust(-Step);
	}

	private void Adjust(double delta)
	{
		double value=Math.Clamp(Value+delta,Minimum,Maximum);
		Value=value;
		Editor.BorderBrush=Brushes.Transparent;
		ValueCommitted?.Invoke(this,new(value));
	}

	private void CommitText()
	{
		try
		{
			double value=EngineeringValue.ParseDisplay(Editor.Text,Scale);
			if(value<Minimum || value>Maximum)
			{
				throw new InvalidDataException($"Zakres: {Minimum} do {Maximum}.");
			}
			Value=value;
			Editor.BorderBrush=Brushes.Transparent;
			ValueCommitted?.Invoke(this,new(value));
		}
		catch(Exception exception) when(exception is InvalidDataException or ArgumentOutOfRangeException)
		{
			Editor.BorderBrush=Brushes.Red;
			Editor.ToolTip=exception.Message;
			SystemSounds.Beep.Play();
		}
	}

	private void RefreshText()
	{
		if(Editor is null)
		{
			return;
		}
		Editor.Text=EngineeringValue.FormatDisplay(Value,Scale,DecimalPlaces);
		Editor.BorderBrush=Brushes.Transparent;
		Editor.ToolTip=null;
	}
}
