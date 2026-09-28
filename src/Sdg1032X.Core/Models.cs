namespace Sdg1032X.Core;

public enum BasicWaveform
{
	Sine,
	Square,
	Ramp,
	Pulse,
	Noise,
	Dc
}

public enum GeneratorParameter
{
	Frequency,
	Amplitude,
	Offset,
	Phase,
	Duty,
	Symmetry,
	PulseWidth,
	NoiseStandardDeviation,
	NoiseMean
}

public enum OutputLoad
{
	HighImpedance,
	Ohms50,
	Custom
}

public enum OutputPolarity
{
	Normal,
	Inverted
}

public sealed record ChannelSnapshot
{
	public required int Channel { get; init; }
	public required BasicWaveform Waveform { get; init; }
	public double FrequencyHz { get; init; }
	public double AmplitudeVpp { get; init; }
	public double OffsetVolts { get; init; }
	public double PhaseDegrees { get; init; }
	public double DutyPercent { get; init; }
	public double SymmetryPercent { get; init; }
	public double PulseWidthSeconds { get; init; }
	public double NoiseStandardDeviation { get; init; }
	public double NoiseMean { get; init; }
	public bool OutputEnabled { get; init; }
	public required OutputLoad Load { get; init; }
	public double? LoadOhms { get; init; }
	public required OutputPolarity Polarity { get; init; }
}
